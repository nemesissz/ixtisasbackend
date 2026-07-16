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
builder.Services.AddCors(options =>
{
    options.AddPolicy(frontendCorsPolicy, policy =>
    {
        // Vite dev server portu tutulu olsa avtomatik başqa port seçir (5173, 5174, ...) —
        // ona görə port deyil, host-u yoxlayırıq (yalnız lokal development üçün təhlükəsizdir)
        policy.SetIsOriginAllowed(origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
                return uri.Host is "localhost" or "127.0.0.1";
            })
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Superadmin yoxdursa bir dəfəlik yaradılır (parol BCrypt ilə hash-lənir)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MmuDbContext>();
    db.Database.Migrate();
    if (!db.Admins.Any(a => a.Role == "superadmin"))
    {
        db.Admins.Add(new MmuIspApi.Models.Admin
        {
            Id = "adm_super_1",
            Name = "Super Admin",
            Email = "admin@mmu.az",
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@2026"),
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

app.UseHttpsRedirection();

app.UseCors(frontendCorsPolicy);

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
