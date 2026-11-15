namespace RoBotClient.Bot.Behavior;

/// <summary>A named build: first job, stat goals (STR, AGI, VIT, INT, DEX, LUK; 0 = no goal), skill order and the
/// skill script that casts what it learns (SKILL_SCRIPT.md; a rule only fires once its skill is known).</summary>
public sealed record BuildPreset(string Name, int JobId, int[] Stats, string SkillOrder, string SkillScript = "");

/// <summary>Classic first-job builds. Skill orders follow the server's SkillTree.toml prerequisites and start with
/// Basic Mastery 8 + First Aid 1: the 9 Novice skill points, all of which must be spent before the Adventuring Bard changes the job.</summary>
public static class BuildPresets
{
    public static readonly BuildPreset[] All =
    {
        new("Agi Swordsman", 1, new[] { 50, 60, 20, 0, 20, 0 }, "BasicMastery 8, FirstAid 1, SwordMastery 10, Bash 10, MagnumBreak 3, IncreasedHPRecovery 5, Provoke 5, Endure 5", "use Bash 0 if self.sp >= 15 and target.hppct > 30"),
        new("Vit Swordsman", 1, new[] { 50, 0, 60, 0, 30, 0 }, "BasicMastery 8, FirstAid 1, IncreasedHPRecovery 10, Bash 10, MagnumBreak 10, SwordMastery 10", "use Bash 0 if self.sp >= 15 and target.hppct > 30"),
        new("Dex Archer",    2, new[] { 20, 50, 0, 0, 70, 0 }, "BasicMastery 8, FirstAid 1, OwlEye 10, DoubleStrafe 10, VultureEye 10, ImproveConcentration 10", "use ImproveConcentration 0 self every 120 if self.sppct > 70\nuse DoubleStrafe 0 if self.sp >= 12 and target.hppct > 20"),
        new("Int Mage",      3, new[] { 0, 0, 20, 70, 50, 0 }, "BasicMastery 8, FirstAid 1, FireBolt 10, ColdBolt 10, LightningBolt 10, NapalmBeat 4, SoulStrike 10, IncreaseSPRecovery 5", "use SoulStrike 0 if self.sppct > 15\nuse FireBolt 0 if self.sppct > 15\nuse ColdBolt 0 if self.sppct > 15\nuse LightningBolt 0 if self.sppct > 15\nuse NapalmBeat 0 if self.sppct > 10"),
        new("Heal Acolyte",  4, new[] { 0, 0, 40, 60, 30, 0 }, "BasicMastery 8, FirstAid 1, Heal 10, DivineProtection 5, Blessing 10, IncreaseAgility 10, Ruwach 1, Teleport 2", "use Heal 0 ally every 2 if target.hppct < 60\nuse Heal 0 self every 3 if self.hppct < 50\nuse Blessing 0 self every 120 if self.sppct > 60\nuse IncreaseAgility 0 self every 120 if self.sppct > 60"),
        new("Agi Thief",     5, new[] { 40, 70, 0, 0, 30, 0 }, "BasicMastery 8, FirstAid 1, DoubleAttack 10, ImproveDodge 10, Envenom 5", "use Envenom 0 if self.sp >= 12 and target.hppct > 30"),
        new("Str Merchant",  6, new[] { 70, 0, 40, 0, 30, 0 }, "BasicMastery 8, FirstAid 1, Mammonite 10, EnlargeWeightLimit 10, Discount 10"),  // ponytail: no script, Mammonite costs zeny per cast; add one once bots manage zeny
    };

    public static BuildPreset? Find(string? name) =>
        All.FirstOrDefault(p => string.Equals(p.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The preset these settings match exactly, else "Custom" (or "" when nothing is set).</summary>
    public static string NameFor(int jobId, int[] stats, string skillOrder) =>
        All.FirstOrDefault(p => p.JobId == jobId && p.Stats.SequenceEqual(stats) && p.SkillOrder == skillOrder.Trim())?.Name
        ?? (jobId == 0 && stats.All(s => s == 0) && string.IsNullOrWhiteSpace(skillOrder) ? "" : "Custom");
}
