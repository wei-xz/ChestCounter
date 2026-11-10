using System.Globalization;
using System.Net;
using System.Text;

namespace ChestCounter;

public static class Pretty
{
    public static string Playlist(string? playlist)
    {
        if (string.IsNullOrEmpty(playlist))
        {
            return L.T("mode inconnu", "unknown mode", "modo desconocido");
        }

        var p = playlist.ToLowerInvariant();
        var parts = new List<string>();
        if (p.Contains("habanero") || p.Contains("ranked")) parts.Add(L.T("Classée", "Ranked", "Clasificatoria"));
        if (p.Contains("nobuild")) parts.Add(L.T("Zéro construction", "Zero Build", "Cero construcción"));
        if (p.Contains("solo")) parts.Add(L.T("Solo", "Solo", "Solitario"));
        else if (p.Contains("duo")) parts.Add(L.T("Duo", "Duos", "Dúos"));
        else if (p.Contains("trio")) parts.Add(L.T("Trio", "Trios", "Tríos"));
        else if (p.Contains("squad")) parts.Add(L.T("Section", "Squads", "Escuadrones"));
        return parts.Count > 0 ? string.Join(" · ", parts) : playlist.Replace("Playlist_", "");
    }

    public static string Placement(MatchRecord m)
    {
        if (m.Placement is not int place || place <= 0)
        {
            return L.T("place inconnue", "unknown placement", "puesto desconocido");
        }
        if (place == 1)
        {
            return L.T("Top 1 !", "Victory Royale!", "¡Victoria magistral!");
        }
        return m.TotalPlayers is int total && total > 0 ? $"{L.Ordinal(place)} / {total}" : L.Ordinal(place);
    }

    public static string Clock(float seconds)
    {
        var s = (int)seconds;
        return $"{s / 60}:{s % 60:00}";
    }

    /// <summary>"à 7:16 · Coffre rare" (certain) or "vers 5:04 · Coffre rare (probable)".</summary>
    public static string Obtained(TreasureFind f)
    {
        if (f.ObtainedAtSeconds is not float t)
        {
            return L.T("moment et source inconnus", "time and source unknown", "momento y origen desconocidos");
        }
        var when = f.TimeExact ? $"{L.T("à", "at", "a los")} {Clock(t)}" : $"{L.T("vers", "around", "hacia los")} {Clock(t)}";
        return f.SourceCertain ? $"{when} · {f.SourceLabel}" : $"{when} · {f.SourceLabel} ({Likely})";
    }

    public static string Likely => L.T("probable", "likely", "probable");

    public static string ItemName(string item)
    {
        var name = item;
        foreach (var prefix in new[] { "WID_", "Athena", "AmmoData" })
        {
            name = name.Replace(prefix, "");
        }
        return name.Replace("_", " ").Trim();
    }
}

/// <summary>Replays analyzed so far / to analyze, shown as a loading card in the report.</summary>
public sealed record ScanProgress(int Done, int Total);

public static class ReportWriter
{
    public static readonly DateTime SessionStartUtc = DateTime.UtcNow;

    /// <summary>Set while several replays are being analyzed: the report shows a loading card and refreshes faster.</summary>
    public static volatile ScanProgress? Progress;

    /// <summary>A match belongs to this session if it ended after the tool was started (backlog replays do not count).</summary>
    public static bool IsThisSession(MatchRecord m)
    {
        var start = m.MatchStartUtc ?? m.AnalyzedAtUtc;
        if (start.Kind == DateTimeKind.Unspecified)
        {
            start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
        }
        var end = start.ToUniversalTime().AddSeconds(m.DurationSeconds ?? 0);
        return end >= SessionStartUtc.AddMinutes(-2);
    }
    private static CultureInfo Fr => L.Culture;

    public static void WriteAll(Store store, Config config, TrackerData data)
    {
        try
        {
            var view = Profiles.View(data, config);
            WriteAtomic(store.ReportPath, Report(config, view, Profiles.List(data)));
            WriteAtomic(store.OverlayPath, Overlay(OverlayModel.Build(view, null, false)));
            WriteAtomic(store.CsvPath, Csv(data));
        }
        catch (Exception ex)
        {
            Console.WriteLine(L.T($"Impossible d'écrire le rapport : {ex.Message}", $"Cannot write the report: {ex.Message}", $"No se puede escribir el informe: {ex.Message}"));
        }
    }

    private static void WriteAtomic(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content, new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }

    private static string H(string? s) => WebUtility.HtmlEncode(s ?? "");

    private static DateTime When(MatchRecord m) => (m.MatchStartUtc ?? m.AnalyzedAtUtc).ToLocalTime();

