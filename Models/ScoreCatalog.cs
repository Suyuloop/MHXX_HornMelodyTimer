using System.Collections.ObjectModel;

namespace HornMelodyTimer.Models;

public sealed record ScoreOption(string Id, string Name, Func<ObservableCollection<SongEffect>> CreateSongs)
{
    public IReadOnlyList<char> NoteCodes => ScoreCatalog.GetNoteCodes(Name);
}

public static class ScoreCatalog
{
    public static IReadOnlyList<ScoreOption> All { get; } =
    [
        new("red-yellow", "紫・紅・黃", CreateRedYellow),
        new("red-orange", "紫・紅・橙", CreateRedOrange),
        new("blue-orange", "紫・青・橙", CreateBlueOrange),
        new("red-blue", "紫・紅・青", CreateRedBlue),
        new("red-green", "紫・紅・緑", CreateRedGreen),
        new("red-sky", "紫・紅・空", CreateRedSky),
        new("blue-green", "紫・青・緑", CreateBlueGreen),
        new("blue-sky", "紫・青・空", CreateBlueSky),
        new("blue-yellow", "紫・青・黃", CreateBlueYellow),
        new("sky-orange", "紫・空・橙", CreateSkyOrange),
        new("green-sky", "紫・緑・空", CreateGreenSky),
        new("green-yellow", "紫・緑・黃", CreateGreenYellow),
        new("sky-yellow", "紫・空・黃", CreateSkyYellow),
        new("green-orange", "紫・緑・橙", CreateGreenOrange),
        new("orange-yellow", "紫・橙・黃", CreateOrangeYellow),
        new("white-red-blue", "白・紅・青", () => CreateWhiteScore("RB")),
        new("white-red-green", "白・紅・緑", () => CreateWhiteScore("RG")),
        new("white-red-sky", "白・紅・空", () => CreateWhiteScore("RS")),
        new("white-red-yellow", "白・紅・黃", () => CreateWhiteScore("RY")),
        new("white-blue-green", "白・青・緑", () => CreateWhiteScore("BG")),
        new("white-blue-sky", "白・青・空", () => CreateWhiteScore("BS")),
        new("white-blue-yellow", "白・青・黃", () => CreateWhiteScore("BY")),
        new("white-green-sky", "白・緑・空", () => CreateWhiteScore("GS")),
        new("white-green-yellow", "白・緑・黃", () => CreateWhiteScore("GY")),
        new("white-sky-yellow", "白・空・黃", () => CreateWhiteScore("SY"))
    ];

    public static ObservableCollection<SongEffect> CreateSongs(string scoreId)
    {
        var score = All.FirstOrDefault(option => option.Id == scoreId) ?? All[0];
        var songs = score.CreateSongs();
        var selfPattern = score.Id.StartsWith("white-", StringComparison.Ordinal) ? "WW" : "PP";
        if (songs.All(song => song.NotePattern != selfPattern))
            songs.Insert(0, Melody("自分強化", selfPattern, 180, 240, 90, 120));
        return songs;
    }

    public static string GetNoteColors(string scoreId)
    {
        var score = All.FirstOrDefault(option => option.Id == scoreId) ?? All[0];
        return string.Concat(score.Name.Select(NoteCode).Where(note => note is not null));
    }

    private static ObservableCollection<SongEffect> CreateRedYellow() =>
    [
        Melody("自分強化", "PP", 180, 240, 90, 120),
        Melody("攻擊力強化（小）", "PRY", 120, 150, 90, 120, ["YPR", "RYP"]),
        Melody("火屬性防禦強化（大）", "YRP", 120, 150, 120, 150),
        Melody("高周波", "YYY", 0, 0, 0, 0)
    ];

    private static ObservableCollection<SongEffect> CreateRedOrange() =>
    [
        Melody("自分強化", "PP", 180, 240, 90, 120),
        Melody("攻擊力強化（大）", "POOR", 90, 120, 60, 90),
        Melody("怯み無効", "RORP", 45, 60, 45, 45),
        Melody("全旋律延長", "ORO", 0, 0, 30, 40, global: true)
    ];

    private static ObservableCollection<SongEffect> CreateBlueOrange() =>
    [
        Melody("攻擊力／防禦力強化（小）", "PBOO", 120, 150, 90, 120, ["OBPB"]),
        Melody("防禦力強化（大）", "PBBP", 90, 120, 60, 90),
        Melody("耐力消耗無效（大）", "POBO", 120, 150, 90, 120),
        Melody("風壓無效", "BBO", 180, 240, 180, 240)
    ];

    private static ObservableCollection<SongEffect> CreateRedBlue() =>
    [
        Melody("攻擊力強化（大）", "PRBP", 90, 120, 60, 90),
        Melody("防禦力強化（大）", "PBBP", 90, 120, 60, 90),
        Melody("體力增加（大）", "RBRP", 180, 240, 180, 240),
        Melody("風壓輕減", "BBR", 180, 240, 120, 180)
    ];

