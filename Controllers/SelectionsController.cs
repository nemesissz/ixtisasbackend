using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MmuIspApi.Data;
using MmuIspApi.Models;
using MmuIspApi.Services;

namespace MmuIspApi.Controllers;

public record SelectionCreateDto(
    string Name, string InstitutionId, string TreeId,
    int StudentCount, int ChoiceCount, List<string> Tiebreaker,
    string ViewMode, bool SourceProportional, int? PreAssignLevel);

public record SelectionUpdateDto(
    string Name, int StudentCount, int ChoiceCount,
    List<string> Tiebreaker, string ViewMode, bool SourceProportional, int? PreAssignLevel);

[ApiController]
[Route("api/[controller]")]
public class SelectionsController : ControllerBase
{
    private readonly MmuDbContext _db;
    public SelectionsController(MmuDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Selection>>> GetAll() =>
        Ok(await _db.Selections.AsNoTracking().ToListAsync());

    [HttpGet("archived")]
    [Authorize(Roles = "admin")]
    [RequirePermission("archive.view")]
    public async Task<ActionResult<IEnumerable<Selection>>> GetArchived() =>
        Ok(await _db.Selections.AsNoTracking().Where(s => s.Status == SelectionStatus.Archived).ToListAsync());

    [HttpGet("{id}")]
    public async Task<ActionResult<Selection>> Get(string id)
    {
        var sel = await _db.Selections.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        return sel is null ? NotFound() : Ok(sel);
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    [RequirePermission("sel.create")]
    public async Task<ActionResult<Selection>> Create(SelectionCreateDto dto)
    {
        var item = new Selection
        {
            Id = $"sel_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}",
            Name = dto.Name,
            InstitutionId = dto.InstitutionId,
            TreeId = dto.TreeId,
            StudentCount = dto.StudentCount,
            ChoiceCount = dto.ChoiceCount,
            Tiebreaker = dto.Tiebreaker ?? new(),
            ViewMode = dto.ViewMode,
            SourceProportional = dto.SourceProportional,
            PreAssignLevel = dto.PreAssignLevel,
            Status = SelectionStatus.Draft,
        };
        _db.Selections.Add(item);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    [RequirePermission("sel.edit")]
    public async Task<IActionResult> Update(string id, SelectionUpdateDto dto)
    {
        var item = await _db.Selections.FindAsync(id);
        if (item is null) return NotFound();
        item.Name = dto.Name;
        item.StudentCount = dto.StudentCount;
        item.ChoiceCount = dto.ChoiceCount;
        item.Tiebreaker = dto.Tiebreaker ?? item.Tiebreaker;
        item.ViewMode = dto.ViewMode;
        item.SourceProportional = dto.SourceProportional;
        item.PreAssignLevel = dto.PreAssignLevel;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    [RequirePermission("sel.delete")]
    public async Task<IActionResult> Delete(string id)
    {
        var item = await _db.Selections.FindAsync(id);
        if (item is null) return NotFound();
        _db.Selections.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/publish")]
    [Authorize(Roles = "admin")]
    [RequirePermission("sel.publish")]
    public Task<IActionResult> Publish(string id) => SetStatus(id, SelectionStatus.Published, s => s.PublishedAt = DateTime.UtcNow);

    [HttpPost("{id}/close")]
    [Authorize(Roles = "admin")]
    [RequirePermission("sel.publish")]
    public Task<IActionResult> Close(string id) => SetStatus(id, SelectionStatus.Closed, s => s.ClosedAt = DateTime.UtcNow);

    [HttpPost("{id}/archive")]
    [Authorize(Roles = "admin")]
    [RequirePermission("sel.publish")]
    public Task<IActionResult> Archive(string id) => SetStatus(id, SelectionStatus.Archived, s => s.ArchivedAt = DateTime.UtcNow);

    [HttpPost("{id}/restore")]
    [Authorize(Roles = "admin")]
    [RequirePermission("archive.restore")]
    public Task<IActionResult> Restore(string id) => SetStatus(id, SelectionStatus.Closed, _ => { });

    private async Task<IActionResult> SetStatus(string id, string status, Action<Selection> apply)
    {
        var item = await _db.Selections.FindAsync(id);
        if (item is null) return NotFound();
        item.Status = status;
        apply(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // db.ts-dəki resetAndAutoSeedSubmissions-un server tərəfi portu: seçimin köhnə sıralamalarını
    // silir, müəssisənin bütün tələbələri üçün qarışdırılmış (pseudo-random) yeni sıralama yaradır
    [HttpPost("{id}/reset-and-autoseed")]
    [Authorize(Roles = "admin")]
    [RequirePermission("sel.edit")]
    public async Task<ActionResult> ResetAndAutoSeed(string id)
    {
        var sel = await _db.Selections.FindAsync(id);
        if (sel is null) return NotFound();

        var leafIds = await _db.SpecialtyNodes.AsNoTracking()
            .Where(n => n.TreeId == sel.TreeId && !_db.SpecialtyNodes.Any(c => c.ParentId == n.Id))
            .Select(n => n.Id)
            .ToListAsync();

        var oldSubs = await _db.Submissions.Where(s => s.SelectionId == id).ToListAsync();
        _db.Submissions.RemoveRange(oldSubs);

        var instUsers = await _db.Students.AsNoTracking()
            .Where(s => s.InstitutionId == sel.InstitutionId)
            .ToListAsync();

        var newSubs = instUsers.Select((u, idx) => new Submission
        {
            Id = $"sub_reset_{id}_{idx + 1}",
            UserId = u.Id,
            UserName = u.Name,
            SelectionId = id,
            Ranking = Shuffle(leafIds, (idx + 1) * 61 + 17),
        }).ToList();

        await _db.Submissions.AddRangeAsync(newSubs);
        await _db.SaveChangesAsync();
        return Ok(new { count = newSubs.Count });
    }

    private static List<string> Shuffle(List<string> arr, int seed)
    {
        int Hash(string s)
        {
            int c0 = s.Length > 0 ? s[0] : 0;
            int c1 = s.Length > 1 ? s[1] : 0;
            int c2 = s.Length > 2 ? s[2] : 0;
            return (seed * (c0 + c1 * 3 + c2 * 7)) % 97;
        }
        return arr.OrderBy(Hash).ToList();
    }
}
