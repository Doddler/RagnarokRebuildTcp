namespace RoBotClient.Bot.Behavior;

/// <summary>Attack-side elements as the server's AttackElement enum (used to look up rows / columns in
/// <see cref="ElementChart"/>). Mirrors RebuildSharedData.Enum.EntityStats.AttackElement intentionally —
/// not referenced as a hard dep so this file stays self-contained.</summary>
public enum SimElement : byte { Neutral, Earth, Water, Fire, Wind, Poison, Undead, Dark, Holy, Ghost, None }

/// <summary>Monster size — drives the per-weapon-class damage modifier (a Dagger vs Large is 75%, etc.).
/// Mirrors RebuildSharedData.Enum.EntityStats.CharacterSize.</summary>
public enum SimSize : byte { Small, Medium, Large, None }

/// <summary>Monster race — kept for symmetry; not currently scaled by the simulator beyond letting the
/// caller stamp it on the forecast for the agent to read. Mirrors CharacterRace.</summary>
public enum SimRace : byte { Formless, Demihuman, Beast, Insect, Angel, Dragon, Aquatic, Plant, Demon, Undead, None }

/// <summary>Weapon classes that affect the size-modifier table. Subset of the server's WeaponClass enum —
/// covers what bots actually equip today (sword / dagger / bow / mace / staff / katar / knuckle / spear).</summary>
public enum SimWeaponClass : byte
{
    Fist, Dagger, OneHandSword, TwoHandSword, OneHandSpear, TwoHandSpear,
    OneHandAxe, TwoHandAxe, Mace, Staff, Bow, Knuckle, Instrument, Whip,
    Book, Katar, TwoHandStaff
}

/// <summary>Server's full elemental chart (ServerData/Db/ElementalChart.csv) inlined so the bot doesn't
/// need to ship that file. Rows are defender [element, level], columns are attack element. Values are
/// integer percents (100 = no change, 200 = double, 0 = immune). The simulator looks up the multiplier
/// with <see cref="GetModifier"/> and applies it as eleMod/100 — same shape as the server formula.
/// Sourced once from ElementalChart.csv; do NOT hand-edit individual cells.</summary>
public static class ElementChart
{
    // Row order: Neutral1..4, Earth1..4, Water1..4, Fire1..4, Wind1..4, Poison1..4, Undead1..4, Dark1..4,
    // Holy1..4, Ghost1..4, None. Column order: Neutral, Earth, Water, Fire, Wind, Poison, Undead, Dark,
    // Holy, Ghost, Special, None.
    private static readonly int[,] Table = new int[,]
    {
        // Neutral
        {100,100,100,100,100,105,100,100,100, 50,100,0},
        {100,100,100,100,100,105,100,100,100, 25,100,0},
        {100,100,100,100,100,105,100,100,100,  5,100,0},
        {100,100,100,100,100,105,100,100,100,  0,100,0},
        // Earth
        {100, 50,100,150, 75,125,100,100,100,100,100,0},
        {100, 25,100,175, 50,120,100,100,100,100,100,0},
        {100,  5,100,200, 25,110,100, 90, 90,100,100,0},
        {100,  0,100,200,  0,100,100, 80, 80,100,100,0},
        // Water
        {100,100, 50, 75,150,100,100,100,100,100,100,0},
        {100,100, 25, 50,175, 80,100,100,100,100,100,0},
        {100,100,  0, 25,200, 60,100, 90, 90,100,100,0},
        {100,100,  0,  0,200, 40,100, 80, 80,100,100,0},
        // Fire
        {100, 75,150, 50,100,125,100,100,100,100,100,0},
        {100, 50,175, 25,100,120,100,100,100,100,100,0},
        {100, 25,200,  5,100,110,100, 90, 90,100,100,0},
        {100,  0,200,  0,100,100,100, 80, 80,100,100,0},
        // Wind
        {100,150, 75,100, 50,125,100,100,100,100,100,0},
        {100,175, 50,100, 25,120,100,100,100,100,100,0},
        {100,200, 25,100,  5,110,100, 90, 90,100,100,0},
        {100,200,  0,100,  0,100,100, 80, 80,100,100,0},
        // Poison
        {100,100,100,100,100,  0, 75, 50,110,100,100,0},
        {100,100,100,100,100,  0, 50, 25,120, 80,100,0},
        {100, 90, 90, 90, 90,  0, 25,  0,130, 60,100,0},
        {100, 80, 80, 80, 80,  0,  0,  0,140, 40,100,0},
        // Undead
        {100, 90,110,125,100, 25,  0,  0,150,110,100,0},
        {100, 80,120,150,100,  5,  0,  0,175,130,100,0},
        {100, 70,130,175,100,  0,  0,  0,200,150,100,0},
        {100, 60,140,200,100,  0,  0,  0,200,170,100,0},
        // Dark
        {100,100,100,100,100, 80, 50,  0,125, 75,100,0},
        {100, 90, 90, 90, 90, 70, 25,  0,150, 50,100,0},
        {100, 80, 80, 80, 80, 60,  0,  0,175, 25,100,0},
        {100, 70, 70, 70, 70, 50,  0,  0,200,  0,100,0},
        // Holy
        {100, 80, 80, 80, 80,108,125,125,  0, 80,100,0},
        {100, 60, 60, 60, 60,112,150,150,  0, 60,100,0},
        {100, 40, 40, 40, 40,116,175,175,  0, 40,100,0},
        {100, 20, 20, 20, 20,120,200,200,  0, 20,100,0},
        // Ghost
        { 75,100,100,100,100,100,110,100,100,150,100,0},
        { 50,100,100,100,100, 90,120,100,100,170,100,0},
        { 25,100,100,100,100, 80,130,100,100,190,100,0},
        {  0,100,100,100,100, 70,140,100,100,200,100,0},
        // None
        {100,100,100,100,100,100,100,100,100,100,100,0},
    };

