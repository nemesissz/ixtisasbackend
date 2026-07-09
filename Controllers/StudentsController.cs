using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MmuIspApi.Data;
using MmuIspApi.Models;
using MmuIspApi.Services;

namespace MmuIspApi.Controllers;

public record StudentCreateDto(
    string? Id, string InstitutionId, string Name, string? ParentName, string? WorkNumber, string? Fin,
    decimal? Score, string? Group, string? Source, string? Gender, string? Year,
    int? Packet, string? Status, string? PrintStatus,
    Dictionary<string, decimal>? Subjects, Dictionary<int, string>? BranchByLevel);

public record StudentUpdateDto(
    string Name, string? ParentName, string? WorkNumber, string? Fin,
    decimal? Score, string? Group, string? Source, string? Gender, string? Year,
    int? Packet, string? Status, string? PrintStatus,
    string? PlacedSpecialty, string? PlacedSpecialtyId, string? PlacedSelectionId, int? ChoiceNum,
    Dictionary<string, decimal>? Subjects, Dictionary<int, string>? BranchByLevel);

// Distribution/Redistribute-in "bazaya yaz"/"rollback"/"qismən yenidən yerləşdirmə" əməliyyatları
// üçün — yalnız yerləşdirmə ilə bağlı sahələr, hamısı bir sorğuda təhvil verilir
public record StudentBulkPatch(
    string Id, string? PlacedSpecialty, string? PlacedSpecialtyId,
    string? PlacedSelectionId, int? ChoiceNum, string? Status);

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin,operator,student")]
public class StudentsController : ControllerBase
{
    private readonly MmuDbContext _db;
    public StudentsController(MmuDbContext db) => _db = db;

    // Tələbə tokeni yalnız ÖZ qeydinə çata bilər; admin/operator hər kimə
    private bool IsOwnRecordOrStaff(string studentId)
    {
        if (User.IsInRole("admin") || User.IsInRole("operator")) return true;
        var selfId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return selfId == studentId;
    }

    [HttpGet]
    [Authorize(Roles = "admin,operator")]
    public async Task<ActionResult<IEnumerable<Student>>> GetAll([FromQuery] string? institutionId)
    {
        var query = _db.Students.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(institutionId))
            query = query.Where(s => s.InstitutionId == institutionId);
        return Ok(await query.ToListAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Student>> Get(string id)
    {
        if (!IsOwnRecordOrStaff(id)) return Forbid();
        var s = await _db.Students.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return s is null ? NotFound() : Ok(s);
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    [RequirePermission("users.edit")]
    public async Task<ActionResult<Student>> Create(StudentCreateDto dto)
    {
        var item = new Student
        {
            Id = string.IsNullOrEmpty(dto.Id) ? $"std_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}" : dto.Id,
            InstitutionId = dto.InstitutionId,
            Name = dto.Name,
            ParentName = dto.ParentName,
            WorkNumber = dto.WorkNumber,
            Fin = dto.Fin,
            Score = dto.Score,
            Group = dto.Group,
            Source = dto.Source,
            Gender = dto.Gender,
            Year = dto.Year,
            Packet = dto.Packet,
            Status = dto.Status ?? "pending",
            PrintStatus = dto.PrintStatus ?? "not_printed",
            Subjects = dto.Subjects ?? new(),
            BranchByLevel = dto.BranchByLevel ?? new(),
        };
        _db.Students.Add(item);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    // Excel idxal / arxivdən bərpa: bir sorğuda çoxlu tam tələbə qeydi yaradır
    [HttpPost("bulk-create")]
    [Authorize(Roles = "admin")]
    [RequirePermission("users.import")]
    public async Task<ActionResult> BulkCreate([FromBody] List<StudentCreateDto> dtos)
    {
        var items = dtos.Select(dto => new Student
        {
            Id = string.IsNullOrEmpty(dto.Id) ? $"std_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}_{Guid.NewGuid():N}"[..24] : dto.Id,
            InstitutionId = dto.InstitutionId,
            Name = dto.Name,
            ParentName = dto.ParentName,
            WorkNumber = dto.WorkNumber,
            Fin = dto.Fin,
            Score = dto.Score,
            Group = dto.Group,
            Source = dto.Source,
            Gender = dto.Gender,
            Year = dto.Year,
            Packet = dto.Packet,
            Status = dto.Status ?? "pending",
            PrintStatus = dto.PrintStatus ?? "not_printed",
            Subjects = dto.Subjects ?? new(),
            BranchByLevel = dto.BranchByLevel ?? new(),
        }).ToList();

        _db.Students.AddRange(items);
        await _db.SaveChangesAsync();
        return Ok(new { count = items.Count });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, StudentUpdateDto dto)
    {
        if (!IsOwnRecordOrStaff(id)) return Forbid();
        var item = await _db.Students.FindAsync(id);
        if (item is null) return NotFound();

        // Tələbə özü yalnız "seçimi göndərdim" statusunu işarələyə bilər — digər sahələr
        // (bal, yerləşdirmə və s.) yalnız admin/operator tərəfindən dəyişdirilə bilər
        var isStaff = User.IsInRole("admin") || User.IsInRole("operator");
        if (isStaff)
        {
            item.Name = dto.Name;
            item.ParentName = dto.ParentName;
            item.WorkNumber = dto.WorkNumber;
            item.Fin = dto.Fin;
            item.Score = dto.Score;
            item.Group = dto.Group;
            item.Source = dto.Source;
            item.Gender = dto.Gender;
            item.Year = dto.Year;
            item.Packet = dto.Packet;
            if (dto.PrintStatus is not null) item.PrintStatus = dto.PrintStatus;
            item.PlacedSpecialty = dto.PlacedSpecialty;
            item.PlacedSpecialtyId = dto.PlacedSpecialtyId;
            item.PlacedSelectionId = dto.PlacedSelectionId;
            item.ChoiceNum = dto.ChoiceNum;
            if (dto.Subjects is not null) item.Subjects = dto.Subjects;
            if (dto.BranchByLevel is not null) item.BranchByLevel = dto.BranchByLevel;
        }
        if (dto.Status is not null) item.Status = dto.Status;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Distribution (bazaya yaz/rollback) və Redistribute (qismən yerləşdirmə) — N tələbənin
    // yerləşdirmə sahələrini bir sorğuda yeniləyir
    [HttpPost("bulk-update")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> BulkUpdate([FromBody] List<StudentBulkPatch> patches)
    {
        var ids = patches.Select(p => p.Id).ToList();
        var items = await _db.Students.Where(s => ids.Contains(s.Id)).ToListAsync();
        var byId = items.ToDictionary(s => s.Id);

        foreach (var p in patches)
        {
            if (!byId.TryGetValue(p.Id, out var item)) continue;
            item.PlacedSpecialty = p.PlacedSpecialty;
            item.PlacedSpecialtyId = p.PlacedSpecialtyId;
            item.PlacedSelectionId = p.PlacedSelectionId;
            item.ChoiceNum = p.ChoiceNum;
            if (p.Status is not null) item.Status = p.Status;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("delete-many")]
    [Authorize(Roles = "admin")]
    [RequirePermission("users.delete")]
    public async Task<IActionResult> DeleteMany([FromBody] List<string> ids)
    {
        var items = await _db.Students.Where(s => ids.Contains(s.Id)).ToListAsync();
        _db.Students.RemoveRange(items);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    [RequirePermission("users.delete")]
    public async Task<IActionResult> Delete(string id)
    {
        var item = await _db.Students.FindAsync(id);
        if (item is null) return NotFound();
        _db.Students.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
