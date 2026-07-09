namespace MmuIspApi.Models;

// Hər müəssisə üçün tələbə login formasının 2 sahəsinin konfiqurasiyası
public class InstitutionLoginConfig
{
    public string InstitutionId { get; set; } = default!;
    public Institution? Institution { get; set; }

    public string Field1Column { get; set; } = "fin";
    public string Field1Label { get; set; } = "FİN Kodu";
    public int Field1Min { get; set; } = 1;
    public int Field1Max { get; set; } = 20;
    public bool Field1Required { get; set; } = true;

    public string Field2Column { get; set; } = "workNumber";
    public string Field2Label { get; set; } = "İş Nömrəsi";
    public int Field2Min { get; set; } = 1;
    public int Field2Max { get; set; } = 20;
    public bool Field2Required { get; set; } = true;
}
