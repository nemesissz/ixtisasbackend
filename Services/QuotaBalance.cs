using MmuIspApi.Models;

namespace MmuIspApi.Services;

// Frontend-dəki MMU-isp/src/quota-pool.ts faylının server tərəfi qarşılığı.
// Mənbə (mülki/lisey) bölgüsü hər ixtisas üçün onu SEÇƏ BİLƏNLƏRİN hovuzundan
// hesablanır. Manual kvotalar hovuzu eyni olan ixtisaslar arasında bir-birini
// kompensasiya etməlidir; etmirsə bir mənbənin yerləri itir və seçimi yayımlamağa
// icazə verilmir. Qaydalar tələbənin öz ekranındakı filtrlərlə eynidir.
public static class QuotaBalance
{
    public record Pool(int Mülki, int Lisey, int Other)
    {
        public int Total => Mülki + Lisey + Other;
    }

    public record Split(int Mülki, int Lisey);

    public record BrokenGroup(int PoolTotal, int PoolMülki, int PoolLisey,
                              int AutoMülki, int AutoLisey, int EffMülki, int EffLisey,
                              List<string> Leaves);

    public record Report(bool Ok, List<BrokenGroup> Broken, int LostMülki, int LostLisey);

    /// <summary>Bir mənbə üzrə slot/namizəd uyğunluğu (quota-pool.ts → SourceSlotCheck).</summary>
    public record SourceSlotCheck(int Candidates, int Seats, int Placeable, int Deficit, int Unfillable);

    /// <summary>Cins limitlərinin cəmi kvotadan az olan ixtisas — konfiqurasiya səhvi.</summary>
    public record GenderCapIssue(string Name, int Quota, int? MaxFemale, int? MaxMale, int Capacity, int Shortfall);

    public record SourceBalanceReport(SourceSlotCheck Mülki, SourceSlotCheck Lisey, bool Ok,
                                      List<GenderCapIssue> CapIssues);

    public static bool CanChoose(Student u, IReadOnlyList<SpecialtyNode> path, int? preAssignLevel)
    {
        if (path.Count == 0) return false;
        var leaf = path[^1];

        // 1) Qrup — hər səviyyədə (təhsilalanın qrupu boşdursa filtr yoxdur)
        var group = (u.Group ?? "").Trim();
        if (group.Length > 0)
            foreach (var n in path)
                if (n.Groups is { Count: > 0 } && !n.Groups.Contains(group)) return false;

        // 2) Əvvəlcədən təyin edilmiş budaq
        if (preAssignLevel is int lv && u.BranchByLevel.TryGetValue(lv, out var b))
        {
            var branch = (b ?? "").Trim();
            if (branch.Length > 0)
            {
                if (lv >= path.Count) return false;
                if (!string.Equals(path[lv].Name.Trim(), branch, StringComparison.OrdinalIgnoreCase)) return false;
            }
        }

        // 3) Cins — yarpaqda
        var gender = (u.Gender ?? "").Trim().ToLowerInvariant();
        if (gender == "qadın" && leaf.AllowFemale == false) return false;
        if (gender == "kişi"  && leaf.AllowMale   == false) return false;

        return true;
    }

    public static Split SplitQuota(int quota, Pool pool)
    {
        var q = Math.Max(0, quota);
        if (q == 0) return new Split(0, 0);
        var b = pool.Mülki + pool.Lisey;
        if (b == 0) return new Split(0, 0);

        var m = (int)Math.Round((double)q * pool.Mülki / b, MidpointRounding.AwayFromZero);
        var l = q - m;
        if (m > pool.Mülki) { l += m - pool.Mülki; m = pool.Mülki; }
        if (l > pool.Lisey) { m += l - pool.Lisey; l = pool.Lisey; }
        if (m > pool.Mülki) m = pool.Mülki;
        return new Split(m, l);
    }

    // ── Qlobal mənbə bölgüsü (quota-pool.ts → globalSourceSplit) ─────────────
    //
    // ⚠ SplitQuota (yerli nisbət) BALANS YOXLAMASINDA İŞLƏDİLMƏMƏLİDİR.
    // O, hər ixtisasın payını yalnız həmin ixtisası seçə bilənlərin baş sayından
    // çıxarır və bir budağa bağlı ("captive") qrupları sıradan çıxarır. Frontend
    // bu qaydadan çoxdan imtina edib; backend onunla qaldığı üçün ekranda balans
    // düzgün görünsə də yayım 409 qaytarırdı. Doğru qayda qlobaldır: hər qrupun
    // ümumi payı öz namizəd sayı qədərdir və girə bildiyi bütün ixtisaslar
    // arasında yerlərə mütənasib yayılır.
    private sealed class Seg
    {
        public string Source = "";
        public int Count;
        public List<int> Elig = new();
        public Student Rep = null!;
    }

