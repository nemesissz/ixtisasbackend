namespace MmuIspApi.Models;

// Silinmiş/bağlanmış seçimlərdən əvvəl təhsilalan siyahısının JSON snapshot-u
public class UserArchive
{
    public string Id { get; set; } = default!;
    public string DataJson { get; set; } = default!;
    public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;
}
