using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MmuIspApi.Data;
using MmuIspApi.Models;
using MmuIspApi.Services;

namespace MmuIspApi.Controllers;

public record LogCreateDto(string Category, string Type, string Message, string? Detail, string? Actor);

[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly MmuDbContext _db;
    public LogsController(MmuDbContext db) => _db = db;

    [HttpGet]
    [Authorize(Roles = "admin")]
    [RequirePermission("logs.view")]
    public async Task<ActionResult<IEnumerable<LogEntry>>> GetAll() =>
        Ok(await _db.Logs.AsNoTracking().OrderByDescending(l => l.Timestamp).Take(1000).ToListAsync());

    // Uğursuz login cəhdləri daxil olmaqla hər hansı JWT olmadan da yazıla bilməlidir
    [HttpPost]
    public async Task<ActionResult<LogEntry>> Add(LogCreateDto dto)
    {
        var item = new LogEntry
        {
            Id = $"log_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}",
            Category = dto.Category,
            Type = dto.Type,
            Message = dto.Message,
            Detail = dto.Detail,
            Actor = dto.Actor ?? "Sistem",
        };
        _db.Logs.Add(item);
        await _db.SaveChangesAsync();

        var toTrim = await _db.Logs.OrderByDescending(l => l.Timestamp).Skip(1000).ToListAsync();
        if (toTrim.Count > 0) _db.Logs.RemoveRange(toTrim);
        await _db.SaveChangesAsync();

        return Ok(item);
    }

    [HttpDelete]
    [Authorize(Roles = "admin")]
    [RequirePermission("logs.view")]
    public async Task<IActionResult> Clear()
    {
        _db.Logs.RemoveRange(_db.Logs);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
