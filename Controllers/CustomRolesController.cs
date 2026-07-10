using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MmuIspApi.Data;
using MmuIspApi.Models;
using MmuIspApi.Services;

namespace MmuIspApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
[RequirePermission("admins.manage")]
public class CustomRolesController : ControllerBase
{
    private readonly MmuDbContext _db;
    public CustomRolesController(MmuDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<string>>> GetAll() =>
        Ok(await _db.CustomRoles.AsNoTracking().Select(r => r.Name).ToListAsync());

    [HttpPost]
    public async Task<ActionResult<string>> Add([FromBody] string name)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrEmpty(trimmed)) return BadRequest();
        if (!await _db.CustomRoles.AnyAsync(r => r.Name == trimmed))
        {
            _db.CustomRoles.Add(new CustomRole { Name = trimmed });
            await _db.SaveChangesAsync();
        }
        return new JsonResult(trimmed);
    }

    [HttpDelete("{name}")]
    public async Task<IActionResult> Remove(string name)
    {
        var role = await _db.CustomRoles.FirstOrDefaultAsync(r => r.Name == name);
        if (role is not null)
        {
            _db.CustomRoles.Remove(role);
            await _db.SaveChangesAsync();
        }
        return NoContent();
    }
}
