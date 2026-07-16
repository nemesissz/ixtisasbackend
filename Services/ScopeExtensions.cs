using System.Security.Claims;

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
}