    private static ObservableCollection<SongEffect> CreateRedGreen() =>
    [
        Melody("攻擊力強化（大）", "PRGP", 90, 120, 60, 90),
        Melody("體力回復（小）", "PGP", 0, 0, 0, 0),
        Melody("回復速度強化（大）", "GGRP", 120, 150, 120, 150)
    ];

    private static ObservableCollection<SongEffect> CreateRedSky() =>
    [
        Melody("攻擊力強化（大）", "PRSP", 90, 120, 60, 90),
        Melody("暑熱無效", "RRS", 240, 300, 240, 300),
        Melody("寒冷無效", "SRS", 240, 300, 240, 300),
        Melody("聽覺保護（小）", "SSRP", 180, 210, 180, 210),
        Melody("耐だるま狀態", "SSS", 180, 240, 180, 240)
    ];

    private static ObservableCollection<SongEffect> CreateBlueGreen() =>
    [
        Melody("耐力消耗無效（大）", "PGBG", 120, 150, 90, 120),
        Melody("風壓輕減", "BBG", 180, 240, 120, 180),
        Melody("體力回復（中）＆解毒", "GBPB", 0, 0, 0, 0),
        Melody("防禦力強化（大）", "PBBP", 90, 120, 60, 90)
    ];

    private static ObservableCollection<SongEffect> CreateBlueSky() =>
    [
        Melody("千里眼", "PBS", 30, 60, 30, 60),
        Melody("耐力消耗無效（大）", "PSBS", 120, 150, 90, 120),
        Melody("風壓輕減", "BBS", 180, 240, 120, 180),
        Melody("氣絕無效", "SBP", 180, 240, 180, 240),
        Melody("耐だるま狀態", "SSS", 180, 240, 180, 240),
        Melody("防禦力強化（大）", "PBBP", 90, 120, 60, 90)
    ];

    private static ObservableCollection<SongEffect> CreateBlueYellow() =>
    [
        Melody("耐力消耗無效（大）", "PYB", 120, 150, 90, 120),
        Melody("風壓無效", "BBYP", 180, 240, 180, 240),
        Melody("冰屬性防禦強化（大）", "YBP", 120, 150, 120, 150),
        Melody("高周波", "YYY", 0, 0, 0, 0)
    ];

    private static ObservableCollection<SongEffect> CreateGreenSky() =>
    [
        Melody("體力回復（小）", "PGP", 0, 0, 0, 0),
        Melody("體力回復（大）", "GGPS", 0, 0, 0, 0),
        Melody("聽覺保護（大）", "PSSG", 180, 210, 180, 210),
        Melody("耐だるま狀態", "SSS", 180, 240, 180, 240)
    ];

    private static ObservableCollection<SongEffect> CreateSkyOrange() =>
    [
        Melody("精靈王的加護", "POPS", 120, 150, 120, 150),
        Melody("全狀態異常無效", "SPOO", 90, 120, 60, 90),
        Melody("耐だるま狀態", "SSS", 180, 240, 180, 240),
        Melody("狀態異常攻擊強化", "SOOP", 90, 120, 60, 90)
    ];

    private static ObservableCollection<SongEffect> CreateGreenYellow() =>
    [
        Melody("體力回復（小）", "PGP", 0, 0, 0, 0),
        Melody("屬性攻擊力強化", "PGYG", 120, 150, 90, 120),
        Melody("精靈王的加護", "GYP", 120, 150, 120, 150),
        Melody("回復速度強化（小）", "GGY", 120, 150, 120, 150),
        Melody("雷屬性防禦強化（大）", "YGP", 120, 150, 120, 150),
        Melody("高周波", "YYY", 0, 0, 0, 0)
    ];

    private static ObservableCollection<SongEffect> CreateSkyYellow() =>
    [
        Melody("龍屬性防禦強化（大）", "PYS", 120, 150, 120, 150),
        Melody("水屬性防禦強化（大）", "YSP", 120, 150, 120, 150),
        Melody("屬性攻擊力強化", "YSYS", 120, 150, 90, 120),
        Melody("耐だるま狀態", "SSS", 180, 240, 180, 240),
        Melody("高周波", "YYY", 0, 0, 0, 0)
    ];

    private static ObservableCollection<SongEffect> CreateGreenOrange() =>
    [
        Melody("體力回復（小）", "PGP", 0, 0, 0, 0),
        Melody("會心率提升＆體力回復（小）", "GOPO", 120, 150, 90, 120),
        Melody("聽覺保護（大）", "OOGP", 180, 210, 180, 210)
    ];

    private static ObservableCollection<SongEffect> CreateOrangeYellow() =>
    [
        Melody("屬性攻擊力強化", "POYO", 120, 150, 90, 120),
        Melody("全屬性耐性強化", "YOP", 90, 120, 60, 90),
        Melody("屬性異常無效", "OYYO", 120, 150, 120, 150),
        Melody("高周波", "YYY", 0, 0, 0, 0)
    ];

