namespace MmuIspApi.Models;

// Bir təhsilalanın bir seçim üçün ixtisas sıralaması (üstünlük göstərdiyi sıra ilə)
public class Submission
{
    public string Id { get; set; } = default!;

    public string UserId { get; set; } = default!;
    public Student? Student { get; set; }

    public string SelectionId { get; set; } = default!;
    public Selection? Selection { get; set; }

    // Üstünlük sırası ilə SpecialtyNode Id-ləri
    public List<string> Ranking { get; set; } = new();

    // Təqdim anındakı ad-soyad (denormalizasiya olunmuş nüsxə)
    public string? UserName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