    // "Logbook" look (direction A): parchment cards on a night-sea background (a day version follows Windows' light
    // mode). Titles use the embedded Pirata One font; figures use Bahnschrift, installed with Windows.
    private const string Style = """
        :root{--bg:#0f1b24;--ink:#efe2c4;--ink2:#f7ecd2;--muted:#b8b0a0;--title:#f0c057;--card:#f2e6c9;--cink:#2a1c0f;--cmuted:#6b5a44;--line:#d8c59c;--track:#e4d3ad;--gold:#c9a24f;--star:#6c3fb5;--team:#1f7f7a;--warn:#a3361f;--warnbg:rgba(200,65,47,.10)}
        @media (prefers-color-scheme:light){:root:not([data-theme="dark"]){--bg:#efe4c9;--ink:#2a1c0f;--ink2:#2a1c0f;--muted:#6b5a44;--title:#7a4a1e;--card:#fffaf0;--line:#e2d2ae;--track:#efe2c4}}
        :root[data-theme="light"]{--bg:#efe4c9;--ink:#2a1c0f;--ink2:#2a1c0f;--muted:#6b5a44;--title:#7a4a1e;--card:#fffaf0;--line:#e2d2ae;--track:#efe2c4}
        *{box-sizing:border-box}
        body{margin:0;background:var(--bg);color:var(--ink);font:15px/1.5 "Segoe UI",system-ui,-apple-system,sans-serif}
        main{max-width:1120px;margin:0 auto;padding:36px 16px 56px;display:flex;flex-direction:column;gap:28px}
        .pirate{font-family:"Pirata One","Segoe UI",serif;font-weight:400}
        .num{font-family:Bahnschrift,"Segoe UI",sans-serif;font-stretch:75%;font-variant-numeric:tabular-nums}
        header{display:flex;flex-wrap:wrap;align-items:flex-end;justify-content:space-between;gap:12px;border-bottom:2px solid var(--gold);padding-bottom:16px}
        .brand{display:flex;align-items:center;gap:14px}
        h1{margin:0;font-size:44px;line-height:1;color:var(--title)}
        .who{font-size:15px;color:var(--muted);margin-top:4px}.who b{color:var(--ink2)}
        .muted{color:var(--muted)}
        h2{margin:0 0 10px;font-size:26px;line-height:1.2;color:var(--title)}
        .tiles{display:grid;grid-template-columns:repeat(6,minmax(0,1fr));gap:12px}
        .tile{background:var(--card);color:var(--cink);border:1px solid var(--line);border-radius:8px;padding:14px 16px;display:flex;flex-direction:column;gap:2px}
        .tile .v{font-size:36px;font-weight:700;line-height:1.1}
        .tile .v.sm{font-size:26px;padding-top:6px}
        .tile .l{font-size:13px;color:var(--cmuted)}
        .tile .s{font-size:12px;color:var(--cmuted);margin-top:4px}
        .tile.hero{grid-column:span 2;background:#7a4a1e;color:#fbecc4;border:2px solid #e7b440;justify-content:space-between}
        .tile.hero .k{font-size:12px;letter-spacing:.14em;text-transform:uppercase;color:#f3cf7a}
        .tile.hero .v{font-size:60px;color:#ffd36a;line-height:1}
        .tile.hero .l{color:#fbecc4}
        @media (max-width:820px){.tiles{grid-template-columns:repeat(2,minmax(0,1fr))}.tile.hero{grid-column:span 2}}
        .card{background:var(--card);color:var(--cink);border:1px solid var(--line);border-radius:8px;padding:6px 16px;overflow-x:auto}
        .two{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:20px}
        @media (max-width:820px){.two{grid-template-columns:1fr}}
        table{width:100%;border-collapse:collapse;font-variant-numeric:tabular-nums}
        th,td{text-align:left;padding:9px 8px;border-bottom:1px solid var(--line);vertical-align:middle}
        th{white-space:nowrap;font-size:12px;text-transform:uppercase;letter-spacing:.08em;color:var(--cmuted);font-weight:600}
        tr:last-child td{border-bottom:none}
        td.n{text-align:right;font-weight:700}
        td.nw{white-space:nowrap}
        .big{font-size:20px}
        .items{color:var(--cmuted);font-size:13px}
        .star{color:var(--star);font-weight:600}
        .gold{color:#b9821c}
        .sw{display:inline-block;width:10px;height:10px;border-radius:2px;margin-right:10px;vertical-align:-1px}
        .meter{display:block;width:140px;height:6px;background:var(--track);border-radius:3px;overflow:hidden}
        .meter i{display:block;height:6px}
        .flag{display:flex;gap:12px;align-items:flex-start;background:#3a1f12;color:#f6dfb0;border:1px solid #c8412f;border-radius:8px;padding:12px 14px;font-size:14px;margin-top:12px}
        tr.unusual td{background:var(--warnbg)}
        .warn{color:var(--warn)}
        .chart{padding:18px 18px 12px}
        .bars{display:flex;align-items:stretch;gap:4px;height:150px;padding-top:26px}
        .col{flex:1;min-width:5px;display:flex;align-items:flex-end;position:relative;cursor:default}
        .bar{width:100%;background:var(--gold);border-radius:3px 3px 0 0;transition:filter .12s}
        .bar.top{background:#c8412f}
        .bar.zero{background:var(--line);height:3px!important}
        .col:hover .bar{filter:brightness(1.15)}
        .tip{position:absolute;left:50%;transform:translateX(-50%);white-space:nowrap;background:var(--cink);color:var(--card);
             padding:3px 8px;border-radius:6px;font-size:12px;font-weight:600;opacity:0;pointer-events:none;transition:opacity .12s;z-index:2}
        .col:nth-child(-n+3) .tip{left:0;transform:none}
        .col:nth-last-child(-n+3) .tip{left:auto;right:0;transform:none}
        .col:hover .tip{opacity:1}
        .axis{display:flex;justify-content:space-between;gap:8px;color:var(--cmuted);font-size:12px;margin-top:8px;border-top:1px dashed var(--line);padding-top:6px}
        .head{display:flex;flex-wrap:wrap;align-items:center;justify-content:space-between;gap:12px;margin-bottom:10px}
        .head h2{margin:0}
        .teamtoggle{display:inline-flex;align-items:center;gap:10px;min-height:44px;font-size:14px;color:var(--ink2);cursor:pointer;user-select:none}
        .teamtoggle input{width:18px;height:18px;accent-color:var(--gold);cursor:pointer}
        .team{display:none}
        body.showteam tr.team{display:table-row}
        body.showteam span.team{display:inline}
        tr.team td{opacity:.8}
        .by{white-space:nowrap}
        .by svg{vertical-align:-3px;margin-right:6px}
        footer{font-size:13px;color:var(--muted);border-top:1px dashed var(--gold);padding-top:14px}
        .loading{display:flex;gap:16px;align-items:center;background:var(--card);color:var(--cink);border:2px solid var(--gold);border-radius:8px;padding:16px 18px}
        .loading .muted{color:var(--cmuted)}
        .spin{width:30px;height:30px;flex:none;border:3px solid var(--line);border-top-color:#b9821c;border-radius:50%;animation:spin .8s linear infinite}
        @keyframes spin{to{transform:rotate(360deg)}}
        .loading .grow{flex:1;min-width:0}
        .pbar{height:8px;background:var(--line);border-radius:4px;overflow:hidden;margin:8px 0 6px}
        .pbar i{display:block;height:100%;background:#b9821c}
        .profiles{margin-top:-16px;font-size:13px}
        .legend{display:flex;gap:18px;font-size:13px;color:var(--cmuted);margin:0 0 4px}
        .stack{width:100%;display:flex;flex-direction:column}
        .stack .bar{width:100%;border-radius:0;height:auto;min-height:0}
        .stack .bar.sb{border-radius:3px 3px 0 0;background:var(--team)}
        .bar.hr{background:#6c5ac7}
        .nums{display:flex;gap:4px;padding-top:6px}
        .nums span{flex:1 1 0;min-width:0;display:flex;justify-content:center;font-size:11px;font-weight:600;color:var(--cink);white-space:nowrap;font-variant-numeric:tabular-nums;height:16px}
        .tot{display:none}
        body.chestteam .own{display:none}
        body.chestteam .bars.tot,body.chestteam .nums.tot,body.chestteam .axis.tot,body.chestteam .legend.tot{display:flex}
        .hline{display:none;width:100%;height:150px}
        body.hourline .hbars{display:none}
        body.hourline .hline{display:block}
        .prob svg{width:100%;height:auto;display:block}
        .prob input{width:110px;font:inherit;padding:6px 8px;border:1px solid var(--line);border-radius:6px;background:#fff8e6;color:var(--cink)}
        .prob .row{display:flex;gap:12px;align-items:center;flex-wrap:wrap;margin-bottom:8px}
        @media (max-width:700px){.nums{display:none!important}}
        """;

