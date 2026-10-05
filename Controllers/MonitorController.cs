using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MmuIspApi.Data;
using MmuIspApi.Models;

namespace MmuIspApi.Controllers;

public record MonitorBeatDto(bool Submitted, string? SelectionId = null);
public record MonitorConfigDto(bool Enabled, int HeartbeatSec, int OfflineSec, int AbandonMin);

// Canlı nəzarət (docs/PLAN-canli-nezaret.md).
// Təhsilalan səhifəsi heartbeat göndərir, superadmin canlı ekranı yüngül poll edir.
// Bütün vaxtlar server UTC-dən — kompüterlərin saat fərqi nəticəyə təsir etmir.
[ApiController]
[Route("api/[controller]")]
public class MonitorController : ControllerBase
{
    private readonly MmuDbContext _db;
    public MonitorController(MmuDbContext db) => _db = db;

    // Heartbeat-lər ardıcıl işlənir: iki təhsilalanın ilk siqnalı eyni anda gələndə
    // iki seans yaranmasın və eyni presence sətri iki dəfə əlavə olunmasın.
    // Bir siqnal bir neçə ms çəkir — 50-100 kompüter üçün növbə hiss olunmur.
    private static readonly SemaphoreSlim BeatLock = new(1, 1);

    private async Task<MonitorConfig> ConfigAsync()
    {
        var c = await _db.MonitorConfigs.FirstOrDefaultAsync(x => x.Id == 1);
        if (c is null) { c = new MonitorConfig { Id = 1 }; _db.MonitorConfigs.Add(c); await _db.SaveChangesAsync(); }
        return c;
    }

    // Seçimin cari (bitməmiş) seansı — hər seçim ayrıca izlənir
    private Task<MonitorSession?> OpenSessionAsync(string? selectionId) =>
        _db.MonitorSessions.Where(s => s.Status != "ended" && s.SelectionId == selectionId)
            .OrderByDescending(s => s.Id).FirstOrDefaultAsync();

    private static int ElapsedSec(MonitorSession s, DateTime now)
    {
        var end = s.EndedAt ?? (s.Status == "paused" && s.LastPausedAt.HasValue ? s.LastPausedAt.Value : now);
        return Math.Max(0, (int)(end - s.StartedAt).TotalSeconds - s.PausedSeconds);
    }

    private static void EndSession(MonitorSession s, DateTime now)
    {
        if (s.Status == "paused" && s.LastPausedAt.HasValue)
            s.PausedSeconds += (int)(now - s.LastPausedAt.Value).TotalSeconds;
        s.Status = "ended"; s.EndedAt = now; s.LastPausedAt = null;
    }

    // Uzun müddət siqnal göndərməyənləri "yarımçıq" kimi işarələ.
    // Seans avtomatik bitmir — hamı təsdiqləsə də yalnız superadmin "Bitir" ilə bağlayır.
    private async Task SweepAsync(MonitorConfig cfg, MonitorSession s, DateTime now)
    {
        var abandonBefore = now.AddMinutes(-Math.Max(1, cfg.AbandonMin));
        var stale = await _db.StudentPresences
            .Where(p => p.SessionId == s.Id && p.State == "active" && p.LastSeenAt < abandonBefore).ToListAsync();
        foreach (var p in stale) p.State = "abandoned";
    }

    // ── Təhsilalan: heartbeat ──────────────────────────────────────────────
    [HttpPost("beat")]
    [Authorize(Roles = "student")]
    public async Task<IActionResult> Beat(MonitorBeatDto dto)
    {
        // Təhsilalan id-si HƏMİŞƏ token-dən — sorğu gövdəsindən yox
        var studentId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(studentId)) return Forbid();

