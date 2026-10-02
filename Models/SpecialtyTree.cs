namespace MmuIspApi.Models;

public class SpecialtyTree
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string InstitutionId { get; set; } = default!;
    public Institution? Institution { get; set; }

    // Bu struktur hansı təhsilalan qrupu üçündür. Qrupu strukturda təyin edirik;
    // seçim isə sadəcə struktur seçərək iştirakçıları buradan miras alır.
    // null = müəssisənin bütün təhsilalanları (köhnə strukturlar).
    public string? CohortId { get; set; }
    public Cohort? Cohort { get; set; }

    // Sıralı səviyyə adları, məs. ["Qoşun növü", "Sahə", "İxtisas"]
    public List<string> LevelNames { get; set; } = new();

    public string? Icon { get; set; }
    public string? Year { get; set; }

    // Mülki/lisey kvota bölgüsü üçün ağac səviyyəli defolt (Selection.SourceProportional-dan asılı deyil)
    public bool SourceProportional { get; set; }

    // Arxiv: struktur silinmir, yalnız gizlədilir. Beləliklə ona bağlı seçimlər
    // (Selection.TreeId) yerində qalır və bərpa ediləndə nəticələr də qayıdır.
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<SpecialtyNode> Nodes { get; set; } = new();
}
