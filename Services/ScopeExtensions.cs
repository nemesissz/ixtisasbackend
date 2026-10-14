using System.Security.Claims;
using MmuIspApi.Data;
using MmuIspApi.Models;

namespace MmuIspApi.Services;

public static class ScopeExtensions
{
    // Cari istifadəçinin JWT-dəki müəssisə əhatəsi (inst claim-ləri).
    // null qaytarırsa → məhdudiyyət yoxdur (superadmin/məhdudlaşdırılmamış hesab, tələbə, anonim).
    // Doludursa → yalnız bu müəssisələr görünməlidir.
    public static List<string>? AllowedInstitutions(this ClaimsPrincipal user)
    {
        var ids = user.FindAll("inst").Select(c => c.Value).ToList();
        return ids.Count > 0 ? ids : null;
    }

    // Yazma əməliyyatları üçün: hesab bu müəssisəyə toxuna bilərmi?
    // Məhdudlaşdırılmamış hesab (superadmin) → həmişə true.
    // Məhdud hesab → yalnız icazəli müəssisələr üçün true (boş/naməlum institution → false).
    public static bool CanAccessInstitution(this ClaimsPrincipal user, string? institutionId)
    {
        var allowed = user.AllowedInstitutions();
        if (allowed is null) return true;
        return institutionId is not null && allowed.Contains(institutionId);
    }

    // Qrup əhatəsi: (qrupla məhdudlaşdırılmış müəssisələr, icazəli qruplar).
    // null → qrup məhdudiyyəti yoxdur.
    public static CohortScope? GetCohortScope(this ClaimsPrincipal user)
    {
        var insts = user.FindAll("cohinst").Select(c => c.Value).ToList();
        if (insts.Count == 0) return null;
        return new CohortScope(insts, user.FindAll("coh").Select(c => c.Value).ToList());
    }

    // Bu müəssisə/qrup cütü hesaba görünürmü? (məhdudlaşdırılmamış müəssisədə hər qrup görünür;
    // məhdudlaşdırılmışda qrupsuz qeydlər görünmür)
    public static bool CanAccessCohort(this ClaimsPrincipal user, string? institutionId, string? cohortId)
    {
        var cs = user.GetCohortScope();
        if (cs is null || institutionId is null || !cs.Institutions.Contains(institutionId)) return true;
        return cohortId is not null && cs.Cohorts.Contains(cohortId);
    }

    // ── Qrup əhatəsi üzrə sorğu filtrləri ──
    public static IQueryable<Student> InCohortScope(this IQueryable<Student> q, ClaimsPrincipal user)
    {
        var cs = user.GetCohortScope();
        if (cs is null) return q;
        var ri = cs.Institutions; var rc = cs.Cohorts;
        return q.Where(s => !ri.Contains(s.InstitutionId) || (s.CohortId != null && rc.Contains(s.CohortId)));
    }

    public static IQueryable<Cohort> InCohortScope(this IQueryable<Cohort> q, ClaimsPrincipal user)
    {
        var cs = user.GetCohortScope();
        if (cs is null) return q;
        var ri = cs.Institutions; var rc = cs.Cohorts;
        return q.Where(c => !ri.Contains(c.InstitutionId) || rc.Contains(c.Id));
    }

    public static IQueryable<SpecialtyTree> InCohortScope(this IQueryable<SpecialtyTree> q, ClaimsPrincipal user)
    {
        var cs = user.GetCohortScope();
        if (cs is null) return q;
        var ri = cs.Institutions; var rc = cs.Cohorts;
        return q.Where(t => !ri.Contains(t.InstitutionId) || (t.CohortId != null && rc.Contains(t.CohortId)));
    }

    // Seçim qrupa strukturu (SpecialtyTree.CohortId) vasitəsilə bağlıdır
    public static IQueryable<Selection> InCohortScope(this IQueryable<Selection> q, ClaimsPrincipal user, MmuDbContext db)
    {
        var cs = user.GetCohortScope();
        if (cs is null) return q;
        var ri = cs.Institutions; var rc = cs.Cohorts;
        return q.Where(s => !ri.Contains(s.InstitutionId)
            || db.SpecialtyTrees.Any(t => t.Id == s.TreeId && t.CohortId != null && rc.Contains(t.CohortId)));
    }
}

public record CohortScope(List<string> Institutions, List<string> Cohorts);
