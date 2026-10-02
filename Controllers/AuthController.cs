using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MmuIspApi.Data;
using MmuIspApi.Models;
using MmuIspApi.Services;

namespace MmuIspApi.Controllers;

// InstitutionId göndərilmir — tələbə hələ giriş etməyib, hansı müəssisəyə aid
// olduğunu bilmirik. Frontend-dəki Landing.tsx kimi bütün müəssisələr üzrə axtarılır.
public record StudentLoginDto(string Field1Value, string Field2Value);

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly MmuDbContext _db;
    private readonly JwtTokenService _jwt;
    public AuthController(MmuDbContext db, JwtTokenService jwt) { _db = db; _jwt = jwt; }

    [HttpPost("student-login")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("login")]
    public async Task<ActionResult> StudentLogin(StudentLoginDto dto)
    {
        var v1 = Norm(dto.Field1Value);
        var v2 = Norm(dto.Field2Value);
        var r1 = (dto.Field1Value ?? "").Trim();
        var r2 = (dto.Field2Value ?? "").Trim();

        var institutions = await _db.Institutions.AsNoTracking().Select(i => i.Id).ToListAsync();
        var configs = await _db.InstitutionLoginConfigs.AsNoTracking().ToListAsync();

        // Verilmiş tələbə hovuzunda uyğun qeydi tapır (frontend Landing.tsx ilə eyni məntiq):
        // hər müəssisənin öz sütun konfiqi + fin/workNumber fallback-ı.
        Student? MatchIn(List<Student> pool)
        {
            foreach (var instId in institutions)
            {
                var cfg = configs.FirstOrDefault(c => c.InstitutionId == instId);
                var col1 = cfg?.Field1Column ?? "fin";
                var col2 = cfg?.Field2Column ?? "workNumber";
                var req1 = cfg?.Field1Required ?? true;
                var req2 = cfg?.Field2Required ?? true;
                var m = pool.FirstOrDefault(s =>
                {
                    if (s.InstitutionId != instId) return false;
                    var a = Norm(StudentColumnValue(s, col1));
                    var b = Norm(StudentColumnValue(s, col2));
                    // İki sıra da yoxlanılır: istifadəçi dəyərləri yerini dəyişik yaza bilər
                    return Matches(a, b, v1, v2, req1, req2)
                        || Matches(a, b, v2, v1, req1, req2);
                });
                if (m is not null) return m;
            }
            return pool.FirstOrDefault(s =>
            {
                var a = Norm(s.Fin ?? "");
                var b = Norm(s.WorkNumber ?? "");
                return (a == v1 && b == v2) || (a == v2 && b == v1);
            });
        }

        // Sürətli yol: fin/workNumber indeksləri ilə yalnız uyğun namizədləri çək (240 yox, ~1-4 sətir).
        // Kursantların əksəriyyəti standart fin/iş-nömrəsi girişi olduğu üçün bu yolla dərhal tapılır.
        var candidates = await _db.Students.AsNoTracking()
            .Where(s => s.Fin == r1 || s.Fin == r2 || s.WorkNumber == r1 || s.WorkNumber == r2)
            .ToListAsync();
        var match = MatchIn(candidates);

        // Tapılmasa (qeyri-standart sütun konfiqi və ya format fərqi) köhnə tam-siyahı məntiqi —
        // davranış dəyişmir, sadəcə uğurlu adi login-lər tam cədvəl oxumur.
        if (match is null)
            match = MatchIn(await _db.Students.AsNoTracking().ToListAsync());

        if (match is null) return Unauthorized();

        var token = _jwt.CreateToken(match.Id, new[] { "student" }, match.Name, institutionId: match.InstitutionId);
        return Ok(new { token, student = match });
    }

    // Sahə "məcburi deyil" konfiqurasiya edilib və boş göndərilibsə, o sahə
    // uyğunlaşdırmada iştirak etmir — giriş yalnız dolu sahə(lər)ə görə olur.
    // Ən azı bir sahə real olaraq uyğunlaşmalıdır: əks halda hər ikisi boş
    // göndərilməklə istənilən hesaba girmək mümkün olardı.
    private static bool Matches(string a, string b, string x, string y, bool req1, bool req2)
    {
        var hits = 0;
        if (x.Length == 0) { if (req1) return false; }
        else { if (a != x) return false; hits++; }

        if (y.Length == 0) { if (req2) return false; }
        else { if (b != y) return false; hits++; }

        return hits > 0;
    }

    // Frontend-dəki normalize funksiyası ilə eyni: Azərbaycan İ/ı/i xüsusi hərflərini
    // ASCII "I"-a çevirib böyük hərfə salır ki, dil-spesifik case-fold fərqi yaranmasın
    private static string Norm(string? v) =>
        (v ?? "").Trim().Replace("İ", "I").Replace("ı", "I").Replace("i", "I").ToUpperInvariant();

    private static string StudentColumnValue(Student s, string column) => column switch
    {
        "fin" => s.Fin ?? "",
        "workNumber" => s.WorkNumber ?? "",
        "firstName" => s.Name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "",
        "lastName" => string.Join(' ', s.Name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1)),
        "parentName" => s.ParentName ?? "",
        "group" => s.Group ?? "",
        _ => "",
    };
}
