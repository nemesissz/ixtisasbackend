using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MmuIspApi.Data;
using MmuIspApi.Models;
using MmuIspApi.Services;

namespace MmuIspApi.Controllers;

public record CohortDto(string InstitutionId, string Label, string? Icon, string? Year, int? SortOrder);

// Təhsilalan qrupları (qəbul dalğaları). Qrup yalnız təhsilalanı əhatələyir —
// struktur və seçim qrupa bağlanmır (bax: Models/Cohort.cs).
[ApiController]
[Route("api/[controller]")]
public class CohortsController : ControllerBase
{
    private readonly MmuDbContext _db;
    public CohortsController(MmuDbContext db) => _db = db;

    // Tələbə giriş ekranı qrup siyahısını login-dən əvvəl oxuya bilər.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Cohort>>> GetAll([FromQuery] string? institutionId)
    {
        var query = _db.Cohorts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(institutionId))
            query = query.Where(c => c.InstitutionId == institutionId);
        var allowed = User.AllowedInstitutions();
        if (allowed is not null)
            query = query.Where(c => allowed.Contains(c.InstitutionId));
        return Ok(await query.OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAt).ToListAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Cohort>> Get(string id)
    {
        var item = await _db.Cohorts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (item is null) return NotFound();
        if (!User.CanAccessInstitution(item.InstitutionId)) return Forbid();
        return Ok(item);
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    [RequirePermission("inst.edit")]
    public async Task<ActionResult<Cohort>> Create(CohortDto dto)
    {
        if (!User.CanAccessInstitution(dto.InstitutionId)) return Forbid();
        if (!await _db.Institutions.AnyAsync(i => i.Id == dto.InstitutionId))
            return BadRequest(new { message = "Müəssisə tapılmadı." });

        var order = dto.SortOrder ?? await _db.Cohorts
            .Where(c => c.InstitutionId == dto.InstitutionId)
            .Select(c => (int?)c.SortOrder).MaxAsync() + 1 ?? 0;

        var item = new Cohort
        {
            Id = $"cohort_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}",
            InstitutionId = dto.InstitutionId,
            Label = dto.Label,
            Icon = dto.Icon,
            Year = dto.Year,
            SortOrder = order,
        };
        _db.Cohorts.Add(item);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    [RequirePermission("inst.edit")]
    public async Task<IActionResult> Update(string id, CohortDto dto)
    {
        var item = await _db.Cohorts.FindAsync(id);
        if (item is null) return NotFound();
        if (!User.CanAccessInstitution(item.InstitutionId)) return Forbid();
        item.Label = dto.Label;
        item.Icon = dto.Icon;
        item.Year = dto.Year;
        if (dto.SortOrder.HasValue) item.SortOrder = dto.SortOrder.Value;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Arxiv: qrup gizlədilir, təhsilalanları yerində qalır.
    [HttpPost("{id}/archive")]
    [Authorize(Roles = "admin")]
    [RequirePermission("inst.edit")]
    public async Task<IActionResult> Archive(string id, [FromQuery] bool value = true)
    {
        var item = await _db.Cohorts.FindAsync(id);
        if (item is null) return NotFound();
        if (!User.CanAccessInstitution(item.InstitutionId)) return Forbid();
        item.IsArchived = value;
        item.ArchivedAt = value ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Silinmə təhsilalanı silmir — FK SetNull olduğu üçün onlar "qrupsuz" qalır.
    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    [RequirePermission("inst.delete")]
    public async Task<IActionResult> Delete(string id)
    {
        var item = await _db.Cohorts.FindAsync(id);
        if (item is null) return NotFound();
        if (!User.CanAccessInstitution(item.InstitutionId)) return Forbid();
        _db.Cohorts.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Təhsilalanları toplu şəkildə bu qrupa köçürür (cohortId = null → qrupdan çıxarır).
    [HttpPost("{id}/assign")]
    [Authorize(Roles = "admin")]
    [RequirePermission("users.edit")]
    public async Task<IActionResult> Assign(string id, [FromBody] List<string> studentIds)
    {
        var target = id == "none" ? null : await _db.Cohorts.FindAsync(id);
        if (id != "none" && target is null) return NotFound();
        if (target is not null && !User.CanAccessInstitution(target.InstitutionId)) return Forbid();

        var students = await _db.Students.Where(s => studentIds.Contains(s.Id)).ToListAsync();
        foreach (var s in students)
        {
            if (!User.CanAccessInstitution(s.InstitutionId)) continue;
            // Qrup yalnız öz müəssisəsinin təhsilalanını saxlaya bilər
            if (target is not null && s.InstitutionId != target.InstitutionId) continue;
            s.CohortId = target?.Id;
        }
        await _db.SaveChangesAsync();
        return Ok(new { count = students.Count });
    }
}
