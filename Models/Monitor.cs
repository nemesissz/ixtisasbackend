namespace MmuIspApi.Models;

// ── Canlı nəzarət (docs/PLAN-canli-nezaret.md) ──────────────────────────────
// Ayrı cədvəllər: Students-ə heç nə yazılmır ki, yerləşdirmə/hesabat məntiqinə
// toxunulmasın və kilid toqquşması olmasın.

// Tək sətirlik konfiqurasiya (Id həmişə 1)
public class MonitorConfig
{
    public int Id { get; set; } = 1;
    public bool Enabled { get; set; }
    public int HeartbeatSec { get; set; } = 30;   // təhsilalan səhifəsinin siqnal intervalı
    public int OfflineSec { get; set; } = 90;     // bu qədər siqnal gəlməsə "əlaqə kəsildi"
    public int AbandonMin { get; set; } = 20;     // bu qədər siqnal gəlməsə "yarımçıq"
}

// Bir seçim seansı — həmin seçimə ilk təhsilalanın siqnalı ilə başlayır.
// Hər seçimin öz seansı var; yalnız superadmin "Bitir" basanda bağlanır.
public class MonitorSession
{
    public int Id { get; set; }
    public string? SelectionId { get; set; }
    public string? SelectionName { get; set; }          // seçim sonradan silinsə də ad görünsün
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string Status { get; set; } = "running";   // running | paused | ended
    public int PausedSeconds { get; set; }             // bitmiş pauzaların cəmi
    public DateTime? LastPausedAt { get; set; }        // cari pauzanın başlanğıcı
}

// Kim indi seçimdədir (bir təhsilalan = bir sətir)
public class StudentPresence
{
    public string StudentId { get; set; } = null!;
    public int SessionId { get; set; }
    public string Name { get; set; } = "";
    public string? Fin { get; set; }
    public string? Group { get; set; }
    public string? InstitutionId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string State { get; set; } = "active";     // active | submitted | abandoned
}
