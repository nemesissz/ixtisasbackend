namespace MmuIspApi.Models;

public class SpecialtyTree
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string InstitutionId { get; set; } = default!;
    public Institution? Institution { get; set; }

    // Sıralı səviyyə adları, məs. ["Qoşun növü", "Sahə", "İxtisas"]
    public List<string> LevelNames { get; set; } = new();

    public string? Icon { get; set; }
    public string? Year { get; set; }

    // Mülki/lisey kvota bölgüsü üçün ağac səviyyəli defolt (Selection.SourceProportional-dan asılı deyil)
    public bool SourceProportional { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<SpecialtyNode> Nodes { get; set; } = new();
}
