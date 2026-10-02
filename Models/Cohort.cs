namespace MmuIspApi.Models;

// Bir müəssisə daxilində təhsilalan qrupu (qəbul dalğası / axın).
// Məsələn "2025-ci ildə qəbul olunanlar" və "hazırlıqdan 1-ə keçənlər" eyni
// müəssisəyə aiddir, amma ayrı-ayrı siyahılardır.
//
// Qrup strukturda təyin olunur (SpecialtyTree.CohortId): hansı təhsilalanlar
// üçün struktur qurulubsa, ona bağlı seçim də həmin təhsilalanları əhatə edir.
// Seçimin öz qrup parametri yoxdur — strukturu seçməklə qrup da müəyyənləşir.
public class Cohort
{
    public string Id { get; set; } = default!;

    public string InstitutionId { get; set; } = default!;
    public Institution? Institution { get; set; }

    public string Label { get; set; } = default!;
    public string? Icon { get; set; }
    public string? Year { get; set; }

    // Siyahıda göstərilmə sırası (kiçikdən böyüyə)
    public int SortOrder { get; set; }

    // Arxiv: qrup silinmir, yalnız gizlədilir — təhsilalanları yerində qalır.
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Student> Students { get; set; } = new();
    public List<SpecialtyTree> SpecialtyTrees { get; set; } = new();
}
