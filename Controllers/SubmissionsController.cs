using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MmuIspApi.Data;
using MmuIspApi.Models;
using MmuIspApi.Services;

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

    // Student yalnız ÖZ userId-si ilə əməliyyat apara bilər (token-dəki sub ilə tutuşdurulur);
    // admin üçün məhdudiyyət yoxdur. Uyğunsuzluqda true → 403 qaytarılmalıdır.
    private bool IsForbiddenForStudent(string userId)
    {
        if (User.IsInRole("admin")) return false;
        var tokenUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return tokenUserId != userId;
    }

    // Müəssisə əhatəsi: seçim icazəli müəssisəyə aiddirmi? (tələbə əhatəsizdir → həmişə true)
    private async Task<bool> SelectionInScopeAsync(string selectionId)
    {
        if (User.AllowedInstitutions() is null) return true;
        var instId = await _db.Selections.AsNoTracking()
            .Where(s => s.Id == selectionId).Select(s => s.InstitutionId).FirstOrDefaultAsync();
        return User.CanAccessInstitution(instId);
    }

    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<IEnumerable<Submission>>> GetAll()
    {
        var query = _db.Submissions.AsNoTracking().AsQueryable();
        // Müəssisə əhatəsi: yalnız icazəli müəssisələrin seçimlərinə aid göndərişlər
        var allowed = User.AllowedInstitutions();
        if (allowed is not null)
        {
            var selIds = await _db.Selections.AsNoTracking()
                .Where(s => allowed.Contains(s.InstitutionId)).Select(s => s.Id).ToListAsync();
            query = query.Where(s => selIds.Contains(s.SelectionId));
        }
        return Ok(await query.ToListAsync());
    }

    [HttpGet("by-user")]
    public async Task<ActionResult<Submission>> GetByUser([FromQuery] string userId, [FromQuery] string selectionId)
    {
        if (IsForbiddenForStudent(userId)) return Forbid();
        if (!await SelectionInScopeAsync(selectionId)) return Forbid();
        var sub = await _db.Submissions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.SelectionId == selectionId);
        return sub is null ? NotFound() : Ok(sub);
    }

    [HttpGet("by-selection/{selectionId}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<IEnumerable<Submission>>> GetBySelection(string selectionId)
    {
        if (!await SelectionInScopeAsync(selectionId)) return Forbid();
        return Ok(await _db.Submissions.AsNoTracking().Where(s => s.SelectionId == selectionId).ToListAsync());
    }

    // userId + selectionId üzrə upsert (varsa ranking-i yeniləyir, yoxdursa yaradır)
    [HttpPost]
    public async Task<ActionResult<Submission>> Save(SubmissionSaveDto dto)
    {
        if (IsForbiddenForStudent(dto.UserId)) return Forbid();
        if (!await SelectionInScopeAsync(dto.SelectionId)) return Forbid();
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
        if (!await SelectionInScopeAsync(selectionId)) return Forbid();
        var sub = await _db.Submissions.FirstOrDefaultAsync(s => s.UserId == userId && s.SelectionId == selectionId);
        if (sub is null) return NotFound();
        _db.Submissions.Remove(sub);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
