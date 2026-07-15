namespace MmuIspApi.Models;

// superadmin | admin | moderator | (xüsusi rollar)
public class Admin
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Email { get; set; }
    public string Username { get; set; } = default!;

    // BCrypt hash — heç vaxt açıq mətn saxlanılmır
    public string PasswordHash { get; set; } = default!;

    // Sərbəst mətn — superadmin/admin/moderator daxil, admin CustomRole yarada bilər
    public string Role { get; set; } = "admin";
    public string Status { get; set; } = "active";
    public DateTime? LastLogin { get; set; }

    // superadmin olmayan hesablar üçün fərdi icazə override-ları (permissions.ts:PERM_GROUPS kodları)
    public List<string>? Permissions { get; set; }

    // Müəssisə əhatəsi: null/boş = bütün müəssisələr (superadmin və məhdudlaşdırılmamış hesablar).
    // Doludursa hesab yalnız bu müəssisə(lər)in datasını görür (institution scope).
    public List<string>? Institutions { get; set; }
}