    private static List<Seg> BuildSegments(
        IReadOnlyList<Student> students, List<List<SpecialtyNode>> leaves, int? preAssignLevel)
    {
        var map = new Dictionary<string, Seg>();
        foreach (var u in students)
        {
            var branch = "";
            if (preAssignLevel is int lv && u.BranchByLevel.TryGetValue(lv, out var b))
                branch = (b ?? "").Trim();
            var key = $"{(u.Group ?? "").Trim()}{(u.Gender ?? "").Trim()}{(u.Source ?? "").Trim()}{branch}";
            if (!map.TryGetValue(key, out var s))
            {
                s = new Seg { Source = (u.Source ?? "").Trim(), Rep = u };
                for (var j = 0; j < leaves.Count; j++)
                    if (CanChoose(u, leaves[j], preAssignLevel)) s.Elig.Add(j);
                map[key] = s;
            }
            s.Count++;
        }
        return map.Values.ToList();
    }

    /// <summary>Seqment tutumları verilmiş halda maksimum axın (Edmonds–Karp).</summary>
    private static (int Total, int[] PerSeg) MaxFlowWith(List<Seg> segs, int[] caps, int[] quotas)
    {
        const int S = 0, T = 1;
        var off = 2;
        var offL = 2 + segs.Count;
        var n = offL + quotas.Length;
        var cap = new int[n, n];

        for (var i = 0; i < segs.Count; i++) cap[S, off + i] = caps[i];
        for (var j = 0; j < quotas.Length; j++) cap[offL + j, T] = Math.Max(0, quotas[j]);
        for (var i = 0; i < segs.Count; i++)
            foreach (var j in segs[i].Elig) cap[off + i, offL + j] = int.MaxValue;

        var total = 0;
        while (true)
        {
            var par = new int[n];
            Array.Fill(par, -1);
            par[S] = S;
            var q = new List<int> { S };
            for (var h = 0; h < q.Count && par[T] < 0; h++)
            {
                var v = q[h];
                for (var w = 0; w < n; w++)
                    if (par[w] < 0 && cap[v, w] > 0) { par[w] = v; q.Add(w); }
            }
            if (par[T] < 0) break;
            var f = int.MaxValue;
            for (var v = T; v != S; v = par[v]) f = Math.Min(f, cap[par[v], v]);
            for (var v = T; v != S; v = par[v]) { cap[par[v], v] -= f; cap[v, par[v]] += f; }
            total += f;
        }

        var perSeg = new int[segs.Count];
        for (var i = 0; i < segs.Count; i++) perSeg[i] = cap[off + i, S];
        return (total, perSeg);
    }

    /// <summary>
    /// Balans xəbərdarlığının toleransı (yer sayı) — quota-pool.ts-dəki
    /// BALANCE_TOLERANCE ilə eyni olmalıdır. Model kəsr paylar hesablayır, ekranda
    /// isə tam ədəd lazımdır; hər qrupun sərhədində ±0.5-ə qədər yuvarlaqlaşdırma
    /// xətası qaçılmazdır və qonşu qruplarda bir-birini tamamlayır. 1 yerlik
    /// buraxılış həmin artefaktı udur, ondan böyüyü real disbalansdır.
    /// </summary>
    public const int BalanceTolerance = 1;

