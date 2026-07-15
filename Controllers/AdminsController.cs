using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MmuIspApi.Data;
using MmuIspApi.Models;
using MmuIspApi.Services;

namespace MmuIspApi.Controllers;

public record AdminCreateDto(string Name, string? Email, string Username, string Password, string Role, List<string>? Permissions, List<string>? Institutions);
public record AdminUpdateDto(string Name, string? Email, string? Password, string Role, string Status, List<string>? Permissions, List<string>? Institutions);
public record AdminLoginDto(string Username, string Password);

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
public class AdminsController : ControllerBase
{
    private readonly MmuDbContext _db;
    private readonly JwtTokenService _jwt;
    public AdminsController(MmuDbContext db, JwtTokenService jwt) { _db = db; _jwt = jwt; }

    [HttpGet]
    [RequirePermission("admins.manage")]
    public async Task<ActionResult<IEnumerable<object>>> GetAll() =>
        Ok(await _db.Admins.AsNoTracking()
            .Select(a => new { a.Id, a.Name, a.Email, a.Username, a.Role, a.Status, a.LastLogin, a.Permissions, a.Institutions })
            .ToListAsync());

    [HttpPost]
    [RequirePermission("admins.manage")]
    public async Task<ActionResult> Create(AdminCreateDto dto)
    {
        if (await _db.Admins.AnyAsync(a => a.Username == dto.Username))
            return Conflict("Bu istifadəçi adı artıq mövcuddur.");

        var item = new Admin
        {
            Id = $"adm_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}",
            Name = dto.Name,
            Email = dto.Email,
            Username = dto.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = dto.Role,
            Status = "active",
            Permissions = dto.Permissions,
            // boş siyahı = məhdudiyyət yoxdur (bütün müəssisələr) → null saxla
            Institutions = dto.Institutions is { Count: > 0 } ? dto.Institutions : null,
        };
        _db.Admins.Add(item);
        await _db.SaveChangesAsync();
        return Ok(new { item.Id, item.Name, item.Email, item.Username, item.Role, item.Status, item.Permissions, item.Institutions });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, AdminUpdateDto dto)
    {
        // Hər admin öz hesabını (adətən sadəcə parolunu) redaktə edə bilər;
        // başqasının hesabını dəyişmək üçün admins.manage tələb olunur.
        var selfId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        var isSelf = selfId == id;
        var canManage = User.IsInRole("superadmin")
            || User.Claims.Any(c => c.Type == "perm" && c.Value == "admins.manage");
        if (!isSelf && !canManage)
            return Forbid();

        var item = await _db.Admins.FindAsync(id);
        if (item is null) return NotFound();
        item.Name = dto.Name;
        item.Email = dto.Email;
        // Rol/status/icazələr yalnız admins.manage ilə dəyişilə bilər — əks halda
        // DTO-da nə gəlirsə gəlsin iqnor olunur (öz-özünə superadmin vermə qarşısı)
        if (canManage)
        {
            item.Role = dto.Role;
            item.Status = dto.Status;
            item.Permissions = dto.Permissions;
            item.Institutions = dto.Institutions is { Count: > 0 } ? dto.Institutions : null;
        }
        if (!string.IsNullOrEmpty(dto.Password))
            item.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [RequirePermission("admins.manage")]
    public async Task<IActionResult> Delete(string id)
    {
        var item = await _db.Admins.FindAsync(id);
        if (item is null) return NotFound();
        _db.Admins.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public Task<ActionResult> Login(AdminLoginDto dto) =>
        // Admin paneli: bütün rollar (superadmin/admin/moderator/xüsusi rollar) buradan girir
        LoginWithRoles(dto, allowedRoles: null, panelScope: "admin");

    private async Task<ActionResult> LoginWithRoles(AdminLoginDto dto, string[]? allowedRoles, string panelScope)
    {
        var candidate = await _db.Admins.FirstOrDefaultAsync(a =>
            a.Username == dto.Username.Trim() && a.Status == "active" &&
            (allowedRoles == null || allowedRoles.Contains(a.Role)));

        if (candidate is null || !BCrypt.Net.BCrypt.Verify(dto.Password, candidate.PasswordHash))
            return Unauthorized();

        candidate.LastLogin = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var roles = candidate.Role == panelScope ? new[] { candidate.Role } : new[] { candidate.Role, panelScope };
        var token = _jwt.CreateToken(candidate.Id, roles, candidate.Name, candidate.Permissions, institutions: candidate.Institutions);

        return Ok(new { token, candidate.Id, candidate.Name, candidate.Email, candidate.Username, candidate.Role, candidate.Permissions, candidate.Institutions });
    }
}
