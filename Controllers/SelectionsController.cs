using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MmuIspApi.Data;
using MmuIspApi.Models;
using MmuIspApi.Services;

namespace MmuIspApi.Controllers;

public record SelectionCreateDto(
    string Name, string InstitutionId, string TreeId,
    int StudentCount, int ChoiceCount, List<string> Tiebreaker,
    string ViewMode, bool SourceProportional, int? PreAssignLevel);

public record SelectionUpdateDto(
    string Name, int StudentCount, int ChoiceCount,
    List<string> Tiebreaker, string ViewMode, bool SourceProportional, int? PreAssignLevel);

[ApiController]
[Route("api/[controller]")]
public class SelectionsController : ControllerBase
{
    // Seçimin iştirakçıları. Qrup seçimin öz parametri deyil — seçdiyi
    // STRUKTURDAN miras alınır (SpecialtyTree.CohortId). Struktur qrupsuzdursa
    // (köhnə strukturlar) müəssisənin bütün təhsilalanları iştirak edir.
    private async Task<List<Student>> ParticipantsAsync(Selection sel)
    {
        var cohortId = await _db.SpecialtyTrees.AsNoTracking()
            .Where(t => t.Id == sel.TreeId).Select(t => t.CohortId).FirstOrDefaultAsync();
        var q = _db.Students.Where(s => s.InstitutionId == sel.InstitutionId);
        if (cohortId != null) q = q.Where(s => s.CohortId == cohortId);
        return await q.ToListAsync();
    }

