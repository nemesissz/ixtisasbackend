namespace MmuIspApi.Models;

public class Institution
{
    public string Id { get; set; } = default!;
    public string Label { get; set; } = default!;
    public string? Icon { get; set; }
    public string? Year { get; set; }

    public List<SpecialtyTree> SpecialtyTrees { get; set; } = new();
    public List<Selection> Selections { get; set; } = new();
    public List<Student> Students { get; set; } = new();
}
