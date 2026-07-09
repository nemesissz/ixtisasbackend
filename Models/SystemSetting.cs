namespace MmuIspApi.Models;

// Tək sətirlik qlobal parametrlər cədvəli (Id həmişə 1)
public class SystemSetting
{
    public int Id { get; set; } = 1;
    public int RedirectDelaySec { get; set; } = 10;

    // Excel idxalında prioritet kimi işarələnmiş fənn adları (qlobal, müəssisələr arası)
    public List<string> PrioritySubjects { get; set; } = new();
}
