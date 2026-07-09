namespace MmuIspApi.Models;

// draft -> published -> closed -> archived
public static class SelectionStatus
{
    public const string Draft = "draft";
    public const string Published = "published";
    public const string Closed = "closed";
    public const string Archived = "archived";
}

public class Selection
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;

    public string InstitutionId { get; set; } = default!;
    public Institution? Institution { get; set; }

    public string TreeId { get; set; } = default!;
    public SpecialtyTree? Tree { get; set; }

    public int StudentCount { get; set; }
    public int ChoiceCount { get; set; }

    // Bərabərlik pozucusu meyarlar, sırası ilə (məs. ["Ümumi imtahan nəticəsi"])
    public List<string> Tiebreaker { get; set; } = new();

    public string ViewMode { get; set; } = "list";
    public bool SourceProportional { get; set; }

    // Ağac dərinlik indeksi: tələbənin BranchByLevel[PreAssignLevel] dəyərinə görə seçə biləcəyi alt-ağac filtrlənir
    public int? PreAssignLevel { get; set; }

    public string Status { get; set; } = SelectionStatus.Draft;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }

    public List<Submission> Submissions { get; set; } = new();
}