    private readonly MmuDbContext _db;
    public SelectionsController(MmuDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Selection>>> GetAll()
    {
        var query = _db.Selections.AsNoTracking().AsQueryable();
        var allowed = User.AllowedInstitutions();
        if (allowed is not null)
            query = query.Where(s => allowed.Contains(s.InstitutionId));
        return Ok(await query.ToListAsync());
    }

    [HttpGet("archived")]
    [Authorize(Roles = "admin")]
    [RequirePermission("archive.view")]
    public async Task<ActionResult<IEnumerable<Selection>>> GetArchived() =>
        Ok(await _db.Selections.AsNoTracking().Where(s => s.Status == SelectionStatus.Archived).ToListAsync());

    [HttpGet("{id}")]
    public async Task<ActionResult<Selection>> Get(string id)
    {
        var sel = await _db.Selections.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        return sel is null ? NotFound() : Ok(sel);
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    [RequirePermission("sel.create")]
    public async Task<ActionResult<Selection>> Create(SelectionCreateDto dto)
    {
        if (!User.CanAccessInstitution(dto.InstitutionId)) return Forbid();
        var item = new Selection
        {
            Id = $"sel_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}",
            Name = dto.Name,
            InstitutionId = dto.InstitutionId,
            TreeId = dto.TreeId,
            StudentCount = dto.StudentCount,
            ChoiceCount = dto.ChoiceCount,
            Tiebreaker = dto.Tiebreaker ?? new(),
            ViewMode = dto.ViewMode,
            SourceProportional = dto.SourceProportional,
            PreAssignLevel = dto.PreAssignLevel,
            Status = SelectionStatus.Draft,
        };
        _db.Selections.Add(item);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    [RequirePermission("sel.edit")]
    public async Task<IActionResult> Update(string id, SelectionUpdateDto dto)
    {
        var item = await _db.Selections.FindAsync(id);
        if (item is null) return NotFound();
        if (!User.CanAccessInstitution(item.InstitutionId)) return Forbid();
        item.Name = dto.Name;
        item.StudentCount = dto.StudentCount;
        item.ChoiceCount = dto.ChoiceCount;
        item.Tiebreaker = dto.Tiebreaker ?? item.Tiebreaker;
        item.ViewMode = dto.ViewMode;
        item.SourceProportional = dto.SourceProportional;
        item.PreAssignLevel = dto.PreAssignLevel;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    [RequirePermission("sel.delete")]
    public async Task<IActionResult> Delete(string id)
    {
        var item = await _db.Selections.FindAsync(id);
        if (item is null) return NotFound();
        if (!User.CanAccessInstitution(item.InstitutionId)) return Forbid();

        var instId = item.InstitutionId;
        _db.Selections.Remove(item);
        await _db.SaveChangesAsync();

        // ⚠ Seçimi silmək təhsilalanlardakı yerləşdirmə izlərini avtomatik silmir.
        // Təmizlənməzsə "yetim yerləşdirmə" qalır: Nəticələr səhifəsi seçim tapmadığı
        // üçün boş görünür, dashboard isə PlacedSpecialtyId-ə görə sayaraq 100%
        // yerləşdi göstərir, təhsilalan isə "yerləşdirilib" sayıldığından redaktə
        // olunmur. Ona görə silinmədən sonra artıq heç bir mövcud seçimə bağlanmayan
        // yerləşdirmələr sıfırlanır.
        var qalanSecimIds = await _db.Selections
            .Where(s => s.InstitutionId == instId)
            .Select(s => s.Id)
            .ToListAsync();

        var yetimler = await _db.Students
            .Where(u => u.InstitutionId == instId
                     && u.PlacedSpecialtyId != null
                     && (u.PlacedSelectionId == null
                         ? qalanSecimIds.Count == 0        // heç bir seçim qalmayıbsa mənbəsizdir
                         : !qalanSecimIds.Contains(u.PlacedSelectionId)))
            .ToListAsync();

        foreach (var u in yetimler)
        {
            u.PlacedSpecialty = null;
            u.PlacedSpecialtyId = null;
            u.PlacedSelectionId = null;
            u.ChoiceNum = null;
            u.Packet = null;
            // Yerləşdirmə getdisə "çap edilib" də mənasını itirir — çap olunan
            // sənəd məhz həmin yerləşdirmə nəticəsidir.
            u.PrintStatus = "not_printed";
        }

        // Seçim silinəndə ona bağlı sıralamalar da (Submission) kaskadla silinir.
        // Sıralaması qalmayan təhsilalan "seçim etdi" sayıla bilməz — statusu
        // geri "pending"-ə qaytarılır, əks halda siyahıda yanlış görünür.
        var sıralamaSahibləri = await _db.Submissions.Select(x => x.UserId).ToListAsync();
        var statusuQalanlar = await _db.Students
            .Where(u => u.InstitutionId == instId && u.Status == "submitted")
            .ToListAsync();
        var geriQaytarılan = statusuQalanlar.Where(u => !sıralamaSahibləri.Contains(u.Id)).ToList();
        foreach (var u in geriQaytarılan) u.Status = "pending";

        if (yetimler.Count > 0 || geriQaytarılan.Count > 0) await _db.SaveChangesAsync();

        return NoContent();
    }

    // Yayımdan əvvəl xəbərdarlıq (bloklamır): bu seçimin iştirakçılarından eyni FİN-li
    // başqa qeydi olan və həmin qeydin başqa yayımdakı seçimdə iştirak etdiyi hallar.
    // Real şəraitdə baş verməməlidir, amma olarsa admin bilsin.
    [HttpGet("{id}/fin-conflicts")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> FinConflicts(string id)
    {
        var sel = await _db.Selections.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (sel is null) return NotFound();
        if (!User.CanAccessInstitution(sel.InstitutionId)) return Forbid();

        var mine = await ParticipantsAsync(sel);
        var mineIds = mine.Select(s => s.Id).ToHashSet();
        var fins = mine.Select(s => s.Fin).Where(f => !string.IsNullOrWhiteSpace(f)).Distinct().ToList();
        if (fins.Count == 0) return Ok(Array.Empty<object>());

        var others = (await _db.Students.AsNoTracking()
                .Where(s => s.Fin != null && fins.Contains(s.Fin))
                .ToListAsync())
            .Where(s => !mineIds.Contains(s.Id)).ToList();
        if (others.Count == 0) return Ok(Array.Empty<object>());

        var published = await _db.Selections.AsNoTracking()
            .Where(x => x.Status == SelectionStatus.Published && x.Id != id)
            .Join(_db.SpecialtyTrees, x => x.TreeId, t => t.Id,
                  (x, t) => new { x.Id, x.Name, x.InstitutionId, t.CohortId })
            .ToListAsync();
        var instNames = await _db.Institutions.AsNoTracking().ToDictionaryAsync(i => i.Id, i => i.Label);
        var cohortNames = await _db.Cohorts.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Label);

        var byFin = mine.Where(s => !string.IsNullOrWhiteSpace(s.Fin))
            .GroupBy(s => FinRules.Norm(s.Fin)).ToDictionary(g => g.Key, g => g.First());
        var result = new List<object>();
        foreach (var o in others)
        {
            if (!byFin.TryGetValue(FinRules.Norm(o.Fin), out var me)) continue;
            var otherSel = published.FirstOrDefault(p =>
                p.InstitutionId == o.InstitutionId && (p.CohortId == null || p.CohortId == o.CohortId));
            if (otherSel is null) continue;
            result.Add(new
            {
                fin = FinRules.Norm(o.Fin), name = me.Name,
                otherStudentId = o.Id, otherStatus = o.Status,
                otherInstitution = instNames.GetValueOrDefault(o.InstitutionId, o.InstitutionId),
                otherCohort = o.CohortId is null ? null : cohortNames.GetValueOrDefault(o.CohortId, o.CohortId),
                otherSelectionId = otherSel.Id, otherSelectionName = otherSel.Name,
            });
        }
        return Ok(result);
    }

    [HttpPost("{id}/publish")]
    [Authorize(Roles = "admin")]
    [RequirePermission("sel.publish")]
    public async Task<IActionResult> Publish(string id)
    {
        var sel = await _db.Selections.FindAsync(id);
        if (sel is null) return NotFound();
        if (!User.CanAccessInstitution(sel.InstitutionId)) return Forbid();

        // Mənbə balansı: proporsional bölgü aktivdirsə, manual kvotalar hovuzu eyni
        // olan ixtisaslar arasında bir-birini kompensasiya etməlidir. Etmirsə bir
        // mənbənin yerləri itir — yayıma icazə verilmir.
        var tree = await _db.SpecialtyTrees.AsNoTracking().FirstOrDefaultAsync(t => t.Id == sel.TreeId);
        if (tree is not null && tree.SourceProportional)
        {
            var nodes = await _db.SpecialtyNodes.AsNoTracking()
                .Where(n => n.TreeId == sel.TreeId).ToListAsync();
            var students = await ParticipantsAsync(sel);

            // Yerləşdirmə mənbə slotlarını qarışdırmır (Distribution.tsx → mərhələ 3
            // sərt rejimdədir), ona görə yayım şərti də real icra olunabilirlikdir:
            // boş qalacaq yer və ya yersiz qalacaq namizəd olmamalıdır.
            var sb = QuotaBalance.CheckSources(nodes, students, sel.PreAssignLevel);
            if (!sb.Ok)
            {
                var parts = new List<string>();
                if (sb.Mülki.Unfillable > 0) parts.Add($"{sb.Mülki.Unfillable} mülki yer boş qalacaq");
                if (sb.Lisey.Unfillable > 0) parts.Add($"{sb.Lisey.Unfillable} lisey yer boş qalacaq");
                if (sb.Mülki.Deficit > 0) parts.Add($"{sb.Mülki.Deficit} mülki namizəd yersiz qalacaq");
                if (sb.Lisey.Deficit > 0) parts.Add($"{sb.Lisey.Deficit} lisey namizəd yersiz qalacaq");

                // Səbəb cins limitidirsə, onu birbaşa və konkret adla göstər
                var capText = sb.CapIssues.Count == 0 ? "" :
                    " Cins limiti kvotadan azdır: "
                    + string.Join(" · ", sb.CapIssues.Take(5).Select(c =>
                        $"{c.Name} (qadın {c.MaxFemale?.ToString() ?? "—"} + kişi {c.MaxMale?.ToString() ?? "—"} = {c.Capacity} < kvota {c.Quota}, {c.Shortfall} yer heç vaxt dolmayacaq)"))
                    + (sb.CapIssues.Count > 5 ? $" və daha {sb.CapIssues.Count - 5}" : "") + ".";

                return Conflict(new
                {
                    message = "Mənbə slotları uyğun gəlmir: " + string.Join(" · ", parts)
                            + $". Mülki {sb.Mülki.Seats} yer / {sb.Mülki.Candidates} namizəd · "
                            + $"lisey {sb.Lisey.Seats} yer / {sb.Lisey.Candidates} namizəd. "
                            + "Seçimi yayımlamazdan əvvəl manual kvotaları düzəldin."
                            + capText,
                    capIssues = sb.CapIssues.Select(c => new
                    {
                        name = c.Name, quota = c.Quota,
                        maxFemale = c.MaxFemale, maxMale = c.MaxMale,
                        capacity = c.Capacity, shortfall = c.Shortfall,
                    }),
                    mülkiSeats = sb.Mülki.Seats,
                    mülkiCandidates = sb.Mülki.Candidates,
                    mülkiUnfillable = sb.Mülki.Unfillable,
                    mülkiDeficit = sb.Mülki.Deficit,
                    liseySeats = sb.Lisey.Seats,
                    liseyCandidates = sb.Lisey.Candidates,
                    liseyUnfillable = sb.Lisey.Unfillable,
                    liseyDeficit = sb.Lisey.Deficit,
                });
            }
        }

        sel.Status = SelectionStatus.Published;
        sel.PublishedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/close")]
    [Authorize(Roles = "admin")]
    [RequirePermission("sel.publish")]
    public Task<IActionResult> Close(string id) => SetStatus(id, SelectionStatus.Closed, s => s.ClosedAt = DateTime.UtcNow);

    [HttpPost("{id}/archive")]
    [Authorize(Roles = "admin")]
    [RequirePermission("sel.publish")]
    public Task<IActionResult> Archive(string id) => SetStatus(id, SelectionStatus.Archived, s => s.ArchivedAt = DateTime.UtcNow);

    [HttpPost("{id}/restore")]
    [Authorize(Roles = "admin")]
    [RequirePermission("archive.restore")]
    public Task<IActionResult> Restore(string id) => SetStatus(id, SelectionStatus.Closed, _ => { });

    private async Task<IActionResult> SetStatus(string id, string status, Action<Selection> apply)
    {
        var item = await _db.Selections.FindAsync(id);
        if (item is null) return NotFound();
        if (!User.CanAccessInstitution(item.InstitutionId)) return Forbid();
        item.Status = status;
        apply(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // db.ts-dəki resetAndAutoSeedSubmissions-un server tərəfi portu.
    // onlyMissing=false ("Sıfırla") — bütün sıralamalar silinib hamısı yenidən yaradılır.
    // onlyMissing=true  ("Doldur")  — təhsilalanın öz göndərdiyi sıralamaya toxunulmur,
    //                                 yalnız seçim etməyənlər doldurulur.
    // Sıralama hər təhsilalan üçün ayrıca təsadüfi qurulur və strukturdakı məhdudiyyətlər
    // (qrup, cins, əvvəlcədən təyin edilmiş budaq) tələbənin öz ekranındakı ilə eyni
    // qaydada tətbiq olunur — bax: MMU-isp/src/pages/student/SelectionPage.tsx
    [HttpPost("{id}/reset-and-autoseed")]
    [Authorize(Roles = "admin")]
    [RequirePermission("sel.edit")]
    public async Task<ActionResult> ResetAndAutoSeed(
        string id, [FromQuery] bool onlyMissing = false, [FromQuery] bool seed = true)
    {
        var sel = await _db.Selections.FindAsync(id);
        if (sel is null) return NotFound();
        if (!User.CanAccessInstitution(sel.InstitutionId)) return Forbid();

        // seed=false ("Sıfırla") — sıralamalar sadəcə silinir, yenisi yaradılmır.
        if (!seed) return await ClearSubmissions(id);

        var nodes = await _db.SpecialtyNodes.AsNoTracking()
            .Where(n => n.TreeId == sel.TreeId)
            .ToListAsync();

        // ParentId -> uşaqlar (ağac sırası ilə); kök node-lar "" açarı altındadır
        var childrenOf = nodes
            .GroupBy(n => n.ParentId ?? "")
            .ToDictionary(g => g.Key, g => g.OrderBy(n => n.SortOrder).ToList());

        var roots = childrenOf.TryGetValue("", out var r) ? r : new List<SpecialtyNode>();

        var instUsers = await ParticipantsAsync(sel);

        var existing = await _db.Submissions
            .Where(s => s.SelectionId == id)
            .ToListAsync();
        var subOf = existing.ToDictionary(s => s.UserId, s => s);

        if (!onlyMissing)
        {
            _db.Submissions.RemoveRange(existing);
            subOf.Clear();
        }

        var rnd = new Random();
        int created = 0, updated = 0, kept = 0, empty = 0;

        foreach (var u in instUsers)
        {
            var sub = subOf.GetValueOrDefault(u.Id);

            // Təhsilalanın özünün göndərdiyi sıralama qorunur.
            // Avtomatik yaradılan sətir "sub_reset_" ilə başlayır və UpdatedAt-ı boşdur;
            // tələbə həmin sətrin üstünə göndəriş etsə, Save() UpdatedAt-ı doldurur (Id dəyişmir).
            if (onlyMissing && sub is not null && (sub.UpdatedAt is not null || !sub.Id.StartsWith("sub_reset_")))
            {
                kept++;
                continue;
            }

            var allowed = AllowedLeafIds(roots, childrenOf, sel, u);
            if (allowed.Count == 0) { empty++; continue; }

            Shuffle(allowed, rnd);

            if (sub is not null)
            {
                sub.Ranking = allowed;
                sub.UserName = u.Name;
                updated++;
            }
            else
            {
                _db.Submissions.Add(new Submission
                {
                    Id = $"sub_reset_{id}_{Guid.NewGuid():N}",
                    UserId = u.Id,
                    UserName = u.Name,
                    SelectionId = id,
                    Ranking = allowed,
                });
                created++;
            }
        }

        await _db.SaveChangesAsync();
        return Ok(new
        {
            count = created + updated,
            created,
            updated,
            kept,
            empty,
            total = instUsers.Count,
        });
    }

    // Seçimin bütün sıralamalarını silir və müəssisədə heç bir seçimdə sıralaması
    // qalmayan hər bir "submitted" statusunu "pending"-ə qaytarır.
    //
    // ⚠ Yalnız indi silinən sıralamaların sahiblərini qaytarmaq kifayət deyil: sıralaması
    // artıq yox olmuş, amma statusu "submitted" qalmış "yetim" sətirlər əks halda həmişəlik
    // ilişib qalır və panel onları "seçim etdi, yerləşdirilmədi" kimi saymağa davam edir.
    // Ona görə filtr silinənlərə görə deyil, faktiki qalıq sıralamalara görə qurulur.
    private async Task<ActionResult> ClearSubmissions(string id)
    {
        var sel = await _db.Selections.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (sel is null) return NotFound();

        var subs = await _db.Submissions.Where(s => s.SelectionId == id).ToListAsync();
        _db.Submissions.RemoveRange(subs);
        // Silinmə əvvəlcə yazılır ki, aşağıdakı "sıralaması qalıbmı" yoxlaması düzgün işləsin.
        await _db.SaveChangesAsync();

        var iştirakçılar = await ParticipantsAsync(sel);
        var mövcudSıralama = await _db.Submissions.Select(s => s.UserId).ToListAsync();
        var toPending = iştirakçılar
            .Where(u => u.Status == "submitted" && !mövcudSıralama.Contains(u.Id))
            .ToList();
        foreach (var u in toPending) u.Status = "pending";

        await _db.SaveChangesAsync();
        return Ok(new { count = 0, deleted = subs.Count, reverted = toPending.Count });
    }

    // Bir təhsilalanın seçə biləcəyi yarpaq (ixtisas) Id-ləri.
    // Qaydalar tələbə ekranındakı filtrlərin eynisidir:
    //   1) qrup — node.Groups doludursa və tələbənin qrupu orada yoxdursa node gizlənir
    //             (tələbənin qrupu boşdursa filtr ümumiyyətlə tətbiq olunmur);
    //   2) budaq — PreAssignLevel təyin edilibsə, həmin dərinlikdə yalnız tələbənin
    //             BranchByLevel dəyəri ilə adı üst-üstə düşən node saxlanılır;
    //   3) cins  — yarpaqda AllowFemale=false qadına, AllowMale=false kişiyə bağlıdır.
    private static List<string> AllowedLeafIds(
        List<SpecialtyNode> roots,
        Dictionary<string, List<SpecialtyNode>> childrenOf,
        Selection sel,
        Student u)
    {
        var group  = (u.Group ?? "").Trim();
        var gender = (u.Gender ?? "").Trim().ToLowerInvariant();

        var level = sel.PreAssignLevel;
        string? branch = null;
        if (level is int lv && u.BranchByLevel.TryGetValue(lv, out var b))
            branch = (b ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(branch)) { branch = null; level = null; }

        var result = new List<string>();

        void Walk(List<SpecialtyNode> list, int depth)
        {
            foreach (var n in list)
            {
                if (group.Length > 0 && n.Groups is { Count: > 0 } && !n.Groups.Contains(group))
                    continue;

                var kids = childrenOf.TryGetValue(n.Id, out var c) ? c : new List<SpecialtyNode>();

                if (level is int target)
                {
                    if (depth < target)
                    {
                        // Təyin edilmiş səviyyəyə çatmayan yarpaqlar uyğun deyil
                        if (kids.Count == 0) continue;
                        Walk(kids, depth + 1);
                        continue;
                    }
                    if (depth == target && !string.Equals(n.Name.Trim(), branch, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                if (kids.Count > 0) { Walk(kids, depth + 1); continue; }

                // yarpaq — cins məhdudiyyəti
                if (gender == "qadın" && n.AllowFemale == false) continue;
                if (gender == "kişi"  && n.AllowMale   == false) continue;

                result.Add(n.Id);
            }
        }

        Walk(roots, 0);
        return result;
    }

    // Fisher–Yates: hər çağırışda həqiqətən fərqli sıra verir.
    // (Əvvəlki versiya node adının ilk hərflərinə görə "hash" qurduğu üçün — bütün Id-lər
    //  eyni prefikslə başladığından — hamıya eyni sıranı verirdi.)
    private static void Shuffle(List<string> arr, Random rnd)
    {
        for (var i = arr.Count - 1; i > 0; i--)
        {
            var j = rnd.Next(i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
    }
}
