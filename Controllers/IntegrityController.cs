using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MmuIspApi.Data;
using MmuIspApi.Services;

namespace MmuIspApi.Controllers;

// Bazanın bütövlük möhürü — data (təhsilalanlar + seçim sıralamaları) deterministik
// serializasiya olunub SHA-256 ilə "barmaq izi" verilir. Eyni data həmişə eyni hash verir.
// Sonra yenidən çağırıб müqayisə etməklə dəyişiklik olub-olmadığı yoxlanır.
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
public class IntegrityController : ControllerBase
{
    private readonly MmuDbContext _db;
    public IntegrityController(MmuDbContext db) => _db = db;

    [HttpGet("seal")]
    [RequirePermission("integrity.view")]
    public async Task<IActionResult> Seal()
    {
        // Müəssisə əhatəsi: scoped admin yalnız icazəli müəssisələri möhürləyir, superadmin hamısını
        var allowed = User.AllowedInstitutions();

        var studentsQ = _db.Students.AsNoTracking().AsQueryable();
        if (allowed is not null) studentsQ = studentsQ.Where(s => allowed.Contains(s.InstitutionId));
        var students = await studentsQ.OrderBy(s => s.Id).ToListAsync();

        var studentIds = students.Select(s => s.Id).ToHashSet();
        var subs = await _db.Submissions.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        subs = subs.Where(x => studentIds.Contains(x.UserId)).ToList();

        var sb = new StringBuilder();

        sb.Append("STUDENTS\n");
        foreach (var s in students)
        {
            sb.Append(s.Id).Append('|')
              .Append(s.InstitutionId).Append('|')
              .Append(s.Name).Append('|')
              .Append(s.ParentName ?? "").Append('|')
              .Append(s.WorkNumber ?? "").Append('|')
              .Append(s.Fin ?? "").Append('|')
              .Append(Num(s.Score)).Append('|')
              .Append(s.Group ?? "").Append('|')
              .Append(s.Source ?? "").Append('|')
              .Append(s.Gender ?? "").Append('|')
              .Append(s.Packet?.ToString(CultureInfo.InvariantCulture) ?? "").Append('|')
              .Append(s.Status).Append('|')
              .Append(s.PrintStatus).Append('|')
              .Append(s.Year ?? "").Append('|')
              .Append(s.PlacedSpecialty ?? "").Append('|')
              .Append(s.PlacedSpecialtyId ?? "").Append('|')
              .Append(s.ChoiceNum?.ToString(CultureInfo.InvariantCulture) ?? "").Append('|')
              .Append(s.PlacedSelectionId ?? "").Append('|')
              .Append(Subjects(s.Subjects)).Append('|')
              .Append(Branches(s.BranchByLevel)).Append('\n');
        }

        sb.Append("SUBMISSIONS\n");
        foreach (var x in subs)
        {
            sb.Append(x.Id).Append('|')
              .Append(x.UserId).Append('|')
              .Append(x.SelectionId).Append('|')
              .Append(string.Join(">", x.Ranking)).Append('\n');
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        return Ok(new
        {
            hash,
            students = students.Count,
            submissions = subs.Count,
            at = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"),
            algorithm = "SHA-256",
        });
    }

    // ── Deterministik köməkçilər ──
    private static string Num(decimal? d) =>
        d?.ToString("0.####", CultureInfo.InvariantCulture) ?? "";

    private static string Subjects(Dictionary<string, decimal>? d)
    {
        if (d is null || d.Count == 0) return "";
        return string.Join(",", d.OrderBy(k => k.Key, StringComparer.Ordinal)
            .Select(k => $"{k.Key}={k.Value.ToString("0.####", CultureInfo.InvariantCulture)}"));
    }

    private static string Branches(Dictionary<int, string>? d)
    {
        if (d is null || d.Count == 0) return "";
        return string.Join(",", d.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}"));
    }
}
