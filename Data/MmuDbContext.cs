using Microsoft.EntityFrameworkCore;
using MmuIspApi.Models;
using System.Text.Json;

namespace MmuIspApi.Data;

public class MmuDbContext : DbContext
{
    public MmuDbContext(DbContextOptions<MmuDbContext> options) : base(options) { }

    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<SpecialtyTree> SpecialtyTrees => Set<SpecialtyTree>();
    public DbSet<SpecialtyNode> SpecialtyNodes => Set<SpecialtyNode>();
    public DbSet<Selection> Selections => Set<Selection>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<CustomRole> CustomRoles => Set<CustomRole>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<UserArchive> UserArchives => Set<UserArchive>();
    public DbSet<TreeArchive> TreeArchives => Set<TreeArchive>();
    public DbSet<LogEntry> Logs => Set<LogEntry>();
    public DbSet<InstitutionLoginConfig> InstitutionLoginConfigs => Set<InstitutionLoginConfig>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var stringListConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        var stringListComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
            (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
            v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
            v => v.ToList());

        var subjectsConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<Dictionary<string, decimal>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<Dictionary<string, decimal>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, decimal>());

        var subjectsComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<Dictionary<string, decimal>>(
            (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
            v => v.Aggregate(0, (h, kv) => HashCode.Combine(h, kv.Key.GetHashCode(), kv.Value.GetHashCode())),
            v => v.ToDictionary(kv => kv.Key, kv => kv.Value));

        // Nullable List<string>? üçün (null → null, override yoxdursa)
        var nullableStringListConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<List<string>?, string?>(
            v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null));

        var nullableStringListComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>?>(
            (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
            v => (v ?? new()).Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
            v => v == null ? null : v.ToList());

        var groupTiebreakersConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<Dictionary<string, List<string>>?, string?>(
            v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, List<string>>>(v, (JsonSerializerOptions?)null));

        var groupTiebreakersComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<Dictionary<string, List<string>>?>(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => (v == null ? "" : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null)).GetHashCode(),
            v => v == null ? null : v.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()));

        var branchByLevelConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<Dictionary<int, string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<Dictionary<int, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<int, string>());

        var branchByLevelComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<Dictionary<int, string>>(
            (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
            v => v.Aggregate(0, (h, kv) => HashCode.Combine(h, kv.Key.GetHashCode(), kv.Value.GetHashCode())),
            v => v.ToDictionary(kv => kv.Key, kv => kv.Value));

        // ── Institution ──────────────────────────────────────────────
        modelBuilder.Entity<Institution>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Label).IsRequired().HasMaxLength(200);
            e.Property(x => x.Icon).HasColumnType("longtext");
        });

        // ── SpecialtyTree ────────────────────────────────────────────
        modelBuilder.Entity<SpecialtyTree>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.LevelNames).HasConversion(stringListConverter, stringListComparer).HasColumnType("json");
            e.Property(x => x.Icon).HasColumnType("longtext");
            e.HasOne(x => x.Institution).WithMany(i => i.SpecialtyTrees)
                .HasForeignKey(x => x.InstitutionId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── SpecialtyNode ────────────────────────────────────────────
        modelBuilder.Entity<SpecialtyNode>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(300);
            e.Property(x => x.Tiebreaker).HasConversion(nullableStringListConverter, nullableStringListComparer).HasColumnType("json");
            e.Property(x => x.Groups).HasConversion(nullableStringListConverter, nullableStringListComparer).HasColumnType("json");
            e.Property(x => x.GroupTiebreakers).HasConversion(groupTiebreakersConverter, groupTiebreakersComparer).HasColumnType("json");
            e.HasOne(x => x.Tree).WithMany(t => t.Nodes)
                .HasForeignKey(x => x.TreeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Parent).WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.TreeId, x.ParentId });
        });

        // ── Selection ────────────────────────────────────────────────
        modelBuilder.Entity<Selection>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Tiebreaker).HasConversion(stringListConverter, stringListComparer).HasColumnType("json");
            e.HasOne(x => x.Institution).WithMany(i => i.Selections)
                .HasForeignKey(x => x.InstitutionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Tree).WithMany()
                .HasForeignKey(x => x.TreeId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── Student ──────────────────────────────────────────────────
        modelBuilder.Entity<Student>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Subjects).HasConversion(subjectsConverter, subjectsComparer).HasColumnType("json");
            e.Property(x => x.BranchByLevel).HasConversion(branchByLevelConverter, branchByLevelComparer).HasColumnType("json");
            e.HasOne(x => x.Institution).WithMany(i => i.Students)
                .HasForeignKey(x => x.InstitutionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.PlacedSpecialtyNode).WithMany()
                .HasForeignKey(x => x.PlacedSpecialtyId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.PlacedSelection).WithMany()
                .HasForeignKey(x => x.PlacedSelectionId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.Fin);
            e.HasIndex(x => new { x.InstitutionId, x.WorkNumber });
        });

        // ── Admin ────────────────────────────────────────────────────
        modelBuilder.Entity<Admin>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).IsRequired().HasMaxLength(100);
            e.Property(x => x.Permissions).HasConversion(nullableStringListConverter, nullableStringListComparer).HasColumnType("json");
            e.Property(x => x.Institutions).HasConversion(nullableStringListConverter, nullableStringListComparer).HasColumnType("json");
            e.HasIndex(x => x.Username).IsUnique();
        });

        // ── CustomRole ───────────────────────────────────────────────
        modelBuilder.Entity<CustomRole>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.Name).IsUnique();
        });

        // ── Submission ───────────────────────────────────────────────
        modelBuilder.Entity<Submission>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Ranking).HasConversion(stringListConverter, stringListComparer).HasColumnType("json");
            e.HasOne(x => x.Student).WithMany(s => s.Submissions)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Selection).WithMany(s => s.Submissions)
                .HasForeignKey(x => x.SelectionId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.SelectionId }).IsUnique();
        });

        // ── Archives / Logs ──────────────────────────────────────────
        modelBuilder.Entity<UserArchive>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DataJson).HasColumnType("json");
        });
        modelBuilder.Entity<TreeArchive>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DataJson).HasColumnType("json");
        });
        modelBuilder.Entity<LogEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Message).IsRequired();
            e.HasIndex(x => x.Timestamp);
        });

        // ── Settings ─────────────────────────────────────────────────
        modelBuilder.Entity<InstitutionLoginConfig>(e =>
        {
            e.HasKey(x => x.InstitutionId);
            e.HasOne(x => x.Institution).WithOne()
                .HasForeignKey<InstitutionLoginConfig>(x => x.InstitutionId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<SystemSetting>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.PrioritySubjects).HasConversion(stringListConverter, stringListComparer).HasColumnType("json");
        });
    }
}