    private static Dictionary<string, Split> GlobalSourceSplit(
        List<List<SpecialtyNode>> leaves, IReadOnlyList<Student> students, int? preAssignLevel,
        out Dictionary<string, (double M, double L)> raw)
    {
        var outMap = new Dictionary<string, Split>();
        raw = new Dictionary<string, (double M, double L)>();
        if (leaves.Count == 0) return outMap;

        var quotas = leaves.Select(p => Math.Max(0, p[^1].Quota ?? 0)).ToArray();
        var segs = BuildSegments(students, leaves, preAssignLevel);
        if (segs.Count == 0) return outMap;

        // Addım 1: hər seqment neçə yer ala bilər
        var demand = segs.Select(s => s.Count).ToArray();
        var demandSum = demand.Sum();
        var full = MaxFlowWith(segs, demand, quotas);
        int[] supply;
        if (full.Total >= demandSum)
        {
            supply = demand;
        }
        else
        {
            // max-min ədalətli: ortaq faizi ikili axtarışla tap, qalığı bir-bir payla
            double lo = 0, hi = 1;
            for (var it = 0; it < 30; it++)
            {
                var mid = (lo + hi) / 2;
                var caps = demand.Select(d => (int)Math.Floor(d * mid)).ToArray();
                var need = caps.Sum();
                if (MaxFlowWith(segs, caps, quotas).Total >= need) lo = mid; else hi = mid;
            }
            supply = demand.Select(d => (int)Math.Floor(d * lo)).ToArray();
            while (true)
            {
                var grew = false;
                for (var i = 0; i < segs.Count; i++)
                {
                    if (supply[i] >= demand[i]) continue;
                    var trial = (int[])supply.Clone();
                    trial[i]++;
                    if (MaxFlowWith(segs, trial, quotas).Total >= trial.Sum()) { supply = trial; grew = true; }
                }
                if (!grew) break;
            }
        }

        // Addım 2: hər seqmentin payını ixtisaslara mütənasib yay (iterativ uyğunlaşdırma)
        var alloc = new double[segs.Count][];
        for (var i = 0; i < segs.Count; i++) alloc[i] = new double[leaves.Count];

        for (var it = 0; it < 200; it++)
        {
            for (var i = 0; i < segs.Count; i++)
            {
                var s = segs[i];
                double tot = 0;
                foreach (var j in s.Elig) tot += alloc[i][j];
                if (tot <= 1e-12)
                {
                    double capSum = 0;
                    foreach (var j in s.Elig) capSum += quotas[j];
                    if (capSum > 0)
                        foreach (var j in s.Elig) alloc[i][j] = supply[i] * quotas[j] / capSum;
                }
                else
                {
                    var f = supply[i] / tot;
                    foreach (var j in s.Elig) alloc[i][j] *= f;
                }
            }
            for (var j = 0; j < leaves.Count; j++)
            {
                double tot = 0;
                for (var i = 0; i < segs.Count; i++) tot += alloc[i][j];
                if (tot > quotas[j] + 1e-12)
                {
                    var f = quotas[j] / tot;
                    for (var i = 0; i < segs.Count; i++) alloc[i][j] *= f;
                }
            }
        }

        // `alloc` "gözlənilən dolma"dır və cəmi kvotadan az ola bilər, amma bu cədvəl
        // kvotanın MƏNBƏLƏR ARASINDA BÖLGÜSÜ kimi işlədilir — yer havada qalmamalıdır.
        // Qalıq namizədi olan mənbələrə hovuz ölçüsünə mütənasib paylanır.
        // (quota-pool.ts-dəki eyni blokun qarşılığı — ikisi birlikdə dəyişdirilməlidir,
        //  əks halda ekrandakı balans ilə yayım yoxlaması yenidən uyğunsuzlaşar.)
        var poolM = new int[leaves.Count];
        var poolL = new int[leaves.Count];
        for (var i = 0; i < segs.Count; i++)
            foreach (var j in segs[i].Elig)
            {
                if (segs[i].Source == "mülki") poolM[j] += segs[i].Count;
                else if (segs[i].Source == "lisey") poolL[j] += segs[i].Count;
            }

        for (var j = 0; j < leaves.Count; j++)
        {
            double m = 0, l = 0;
            for (var i = 0; i < segs.Count; i++)
            {
                if (segs[i].Source == "mülki") m += alloc[i][j];
                else if (segs[i].Source == "lisey") l += alloc[i][j];
            }
            var q = quotas[j];
            var mi = Math.Max(0, Math.Min((int)Math.Round(m, MidpointRounding.AwayFromZero), q));
            var li = Math.Max(0, Math.Min((int)Math.Round(l, MidpointRounding.AwayFromZero), q - mi));

            var rem = q - mi - li;
            if (rem > 0)
            {
                var freeM = Math.Max(0, poolM[j] - mi);
                var freeL = Math.Max(0, poolL[j] - li);
                if (freeM > 0 && freeL > 0)
                {
                    var addM = Math.Min(freeM, (int)Math.Round((double)rem * freeM / (freeM + freeL),
                                                               MidpointRounding.AwayFromZero));
                    mi += addM; rem -= addM;
                    var addL = Math.Min(freeL, rem);
                    li += addL; rem -= addL;
                    if (rem > 0) { var extra = Math.Min(freeM - addM, rem); mi += extra; rem -= extra; }
                }
                else if (freeM > 0) { var add = Math.Min(freeM, rem); mi += add; }
                else if (freeL > 0) { var add = Math.Min(freeL, rem); li += add; }
                // qalıq hələ də varsa, doğrudan da namizəd çatmır — yer boş qalacaq
            }

            // Yuvarlaqlaşdırmadan əvvəlki kəsr dəyərlər — balans qərarı bunlarla verilir
            raw[leaves[j][^1].Id] = (m, l);
            outMap[leaves[j][^1].Id] = new Split(mi, li);
        }
        return outMap;
    }

