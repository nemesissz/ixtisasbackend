using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MmuIspApi.Data;
using MmuIspApi.Models;
using MmuIspApi.Services;

namespace MmuIspApi.Controllers;

public record SpecialtyTreeCreateDto(
    string Name, string InstitutionId, List<string> LevelNames,
    string? Icon, string? Year, bool SourceProportional,
    // Bu struktur hansı təhsilalan qrupu üçündür (null = bütün müəssisə)
    string? CohortId = null);

public record SpecialtyTreeUpdateDto(
    string Name, List<string> LevelNames, string? Icon, string? Year, bool SourceProportional,
    string? CohortId = null);

// Nested node şəkli — frontend-dəki TNode (Specialties.tsx) ilə eynidir.
// JsonPropertyName: frontend sahə adı "mülkiQuota" (ü hərfi ilə) — default camelCase
// bunu "mulkiQuota"-ya çevirərdi, ona görə açıq şəkildə map olunur.
public record SpecialtyNodeDto(
    string Id, string Name, int? Quota, List<SpecialtyNodeDto> Children,
    List<string>? Tiebreaker, Dictionary<string, List<string>>? GroupTiebreakers, List<string>? Groups,
    Dictionary<string, List<string>>? Filters,
    string? QuotaMode,
    [property: JsonPropertyName("mülkiQuota")] int? MulkiQuota,
    int? LiseyQuota,
    bool? AllowFemale, bool? AllowMale, int? MaxFemale, int? MaxMale);

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SpecialtyTreesController : ControllerBase
{
    private readonly MmuDbContext _db;
    public SpecialtyTreesController(MmuDbContext db) => _db = db;

    // Orijinal localStorage modelində hər ağac həmişə tam node dəstəsi ilə birlikdə saxlanılırdı
    // (treeDb.countSpecialties/totalQuota kimi funksiyalar getAll() siyahısındakı ağaclar üzərində
    // birbaşa işləyir) — ona görə siyahı endpoint-i də hər ağacı tam (nodes daxil) qaytarır.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetAll([FromQuery] bool archived = false)
    {
        // archived=false → yalnız aktiv strukturlar (İxtisaslar səhifəsi)
        // archived=true  → yalnız arxivdəkilər (Arxiv səhifəsi)
        var trees = await _db.SpecialtyTrees.AsNoTracking()
            .Where(t => t.IsArchived == archived).ToListAsync();
        var allNodes = await _db.SpecialtyNodes.AsNoTracking().OrderBy(n => n.SortOrder).ToListAsync();
        var nodesByTree = allNodes.GroupBy(n => n.TreeId).ToDictionary(g => g.Key, g => g.ToList());

        return Ok(trees.Select(tree => BuildTreeDto(tree, nodesByTree.GetValueOrDefault(tree.Id, new()))));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<object>> Get(string id)
    {
        var tree = await _db.SpecialtyTrees.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (tree is null) return NotFound();

        var nodes = await _db.SpecialtyNodes.AsNoTracking()
            .Where(n => n.TreeId == id)
            .OrderBy(n => n.SortOrder)
            .ToListAsync();

        return Ok(BuildTreeDto(tree, nodes));
    }

    private static object BuildTreeDto(SpecialtyTree tree, List<SpecialtyNode> nodes) => new
    {
        tree.Id,
        tree.Name,
        tree.InstitutionId,
        // Strukturun təhsilalan qrupu — frontend bunu oxuyur və geri yazır;
        // burada olmasa hər yeniləmədə qrup itərdi.
        tree.CohortId,
        tree.LevelNames,
        tree.Icon,
        tree.Year,
        tree.SourceProportional,
        tree.IsArchived,
        tree.ArchivedAt,
        tree.CreatedAt,
        Nodes = BuildTree(nodes, null),
    };

    [HttpPost]
    [Authorize(Roles = "admin")]
    [RequirePermission("tree.edit")]
    public async Task<ActionResult<SpecialtyTree>> Create(SpecialtyTreeCreateDto dto)
    {
        if (!User.CanAccessInstitution(dto.InstitutionId)) return Forbid();
        var item = new SpecialtyTree
        {
            Id = $"tree_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}",
            Name = dto.Name,
            InstitutionId = dto.InstitutionId,
            LevelNames = dto.LevelNames ?? new(),
            Icon = dto.Icon,
            Year = dto.Year,
            SourceProportional = dto.SourceProportional,
            CohortId = dto.CohortId,
        };
        _db.SpecialtyTrees.Add(item);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    [RequirePermission("tree.edit")]
    public async Task<IActionResult> Update(string id, SpecialtyTreeUpdateDto dto)
    {
        var item = await _db.SpecialtyTrees.FindAsync(id);
        if (item is null) return NotFound();
        if (!User.CanAccessInstitution(item.InstitutionId)) return Forbid();
        item.Name = dto.Name;
        item.LevelNames = dto.LevelNames ?? item.LevelNames;
        item.Icon = dto.Icon;
        item.Year = dto.Year;
        item.SourceProportional = dto.SourceProportional;
        item.CohortId = dto.CohortId;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Bütün ağac strukturunu bir dəfəyə əvəz edir (frontend hər redaktədə tam `nodes` array-ini göndərir)
    [HttpPut("{id}/nodes")]
    [Authorize(Roles = "admin")]
    [RequirePermission("tree.edit")]
    public async Task<IActionResult> ReplaceNodes(string id, List<SpecialtyNodeDto> nodes)
    {
        var tree = await _db.SpecialtyTrees.FindAsync(id);
        if (tree is null) return NotFound();
        if (!User.CanAccessInstitution(tree.InstitutionId)) return Forbid();

        var existing = await _db.SpecialtyNodes.Where(n => n.TreeId == id).ToListAsync();
        _db.SpecialtyNodes.RemoveRange(existing);

        var flat = new List<SpecialtyNode>();
        Flatten(nodes, id, null, flat);
        await _db.SpecialtyNodes.AddRangeAsync(flat);

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Arxivləmə silmə DEYİL: sətir yerində qalır, yalnız IsArchived=true olur.
    // Səbəb: Selection.TreeId bu struktura Restrict FK ilə bağlıdır — silinsə,
    // həmin dövrdə edilmiş seçimlərin nəticələri də itərdi.
    [HttpPost("{id}/archive")]
    [Authorize(Roles = "admin")]
    [RequirePermission("tree.delete")]
    public Task<IActionResult> Archive(string id) => SetArchived(id, true);

    [HttpPost("{id}/restore")]
    [Authorize(Roles = "admin")]
    [RequirePermission("archive.restore")]
    public Task<IActionResult> Restore(string id) => SetArchived(id, false);

    private async Task<IActionResult> SetArchived(string id, bool archived)
    {
        var item = await _db.SpecialtyTrees.FindAsync(id);
        if (item is null) return NotFound();
        if (!User.CanAccessInstitution(item.InstitutionId)) return Forbid();
        var now = DateTime.UtcNow;
        item.IsArchived = archived;
        item.ArchivedAt = archived ? now : null;

        // Struktura bağlı seçimlər onunla birlikdə arxivə gedir/qayıdır.
        var sels = await _db.Selections.Where(s => s.TreeId == id).ToListAsync();
        foreach (var sel in sels)
        {
            if (archived)
            {
                // Onsuz da arxivdə olana toxunma — bərpada onu geri qaytarmamalıyıq.
                if (sel.Status == SelectionStatus.Archived) continue;
                sel.Status = SelectionStatus.Archived;
                sel.ArchivedAt = now;
                sel.ArchivedWithTree = true;
            }
            else if (sel.ArchivedWithTree)
            {
                sel.Status = SelectionStatus.Closed;
                sel.ArchivedAt = null;
                sel.ArchivedWithTree = false;
            }
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    [RequirePermission("tree.delete")]
    public async Task<IActionResult> Delete(string id)
    {
        var item = await _db.SpecialtyTrees.FindAsync(id);
        if (item is null) return NotFound();
        if (!User.CanAccessInstitution(item.InstitutionId)) return Forbid();

        // Seçimlər bu struktura Restrict FK ilə bağlıdır — silinsə baza xəta verir.
        // Ona görə əvvəlcədən yoxlayıb aydın mesaj qaytarırıq.
        var usedBy = await _db.Selections.Where(s => s.TreeId == id)
                                         .Select(s => new { s.Name, s.Status }).ToListAsync();
        if (usedBy.Count > 0)
        {
            // Arxivlənmiş seçim də bazada qalır və nəticələri göstərmək üçün
            // bu struktura ehtiyac duyur — ona görə arxivləmək maneəni aradan qaldırmır.
            var names = string.Join(", ", usedBy.Select(s =>
                s.Status == "archived" ? $"\"{s.Name}\" (arxivdə)" : $"\"{s.Name}\""));
            return Conflict(new { message =
                $"Bu struktur {usedBy.Count} seçimə bağlıdır: {names}. " +
                "Nəticələri göstərmək üçün həmin seçimlərə bu struktur lazımdır, ona görə tam silinmə mümkün deyil. " +
                "Strukturu gözdən itirmək istəyirsənsə \"Arxivlə\" düyməsini işlət — seçimlər toxunulmaz qalır və bərpa edəndə hər şey geri qayıdır." });
        }

        _db.SpecialtyTrees.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static List<SpecialtyNodeDto> BuildTree(List<SpecialtyNode> flat, string? parentId) =>
        flat.Where(n => n.ParentId == parentId)
            .Select(n => new SpecialtyNodeDto(
                n.Id, n.Name, n.Quota, BuildTree(flat, n.Id),
                n.Tiebreaker, n.GroupTiebreakers, n.Groups, n.Filters,
                n.QuotaMode, n.MulkiQuota, n.LiseyQuota,
                n.AllowFemale, n.AllowMale, n.MaxFemale, n.MaxMale))
            .ToList();

    private static void Flatten(List<SpecialtyNodeDto> nodes, string treeId, string? parentId, List<SpecialtyNode> outList)
    {
        var order = 0;
        foreach (var n in nodes)
        {
            outList.Add(new SpecialtyNode
            {
                Id = n.Id,
                TreeId = treeId,
                ParentId = parentId,
                Name = n.Name,
                Quota = n.Quota,
                SortOrder = order++,
                Tiebreaker = n.Tiebreaker,
                GroupTiebreakers = n.GroupTiebreakers,
                Groups = n.Groups,
                Filters = n.Filters,
                QuotaMode = n.QuotaMode,
                MulkiQuota = n.MulkiQuota,
                LiseyQuota = n.LiseyQuota,
                AllowFemale = n.AllowFemale,
                AllowMale = n.AllowMale,
                MaxFemale = n.MaxFemale,
                MaxMale = n.MaxMale,
            });
            if (n.Children?.Count > 0)
                Flatten(n.Children, treeId, n.Id, outList);
        }
    }
}
