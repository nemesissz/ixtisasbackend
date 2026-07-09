namespace MmuIspApi.Models;

// Adjacency-list ağac: hər node öz TreeId-sinə aiddir, ParentId null-dırsa kök səviyyədədir.
public class SpecialtyNode
{
    public string Id { get; set; } = default!;
    public string TreeId { get; set; } = default!;
    public SpecialtyTree? Tree { get; set; }

    public string? ParentId { get; set; }
    public SpecialtyNode? Parent { get; set; }
    public List<SpecialtyNode> Children { get; set; } = new();

    public string Name { get; set; } = default!;

    // Yalnız yarpaq (ixtisas) node-larda dolur
    public int? Quota { get; set; }

    // Uşaqların array sırasını qorumaq üçün
    public int SortOrder { get; set; }

    // Bu yarpaq üçün bərabərlik pozucusu fənn sırası (yoxdursa əcdaddan miras alınır)
    public List<string>? Tiebreaker { get; set; }

    // Tələbə qrupuna görə tiebreaker override-ı: qrup adı -> fənn sırası
    public Dictionary<string, List<string>>? GroupTiebreakers { get; set; }

    // Bu node yalnız bu qruplara aid tələbələrə göstərilir (boşdursa hamıya açıqdır)
    public List<string>? Groups { get; set; }

    public string? QuotaMode { get; set; }   // "auto" | "manual"
    public int? MulkiQuota { get; set; }
    public int? LiseyQuota { get; set; }

    public bool? AllowFemale { get; set; }
    public bool? AllowMale { get; set; }
    public int? MaxFemale { get; set; }
    public int? MaxMale { get; set; }
}
