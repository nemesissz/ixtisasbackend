namespace MmuIspApi.Models;

// Frontend-dəki "user" (təhsilalan/student) qeydi
public class Student
{
    public string Id { get; set; } = default!;

    public string InstitutionId { get; set; } = default!;
    public Institution? Institution { get; set; }

    public string Name { get; set; } = default!;
    public string? ParentName { get; set; }
    public string? WorkNumber { get; set; }
    public string? Fin { get; set; }

    public decimal? Score { get; set; }
    public string? Group { get; set; }
    public string? Source { get; set; }   // "lisey" | "mülki"
    public string? Gender { get; set; }   // "qadın" | "kişi"

    public int? Packet { get; set; }
    public string Status { get; set; } = "pending";
    public string PrintStatus { get; set; } = "not_printed";
    public string? Year { get; set; }

    // Yerləşdirmənin göstərilən yolu (məs. "Hava Qüvvələri → Uçuş heyəti → Qırıcı pilot"),
    // PlacedSpecialtyId-dən ayrı saxlanılır (frontend hər ikisini müstəqil oxuyur/yazır)
    public string? PlacedSpecialty { get; set; }
    public string? PlacedSpecialtyId { get; set; }
    public SpecialtyNode? PlacedSpecialtyNode { get; set; }

    public int? ChoiceNum { get; set; }
    public string? PlacedSelectionId { get; set; }
    public Selection? PlacedSelection { get; set; }

    // Fənn -> bal (məs. {"Riyaziyyat": 31.0, "İngilis Dili": 14.0})
    public Dictionary<string, decimal> Subjects { get; set; } = new();

    // Ağac səviyyəsi indeksi -> qabaqcadan təyin edilmiş budaq adı (Selection.PreAssignLevel ilə işləyir)
    public Dictionary<int, string> BranchByLevel { get; set; } = new();

    public List<Submission> Submissions { get; set; } = new();
}
