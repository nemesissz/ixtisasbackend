using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MmuIspApi.Data;
using MmuIspApi.Models;

namespace MmuIspApi.SeedData;

// db.ts-də localStorage-a bir dəfəlik seed olunan real BHK məlumatlarını
// (institution + ixtisas ağacı + 388 təhsilalan + seçim sıralamaları) MySQL-ə köçürür.
// İdempotentdir: "bhk" institution artıq varsa heç nə etmir.
public static class BhkImporter
{
    private record SeedStudent(
        string Id, string Institution, string Name, string? ParentName, string? WorkNumber,
        string? Fin, decimal? Score, string? Group, string? Source, int? Packet,
        string Status, string PrintStatus, string? PlacedSpecialty, Dictionary<string, decimal> Subjects);

    private record SeedNode(string Id, string Name, int? Quota, List<SeedNode> Children);

    private record SeedRanking(string Fin, List<string> Ranking);

    public static async Task RunAsync(MmuDbContext db, string seedDataDir)
    {
        if (await db.Institutions.AnyAsync(i => i.Id == "bhk"))
        {
            Console.WriteLine("BHK artıq mövcuddur — idxal edilmir.");
            return;
        }

        var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var students = JsonSerializer.Deserialize<List<SeedStudent>>(
            await File.ReadAllTextAsync(Path.Combine(seedDataDir, "bhk_students.json")), jsonOpts)!;
        var nodes = JsonSerializer.Deserialize<List<SeedNode>>(
            await File.ReadAllTextAsync(Path.Combine(seedDataDir, "bhk_tree_nodes.json")), jsonOpts)!;
        var rankings = JsonSerializer.Deserialize<List<SeedRanking>>(
            await File.ReadAllTextAsync(Path.Combine(seedDataDir, "bhk_rankings.json")), jsonOpts)!;

        Console.WriteLine($"Oxundu: {students.Count} tələbə, {nodes.Count} kök node, {rankings.Count} sıralama.");

        db.Institutions.Add(new Institution { Id = "bhk", Label = "BHK", Icon = "🎖️" });

        db.SpecialtyTrees.Add(new SpecialtyTree
        {
            Id = "tree_bhk",
            Name = "BHK İxtisas Strukturu",
            InstitutionId = "bhk",
            LevelNames = new List<string> { "Qoşun Növü", "Orta ixtisas təhsili üzrə ixtisaslar", "Hərbi Uçot İxtisası" },
        });

        var flatNodes = new List<SpecialtyNode>();
        FlattenNodes(nodes, "tree_bhk", null, flatNodes);
        db.SpecialtyNodes.AddRange(flatNodes);

        db.Selections.Add(new Selection
        {
            Id = "sel_bhk",
            Name = "BHK İxtisas Seçimi",
            InstitutionId = "bhk",
            TreeId = "tree_bhk",
            StudentCount = 388,
            ChoiceCount = 19,
            Tiebreaker = new List<string> { "Ümumi imtahan nəticəsi" },
            ViewMode = "list",
            SourceProportional = false,
            Status = SelectionStatus.Published,
            PublishedAt = DateTime.UtcNow,
        });

        var finToId = new Dictionary<string, string>();
        foreach (var s in students)
        {
            db.Students.Add(new Student
            {
                Id = s.Id,
                InstitutionId = "bhk",
                Name = s.Name,
                ParentName = s.ParentName,
                WorkNumber = s.WorkNumber,
                Fin = s.Fin,
                Score = s.Score,
                Group = s.Group,
                Source = s.Source,
                Packet = s.Packet,
                Status = s.Status,
                PrintStatus = s.PrintStatus,
                PlacedSpecialtyId = s.PlacedSpecialty,
                Subjects = s.Subjects ?? new(),
            });
            if (!string.IsNullOrEmpty(s.Fin)) finToId[s.Fin.Trim()] = s.Id;
        }

        var n = 0;
        var submittedIds = new HashSet<string>();
        foreach (var r in rankings)
        {
            if (!finToId.TryGetValue(r.Fin.Trim(), out var studentId)) continue;
            db.Submissions.Add(new Submission
            {
                Id = $"sub_bhk_{++n}",
                UserId = studentId,
                SelectionId = "sel_bhk",
                Ranking = r.Ranking,
            });
            submittedIds.Add(studentId);
        }

        await db.SaveChangesAsync();

        // Sıralama təqdim etmiş tələbələrin statusunu "submitted"-ə yenilə
        var toUpdate = await db.Students.Where(s => submittedIds.Contains(s.Id)).ToListAsync();
        foreach (var s in toUpdate) s.Status = "submitted";
        await db.SaveChangesAsync();

        Console.WriteLine($"İdxal tamamlandı: {students.Count} tələbə, {flatNodes.Count} node, {n} sıralama.");
    }

    private static void FlattenNodes(List<SeedNode> nodes, string treeId, string? parentId, List<SpecialtyNode> outList)
    {
        var order = 0;
        foreach (var node in nodes)
        {
            outList.Add(new SpecialtyNode
            {
                Id = node.Id,
                TreeId = treeId,
                ParentId = parentId,
                Name = node.Name,
                Quota = node.Quota,
                SortOrder = order++,
            });
            if (node.Children?.Count > 0)
                FlattenNodes(node.Children, treeId, node.Id, outList);
        }
    }
}
