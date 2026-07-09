namespace MmuIspApi.Models;

// superadmin | admin | moderator | operator
public class Admin
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Email { get; set; }
    public string Username { get; set; } = default!;

    // BCrypt hash — heç vaxt açıq mətn saxlanılmır
    public string PasswordHash { get; set; } = default!;

    // Sərbəst mətn — superadmin/admin/moderator/operator daxil, admin CustomRole yarada bilər
    public string Role { get; set; } = "operator";
    public string Status { get; set; } = "active";
    public DateTime? LastLogin { get; set; }

    // superadmin olmayan hesablar üçün fərdi icazə override-ları (permissions.ts:PERM_GROUPS kodları)
    public List<string>? Permissions { get; set; }
}
