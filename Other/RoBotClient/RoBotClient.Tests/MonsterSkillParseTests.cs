using System.Text.RegularExpressions;
using RebuildSharedData.Enum;
using RoBotClient.Bot.Behavior;
using RoBotClient.Bot.GameData;

namespace RoBotClient.Tests;

public class MonsterSkillParseTests
{
    [Fact]
    public void GreatestGeneral_PunishesRangedAttacks_ElderWillowDoesNot()
    {
        var warps = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..", "RoRebuildServer", "GameConfig", "ServerData", "Script", "Warps"));
        var db = GameDatabase.Load("", "", warps);
        Assert.True(db.PunishesRangedAttacks("GREATEST_GENERAL"));
        Assert.False(db.PunishesRangedAttacks("ELDER_WILLOW"));
    }

    [Fact]
    public void AttackDelay_ComesFromMonstersCsv()
    {
        var warps = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..", "RoRebuildServer", "GameConfig", "ServerData", "Script", "Warps"));
        var db = GameDatabase.Load("", "", warps);
        Assert.Equal(0.432, db.MonsterAttackDelay(4132), 3); // Matyr
        Assert.Equal(1.78, db.MonsterAttackDelay(4185), 3);  // Zerom
    }

    private static string Server => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
        "..", "..", "..", "..", "..", "..", "RoRebuildServer"));

    [Fact]
    public void ElderWillow_FireBolt_IsInTheForecast()
    {
        var db = GameDatabase.Load("", "", Path.Combine(Server, "GameConfig", "ServerData", "Script", "Warps"));
        var casts = db.MonsterCasts("ELDER_WILLOW");
        Assert.Contains(new GameDatabase.MonsterCast(CharacterSkill.FireBolt, 3, 50, 1.5, 5, 1), casts);
        Assert.Contains(casts, c => c.Skill == CharacterSkill.FireAttack && c.Level == 2);
        Assert.Equal(31, db.MonsterInt(4055));
        // StateChase only has the bolt; Fire Attack is melee-state only.
        Assert.Equal(new[] { CharacterSkill.FireBolt }, db.MonsterChaseCasts("ELDER_WILLOW").Select(c => c.Skill));

        // Elder Willow (lv20, ATK 52-63, INT 31) vs a lv20 merchant, INT 1, MDEF 0, VIT 10: a bolt hit is
        // MATK ~58 − 6 ≈ 52, three per cast — the 120–162 hits in Bot3's log. Each cast also replaces ~2 swings,
        // so the net gain against this low-DEF bot is small (~2.3 DPS on ~30).
        var me = new Combatant(20, 60, 70, 5, 10, 20, 10, 0, 0, 1.0, 400, 400);
        var willow = new Combatant(20, 52, 63, 6, 13, 22, 10, 0, 0, 1.372, 963, 963);
        var skillDps = BattleSimulator.MonsterSkillDps(me, 1, 0, willow, 31, casts);
        Assert.InRange(skillDps, 1, 10);
        var without = BattleSimulator.Forecast(me, willow, 1.0, 999);
        var with = BattleSimulator.Forecast(me, willow, new CombatModifiers(MonsterSkillDps: skillDps), 1.0, 999);
        Assert.True(with.SecondsToKillMe < without.SecondsToKillMe);
        // Cast every ~32 s per skill, two skills, rolled on a 1.372 s swing: ~9% of its actions are skills,
        // which is the share of 75+ hits Elder Willow landed in the bot logs (571 of 6,507).
        Assert.InRange(casts.Sum(c => 1 / (1.372 * (1 - 0.051) / 0.051 + c.CastSeconds + c.DelaySeconds)) * 1.372, 0.07, 0.11);
    }

    // Unmodeled on purpose: ground events, distance falloff or % HP damage.
    private static readonly HashSet<string> Unmodeled = new()
        { "None", "BrandishSpear", "MeteorStorm", "FirePillar", "Demonstration", "SelfDestruct", "PhysBreaker", "VenomDust" };

    [Fact]
    public void EveryDamagingMonsterSkill_HasADamageShape()
    {
        var handlers = Directory.EnumerateFiles(Path.Combine(Server, "RoRebuildServer", "Simulation", "Skills", "SkillHandlers"), "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText).Where(t => t.Contains("CalculateCombatResult") || t.Contains(": TargetedSpellBase") || t.Contains(": ElementalAttackHandler")).ToList();
        var used = Directory.EnumerateFiles(Path.Combine(Server, "GameConfig", "ServerData", "Script", "MonsterSkills"), "*.txt", SearchOption.AllDirectories)
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"TryCast\((\w+),").Select(m => m.Groups[1].Value)).ToHashSet();
        var missing = used.Where(s => !Unmodeled.Contains(s) && Enum.TryParse<CharacterSkill>(s, out var skill)
                && handlers.Any(t => t.Contains($"CharacterSkill.{s})") || t.Contains($"CharacterSkill.{s},"))
                && BattleSimulator.MonsterSkillShape(skill, 1) == null).ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void MonsterSkillReach_MatchesTheServerHandlers()
    {
        var wrong = new List<string>();
        foreach (var f in Directory.EnumerateFiles(Path.Combine(Server, "RoRebuildServer", "Simulation", "Skills", "SkillHandlers"), "*.cs", SearchOption.AllDirectories))
        {
            var t = File.ReadAllText(f);
            foreach (Match a in Regex.Matches(t, @"\[SkillHandler\(CharacterSkill\.(\w+)(?:,\s*SkillClass\.(\w+))?"))
            {
                if (!Enum.TryParse<CharacterSkill>(a.Groups[1].Value, out var skill) || BattleSimulator.MonsterSkillShape(skill, 1) is not { } s) continue;
                // A literal "=> 7;" override, else the class default: Magic 9, anything else the monster's range (here 1).
                var lit = Regex.Match(t, @"GetSkillRange\(CombatEntity source, int lvl\)\s*=>\s*(\d+);");
                if (!lit.Success && t.Contains("GetSkillRange(")) continue; // computed per level/source: checked by hand
                var expected = lit.Success ? int.Parse(lit.Groups[1].Value)
                    : a.Groups[2].Value == "Magic" || t.Contains(": TargetedSpellBase") ? 9 : 1;
                if (BattleSimulator.MonsterSkillReach(s, 1) != expected) wrong.Add($"{skill}: {BattleSimulator.MonsterSkillReach(s, 1)} vs server {expected}");
            }
        }
        Assert.Empty(wrong);
    }
}
