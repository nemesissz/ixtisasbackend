using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MmuIspApi.Data;
using MmuIspApi.Models;
using MmuIspApi.Services;

namespace MmuIspApi.Controllers;

public record LoginFieldConfigDto(string Column, string Label, int Min, int Max, bool Required);
public record InstLoginConfigDto(LoginFieldConfigDto Field1, LoginFieldConfigDto Field2);

[ApiController]
[Route("api/[controller]")]
public class SystemSettingsController : ControllerBase
{
    private readonly MmuDbContext _db;
    public SystemSettingsController(MmuDbContext db) => _db = db;

    private static readonly InstLoginConfigDto DefaultConfig = new(
        new LoginFieldConfigDto("fin", "FİN Kodu", 1, 20, true),
        new LoginFieldConfigDto("workNumber", "İş Nömrəsi", 1, 20, true));

    [HttpGet("inst-config/{institutionId}")]
    public async Task<ActionResult<InstLoginConfigDto>> GetInstConfig(string institutionId)
    {
        var cfg = await _db.InstitutionLoginConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.InstitutionId == institutionId);
        if (cfg is null) return Ok(DefaultConfig);

        return Ok(new InstLoginConfigDto(
            new LoginFieldConfigDto(cfg.Field1Column, cfg.Field1Label, cfg.Field1Min, cfg.Field1Max, cfg.Field1Required),
            new LoginFieldConfigDto(cfg.Field2Column, cfg.Field2Label, cfg.Field2Min, cfg.Field2Max, cfg.Field2Required)));
    }

    [HttpPut("inst-config/{institutionId}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> SetInstConfig(string institutionId, InstLoginConfigDto dto)
    {
        // Müəssisə əhatəsi: scoped admin yalnız öz müəssisəsinin login konfigini dəyişə bilər
        if (!User.CanAccessInstitution(institutionId)) return Forbid();
        var cfg = await _db.InstitutionLoginConfigs.FirstOrDefaultAsync(c => c.InstitutionId == institutionId);
        if (cfg is null)
        {
            cfg = new InstitutionLoginConfig { InstitutionId = institutionId };
            _db.InstitutionLoginConfigs.Add(cfg);
        }
        cfg.Field1Column = dto.Field1.Column; cfg.Field1Label = dto.Field1.Label;
        cfg.Field1Min = dto.Field1.Min; cfg.Field1Max = dto.Field1.Max; cfg.Field1Required = dto.Field1.Required;
        cfg.Field2Column = dto.Field2.Column; cfg.Field2Label = dto.Field2.Label;
        cfg.Field2Min = dto.Field2.Min; cfg.Field2Max = dto.Field2.Max; cfg.Field2Required = dto.Field2.Required;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("redirect-delay")]
    public async Task<ActionResult<int>> GetRedirectDelay()
    {
        var s = await _db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1);
        return Ok(s?.RedirectDelaySec ?? 10);
    }

    [HttpPut("redirect-delay")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> SetRedirectDelay([FromBody] int seconds)
    {
        var s = await _db.SystemSettings.FirstOrDefaultAsync(x => x.Id == 1);
        if (s is null) { s = new SystemSetting { Id = 1 }; _db.SystemSettings.Add(s); }
        s.RedirectDelaySec = Math.Max(0, seconds);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("priority-subjects")]
    public async Task<ActionResult<List<string>>> GetPrioritySubjects()
    {
        var s = await _db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1);
        return Ok(s?.PrioritySubjects ?? new List<string>());
    }

    [HttpPut("priority-subjects")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> SetPrioritySubjects([FromBody] List<string> subjects)
    {
        var s = await _db.SystemSettings.FirstOrDefaultAsync(x => x.Id == 1);
        if (s is null) { s = new SystemSetting { Id = 1 }; _db.SystemSettings.Add(s); }
        s.PrioritySubjects = subjects ?? new();
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("reset")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Reset()
    {
        _db.InstitutionLoginConfigs.RemoveRange(_db.InstitutionLoginConfigs);
        var s = await _db.SystemSettings.FirstOrDefaultAsync(x => x.Id == 1);
        if (s is null) { s = new SystemSetting { Id = 1 }; _db.SystemSettings.Add(s); }
        s.RedirectDelaySec = 10;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