    /// <summary>How much of <paramref name="attack"/>-element damage gets through to a defender with
    /// element <paramref name="defender"/> at level 1..4. Returns 100 (= no scale) for None or out-of-range
    /// inputs so callers can default safely when monster data is missing.</summary>
    public static int GetModifier(SimElement attack, SimElement defender, int defenderLevel)
    {
        if (defender == SimElement.None) return 100;
        if (attack == SimElement.None) return 100;
        if (defenderLevel < 1) defenderLevel = 1;
        if (defenderLevel > 4) defenderLevel = 4;

        // Row index in Table[]: 4 levels per base element, layout matches the CSV.
        var defRowBase = (int)defender * 4;
        if (defender == SimElement.None) defRowBase = 10 * 4;     // last "None" row
        var row = defRowBase + defenderLevel - 1;
        var col = (int)attack;
        if (row >= Table.GetLength(0) || col >= Table.GetLength(1)) return 100;
        return Table[row, col];
    }

    /// <summary>Parse a string like "Earth2" / "Neutral1" / "" into element+level. Returns
    /// (None, 1) on empty/unknown strings — the chart returns 100 for None, so the simulator falls back
    /// gracefully when monster data is partial.</summary>
    public static (SimElement element, int level) Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return (SimElement.None, 1);
        // Trailing digit is the level.
        var level = 1;
        var i = raw.Length - 1;
        if (i >= 0 && char.IsDigit(raw[i])) { level = raw[i] - '0'; i--; }
        var baseName = raw[..(i + 1)];
        return baseName.ToLowerInvariant() switch
        {
            "neutral" => (SimElement.Neutral, level),
            "earth"   => (SimElement.Earth,   level),
            "water"   => (SimElement.Water,   level),
            "fire"    => (SimElement.Fire,    level),
            "wind"    => (SimElement.Wind,    level),
            "poison"  => (SimElement.Poison,  level),
            "undead"  => (SimElement.Undead,  level),
            "dark"    => (SimElement.Dark,    level),
            "holy"    => (SimElement.Holy,    level),
            "ghost"   => (SimElement.Ghost,   level),
            _         => (SimElement.None,    1),
        };
    }
}

/// <summary>Per-weapon-class size penalty: how much damage a weapon does versus a Small/Medium/Large
/// target. Values are integer percents (100 = no change). Numbers follow standard RO weapon-vs-size
/// table; see eAthena/rAthena <c>size_fix.txt</c> for the reference.</summary>
public static class WeaponSizeChart
{
    private static readonly int[,] Table = new int[,]
    {
        //                     Small  Medium  Large
        /* Fist           */ { 100,    100,    100 },
        /* Dagger         */ { 100,    100,     50 },
        /* OneHandSword   */ {  75,    100,     75 },
        /* TwoHandSword   */ {  75,     75,    100 },
        /* OneHandSpear   */ {  75,    100,    100 },
        /* TwoHandSpear   */ {  75,    100,    100 },
        /* OneHandAxe     */ {  75,    100,    100 },
        /* TwoHandAxe     */ {  75,    100,    100 },
        /* Mace           */ {  75,    100,    100 },
        /* Staff          */ { 100,    100,    100 },
        /* Bow            */ { 100,    100,     75 },
        /* Knuckle        */ { 100,     75,     50 },
        /* Instrument     */ { 100,     75,     75 },
        /* Whip           */ {  75,     75,    100 },
        /* Book           */ { 100,    100,     75 },
        /* Katar          */ {  75,    100,     75 },
        /* TwoHandStaff   */ { 100,    100,    100 },
    };

    public static int GetModifier(SimWeaponClass weapon, SimSize size)
    {
        // Ragnarok Rebuild currently applies 100% damage regardless of weapon-vs-size match — the size
        // penalty hooks exist in the server damage path but the table is disabled. Returning 100 here
        // unconditionally so the simulator stops vetoing winnable fights (a Dagger vs Large was getting a
        // 50% sizeMod and flipping CanWin to false when the real combat does full damage). The lookup
        // table is preserved above in case the server re-enables it; flip a feature flag here when that
        // happens.
        return 100;
    }

    public static SimSize ParseSize(string raw) => raw?.ToLowerInvariant() switch
    {
        "small"  => SimSize.Small,
        "medium" => SimSize.Medium,
        "large"  => SimSize.Large,
        _        => SimSize.None,
    };

    public static SimRace ParseRace(string raw) => raw?.ToLowerInvariant() switch
    {
        "formless"  => SimRace.Formless,
        "demihuman" => SimRace.Demihuman,
        "beast"     => SimRace.Beast,
        "insect"    => SimRace.Insect,
        "angel"     => SimRace.Angel,
        "dragon"    => SimRace.Dragon,
        "aquatic"   => SimRace.Aquatic,
        "plant"     => SimRace.Plant,
        "demon"     => SimRace.Demon,
        "undead"    => SimRace.Undead,
        _           => SimRace.None,
    };
}
