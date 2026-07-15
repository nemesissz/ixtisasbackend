using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MmuIspApi.Data;
using MmuIspApi.Models;
using MmuIspApi.Services;

namespace MmuIspApi.Controllers;

public record InstitutionDto(string Label, string? Icon, string? Year);

[ApiController]
[Route("api/[controller]")]
public class InstitutionsController : ControllerBase
{
    private readonly MmuDbContext _db;
    public InstitutionsController(MmuDbContext db) => _db = db;

    // Tələbə giriş ekranı institution siyahısını login-dən ƏVVƏL oxuyur — açıq qalır.
    // Müəssisə əhatəli admin girmişsə yalnız icazəli müəssisələri görür.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Institution>>> GetAll()
    {
        var query = _db.Institutions.AsNoTracking().AsQueryable();
        var allowed = User.AllowedInstitutions();
        if (allowed is not null)
            query = query.Where(i => allowed.Contains(i.Id));
        return Ok(await query.ToListAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Institution>> Get(string id)
    {
        var inst = await _db.Institutions.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
        return inst is null ? NotFound() : Ok(inst);
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    [RequirePermission("inst.create")]
    public async Task<ActionResult<Institution>> Create(InstitutionDto dto)
    {
        var slug = Slugify(dto.Label);
        var id = $"{slug}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}";
        var item = new Institution { Id = id, Label = dto.Label, Icon = dto.Icon, Year = dto.Year };
        _db.Institutions.Add(item);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    [RequirePermission("inst.edit")]
    public async Task<IActionResult> Update(string id, InstitutionDto dto)
    {
        var item = await _db.Institutions.FindAsync(id);
        if (item is null) return NotFound();
        item.Label = dto.Label;
        item.Icon = dto.Icon;
        item.Year = dto.Year;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    [RequirePermission("inst.delete")]
    public async Task<IActionResult> Delete(string id)
    {
        var item = await _db.Institutions.FindAsync(id);
        if (item is null) return NotFound();
        _db.Institutions.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Müəssisənin bütün tələbələrinin yerləşdirmə/status sahələrini sıfırlayır (təzə seçim dövrü üçün)
    [HttpPost("{id}/reset-students")]
    [Authorize(Roles = "admin")]
    [RequirePermission("users.delete")]
    public async Task<IActionResult> ResetStudents(string id)
    {
        var students = await _db.Students.Where(s => s.InstitutionId == id).ToListAsync();
        foreach (var s in students)
        {
            s.Status = "pending";
            s.PrintStatus = "not_printed";
            s.PlacedSpecialty = null;
            s.PlacedSpecialtyId = null;
            s.PlacedSelectionId = null;
            s.ChoiceNum = null;
            s.Packet = null;
        }
        await _db.SaveChangesAsync();
        return Ok(new { count = students.Count });
    }

    private static string Slugify(string label)
    {
        var normalized = label.ToLowerInvariant().Trim();
        var chars = normalized.Select(c => char.IsLetterOrDigit(c) ? c : '_');
        var slug = new string(chars.ToArray()).Trim('_');
        return string.IsNullOrEmpty(slug) ? "inst" : slug;
    }
}
