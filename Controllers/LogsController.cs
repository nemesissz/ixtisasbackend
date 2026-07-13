using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MmuIspApi.Data;
using MmuIspApi.Models;
using MmuIspApi.Services;

namespace MmuIspApi.Controllers;

public record LogCreateDto(string Category, string Type, string Message, string? Detail, string? Actor, string? Ip);

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

    // Müraciət edənin IP-si: nginx proxy arxasındayıqsa X-Forwarded-For / X-Real-IP,
    // birbaşa müraciətdə isə TCP bağlantısının ünvanı
    private string? ClientIp()
    {
        var fwd = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(fwd)) return fwd.Split(',')[0].Trim();
        var real = Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(real)) return real.Trim();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        return ip == "::1" ? "127.0.0.1" : ip;
    }

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
            // Frontend host-dakı ip-helper-dən real IP-ni göndəribsə onu üstün tut
            // (Docker Desktop Windows-da proxy mənbə IP-ni itirir); yoxsa özümüz tapaq
            Ip = string.IsNullOrWhiteSpace(dto.Ip) ? ClientIp() : dto.Ip.Trim(),
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
