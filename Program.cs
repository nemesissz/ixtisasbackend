using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MmuIspApi.Data;
using MmuIspApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers().AddJsonOptions(opts =>
{
    opts.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts =>
{
    opts.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT token: Bearer {token}",
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
    });
    opts.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default appsettings.json faylında tapılmadı.");

builder.Services.AddDbContext<MmuDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

builder.Services.AddSingleton<JwtTokenService>();

var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

builder.Services.AddAuthorization();

// Login endpoint-ləri üçün IP-yə görə rate-limit (brute-force qarşısı).
// Hər IP dəqiqədə maksimum 100 login cəhdi — real izdiham (ayrı cihazlar, hər biri 1-2 cəhd)
// təsirlənmir; yalnız bir IP-dən onlarla ard-arda cəhd (skript/hücum) 429 alır.
const string loginRateLimit = "login";
// Gerçək müştəri IP-si: nginx arxasında RemoteIpAddress həmişə nginx-dir, ona görə
// X-Forwarded-For / X-Real-IP oxunur (nginx.conf bunları ötürür). Belə olmasa
// bütün kursantlar bir IP kimi sayılıb real izdiham bloklanardı.
static string ClientKey(HttpContext ctx)
{
    var fwd = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(fwd)) return fwd.Split(',')[0].Trim();
    var real = ctx.Request.Headers["X-Real-IP"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(real)) return real.Trim();
    return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(loginRateLimit, httpContext =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ClientKey(httpContext),
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

const string frontendCorsPolicy = "FrontendCors";

// İcazəli mənşələr:
//   1) Cors__AllowedOrigins env dəyişəni verilibsə — yalnız oradakı siyahı
//      (məs. "http://192.168.50.1,http://isp.mmu.local"). İnternetə açıq quraşdırmada bunu işlədin.
//   2) Verilməyibsə — lokal maşın və DAXİLİ ŞƏBƏKƏ (RFC1918) mənşələri.
//      Proqram LAN-da işlədiyi üçün lazımdır: frontend http://192.168.50.1:5174-dən
//      açılıb API-yə http://192.168.50.1:5199 ilə müraciət edəndə bu cross-origin sayılır.
//      Yalnız localhost-a icazə vermək şəbəkədəki bütün kompüterlərdə CORS xətası verirdi.
//
// Qeyd: nginx arxasında (istehsal Docker qurğusu) sorğular eyni mənşədən gedir,
// ona görə orada bu siyasət ümumiyyətlə işə düşmür — bu, birbaşa giriş halı üçündür.
var corsAllowedOrigins = builder.Configuration["Cors:AllowedOrigins"]
    ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? [];

// Mənşə hostu lokal maşın və ya daxili (marşrutlanmayan) şəbəkə ünvanıdırmı
static bool IsLocalOrPrivateHost(string host)
{
    if (host is "localhost" or "::1") return true;
    if (!System.Net.IPAddress.TryParse(host, out var ip)) return false;
    if (System.Net.IPAddress.IsLoopback(ip)) return true;
    if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;

    var b = ip.GetAddressBytes();
    return b[0] switch
    {
        10 => true,                        // 10.0.0.0/8
        172 => b[1] >= 16 && b[1] <= 31,   // 172.16.0.0/12
        192 => b[1] == 168,                // 192.168.0.0/16
        169 => b[1] == 254,                // 169.254.0.0/16 (link-local)
        _ => false,
    };
}

builder.Services.AddCors(options =>
{
    options.AddPolicy(frontendCorsPolicy, policy =>
    {
        if (corsAllowedOrigins.Length > 0)
            policy.WithOrigins(corsAllowedOrigins);
        else
            // Port yoxlanmır: Vite tutulu portda 5173/5174/... arasında sıçrayır
            policy.SetIsOriginAllowed(origin =>
                Uri.TryCreate(origin, UriKind.Absolute, out var uri) && IsLocalOrPrivateHost(uri.Host));

        policy.AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

// Superadmin yoxdursa bir dəfəlik yaradılır (parol BCrypt ilə hash-lənir).
// Parol mənbə kodunda saxlanmır — Seed__AdminPassword env dəyişənindən oxunur
// (istehsalda .env faylından gəlir). Development-də rahatlıq üçün sabit dəyər işlənir.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MmuDbContext>();
    db.Database.Migrate();
    if (!db.Admins.Any(a => a.Role == "superadmin"))
    {
        var seed = app.Configuration.GetSection("Seed");
        var seedPassword = seed["AdminPassword"];

        if (string.IsNullOrWhiteSpace(seedPassword))
        {
            // İstehsalda susmaqla zəif parola düşmək təhlükəlidir — açıq xəta veririk
            if (!app.Environment.IsDevelopment())
                throw new InvalidOperationException(
                    "Superadmin yaradıla bilmir: Seed__AdminPassword təyin olunmayıb. " +
                    ".env faylında SEED_ADMIN_PASSWORD dəyərini verin və konteyneri yenidən başladın.");
            seedPassword = "Admin@2026";
        }

        db.Admins.Add(new MmuIspApi.Models.Admin
        {
            Id = "adm_super_1",
            Name = "Super Admin",
            Email = seed["AdminEmail"] ?? "admin@mmu.az",
            Username = seed["AdminUsername"] ?? "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(seedPassword),
            Role = "superadmin",
            Status = "active",
        });
        db.SaveChanges();
    }

    // `dotnet run -- import-bhk` ilə db.ts-dəki real BHK seed datası (institution,
    // ixtisas ağacı, 388 tələbə, sıralamalar) MySQL-ə bir dəfəlik köçürülür.
    if (args.Contains("import-bhk"))
    {
        var seedDir = Path.Combine(AppContext.BaseDirectory, "SeedData");
        await MmuIspApi.SeedData.BhkImporter.RunAsync(db, seedDir);
        return;
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// İstehsalda konteynerdə yalnız HTTP portu var (TLS/şəbəkə girişini nginx idarə edir) —
// belə olduqda yönləndirmə hədəf port tapa bilmir və hər başlanğıcda xəbərdarlıq verir.
if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseCors(frontendCorsPolicy);

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Sadə canlılıq yoxlaması — serverdə `curl http://localhost/api/health` ilə
// backend-in qalxdığını və bazaya çıxışının olduğunu bir əmrlə görmək üçün
app.MapGet("/api/health", async (MmuDbContext db) =>
{
    var dbOk = await db.Database.CanConnectAsync();
    return dbOk
        ? Results.Ok(new { status = "ok", database = "ok" })
        : Results.Json(new { status = "degraded", database = "unreachable" }, statusCode: 503);
}).AllowAnonymous();

app.Run();
