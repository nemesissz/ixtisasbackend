using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MmuIspApi.Data;
using MmuIspApi.Models;

namespace MmuIspApi.Controllers;

public record SubmissionSaveDto(string UserId, string? UserName, string SelectionId, List<string> Ranking);

// admin paneli üçün oxu, tələbə üçün öz sıralamasını göndərmə/oxuma
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin,student")]
public class SubmissionsController : ControllerBase
{
    private readonly MmuDbContext _db;
    public SubmissionsController(MmuDbContext db) => _db = db;

    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<IEnumerable<Submission>>> GetAll() =>
        Ok(await _db.Submissions.AsNoTracking().ToListAsync());

    [HttpGet("by-user")]
    public async Task<ActionResult<Submission>> GetByUser([FromQuery] string userId, [FromQuery] string selectionId)
    {
        var sub = await _db.Submissions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.SelectionId == selectionId);
        return sub is null ? NotFound() : Ok(sub);
    }

    [HttpGet("by-selection/{selectionId}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<IEnumerable<Submission>>> GetBySelection(string selectionId) =>
        Ok(await _db.Submissions.AsNoTracking().Where(s => s.SelectionId == selectionId).ToListAsync());

    // userId + selectionId üzrə upsert (varsa ranking-i yeniləyir, yoxdursa yaradır)
    [HttpPost]
    public async Task<ActionResult<Submission>> Save(SubmissionSaveDto dto)
    {
        var existing = await _db.Submissions
            .FirstOrDefaultAsync(s => s.UserId == dto.UserId && s.SelectionId == dto.SelectionId);

        if (existing is not null)
        {
            existing.Ranking = dto.Ranking;
            existing.UserName = dto.UserName ?? existing.UserName;
            existing.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(existing);
        }

        var item = new Submission
        {
            // ⚠ Konkurentlik: 100 kursant eyni millisaniyədə göndərəndə ID toqquşmasın deyə
            // unikal Guid suffiksi əlavə olunur (əvvəl yalnız timestamp idi → duplicate PK → 500)
            Id = $"sub_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}_{Guid.NewGuid():N}",
            UserId = dto.UserId,
            UserName = dto.UserName,
            SelectionId = dto.SelectionId,
            Ranking = dto.Ranking,
        };
        _db.Submissions.Add(item);
        await _db.SaveChangesAsync();
        return Ok(item);
    }

    // Bir tələbənin bir seçim üçün sıralamasını silir (admin "statusu pending-ə qaytar" əməliyyatı)
    [HttpDelete("by-user")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteByUser([FromQuery] string userId, [FromQuery] string selectionId)
    {
        var sub = await _db.Submissions.FirstOrDefaultAsync(s => s.UserId == userId && s.SelectionId == selectionId);
        if (sub is null) return NotFound();
        _db.Submissions.Remove(sub);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