        var now = DateTime.UtcNow;
        await BeatLock.WaitAsync();
        try
        {
            var cfg = await ConfigAsync();
            if (!cfg.Enabled) return Ok(new { enabled = false, nextBeatSec = 60, serverNow = now });

            var st = await _db.Students.AsNoTracking()
                .Where(x => x.Id == studentId)
                .Select(x => new { x.Name, x.Fin, x.Group, x.InstitutionId, x.Status })
                .FirstOrDefaultAsync();
            if (st is null) return NotFound();
            var submitted = dto.Submitted || st.Status == "submitted";

            // Seçimin adı seansda saxlanılır — canlı ekranda hansı seçimin getdiyi görünsün
            var selectionId = string.IsNullOrWhiteSpace(dto.SelectionId) ? null : dto.SelectionId;
            string? selectionName = null;
            if (selectionId is not null)
            {
                var sel = await _db.Selections.AsNoTracking()
                    .Where(x => x.Id == selectionId).Select(x => new { x.Name }).FirstOrDefaultAsync();
                if (sel is null) return NotFound();
                selectionName = sel.Name;
            }

            var p = await _db.StudentPresences.FirstOrDefaultAsync(x => x.StudentId == studentId);
            var s = await OpenSessionAsync(selectionId);

            // Artıq təsdiqləmiş və qeydə alınmış — heç nə etmirik
            if (p is not null && p.State == "submitted" && submitted)
                return Ok(new { enabled = true, nextBeatSec = cfg.HeartbeatSec, serverNow = now });

            // Təsdiqləmiş, amma heç izlənməyib (sistem sonradan yandırılıb) — seans açmırıq
            if (submitted && p is null)
                return Ok(new { enabled = true, nextBeatSec = cfg.HeartbeatSec, serverNow = now });

            // Bu seçimə ilk təhsilalan → yeni seans
            if (s is null)
            {
                s = new MonitorSession
                {
                    StartedAt = now, Status = "running",
                    SelectionId = selectionId, SelectionName = selectionName,
                };
                _db.MonitorSessions.Add(s);
                await _db.SaveChangesAsync();
            }

            if (p is null)
            {
                p = new StudentPresence
                {
                    StudentId = studentId, SessionId = s.Id,
                    Name = st.Name ?? "", Fin = st.Fin, Group = st.Group, InstitutionId = st.InstitutionId,
                    StartedAt = now, LastSeenAt = now, State = "active",
                };
                _db.StudentPresences.Add(p);
            }
            else if (p.SessionId != s.Id)
            {
                // Başqa (bitmiş və ya fərqli seçimin) seansından qalan sətir — bu seansda sıfırdan başlayır
                p.SessionId = s.Id; p.StartedAt = now; p.SubmittedAt = null;
                p.Name = st.Name ?? ""; p.Fin = st.Fin; p.Group = st.Group; p.InstitutionId = st.InstitutionId;
            }

            p.LastSeenAt = now;
            if (submitted) { p.State = "submitted"; p.SubmittedAt = now; }
            else p.State = "active";   // "yarımçıq" sayılmışdısa və qayıdıbsa — yenidən aktiv

            await _db.SaveChangesAsync();
            if (submitted) { await SweepAsync(cfg, s, now); await _db.SaveChangesAsync(); }

            return Ok(new { enabled = true, nextBeatSec = cfg.HeartbeatSec, serverNow = now });
        }
        finally { BeatLock.Release(); }
    }

    // ── Superadmin: canlı ekran ────────────────────────────────────────────
    [HttpGet("live")]
    [Authorize(Roles = "superadmin")]
    public async Task<IActionResult> Live([FromQuery] int? sessionId)
    {
        var now = DateTime.UtcNow;
        var cfg = await ConfigAsync();

        // Seans siyahısı: bütün açıq seanslar + son bitmiş seanslar (seçici üçün)
        var openList = await _db.MonitorSessions.AsNoTracking()
            .Where(x => x.Status != "ended").OrderByDescending(x => x.Id).ToListAsync();
        var endedList = await _db.MonitorSessions.AsNoTracking()
            .Where(x => x.Status == "ended").OrderByDescending(x => x.Id).Take(10).ToListAsync();
        var sessions = openList.Concat(endedList).Select(x => new
        {
            x.Id, x.SelectionId, x.SelectionName, x.Status, x.StartedAt, x.EndedAt,
        }).ToList();

        // Seçilmiş seans; verilməyibsə — ən son açıq, o da yoxdursa ən son bitmiş
        var s = sessionId.HasValue
            ? await _db.MonitorSessions.FirstOrDefaultAsync(x => x.Id == sessionId.Value)
            : null;
        if (s is null && sessions.Count > 0)
        {
            var pickId = sessions[0].Id;
            s = await _db.MonitorSessions.FirstOrDefaultAsync(x => x.Id == pickId);
        }

        if (s is not null && s.Status != "ended")
        {
            await BeatLock.WaitAsync();
            try { await SweepAsync(cfg, s, now); await _db.SaveChangesAsync(); }
            finally { BeatLock.Release(); }
        }

        var rows = s is null ? new List<StudentPresence>()
            : await _db.StudentPresences.AsNoTracking().Where(p => p.SessionId == s.Id).ToListAsync();

        var instLabels = await _db.Institutions.AsNoTracking().ToDictionaryAsync(i => i.Id, i => i.Label);
        string? Inst(string? id) => id != null && instLabels.TryGetValue(id, out var l) ? l : null;

        var offlineBefore = now.AddSeconds(-Math.Max(10, cfg.OfflineSec));
        var active = rows.Where(p => p.State == "active").OrderBy(p => p.StartedAt).Select(p => new
        {
            p.StudentId, p.Name, p.Fin, p.Group, institution = Inst(p.InstitutionId),
            p.StartedAt, p.LastSeenAt,
            elapsedSec = (int)(now - p.StartedAt).TotalSeconds,
            offline = p.LastSeenAt < offlineBefore,
        }).ToList();

        var abandoned = rows.Where(p => p.State == "abandoned").OrderBy(p => p.StartedAt).Select(p => new
        {
            p.StudentId, p.Name, p.Fin, p.Group, institution = Inst(p.InstitutionId),
            p.StartedAt, p.LastSeenAt,
            elapsedSec = (int)(p.LastSeenAt - p.StartedAt).TotalSeconds,
        }).ToList();

        var done = rows.Where(p => p.State == "submitted" && p.SubmittedAt.HasValue)
            .Select(p => new { p.Name, p.Fin, sec = (int)(p.SubmittedAt!.Value - p.StartedAt).TotalSeconds })
            .OrderBy(x => x.sec).ToList();

        return Ok(new
        {
            serverNow = now,
            config = new MonitorConfigDto(cfg.Enabled, cfg.HeartbeatSec, cfg.OfflineSec, cfg.AbandonMin),
            sessions,
            session = s is null ? null : new
            {
                s.Id, s.SelectionId, s.SelectionName, s.Status, s.StartedAt, s.EndedAt,
                elapsedSec = ElapsedSec(s, now),
            },
            active,
            abandoned,
            stats = new
            {
                submitted = done.Count,
                fastest = done.Count > 0 ? done.First() : null,
                slowest = done.Count > 0 ? done.Last() : null,
                averageSec = done.Count > 0 ? (int)done.Average(x => x.sec) : (int?)null,
            },
        });
    }

    [HttpGet("config")]
    [Authorize(Roles = "superadmin")]
    public async Task<ActionResult<MonitorConfigDto>> GetConfig()
    {
        var c = await ConfigAsync();
        return Ok(new MonitorConfigDto(c.Enabled, c.HeartbeatSec, c.OfflineSec, c.AbandonMin));
    }

    // Yandır/söndür dəyişəndə bu sistemə aid hər şey sıfırlanır (seanslar + presence)
    [HttpPut("config")]
    [Authorize(Roles = "superadmin")]
    public async Task<IActionResult> SetConfig(MonitorConfigDto dto)
    {
        await BeatLock.WaitAsync();
        try
        {
            var c = await ConfigAsync();
            var toggled = c.Enabled != dto.Enabled;
            c.Enabled = dto.Enabled;
            c.HeartbeatSec = Math.Clamp(dto.HeartbeatSec, 5, 600);
            c.OfflineSec = Math.Clamp(dto.OfflineSec, 15, 3600);
            c.AbandonMin = Math.Clamp(dto.AbandonMin, 1, 240);
            if (toggled) await ResetAllAsync();
            await _db.SaveChangesAsync();
            return NoContent();
        }
        finally { BeatLock.Release(); }
    }

    private async Task ResetAllAsync()
    {
        await _db.StudentPresences.ExecuteDeleteAsync();
        await _db.MonitorSessions.ExecuteDeleteAsync();
    }

    // ── Superadmin: seans idarəsi ─────────────────────────────────────────
    [HttpPost("session/{id:int}/{op}")]
    [Authorize(Roles = "superadmin")]
    public async Task<IActionResult> SessionAction(int id, string op)
    {
        var now = DateTime.UtcNow;
        await BeatLock.WaitAsync();
        try
        {
            var s = await _db.MonitorSessions.FirstOrDefaultAsync(x => x.Id == id && x.Status != "ended");
            if (s is null) return NotFound();
            switch (op)
            {
                case "pause":
                    if (s.Status == "running") { s.Status = "paused"; s.LastPausedAt = now; }
                    break;
                case "resume":
                    if (s.Status == "paused" && s.LastPausedAt.HasValue)
                    {
                        s.PausedSeconds += (int)(now - s.LastPausedAt.Value).TotalSeconds;
                        s.Status = "running"; s.LastPausedAt = null;
                    }
                    break;
                case "end":
                    EndSession(s, now);
                    break;
                default:
                    return BadRequest();
            }
            await _db.SaveChangesAsync();
            return NoContent();
        }
        finally { BeatLock.Release(); }
    }
}
