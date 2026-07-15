using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace MmuIspApi.Services;

public class JwtTokenService
{
    private readonly IConfiguration _config;
    public JwtTokenService(IConfiguration config) => _config = config;

    // `roles`: real rolla yanaşı ümumi panel-scope-u da daşıyır (məs. ["moderator","admin"])
    // ki, [Authorize(Roles="admin")] bütün admin-panel hesablarını (superadmin/admin/moderator/custom) tutsun.
    public string CreateToken(string id, IEnumerable<string> roles, string name, IEnumerable<string>? permissions = null, string? institutionId = null, IEnumerable<string>? institutions = null)
    {
        var jwt = _config.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, id),
            new("name", name),
        };
        foreach (var r in roles) claims.Add(new Claim(ClaimTypes.Role, r));
        if (institutionId is not null) claims.Add(new Claim("institutionId", institutionId));
        foreach (var p in permissions ?? Enumerable.Empty<string>())
            claims.Add(new Claim("perm", p));
        // Müəssisə əhatəsi (scope) — doludursa hesab yalnız bu müəssisələri görür
        foreach (var inst in institutions ?? Enumerable.Empty<string>())
            claims.Add(new Claim("inst", inst));

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(double.Parse(jwt["ExpiryMinutes"] ?? "480")),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
