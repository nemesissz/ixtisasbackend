namespace MmuIspApi.Models;

// Silinmiş/dəyişdirilmiş ixtisas ağaclarının JSON snapshot-u
public class TreeArchive
{
    public string Id { get; set; } = default!;
    public string DataJson { get; set; } = default!;
    public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;
}
