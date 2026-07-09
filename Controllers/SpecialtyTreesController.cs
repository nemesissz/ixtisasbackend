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
    string? Icon, string? Year, bool SourceProportional);

public record SpecialtyTreeUpdateDto(
    string Name, List<string> LevelNames, string? Icon, string? Year, bool SourceProportional);

// Nested node şəkli — frontend-dəki TNode (Specialties.tsx) ilə eynidir.
// JsonPropertyName: frontend sahə adı "mülkiQuota" (ü hərfi ilə) — default camelCase
// bunu "mulkiQuota"-ya çevirərdi, ona görə açıq şəkildə map olunur.
public record SpecialtyNodeDto(
    string Id, string Name, int? Quota, List<SpecialtyNodeDto> Children,
    List<string>? Tiebreaker, Dictionary<string, List<string>>? GroupTiebreakers, List<string>? Groups,
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
    public async Task<ActionResult<IEnumerable<object>>> GetAll()
    {
        var trees = await _db.SpecialtyTrees.AsNoTracking().ToListAsync();
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
        tree.LevelNames,
        tree.Icon,
        tree.Year,
        tree.SourceProportional,
        tree.CreatedAt,
        Nodes = BuildTree(nodes, null),
    };

    [HttpPost]
    [Authorize(Roles = "admin")]
    [RequirePermission("tree.edit")]
    public async Task<ActionResult<SpecialtyTree>> Create(SpecialtyTreeCreateDto dto)
    {
        var item = new SpecialtyTree
        {
            Id = $"tree_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}",
            Name = dto.Name,
            InstitutionId = dto.InstitutionId,
            LevelNames = dto.LevelNames ?? new(),
            Icon = dto.Icon,
            Year = dto.Year,
            SourceProportional = dto.SourceProportional,
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
        item.Name = dto.Name;
        item.LevelNames = dto.LevelNames ?? item.LevelNames;
        item.Icon = dto.Icon;
        item.Year = dto.Year;
        item.SourceProportional = dto.SourceProportional;
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

        var existing = await _db.SpecialtyNodes.Where(n => n.TreeId == id).ToListAsync();
        _db.SpecialtyNodes.RemoveRange(existing);

        var flat = new List<SpecialtyNode>();
        Flatten(nodes, id, null, flat);
        await _db.SpecialtyNodes.AddRangeAsync(flat);

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
        _db.SpecialtyTrees.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static List<SpecialtyNodeDto> BuildTree(List<SpecialtyNode> flat, string? parentId) =>
        flat.Where(n => n.ParentId == parentId)
            .Select(n => new SpecialtyNodeDto(
                n.Id, n.Name, n.Quota, BuildTree(flat, n.Id),
                n.Tiebreaker, n.GroupTiebreakers, n.Groups,
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