    private static ObservableCollection<SongEffect> CreateWhiteScore(string secondaryColors)
    {
        var availableNotes = $"W{secondaryColors}";
        return new ObservableCollection<SongEffect>(CreateWhiteMelodies()
            .Where(song => song.NotePattern.All(availableNotes.Contains)));
    }

    private static List<SongEffect> CreateWhiteMelodies() =>
    [
        Melody("自分強化", "WW", 180, 240, 90, 120),
        Melody("攻擊力強化（小）", "WRR", 120, 150, 90, 120),
        Melody("體力增加（小）", "RBW", 180, 240, 180, 240),
        Melody("體力回復（小）", "WGW", 0, 0, 0, 0),
        Melody("回復速度強化（小）", "GGRW", 120, 150, 120, 150),
        Melody("暑熱無效", "RRS", 240, 300, 240, 300),
        Melody("寒冷無效", "SRS", 240, 300, 240, 300),
        Melody("だるま無效", "SSS", 180, 240, 180, 240),
        Melody("聽覺保護（小）", "SSRW", 180, 210, 180, 210),
        Melody("火屬性防禦強化（小）＆火異常無效（小）", "YRW", 120, 150, 120, 150, displayRowsPerActivation: 1),
        Melody("高周波", "YYY", 0, 0, 0, 0),
        Melody("防禦力強化（小）", "WBB", 120, 150, 90, 120),
        Melody("風壓輕減", "BBR", 180, 240, 120, 180),
        Melody("耐力消耗無效（小）", "WGB", 90, 120, 60, 90),
        Melody("風壓輕減", "GBBG", 180, 240, 120, 180),
        Melody("體力回復（小）＆解毒", "GBWB", 0, 0, 0, 0),
        Melody("千里眼", "WBS", 30, 60, 30, 60),
        Melody("耐力消耗無效（小）", "WSBS", 90, 120, 60, 90),
        Melody("風壓輕減", "SBBS", 180, 240, 120, 180),
        Melody("耐力消耗無效（小）", "WYB", 90, 120, 60, 90),
        Melody("冰屬性防禦強化（小）＆冰異常無效（小）", "YBW", 120, 150, 120, 150, displayRowsPerActivation: 1),
        Melody("體力回復（中）＆消臭", "GWSG", 0, 0, 0, 0),
        Melody("回復速度強化（小）", "GGY", 120, 150, 120, 150),
        Melody("雷屬性防禦強化（小）＆雷異常無效（小）", "YGW", 120, 150, 120, 150, displayRowsPerActivation: 1),
        Melody("龍屬性防禦強化（小）", "WYS", 120, 150, 120, 150),
        Melody("水屬性防禦強化（小）＆水異常無效（小）", "YSW", 120, 150, 120, 150, displayRowsPerActivation: 1),
        Melody("屬性攻擊力強化", "YSYS", 120, 150, 90, 120),
        Melody("耐震", "SSY", 180, 240, 180, 240),
        Melody("麻痺無效", "SYW", 180, 240, 180, 240)
    ];

    private static SongEffect Melody(
        string name,
        string pattern,
        int duration,
        int maestroDuration,
        int extension,
        int maestroExtension,
        string[]? aliases = null,
        bool global = false,
        int? displayRowsPerActivation = null) =>
        new()
        {
            Name = name,
            Notes = string.Join(" ", pattern.Select(NoteName)),
            NotePattern = pattern,
            AcceptedPatterns = aliases is null ? [pattern] : [pattern, .. aliases],
            DurationSeconds = duration,
            MaestroDurationSeconds = maestroDuration,
            ExtensionSeconds = extension,
            MaestroExtensionSeconds = maestroExtension,
            IsGlobalExtension = global,
            DisplayRowsPerActivation = displayRowsPerActivation ??
                (name.Contains('＆') || name.Contains('&') || name.Contains('／') ? 2 : 1)
        };

    private static string NoteName(char note) => note switch
    {
        'P' => "紫",
        'W' => "白",
        'R' => "紅",
        'B' => "青",
        'G' => "緑",
        'S' => "空",
        'Y' => "黃",
        'O' => "橙",
        _ => throw new ArgumentOutOfRangeException(nameof(note), note, "Unknown melody note.")
    };

    public static IReadOnlyList<char> GetNoteCodes(string value) =>
        value.Select(NoteCode)
            .Where(note => note.HasValue)
            .Select(note => note!.Value)
            .ToArray();

    private static char? NoteCode(char note) => note switch
    {
        '紫' => 'P',
        '白' => 'W',
        '紅' => 'R',
        '青' => 'B',
        '緑' => 'G',
        '空' => 'S',
        '黃' => 'Y',
        '橙' => 'O',
        _ => null
    };
}
