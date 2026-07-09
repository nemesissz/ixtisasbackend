using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MmuIspApi.Data;
using MmuIspApi.Models;
using MmuIspApi.Services;
using System.Text.Json;

namespace MmuIspApi.Controllers;

[ApiController]
[Route("api/user-archives")]
[Authorize(Roles = "admin")]
[RequirePermission("archive.view")]
public class UserArchivesController : ControllerBase
{
    private readonly MmuDbContext _db;
    public UserArchivesController(MmuDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetAll() =>
        Ok((await _db.UserArchives.AsNoTracking().ToListAsync())
            .Select(a => new { a.Id, a.ArchivedAt, Data = JsonSerializer.Deserialize<JsonElement>(a.DataJson) }));

    [HttpPost]
    public async Task<ActionResult> Save([FromBody] JsonElement data)
    {
        var item = new UserArchive
        {
            Id = $"uarch_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}",
            DataJson = data.GetRawText(),
        };
        _db.UserArchives.Add(item);
        await _db.SaveChangesAsync();
        return Ok(new { item.Id, item.ArchivedAt });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var item = await _db.UserArchives.FindAsync(id);
        if (item is null) return NotFound();
        _db.UserArchives.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

[ApiController]
[Route("api/tree-archives")]
[Authorize(Roles = "admin")]
[RequirePermission("archive.view")]
public class TreeArchivesController : ControllerBase
{
    private readonly MmuDbContext _db;
    public TreeArchivesController(MmuDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetAll() =>
        Ok((await _db.TreeArchives.AsNoTracking().ToListAsync())
            .Select(a => new { a.Id, a.ArchivedAt, Data = JsonSerializer.Deserialize<JsonElement>(a.DataJson) }));

    [HttpPost]
    public async Task<ActionResult> Save([FromBody] JsonElement data)
    {
        var item = new TreeArchive
        {
            Id = $"tarch_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}",
            DataJson = data.GetRawText(),
        };
        _db.TreeArchives.Add(item);
        await _db.SaveChangesAsync();
        return Ok(new { item.Id, item.ArchivedAt });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var item = await _db.TreeArchives.FindAsync(id);
        if (item is null) return NotFound();
        _db.TreeArchives.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
