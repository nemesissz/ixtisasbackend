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

    // IPv4-mapped IPv6 (::ffff:172.18.0.1) və loopback (::1) təmiz IPv4-ə gətir
    private static string? Clean(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return ip;
        ip = ip.Trim();
        if (ip == "::1") return "127.0.0.1";
        const string mapped = "::ffff:";
        if (ip.StartsWith(mapped, System.StringComparison.OrdinalIgnoreCase)) ip = ip[mapped.Length..];
        return ip;
    }

    // Müraciət edənin IP-si: nginx proxy arxasındayıqsa X-Forwarded-For / X-Real-IP,
    // birbaşa müraciətdə isə TCP bağlantısının ünvanı
    private string? ClientIp()
    {
        var fwd = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(fwd)) return Clean(fwd.Split(',')[0]);
        var real = Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(real)) return Clean(real);
        var addr = HttpContext.Connection.RemoteIpAddress;
        if (addr != null && addr.IsIPv4MappedToIPv6) addr = addr.MapToIPv4();
        return Clean(addr?.ToString());
    }

    // Uğursuz login cəhdləri JWT olmadan yazılmalıdır (istifadəçinin hələ tokeni yoxdur).
    // Amma girişsiz çağırış YALNIZ uğursuz giriş logunu yaza bilər — əks halda kənar şəxs
    // saxta/kütləvi log göndərib real audit izini (son 1000 limiti) sıxışdırıb çıxara bilər.
    // Autentifikasiya olunmuş admin/tələbə (tokeni var) hər cür logu yaza bilər.
    [HttpPost]
    public async Task<ActionResult<LogEntry>> Add(LogCreateDto dto)
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
        {
            var isFailedLogin =
                string.Equals(dto.Type, "warning", StringComparison.OrdinalIgnoreCase)
                && (dto.Message?.Contains("giriş", StringComparison.OrdinalIgnoreCase) ?? false);
            if (!isFailedLogin) return Unauthorized();
        }

        var item = new LogEntry
        {
            // Konkurentlik: 100 kursant eyni millisaniyədə login edəndə log ID toqquşmasın deyə
            // unikal Guid suffiksi (əvvəl yalnız timestamp idi → duplicate PK → 500, log itirdi)
            Id = $"log_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}_{Guid.NewGuid():N}",
            Category = dto.Category,
            Type = dto.Type,
            Message = dto.Message,
            Detail = dto.Detail,
            Actor = dto.Actor ?? "Sistem",
            Ip = ClientIp(),
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