    /// <summary>@font-face of the embedded headings font (empty if the resource is missing: the page falls back to Segoe UI).</summary>
    private static readonly string FontFace = LoadFontFace();

    private static string LoadFontFace()
    {
        try
        {
            using var s = typeof(ReportWriter).Assembly.GetManifestResourceStream("PirataOne.woff2");
            if (s == null)
            {
                return "";
            }
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return "@font-face{font-family:'Pirata One';src:url(data:font/woff2;base64," + Convert.ToBase64String(ms.ToArray()) + ") format('woff2');font-display:swap}";
        }
        catch
        {
            return "";
        }
    }

    private const string ChestIcon = """<svg width="52" height="52" viewBox="0 0 56 56" aria-hidden="true"><rect x="6" y="20" width="44" height="28" rx="4" fill="#7a4a1e" stroke="#e7b440" stroke-width="3"/><path d="M6 26 Q28 4 50 26" fill="#965d27" stroke="#e7b440" stroke-width="3"/><rect x="24" y="24" width="8" height="10" rx="2" fill="#e7b440"/><line x1="6" y1="34" x2="50" y2="34" stroke="#e7b440" stroke-width="2"/></svg>""";
    private const string TeamIcon = """<svg width="16" height="16" viewBox="0 0 24 24" aria-hidden="true"><circle cx="9" cy="8" r="3.2" fill="none" stroke="#1f7f7a" stroke-width="2"/><path d="M3 20c0-3.5 2.7-6 6-6s6 2.5 6 6" fill="none" stroke="#1f7f7a" stroke-width="2"/><circle cx="17" cy="9" r="2.6" fill="none" stroke="#1f7f7a" stroke-width="2"/><path d="M16 14c3 0 5 2.2 5 5" fill="none" stroke="#1f7f7a" stroke-width="2"/></svg>""";
    private const string FlagIcon = """<svg width="18" height="18" viewBox="0 0 24 24" aria-hidden="true" style="flex:none;margin-top:2px"><path d="M5 21V4M5 4h11l-2 4 2 4H5" fill="none" stroke="#ff8a6a" stroke-width="2" stroke-linejoin="round"/></svg>""";

    private static readonly Dictionary<string, string> KindColor = new()
    {
        [ChestKinds.Normal] = "#c9a24f",
        [ChestKinds.Rare] = "#2c7fb8",
        [ChestKinds.Buried] = "#8a5a2b",
        [ChestKinds.Extraction] = "#1f7f7a",
        [ChestKinds.PosterChest] = "#7a4fc2",
        [ChestKinds.Other] = "#9a8f7a",
    };

