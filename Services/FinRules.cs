using Microsoft.EntityFrameworkCore;
using MmuIspApi.Data;
using MmuIspApi.Models;

namespace MmuIspApi.Services;

// FİN unikallığı QRUP daxilindədir: eyni FİN-li təhsilalan başqa qrupda və ya başqa
// müəssisədə ayrıca qeyd kimi ola bilər (yenidən idxal olunub yeni seçimə girməsi üçün).
// Seçim/yerləşdirmə tələbə Id-sinə bağlı olduğundan bu qeydlər bir-birinə qarışmır.
// Qrupsuz təhsilalanlar (CohortId = null) müəssisə daxilində ayrıca bir qrup sayılır.
public static class FinRules
{
    // AuthController.Norm və frontend normalize ilə eyni
    public static string Norm(string? v) =>
        (v ?? "").Trim().Replace("İ", "I").Replace("ı", "I").Replace("i", "I").ToUpperInvariant();

    public static string ScopeKey(string institutionId, string? cohortId) =>
        cohortId != null ? $"c:{cohortId}" : $"i:{institutionId}";

    public record Candidate(string? Id, string InstitutionId, string? CohortId, string? Fin, string? Name, int Row);
    public record Conflict(string Fin, string? Name, int Row, string? ExistingId, string? ExistingName, bool InFile);

    // Verilmiş (yeni və ya dəyişdirilmiş) qeydlər üçün eyni qrupda FİN toqquşmalarını tapır:
    // həm siyahının öz içində, həm də bazadakı digər qeydlərlə. Boş FİN yoxlanılmır.
    public static async Task<List<Conflict>> FindAsync(MmuDbContext db, IReadOnlyList<Candidate> items)
    {
        var result = new List<Conflict>();
        var withFin = items.Where(i => Norm(i.Fin).Length > 0).ToList();
        if (withFin.Count == 0) return result;

        var ownIds = withFin.Where(i => i.Id != null).Select(i => i.Id!).ToHashSet();
        var cohortIds = withFin.Where(i => i.CohortId != null).Select(i => i.CohortId!).Distinct().ToList();
        var noCohortInsts = withFin.Where(i => i.CohortId == null).Select(i => i.InstitutionId).Distinct().ToList();

        var existing = await db.Students.AsNoTracking()
            .Where(s => s.Fin != null && s.Fin != "" &&
                        ((s.CohortId != null && cohortIds.Contains(s.CohortId)) ||
                         (s.CohortId == null && noCohortInsts.Contains(s.InstitutionId))))
            .Select(s => new { s.Id, s.InstitutionId, s.CohortId, s.Fin, s.Name })
            .ToListAsync();
        // Dəyişdirilən qeydlərin köhnə halı nəzərə alınmır — onların yeni halı siyahıdadır
        var byKey = existing.Where(s => !ownIds.Contains(s.Id))
            .GroupBy(s => (ScopeKey(s.InstitutionId, s.CohortId), Norm(s.Fin)))
            .ToDictionary(g => g.Key, g => g.First());

        var seen = new Dictionary<(string, string), Candidate>();
        foreach (var c in withFin)
        {
            var key = (ScopeKey(c.InstitutionId, c.CohortId), Norm(c.Fin));
            if (byKey.TryGetValue(key, out var ex))
                result.Add(new Conflict(Norm(c.Fin), c.Name, c.Row, ex.Id, ex.Name, false));
            else if (seen.TryGetValue(key, out var first))
                result.Add(new Conflict(Norm(c.Fin), c.Name, c.Row, first.Id, first.Name, true));
            else
                seen[key] = c;
        }
        return result;
    }

    public static string Message(List<Conflict> conflicts)
    {
        var head = string.Join(" · ", conflicts.Take(5).Select(c =>
            $"{c.Fin} ({c.Name}{(c.InFile ? " — siyahıda təkrarlanır" : $" — qrupda artıq var: {c.ExistingName}")})"));
        var more = conflicts.Count > 5 ? $" və daha {conflicts.Count - 5}" : "";
        return $"FİN toqquşması: bu qrupda eyni FİN-li təhsilalan ola bilməz. {head}{more}.";
    }

    public static object Body(List<Conflict> conflicts) => new
    {
        message = Message(conflicts),
        code = "fin_conflict",
        conflicts = conflicts.Select(c => new
        {
            fin = c.Fin, name = c.Name, row = c.Row,
            existingId = c.ExistingId, existingName = c.ExistingName, inFile = c.InFile,
        }),
    };
}