    /// <summary>Ağacı gəzib hər yarpaq üçün hovuzu qurur və balansı yoxlayır.</summary>
    public static Report Check(
        IReadOnlyList<SpecialtyNode> nodes,
        IReadOnlyList<Student> students,
        int? preAssignLevel)
    {
        var childrenOf = nodes.GroupBy(n => n.ParentId ?? "")
                              .ToDictionary(g => g.Key, g => g.OrderBy(n => n.SortOrder).ToList());
        var roots = childrenOf.TryGetValue("", out var r) ? r : new List<SpecialtyNode>();

        var leaves = new List<List<SpecialtyNode>>();
        void Walk(List<SpecialtyNode> list, List<SpecialtyNode> anc)
        {
            foreach (var n in list)
            {
                var path = new List<SpecialtyNode>(anc) { n };
                if (childrenOf.TryGetValue(n.Id, out var kids) && kids.Count > 0) Walk(kids, path);
                else leaves.Add(path);
            }
        }
        Walk(roots, new List<SpecialtyNode>());

        // Avtomatik bölgü cədvəli — frontend-dəki globalSourceSplitCached ilə eyni qayda.
        var table = GlobalSourceSplit(leaves, students, preAssignLevel, out var rawTable);

        // hovuz imzasına görə qruplaşdır
        var groups = new Dictionary<string, (Pool pool, int autoM, int autoL, int effM, int effL,
                                             double rawM, double rawL, List<string> names)>();

        foreach (var path in leaves)
        {
            var leaf = path[^1];
            var ids = new List<string>();
            int m = 0, l = 0, o = 0;
            foreach (var u in students)
            {
                if (!CanChoose(u, path, preAssignLevel)) continue;
                ids.Add(u.Id);
                if (u.Source == "mülki") m++;
                else if (u.Source == "lisey") l++;
                else o++;
            }
            ids.Sort(StringComparer.Ordinal);
            var key = string.Join("|", ids);
            var pool = new Pool(m, l, o);

            var q = leaf.Quota ?? 0;
            // Cədvəl hesablana bilməyibsə yerli nisbət ehtiyat variant kimi qalır
            // (frontend-dəki autoSplit da eyni ehtiyata düşür).
            var auto = table.TryGetValue(leaf.Id, out var g0) ? g0 : SplitQuota(q, pool);
            var eff = leaf.QuotaMode == "manual" && leaf.MulkiQuota != null && leaf.LiseyQuota != null
                ? new Split(leaf.MulkiQuota.Value, leaf.LiseyQuota.Value)
                : auto;

            // Kəsr dəyər cədvəldə yoxdursa yuvarlaq dəyər ehtiyat kimi götürülür
            var rawPair = rawTable.TryGetValue(leaf.Id, out var r0)
                ? r0 : ((double)auto.Mülki, (double)auto.Lisey);

            if (!groups.TryGetValue(key, out var g))
                g = (pool, 0, 0, 0, 0, 0d, 0d, new List<string>());
            g = (pool, g.autoM + auto.Mülki, g.autoL + auto.Lisey,
                       g.effM + eff.Mülki,   g.effL + eff.Lisey,
                       g.rawM + rawPair.Item1, g.rawL + rawPair.Item2, g.names);
            if (eff.Mülki != auto.Mülki || eff.Lisey != auto.Lisey) g.names.Add(leaf.Name);
            groups[key] = g;
        }

        var broken = new List<BrokenGroup>();
        int lostM = 0, lostL = 0;
        foreach (var (_, g) in groups)
        {
            // Sapma XAM modelə görə ölçülür və tolerans tətbiq olunur — ayrı-ayrı
            // yuvarlaqlaşdırılmış dəyərlərin cəmi qrup sərhədində 1 yerlik yalan
            // sapma yaradır (bax: BalanceTolerance).
            var dm = g.effM - g.rawM;
            var dl = g.effL - g.rawL;
            if (Math.Abs(dm) <= BalanceTolerance + 1e-9 && Math.Abs(dl) <= BalanceTolerance + 1e-9) continue;
            broken.Add(new BrokenGroup(g.pool.Total, g.pool.Mülki, g.pool.Lisey,
                                       g.autoM, g.autoL, g.effM, g.effL, g.names));
            if (dm < 0) lostM += (int)Math.Round(-dm, MidpointRounding.AwayFromZero);
            if (dl < 0) lostL += (int)Math.Round(-dl, MidpointRounding.AwayFromZero);
        }

        return new Report(broken.Count == 0, broken, lostM, lostL);
    }
    /// <summary>
    /// Cins limitlərini də nəzərə alan maksimum axın (quota-pool.ts → maxFlowGendered).
    /// S → seqment → (qadın/kişi qapısı) → ixtisas → T. Cinsi yazılmayanlar qapısız keçir.
    ///
    /// ⚠ maxFemale/maxMale bütün ixtisas üzrədir, mənbə üzrə deyil. Yerlər tək mənbəyə
    /// aid olduqda nəticə dəqiqdir; iki mənbə arasında bölünübsə yoxlama bir qədər
    /// nikbin olur (cins və mənbə limitləri bir-birini kəsən iki ölçüdür).
    /// </summary>
    private static int MaxFlowGendered(
        List<Seg> segs, int[] caps, int[] quotas, List<List<SpecialtyNode>> leaves)
    {
        var L = quotas.Length;
        const int S = 0, T = 1;
        var segOff = 2;
        var femOff = segOff + segs.Count;
        var malOff = femOff + L;
        var leafOff = malOff + L;
        var n = leafOff + L;

        var cap = new int[n, n];
        for (var i = 0; i < segs.Count; i++) cap[S, segOff + i] = caps[i];
        for (var j = 0; j < L; j++)
        {
            var leaf = leaves[j][^1];
            var q = Math.Max(0, quotas[j]);
            var mf = leaf.AllowFemale == false ? 0 : (leaf.MaxFemale != null ? Math.Min(leaf.MaxFemale.Value, q) : q);
            var mm = leaf.AllowMale   == false ? 0 : (leaf.MaxMale   != null ? Math.Min(leaf.MaxMale.Value,   q) : q);
            cap[femOff + j, leafOff + j] = mf;
            cap[malOff + j, leafOff + j] = mm;
            cap[leafOff + j, T] = q;
        }
        for (var i = 0; i < segs.Count; i++)
        {
            var g = (segs[i].Rep.Gender ?? "").Trim().ToLowerInvariant();
            foreach (var j in segs[i].Elig)
            {
                var to = g == "qadın" ? femOff + j : g == "kişi" ? malOff + j : leafOff + j;
                cap[segOff + i, to] = int.MaxValue;
            }
        }

        var total = 0;
        while (true)
        {
            var par = new int[n];
            Array.Fill(par, -1);
            par[S] = S;
            var q2 = new List<int> { S };
            for (var h = 0; h < q2.Count && par[T] < 0; h++)
            {
                var v = q2[h];
                for (var w = 0; w < n; w++)
                    if (par[w] < 0 && cap[v, w] > 0) { par[w] = v; q2.Add(w); }
            }
            if (par[T] < 0) break;
            var f = int.MaxValue;
            for (var v = T; v != S; v = par[v]) f = Math.Min(f, cap[par[v], v]);
            for (var v = T; v != S; v = par[v]) { cap[par[v], v] -= f; cap[v, par[v]] += f; }
            total += f;
        }
        return total;
    }

// ── Sərt mənbə rejimində real tarazlıq (quota-pool.ts → sourceBalance) ──────
    //
    // Yerləşdirmənin 3-cü mərhələsi mənbələri qarışdırmır: mülki slotu yalnız
    // mülki, lisey slotu yalnız lisey doldura bilər. Ona görə yayım şərti də
    // modelə yaxınlıq deyil, REAL icra olunabilirlikdir:
    //   əskik slot = yer tapa bilməyən namizəd · artıq slot = boş qalacaq yer
    // Tolerans yoxdur — 1 yer də real itkidir.
    public static SourceBalanceReport CheckSources(
        IReadOnlyList<SpecialtyNode> nodes,
        IReadOnlyList<Student> students,
        int? preAssignLevel)
    {
        var childrenOf = nodes.GroupBy(n => n.ParentId ?? "")
                              .ToDictionary(g => g.Key, g => g.OrderBy(n => n.SortOrder).ToList());
        var roots = childrenOf.TryGetValue("", out var r) ? r : new List<SpecialtyNode>();

        var leaves = new List<List<SpecialtyNode>>();
        void Walk(List<SpecialtyNode> list, List<SpecialtyNode> anc)
        {
            foreach (var n in list)
            {
                var path = new List<SpecialtyNode>(anc) { n };
                if (childrenOf.TryGetValue(n.Id, out var kids) && kids.Count > 0) Walk(kids, path);
                else leaves.Add(path);
            }
        }
        Walk(roots, new List<SpecialtyNode>());
        if (leaves.Count == 0)
            return new SourceBalanceReport(new SourceSlotCheck(0, 0, 0, 0, 0),
                                           new SourceSlotCheck(0, 0, 0, 0, 0), true,
                                           new List<GenderCapIssue>());

        var table = GlobalSourceSplit(leaves, students, preAssignLevel, out _);
        var segs = BuildSegments(students, leaves, preAssignLevel);

        // Hər yarpağın qüvvədə olan bölgüsü: manual varsa o, yoxsa avtomatik cədvəl
        var eff = new List<Split>();
        foreach (var path in leaves)
        {
            var leaf = path[^1];
            var q = leaf.Quota ?? 0;
            Pool pool;
            {
                int m = 0, l = 0, o = 0;
                foreach (var u in students)
                {
                    if (!CanChoose(u, path, preAssignLevel)) continue;
                    if (u.Source == "mülki") m++; else if (u.Source == "lisey") l++; else o++;
                }
                pool = new Pool(m, l, o);
            }
            var auto = table.TryGetValue(leaf.Id, out var g0) ? g0 : SplitQuota(q, pool);
            eff.Add(leaf.QuotaMode == "manual" && leaf.MulkiQuota != null && leaf.LiseyQuota != null
                ? new Split(leaf.MulkiQuota.Value, leaf.LiseyQuota.Value)
                : auto);
        }

        SourceSlotCheck Check1(string src)
        {
            var quotas = eff.Select(e => Math.Max(0, src == "mülki" ? e.Mülki : e.Lisey)).ToArray();
            var seats = quotas.Sum();
            var mySegs = segs.Where(s => s.Source == src).ToList();
            if (mySegs.Count == 0) return new SourceSlotCheck(0, seats, 0, 0, seats);
            var caps = mySegs.Select(s => s.Count).ToArray();
            var candidates = caps.Sum();
            var flow = MaxFlowGendered(mySegs, caps, quotas, leaves);
            return new SourceSlotCheck(candidates, seats, flow, candidates - flow, seats - flow);
        }

        var mü = Check1("mülki");
        var li = Check1("lisey");

        // Cins limitlərinin cəmi kvotadan azdırsa, həmin yerlər heç bir halda dolmayacaq
        var capIssues = new List<GenderCapIssue>();
        foreach (var path in leaves)
        {
            var leaf = path[^1];
            var q = leaf.Quota ?? 0;
            if (q <= 0) continue;
            if (leaf.MaxFemale == null && leaf.MaxMale == null
                && leaf.AllowFemale != false && leaf.AllowMale != false) continue;
            var mf = leaf.AllowFemale == false ? 0 : (leaf.MaxFemale != null ? Math.Min(leaf.MaxFemale.Value, q) : q);
            var mm = leaf.AllowMale   == false ? 0 : (leaf.MaxMale   != null ? Math.Min(leaf.MaxMale.Value,   q) : q);
            var capacity = Math.Min(q, mf + mm);
            if (capacity >= q) continue;
            capIssues.Add(new GenderCapIssue(leaf.Name, q,
                leaf.AllowFemale == false ? 0 : leaf.MaxFemale,
                leaf.AllowMale   == false ? 0 : leaf.MaxMale,
                capacity, q - capacity));
        }

        return new SourceBalanceReport(mü, li,
            mü.Deficit == 0 && mü.Unfillable == 0 && li.Deficit == 0 && li.Unfillable == 0,
            capIssues);
    }
}