    public static string Report(Config config, TrackerData data, IReadOnlyList<Profile> profiles)
    {
        var progress = Progress;
        var matches = data.Matches.OrderByDescending(When).ToList();
        var totalChests = matches.Sum(m => m.ChestsOpened);
        var avg = matches.Count > 0 ? (double)totalChests / matches.Count : 0;
        var best = matches.Count > 0 ? matches.Max(m => m.ChestsOpened) : 0;
        var treasures = matches.Sum(m => m.TreasureFinds.Count);
        var teamTreasures = matches.Sum(m => m.TeamTreasureFinds.Count);
        var session = matches.Where(IsThisSession).Sum(m => m.ChestsOpened);
        var teamChests = matches.Sum(m => m.TeamChestsOpened);
        var ignoredShort = data.ProcessedFiles.Values.Count(v => v == "short");
        var playedSeconds = matches.Sum(m => m.DurationSeconds ?? 0);
        var player = matches.Select(m => m.PlayerName).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? "";
        // Own treasures only: the team's are found with chests that are not counted here.
        var every = treasures > 0 ? (int)Math.Round((double)totalChests / treasures) : 0;

        var sb = new StringBuilder();
        var title = L.T("Compteur de coffres", "Chest Counter", "Contador de cofres");
        var updated = L.T(
            $"Mis à jour le {DateTime.Now.ToString("dd/MM/yyyy à HH:mm:ss", Fr)} · actualisation auto",
            $"Updated {DateTime.Now.ToString("d MMM yyyy 'at' HH:mm:ss", Fr)} · auto refresh",
            $"Actualizado el {DateTime.Now.ToString("dd/MM/yyyy 'a las' HH:mm:ss", Fr)} · actualización automática");
        string Tile(string value, string label, string? sub = null) =>
            $"""<div class="tile"><div class="v num">{value}</div><div class="l">{label}</div>{(sub != null ? $"<div class=\"s\">{sub}</div>" : "")}</div>""";

        sb.Append($"""
            <!doctype html><html lang="{L.Code}"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            {(progress != null ? "<meta http-equiv=\"refresh\" content=\"2\">" : "")}
            <title>{title}</title><style>{FontFace}{Style}</style></head><body><main>
            <header><div class="brand">{ChestIcon}<div><h1 class="pirate">{title}</h1>{(player.Length > 0 ? $"<div class=\"who\"><b>{H(player)}</b></div>" : "")}</div></div>
            <span class="muted" style="font-size:13px">{updated}</span></header>
            {LoadingCard(progress)}{ProfilesLine(profiles, player)}
            <section class="tiles">
              <div class="tile hero"><div class="k">{L.T("Coffres au total", "Chests in total", "Cofres en total")}</div><div class="v num">{totalChests.ToString("N0", Fr)}</div><div class="l">{L.T($"ouverts ou détruits par toi · {session.ToString("N0", Fr)} depuis le lancement", $"opened or destroyed by you · {session.ToString("N0", Fr)} since the tool started", $"abiertos o destruidos por ti · {session.ToString("N0", Fr)} desde que se abrió")}{(teamChests > 0 ? "<br><b>" + L.T($"{(totalChests + teamChests).ToString("N0", Fr)} avec ton équipe", $"{(totalChests + teamChests).ToString("N0", Fr)} including your team", $"{(totalChests + teamChests).ToString("N0", Fr)} con tu equipo") + "</b> " + L.T($"(+{teamChests.ToString("N0", Fr)} de tes coéquipiers)", $"(+{teamChests.ToString("N0", Fr)} by teammates)", $"(+{teamChests.ToString("N0", Fr)} de tus compañeros)") : "")}</div></div>
              {Tile(matches.Count.ToString("N0", Fr), L.T("parties analysées", "matches analyzed", "partidas analizadas"), ignoredShort > 0 ? (ignoredShort == 1 ? L.T("1 partie de moins de 2 min ignorée", "1 game under 2 min ignored", "1 partida de menos de 2 min ignorada") : L.T($"{ignoredShort} parties de moins de 2 min ignorées", $"{ignoredShort} games under 2 min ignored", $"{ignoredShort} partidas de menos de 2 min ignoradas")) : null)}
              {Tile(avg.ToString("0.0", Fr), L.T("coffres par partie", "chests per match", "cofres por partida"))}
              {Tile((treasures + teamTreasures).ToString("N0", Fr), L.T("trésors trouvés", "treasures found", "tesoros encontrados"),
                  (teamTreasures > 0 ? L.T($"{treasures} à toi + {teamTreasures} équipe (estimation)", $"{treasures} yours + {teamTreasures} team (estimate)", $"{treasures} tuyos + {teamTreasures} equipo (estimación)") : null)
                  + (every > 0 ? (teamTreasures > 0 ? "<br>" : "") + L.T($"1 tous les {every.ToString("N0", Fr)} coffres", $"1 every {every.ToString("N0", Fr)} chests", $"1 cada {every.ToString("N0", Fr)} cofres") : ""))}
              <div class="tile"><div class="v num sm">{Hm(playedSeconds, false)}</div><div class="l">{L.T("heures jouées", "hours played", "horas jugadas")}</div><div class="s">{L.T("temps en partie", "time in matches", "tiempo en partida")}</div></div>
            </section>
            """);

        // Last 30 days: one column per day, the value under each column.
        var today = DateTime.Now.Date;
        var days = Enumerable.Range(0, 30).Select(i => today.AddDays(i - 29)).ToList();
        List<double> PerDay(Func<MatchRecord, double> f) => days.Select(d => matches.Where(m => When(m).Date == d).Sum(f)).ToList();
        var chestsOwn = PerDay(m => m.ChestsOpened);
        var chestsTeam = PerDay(m => m.TeamChestsOpened);
        var chestsAll = chestsOwn.Select((v, i) => v + chestsTeam[i]).ToList();
        var treasuresOwn = PerDay(m => m.TreasureFinds.Count);
        var treasuresTeam = PerDay(m => m.TeamTreasureFinds.Count);
        var treasuresAll = treasuresOwn.Select((v, i) => v + treasuresTeam[i]).ToList();
        var hours = PerDay(m => (m.DurationSeconds ?? 0) / 3600.0);
        string DayName(int i) => days[i].ToString(L.T("ddd dd/MM", "ddd d MMM", "ddd dd/MM"), Fr);
        string ShortDay(DateTime d) => d.ToString(L.T("dd/MM", "d MMM", "dd/MM"), Fr);
        string Sep() => L.T(" : ", ": ", ": ");
        string Axis(string cls, string mid) => $"<div class=\"axis {cls}\"><span>{ShortDay(days[0])}</span>{(mid.Length > 0 ? $"<span>{mid}</span>" : "")}<span>{L.T("aujourd'hui", "today", "hoy")}</span></div>";
        string BestDay(List<double> v)
        {
            var mx = v.Max();
            return mx <= 0 ? "" : $"{L.T("meilleur jour", "best day", "mejor día")} : {ShortDay(days[v.IndexOf(mx)])} · {L.Count((int)mx, "coffre", "chest", "cofre")}";
        }
        string Legend(string cls, string second) => $"<div class=\"legend {cls}\"><span><i class=\"sw\" style=\"background:var(--gold)\"></i>{L.T("toi", "you", "tú")}</span><span><i class=\"sw\" style=\"background:var(--team)\"></i>{second}</span></div>";
        (string Bars, string Nums) Columns(string cls, List<double> a, List<double>? b, Func<double, string> shown, Func<int, string> tip, string barCss = "", bool highlight = false)
        {
            var tot = a.Select((v, i) => v + (b != null ? b[i] : 0)).ToList();
            var mx = Math.Max(tot.Max(), 1e-9);
            var bars = new StringBuilder($"<div class=\"bars {cls}\">");
            var nums = new StringBuilder($"<div class=\"nums {cls}\">");
            for (var i = 0; i < tot.Count; i++)
            {
                var h = (int)Math.Round(100.0 * tot[i] / mx);
                var t = H(tip(i));
                string inner = tot[i] <= 0 ? "<div class=\"bar zero\"></div>"
                    : b != null ? Inv($"<div class=\"stack\" style=\"height:{h}%\"><div class=\"bar sb\" style=\"flex:{b[i]:0.###}\"></div><div class=\"bar {barCss}\" style=\"flex:{a[i]:0.###}\"></div></div>")
                    : $"<div class=\"bar {barCss}{(highlight && tot[i] >= mx ? " top" : "")}\" style=\"height:{h}%\"></div>";
                bars.Append($"<div class=\"col\" aria-label=\"{t}\"><span class=\"tip\" style=\"bottom:calc({Math.Max(h, 3)}% + 6px)\">{t}</span>{inner}</div>");
                nums.Append($"<span>{(tot[i] > 0 ? shown(tot[i]) : "")}</span>");
            }
            return (bars.Append("</div>").ToString(), nums.Append("</div>").ToString());
        }
        string Whole(double v) => ((int)Math.Round(v)).ToString("N0", Fr);

        // Chests per day: yours, or yours + the teammates' (checkbox).
        var chestOwn = Columns("own", chestsOwn, null, Whole,
            i => $"{DayName(i)}{Sep()}{L.Count((int)chestsOwn[i], "coffre", "chest", "cofre")}", highlight: true);
        var chestTot = Columns("tot", chestsOwn, chestsTeam, Whole,
            i => $"{DayName(i)}{Sep()}{L.Count((int)chestsAll[i], "coffre", "chest", "cofre")} ({L.T("toi", "you", "tú")} {(int)chestsOwn[i]} + {L.T("équipe", "team", "equipo")} {(int)chestsTeam[i]})");
        sb.Append($"<section><div class=\"head\"><h2 class=\"pirate\">{L.T("Coffres par jour", "Chests per day", "Cofres por día")}</h2>"
            + (teamChests > 0 ? $"<label class=\"teamtoggle\"><input type=\"checkbox\" id=\"chestteam\"> {L.T("Inclure les coéquipiers", "Include teammates", "Incluir compañeros")}</label>" : "")
            + $"</div><div class=\"card chart\">{Legend("tot", L.T("coéquipiers", "teammates", "compañeros"))}{chestOwn.Bars}{chestTot.Bars}{chestOwn.Nums}{chestTot.Nums}"
            + Axis("own", BestDay(chestsOwn)) + Axis("tot", BestDay(chestsAll)) + "</div></section>");

        // Treasures per day, teammates included (their share is an estimate).
        if (treasuresAll.Sum() > 0)
        {
            var tr = Columns("tre", treasuresOwn, treasuresTeam, Whole,
                i => $"{DayName(i)}{Sep()}★ {(int)treasuresAll[i]} ({L.T("toi", "you", "tú")} {(int)treasuresOwn[i]} + {L.T("équipe", "team", "equipo")} {(int)treasuresTeam[i]})");
            sb.Append($"<section><h2 class=\"pirate\">{L.T("Trésors par jour, équipe comprise", "Treasures per day, with your team", "Tesoros por día, con tu equipo")}</h2><div class=\"card chart\">"
                + Legend("", L.T("coéquipiers (estimation)", "teammates (estimate)", "compañeros (estimación)")) + tr.Bars + tr.Nums + Axis("", L.T("toi + coéquipiers", "you + teammates", "tú + compañeros")) + "</div></section>");
        }

        // Hours played per day: bars, or a line (checkbox).
        if (hours.Sum() > 0)
        {
            var hr = Columns("hbars", hours, null, v => Hm(v * 3600, true), i => $"{DayName(i)}{Sep()}{Hm(hours[i] * 3600, false)}", "hr");
            var hmax = Math.Max(hours.Max(), 1e-9);
            var line = new StringBuilder("<svg class=\"hline\" viewBox=\"0 0 1000 150\" preserveAspectRatio=\"none\" aria-hidden=\"true\"><polyline fill=\"none\" stroke=\"#6c5ac7\" stroke-width=\"3\" stroke-linejoin=\"round\" vector-effect=\"non-scaling-stroke\" points=\"");
            var dots = new StringBuilder();
            for (var i = 0; i < hours.Count; i++)
            {
                var x = (i + 0.5) * 1000 / hours.Count;
                var y = 149 - hours[i] / hmax * 123;
                line.Append(Inv($"{x:0.#},{y:0.#} "));
                if (hours[i] > 0)
                {
                    dots.Append(Inv($"<line x1=\"{x:0.#}\" y1=\"{y:0.#}\" x2=\"{x:0.#}\" y2=\"{y + 0.01:0.##}\" stroke=\"#6c5ac7\" stroke-width=\"9\" stroke-linecap=\"round\" vector-effect=\"non-scaling-stroke\"><title>{H(DayName(i))}{Sep()}{Hm(hours[i] * 3600, false)}</title></line>"));
                }
            }
            line.Append("\"/>").Append(dots).Append("</svg>");
            sb.Append($"<section><div class=\"head\"><h2 class=\"pirate\">{L.T("Heures jouées par jour", "Hours played per day", "Horas jugadas por día")}</h2>"
                + $"<label class=\"teamtoggle\"><input type=\"checkbox\" id=\"hourline\"> {L.T("Courbe", "Line graph", "Gráfico de líneas")}</label></div><div class=\"card chart\">"
                + hr.Bars + line + hr.Nums + Axis("", L.T("temps en partie, parties de moins de 2 min non comptées", "time in matches, games under 2 min not counted", "tiempo en partida, partidas de menos de 2 min no contadas")) + "</div></section>");
        }

        sb.Append(ProbabilitySection());

        // By chest type (with the other interactions, not in the total) | where the player's treasures come from.
        var byKind = matches.SelectMany(m => m.Chests).GroupBy(c => ChestKinds.Canonical(c.Kind)).OrderByDescending(g => g.Count()).ToList();
        var kindMax = Math.Max(1, byKind.Count > 0 ? byKind.Max(g => g.Count()) : 1);
        sb.Append($"<div class=\"two\"><section><h2 class=\"pirate\">{L.T("Par type de coffre", "By chest type", "Por tipo de cofre")}</h2><div class=\"card\"><table>");
        foreach (var g in byKind)
        {
            var color = KindColor.GetValueOrDefault(g.Key, "#9a8f7a");
            var w = Math.Max(2, (int)Math.Round(100.0 * g.Count() / kindMax));
            sb.Append($"<tr><td><span class=\"sw\" style=\"background:{color}\"></span>{H(L.Name(g.Key))}</td><td style=\"width:150px\"><span class=\"meter\"><i style=\"width:{w}%;background:{color}\"></i></span></td><td class=\"n num big\">{g.Count().ToString("N0", Fr)}</td></tr>");
        }
        var destroyedTotal = matches.Sum(m => m.Chests.Count(c => c.Destroyed));
        var notes = new List<string>();
        if (destroyedTotal > 0)
        {
            notes.Add(L.T($"dont {destroyedTotal.ToString("N0", Fr)} détruits (cassés au lieu d'être ouverts)", $"of which {destroyedTotal.ToString("N0", Fr)} destroyed (broken instead of opened)", $"de ellos, {destroyedTotal.ToString("N0", Fr)} destruidos (rotos en lugar de abiertos)"));
        }
        sb.Append("</table>");
        if (notes.Count > 0)
        {
            sb.Append($"<div class=\"items\" style=\"padding:10px 0 8px\">{string.Join(" · ", notes)}</div>");
        }
        sb.Append("</div></section>");

        var treasureRows = matches.SelectMany(m => m.TreasureFinds.Select(f => (m, f, Team: false))).ToList();
        var teamRows = matches.SelectMany(m => m.TeamTreasureFinds.Select(f => (m, f, Team: true))).ToList();
        sb.Append($"<section><h2 class=\"pirate\">{L.T("D'où viennent tes trésors", "Where your treasures come from", "De dónde vienen tus tesoros")}</h2><div class=\"card\"><table>");
        if (treasureRows.Count == 0)
        {
            sb.Append($"<tr><td class=\"items\">{L.T("Pas encore de trésor.", "No treasure yet.", "Todavía ningún tesoro.")}</td></tr>");
        }
        foreach (var g in treasureRows.GroupBy(r => r.f.SourceLabel).OrderByDescending(g => g.Count()))
        {
            var isChest = g.First().f.SourceIsChest;
            sb.Append(isChest
                ? $"<tr><td>{H(g.Key)}</td><td class=\"n num big\">{g.Count()}</td></tr>"
                : $"<tr class=\"unusual\"><td class=\"warn\">{H(g.Key)} <span style=\"font-size:12px\">· {L.T("fiabilité faible", "low reliability", "fiabilidad baja")}</span></td><td class=\"n num big warn\">{g.Count()}</td></tr>");
        }
        sb.Append("</table></div>");
        var unusual = treasureRows.Where(r => !r.f.SourceIsChest).ToList();
        if (unusual.Count > 0)
        {
            var count = L.Count(unusual.Count, "trésor obtenu", "trésors obtenus", "treasure", "treasures", "tesoro obtenido", "tesoros obtenidos");
            sb.Append($"<div class=\"flag\">{FlagIcon}<span>" + L.T(
                $"<b>{count} autrement qu'avec un coffre.</b> Fiabilité faible : il s'agit très probablement d'une erreur dans la façon dont les replays sont analysés.",
                $"<b>{count} obtained other than from a chest.</b> Low reliability: most likely a mistake in the way the replays are analyzed.",
                $"<b>{count} de otra forma que con un cofre.</b> Fiabilidad baja: muy probablemente un error en la forma de analizar las repeticiones.") + "</span></div>");
        }
        sb.Append("</section></div>");

        // Treasures: the player's own, then (hidden unless asked) those found by teammates, shared with the whole team.
        if (treasureRows.Count > 0 || teamRows.Count > 0)
        {
            sb.Append($"<section><div class=\"head\"><h2 class=\"pirate\">{L.T("Trésors trouvés", "Treasures found", "Tesoros encontrados")} · <span class=\"num\">{treasures}</span></h2>");
            if (teamRows.Count > 0)
            {
                sb.Append($"<label class=\"teamtoggle\"><input type=\"checkbox\" id=\"showteam\"> {L.T($"Afficher aussi l'équipe (+{teamRows.Count}, estimation)", $"Also show the team (+{teamRows.Count}, estimate)", $"Mostrar también el equipo (+{teamRows.Count}, estimación)")}</label>");
            }
            sb.Append($"</div><div class=\"card\"><table><tr><th>{L.T("Partie", "Match", "Partida")}</th><th>{L.T("Trésor", "Treasure", "Tesoro")}</th><th>{L.T("Obtenu à", "Obtained at", "Momento")}</th><th>{L.T("Source", "Source", "Origen")}</th><th>{L.T("Par", "By", "Por")}</th></tr>");
            foreach (var (m, f, team) in treasureRows.Concat(teamRows).OrderByDescending(r => When(r.m)).ThenBy(r => r.f.ObtainedAtSeconds ?? 0))
            {
                var when = f.ObtainedAtSeconds is float t ? (f.TimeExact ? Pretty.Clock(t) : $"{L.T("vers", "around", "hacia los")} {Pretty.Clock(t)}") : "?";
                string source;
                if (team && string.IsNullOrEmpty(f.Teammate))
                {
                    source = $"<span class=\"items\">{L.T("coffre hors de ta vue", "chest out of your sight", "cofre fuera de tu vista")}</span>";
                }
                else if (!f.SourceIsChest)
                {
                    source = $"<span class=\"warn\">{H(f.SourceLabel)} · {L.T("fiabilité faible", "low reliability", "fiabilidad baja")}</span>";
                }
                else
                {
                    source = H(f.SourceLabel) + (f.SourceCertain ? "" : $" <span class=\"items\">({Pretty.Likely})</span>");
                }
                var who = !team ? L.T("toi", "you", "tú")
                    : TeamIcon + (string.IsNullOrEmpty(f.Teammate) ? L.T("un coéquipier", "a teammate", "un compañero") : H(f.Teammate));
                var cls = team ? "team" : f.SourceIsChest ? "" : "unusual";
                var stars = f.FromGoldenChest ? "<span class=\"gold\">★★</span>" : "<span class=\"star\">★</span>";
                sb.Append($"<tr{(cls.Length > 0 ? $" class=\"{cls}\"" : "")}><td class=\"nw num items\">{L.DateTimeShort(When(m))}</td><td>{stars} <b>{H(f.DisplayName)}</b></td><td class=\"nw num\">{when}</td><td>{source}</td><td class=\"by\">{who}</td></tr>");
            }
            sb.Append("</table></div></section>");
        }

        // Latest matches.
        sb.Append($"<section><h2 class=\"pirate\">{L.T("Dernières parties", "Latest matches", "Últimas partidas")}</h2><div class=\"card\"><table><tr><th>{L.T("Date", "Date", "Fecha")}</th><th>{L.T("Mode", "Mode", "Modo")}</th><th>{L.T("Place", "Placement", "Puesto")}</th><th style=\"text-align:right\">{L.T("Coffres", "Chests", "Cofres")}</th><th>{L.T("Trésor", "Treasure", "Tesoro")}</th></tr>");
        foreach (var m in matches.Take(100))
        {
            var treasure = string.Join("<br>", m.TreasureFinds.Select(f => $"<span class=\"{(f.FromGoldenChest ? "gold" : "star")}\">{(f.FromGoldenChest ? "★★" : "★")} {H(f.DisplayName)}</span>"))
                + string.Concat(m.TeamTreasureFinds.Select((f, i) => $"<span class=\"team\">{(i == 0 && m.TreasureFinds.Count == 0 ? "" : "<br>")}{TeamIcon} ★ {H(f.DisplayName)}</span>"));
            var win = m.Placement == 1;
            sb.Append($"<tr><td class=\"nw num items\">{L.DateTimeShort(When(m))}</td><td>{H(Pretty.Playlist(m.Playlist))}</td><td class=\"nw num\"{(win ? " style=\"color:#b9821c;font-weight:700\"" : "")}>{H(Pretty.Placement(m))}</td><td class=\"n num big\">{m.ChestsOpened}{(m.TeamChestsOpened > 0 ? $"<span class=\"team\" style=\"font-size:12px;font-weight:400\"> +{m.TeamChestsOpened}</span>" : "")}</td><td class=\"items\">{treasure}</td></tr>");
        }
        sb.Append("</table></div></section>");

        var script = "<script>" + JsCore + "var T=" + totalChests.ToString(CultureInfo.InvariantCulture) + ";" + JsProb + (progress == null ? JsReload : "") + "</script>";
        sb.Append(L.T(
            "<footer>Compte les coffres que tu as ouverts ou détruits toi-même, d'après tes replays (« rediffusions » dans Fortnite), analysés après chaque partie. Les caisses de munitions et de nourriture ne sont pas comptées. ★★ = coffre doré de la chasse au trésor, détecté de façon certaine. ★ = trésor reçu grâce à un de tes coffres (moment exact), nommé quand l'objet est identifiable. L'icône équipe marque un trésor trouvé par un coéquipier (estimation : déduit des trésors que tu reçois sans avoir ouvert de coffre, car la chasse au trésor les partage avec toute l'équipe) ; il n'entre pas dans tes totaux.</footer>",
            "<footer>Counts the chests you opened or destroyed yourself, from your replays, analyzed after each match. Ammo boxes and food boxes are not counted. ★★ = golden treasure hunt chest, detected for sure. ★ = treasure received from one of your own chests (exact moment), named when the item can be identified. The team icon marks a treasure found by a teammate (estimate: deduced from the treasures you receive without opening a chest, as the treasure hunt shares them with the whole team); it is not in your totals.</footer>",
            "<footer>Cuenta los cofres que has abierto o destruido tú mismo, a partir de tus repeticiones, analizadas después de cada partida. Las cajas de munición y de comida no se cuentan. ★★ = cofre dorado de la búsqueda del tesoro, detectado con seguridad. ★ = tesoro recibido de uno de tus cofres (momento exacto), con nombre cuando el objeto se puede identificar. El icono de equipo marca un tesoro encontrado por un compañero (estimación: deducido de los tesoros que recibes sin abrir un cofre, ya que la búsqueda del tesoro los comparte con todo el equipo); no cuenta en tus totales.</footer>"));
        sb.Append(script).Append("</main></body></html>");
        return sb.ToString();
    }

    private const double ShopOdds = 137500;

    private static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

    private static string Hm(double seconds, bool compact)
    {
        var t = (int)Math.Round(seconds / 60.0);
        var h = t / 60;
        var m = t % 60;
        return compact ? (h > 0 ? $"{h}h{m:00}" : $"{m}m") : $"{h} h {m:00} min";
    }

    private const string JsCore = """(function(){function tog(id,cls,key){var b=document.getElementById(id);if(!b)return;var on=false;try{on=localStorage.getItem(key)==='1'}catch(e){}function set(v){document.body.classList.toggle(cls,v);b.checked=v}set(on);b.addEventListener('change',function(){set(b.checked);try{localStorage.setItem(key,b.checked?'1':'0')}catch(e){}})}tog('showteam','showteam','chestcounter-showteam');tog('chestteam','chestteam','chestcounter-chestteam');tog('hourline','hourline','chestcounter-hourline');})();""";

    private const string JsProb = """(function(){var i=document.getElementById('startchests');if(!i)return;var R=137500,k='chestcounter-startchests',lang=document.documentElement.lang||undefined,nf=new Intl.NumberFormat(lang),dot=document.getElementById('pdot'),gd=document.getElementById('pguide'),lab=document.getElementById('plabel'),sum=document.getElementById('psum'),tpl=sum.getAttribute('data-t');function pct(q){var d=q<0.01?2:1;return new Intl.NumberFormat(lang,{minimumFractionDigits:d,maximumFractionDigits:d}).format(q*100)+'%'}var s0=0;try{s0=parseInt(localStorage.getItem(k)||'0',10)||0}catch(e){}i.value=s0;function upd(){var s=parseInt(i.value,10);if(!(s>=0))s=0;var n=s+T,q=1-Math.pow(1-1/R,n),l=Math.min(6,Math.max(2,Math.log(Math.max(n,1))/Math.LN10)),x=40+135*(l-2),y=170-160*q;dot.setAttribute('cx',x);dot.setAttribute('cy',y);gd.setAttribute('x1',x);gd.setAttribute('x2',x);gd.setAttribute('y1',y);gd.setAttribute('y2',Math.max(y-24,16));lab.setAttribute('x',x>520?x-8:x+8);lab.setAttribute('text-anchor',x>520?'end':'start');lab.setAttribute('y',Math.max(y-28,16));lab.textContent=pct(q);sum.textContent=tpl.replace('{s}',nf.format(s)).replace('{t}',nf.format(T)).replace('{n}',nf.format(n)).replace('{p}',pct(q));try{localStorage.setItem(k,String(s))}catch(e){}}i.addEventListener('input',upd);upd();})();""";

    // The page reloads itself to stay up to date, except while the starting amount is being typed.
    private const string JsReload = """setInterval(function(){var a=document.activeElement;if(a&&a.id==='startchests')return;location.reload()},15000);""";

    /// <summary>Chance of at least one Pirate Shop Access (1 in 137,500 per chest, Epic Games) by number of chests opened, log scale.</summary>
    private static string ProbabilitySection()
    {
        var pts = new StringBuilder();
        for (var l = 2.0; l <= 6.0001; l += 0.25)
        {
            var p = 1 - Math.Pow(1 - 1 / ShopOdds, Math.Pow(10, l));
            pts.Append(Inv($"{40 + 135 * (l - 2):0.#},{170 - 160 * p:0.#} "));
        }
        var n50 = ShopOdds * Math.Log(2);
        var x50 = 40 + 135 * (Math.Log10(n50) - 2);
        var grid = new StringBuilder();
        var ticks = new StringBuilder();
        for (var k = 2; k <= 6; k++)
        {
            var tx = 40 + 135 * (k - 2);
            if (k is > 2 and < 6)
            {
                grid.Append(Inv($"<line x1=\"{tx}\" y1=\"10\" x2=\"{tx}\" y2=\"170\" stroke-dasharray=\"2 5\"/>"));
            }
            ticks.Append(Inv($"<text x=\"{tx}\" y=\"188\" text-anchor=\"middle\">")).Append(Math.Pow(10, k).ToString("N0", Fr)).Append("</text>");
        }
        var round50 = ((int)Math.Round(n50 / 1000.0) * 1000).ToString("N0", Fr);
        var title = L.T("Accès à la boutique pirate : probabilité", "Pirate Shop Access odds", "Probabilidad de Acceso a la tienda pirata");
        var template = L.T(
            "{s} avant le compteur + {t} comptés = {n} coffres · chance d'avoir au moins un accès : {p}",
            "{s} before the counter + {t} tracked = {n} chests · chance of at least one Pirate Shop Access: {p}",
            "{s} antes del contador + {t} contados = {n} cofres · probabilidad de al menos un acceso: {p}");
        return $"""
            <section class="prob"><h2 class="pirate">{title}</h2><div class="card chart">
            <div class="row"><label for="startchests">{L.T("Quantité de départ (coffres ouverts avant d'utiliser le compteur)", "Starting amount (chests opened before you started using the counter)", "Cantidad inicial (cofres abiertos antes de usar el contador)")}</label><input id="startchests" type="number" min="0" step="1" value="0"></div>
            <div class="items" id="psum" data-t="{H(template)}" style="padding:0 0 10px"></div>
            <svg viewBox="0 0 620 214" role="img" aria-label="{H(title)}">
            <g stroke="#d8c59c" stroke-width="1"><line x1="40" y1="170" x2="580" y2="170"/><line x1="40" y1="90" x2="580" y2="90" stroke-dasharray="4 4"/><line x1="40" y1="10" x2="580" y2="10" stroke-dasharray="4 4"/>{grid}</g>
            <g font-size="12" fill="#6b5a44"><text x="6" y="174">0%</text><text x="6" y="94">50%</text><text x="2" y="14">100%</text>{ticks}
            <text x="310" y="207" text-anchor="middle">{L.T("coffres ouverts (échelle logarithmique)", "chests opened (log scale)", "cofres abiertos (escala logarítmica)")}</text>
            <text x="{Inv($"{x50 - 10:0.#}")}" y="80" text-anchor="end">{L.T($"50 % à environ {round50} coffres", $"50% at about {round50} chests", $"50 % con unos {round50} cofres")}</text></g>
            <polyline fill="none" stroke="#7a4a1e" stroke-width="3" stroke-linejoin="round" points="{pts}"/>
            <circle cx="{Inv($"{x50:0.#}")}" cy="90" r="4" fill="#7a4a1e"/>
            <line id="pguide" x1="40" y1="170" x2="40" y2="146" stroke="#a3361f" stroke-width="1.5"/><circle id="pdot" cx="40" cy="170" r="5" fill="#c8412f"/>
            <text id="plabel" x="48" y="142" font-size="15" font-weight="600" fill="#a3361f"></text>
            </svg>
            <div class="items" style="padding:6px 0 10px">{L.T("Taux officiel : 1 sur 137 500 par coffre en partie normale (Epic Games). Les Heures du trésor le font monter, jusqu'à ×2 (1 sur 68 750).", "Official rate: 1 in 137,500 per chest in normal matches (Epic Games). Treasure Hours raise it, up to 2× so far (1 in 68,750).", "Tasa oficial: 1 entre 137 500 por cofre en partidas normales (Epic Games). Las Horas del tesoro la suben, hasta ×2 (1 entre 68 750).")}</div>
            </div></section>
            """;
    }

    private static string LoadingCard(ScanProgress? p)
    {
        if (p == null)
        {
            return "";
        }
        var pct = p.Total > 0 ? (int)Math.Round(100.0 * p.Done / p.Total) : 0;
        return L.T(
            $"""<div class="loading"><div class="spin"></div><div class="grow"><b>Analyse de tes replays… {p.Done} / {p.Total}</b><div class="pbar"><i style="width:{pct}%"></i></div><span class="muted">Cette page se met à jour toute seule : les chiffres ci-dessous augmentent au fil de la lecture des replays.</span></div></div>""",
            $"""<div class="loading"><div class="spin"></div><div class="grow"><b>Analyzing your replays… {p.Done} / {p.Total}</b><div class="pbar"><i style="width:{pct}%"></i></div><span class="muted">This page updates by itself: the numbers below grow as the replays are read.</span></div></div>""",
            $"""<div class="loading"><div class="spin"></div><div class="grow"><b>Analizando tus repeticiones… {p.Done} / {p.Total}</b><div class="pbar"><i style="width:{pct}%"></i></div><span class="muted">Esta página se actualiza sola: las cifras de abajo aumentan a medida que se leen las repeticiones.</span></div></div>""");
    }

    private static string ProfilesLine(IReadOnlyList<Profile> profiles, string player)
    {
        var others = profiles.Where(p => p.Name != player).Select(p => H(p.Name)).ToList();
        if (others.Count == 0)
        {
            return "";
        }
        return $"""<div class="profiles muted">{L.T("Autres profils sur ce PC : ", "Other profiles on this PC: ", "Otros perfiles en este PC: ")}{string.Join(", ", others)} · {L.T("« Changer de profil » dans le compteur", "\"Switch profile\" in the counter", "«Cambiar de perfil» en el contador")}</div>""";
    }

    /// <summary>Rewrites only the OBS overlay page (called on every live update).</summary>

    public static void WriteOverlay(Store store, OverlayView view)
    {
        try
        {
            WriteAtomic(store.OverlayPath, Overlay(view));
        }
        catch
        {
            // the page is a convenience: never stop the tracker for it
        }
    }

    public static string Overlay(OverlayView v)
    {
        var rows = new StringBuilder();
        foreach (var r in v.Rows)
        {
            rows.Append($"<div class=\"row{(r.Treasure ? " t" : "")}\"><span>{H(r.Label)}</span><b>{H(r.Value)}{(r.Detail != null ? $" <small>{H(r.Detail)}</small>" : "")}</b></div>");
        }
        return $$"""
            <!doctype html><html lang="{{L.Code}}"><head><meta charset="utf-8"><meta http-equiv="refresh" content="10">
            <title>{{L.T("Overlay coffres", "Chest overlay", "Overlay de cofres")}}</title>
            <style>
            html,body{margin:0;background:transparent;color:#e2e2e8;font:500 16px/1.3 "Segoe UI",system-ui,sans-serif}
            .box{display:inline-block;min-width:260px;padding:14px 18px 12px;border-radius:16px;background:rgba(18,18,22,.86);border:1px solid #3a3a42;text-shadow:0 1px 3px rgba(0,0,0,.6)}
            .head{display:flex;justify-content:space-between;gap:16px;font:700 12px "Segoe UI",sans-serif;letter-spacing:.08em;color:#9696a2}
            .live{color:#60d680}
            .counters{display:flex;align-items:stretch;gap:18px;margin:4px 0 10px}
            .counters>div{display:flex;flex-direction:column}
            .sep{width:1px;background:#2c2c34;margin:8px 0 2px}
            .big{font-size:40px;font-weight:800;line-height:1.1;font-variant-numeric:tabular-nums}
            .total{color:#f2b84b}.cur{color:#6ee096}.none{color:#50505c}
            .cap{font-size:11px;font-weight:600;color:#82828e}
            .rows{border-top:1px solid #2c2c34;padding-top:8px;display:grid;grid-template-columns:auto 1fr;gap:4px 12px}
            .row{display:contents}.row span{font-size:13px;font-weight:600;color:#82828e}.row b{font-weight:500}
            .row.t b{color:#c4a0ff}.row small{font-size:11px;font-weight:600;color:#82828e}
            </style></head><body><div class="box">
            <div class="head"><span>{{L.T("COFFRES", "CHESTS", "COFRES")}}</span>{{(v.Badge != null ? $"<span class=\"live\">{H(v.Badge)}</span>" : "")}}</div>
            <div class="counters">
              <div><span class="big total">{{H(v.Total)}}</span><span class="cap">total</span></div>
              <div class="sep"></div>
              <div><span class="big {{(v.Current == null ? "none" : v.CurrentLive ? "cur" : "")}}">{{H(v.Current ?? "—")}}</span><span class="cap">{{H(v.CurrentCaption)}}</span></div>
            </div>
            <div class="rows">{{rows}}</div>
            </div></body></html>
            """;
    }

    public static string Csv(TrackerData data)
    {
        var sb = new StringBuilder();
        sb.AppendLine(L.T("date;mode;joueur;place;joueurs;coffres;caisses_munitions;tresors;fichier", "date;mode;player;placement;players;chests;ammo_boxes;treasures;file", "fecha;modo;jugador;puesto;jugadores;cofres;cajas_municion;tesoros;archivo"));

        foreach (var m in data.Matches.OrderBy(When))
        {
            var t = string.Join(" + ", m.TreasureFinds.Select(f => f.DisplayName));
            sb.AppendLine(string.Join(";",
                When(m).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                Pretty.Playlist(m.Playlist), m.PlayerName, m.Placement, m.TotalPlayers,
                m.ChestsOpened, m.AmmoBoxesOpened, t, m.ReplayFile));
        }
        return sb.ToString();
    }
}
