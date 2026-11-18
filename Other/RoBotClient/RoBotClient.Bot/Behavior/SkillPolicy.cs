using RebuildSharedData.Enum;

namespace RoBotClient.Bot.Behavior;

/// <summary>Is an offensive cast worth its SP right now, and at which level? Pure math, no I/O (see SkillPolicyTests).
/// A damage skill's value is the number of auto-attacks it replaces: it shares the attack cooldown
/// (ApplyCooldownForAttackAction), so a Bash that saves 3 swings saves 3 attack intervals. Overkill wastes it:
/// Bash on a mob one swing from death saves nothing. Debuff skills are worthless on a target already carrying the debuff.</summary>
public static class SkillPolicy
{
    /// <summary>Skill damage as a multiple of one auto-attack, from the server's skill handlers (0 = not modeled).</summary>
    // ponytail: multiplier on post-DEF damage; the server applies it pre-DEF, so vs high-DEF mobs the skill is a bit underrated.
    public static double DamageMultiplier(CharacterSkill skill, int level) => skill switch
    {
        CharacterSkill.Bash => 1 + 0.3 * level,               // BashHandler
        CharacterSkill.MagnumBreak => 1 + 0.2 * level,        // MagnumBreakHandler (AoE, single-target value here)
        CharacterSkill.Envenom => 1 + 0.05 * level,           // EnvenomHandler, poison is the real payload
        CharacterSkill.DoubleStrafe => 2 * (0.9 + 0.1 * level), // DoubleStrafeHandler, 2 hits
        CharacterSkill.Mammonite => 1 + 0.5 * level,          // MammoniteHandler (also costs zeny, budgeted elsewhere)
        _ => 0,
    };

    /// <summary>The status a skill puts on its target; recasting while it's up only buys the skill's damage, if any.</summary>
    public static CharacterStatusEffect? AppliedDebuff(CharacterSkill skill) => skill switch
    {
        CharacterSkill.Envenom => CharacterStatusEffect.Poison,
        CharacterSkill.Provoke => CharacterStatusEffect.Provoke,
        _ => null,
    };

    /// <summary>Chance the skill's debuff lands on a hit, before the target's resist (EnvenomHandler: 50% + 5%/lv). 0 = not modeled.</summary>
    public static double DebuffChance(CharacterSkill skill, int level) => skill switch
    {
        CharacterSkill.Envenom => (500 + 50 * level) / 1000.0,
        _ => 0,
    };

    /// <summary>Target's multiplier on that chance, TryPoisonOnTarget: 0.99^VIT (VIT x2 on bosses), Undead immune.</summary>
    public static double PoisonResist(int vit, bool boss, bool undead)
        => undead ? 0 : Math.Pow(0.99, boss ? vit * 2 : vit);

    /// <summary>CombatEntity.CheckLuckModifiedRandomChanceVsTarget for a player vs a monster: the chance scaled by
    /// (myLuk + mod) / (mobLuk + mod), mod = 150 + (chance‰ - 100) / 4, mob LUK capped at mine + 100.</summary>
    public static double LuckModifiedChance(double chance, int myLuk, int targetLuk)
    {
        if (chance <= 0) return 0;
        if (chance > 1) return 1;
        var mod = 150 + (chance > 0.1 ? (int)((chance * 1000 - 100) / 4) : 0);
        targetLuk = Math.Max(0, Math.Min(targetLuk, myLuk + 100));
        return Math.Min(1, chance * (myLuk + mod) / (targetLuk + mod));
    }

    /// <summary>A poisoned monster's (Def, Vit), StatusPoison.OnApply: VIT -VIT/4, hard DEF -25% (-10% on bosses).</summary>
    public static (int Def, int Vit) PoisonedDefense(int def, int vit, bool boss)
        => (def * (boss ? 90 : 75) / 100, vit - vit / 4);

    /// <summary>StatusPoison on a monster: 15 + 1.5 s, one tick every 2 s, each tick = half the Envenom hit.</summary>
    public const int PoisonTicks = 8;
    public const double PoisonTickSeconds = 2, PoisonSeconds = 16.5;

    /// <summary>Like HitsSaved, but the cast also lands poison with &lt;paramref name="landChance"/&gt; (hit chance is already in
    /// autoDmg/hitDmg): its expected ticks keep chipping while we swing, and swings inside the poison window hit the
    /// weakened DEF/VIT (&lt;paramref name="poisonedAutoDmg"/&gt;, 0 = same as autoDmg). A mob that dies before that adds up
    /// isn't worth the SP, so failed rolls on a low mob stop being retried on their own.</summary>
    public static int PoisonHitsSaved(int hp, double autoDmg, double hitDmg, double landChance, double attackInterval,
        double poisonedAutoDmg = 0)
    {
        if (hp <= 0 || autoDmg <= 0) return 0;
        var without = (int)Math.Ceiling(hp / autoDmg);
        var tick = landChance * hitDmg / 2;
        var buffedSwing = autoDmg + landChance * (Math.Max(poisonedAutoDmg, autoDmg) - autoDmg);
        double swings = 0;
        var after = 0;
        while (after < without && hp - hitDmg - swings
               - Math.Min(PoisonTicks, Math.Floor(after * attackInterval / PoisonTickSeconds)) * tick > 0)
        {
            after++;
            swings += after * attackInterval <= PoisonSeconds ? buffedSwing : autoDmg;
        }
        return without - 1 - after;
    }

    /// <summary>Area skills: Chebyshev radius from the server handler's GatherEnemiesInArea, and whether it's centered on
    /// the caster (else on the target cell). null = single-target.</summary>
    public static (int Radius, bool OnSelf)? Aoe(CharacterSkill skill) => skill switch
    {
        CharacterSkill.MagnumBreak => (2, true),     // MagnumBreakHandler blastDistance
        CharacterSkill.ArrowShower => (2, false),    // ArrowShowerHandler 5x5
        CharacterSkill.CartRevolution => (1, false), // CartRevolutionHandler splash around the target
        _ => null,
    };

    /// <summary>A buff an area skill gives its caster: extra damage per swing, as a share of the swing, in Element.</summary>
    public readonly record struct SelfBuffInfo(CharacterStatusEffect Status, double DurationSec, SimElement Element, double BonusPerSwing);

    public static SelfBuffInfo? SelfBuff(CharacterSkill skill, int level) => skill switch
    {
        // MagnumBreakHandler: 20 s status; StatusMagnumBreak: +(10 + lv)% of each physical hit as fire damage
        CharacterSkill.MagnumBreak => new(CharacterStatusEffect.MagnumBreak, 20, SimElement.Fire, (10 + level) / 100.0),
        _ => null,
    };

    /// <summary>Swings a lone-target buff cast saves on this target: the cast's own hit plus the bonus on the swings left
    /// while the buff lasts. &lt;paramref name="eleMod"/&gt; = buff element vs target, relative to the auto-attack's (a fire mob
    /// shrugs off Magnum Break's fire, so the buff saves nothing and the SP is better kept for Bash).</summary>
    // ponytail: only counts the current target; a 20 s buff can carry into the next mob. Count those if fights are short.
    public static int BuffCastHitsSaved(CharacterSkill skill, int level, int hp, double autoDmg, double attackInterval, double eleMod)
    {
        if (SelfBuff(skill, level) is not { } b || autoDmg <= 0) return 0;
        var hit = autoDmg * DamageMultiplier(skill, level) * eleMod;
        var swingsLeft = Math.Ceiling(Math.Max(0, hp - hit) / autoDmg);
        var buffed = Math.Min(swingsLeft, Math.Floor(b.DurationSec / Math.Max(0.1, attackInterval)));
        return HitsSaved(hp, autoDmg, hit + buffed * autoDmg * b.BonusPerSwing * eleMod);
    }

    /// <summary>An area skill on one enemy pays AoE SP for single-target damage. Needs 2+ enemies in the blast, or, as a
    /// self-buff while the buff is down, to save ≥1 swing on this target at no worse swings-per-SP than the best
    /// single-target skill in the script (&lt;paramref name="bestAltPerSp"/&gt;, 0 = none).</summary>
    public static bool AoeWorthIt(CharacterSkill skill, int level, int enemiesInArea, bool selfHasBuff,
        int buffHitsSaved, int buffSpCost, double bestAltPerSp)
        => Aoe(skill) == null || enemiesInArea >= 2
           || (SelfBuff(skill, level) != null && !selfHasBuff && buffHitsSaved >= 1
               && (double)buffHitsSaved / Math.Max(1, buffSpCost) >= bestAltPerSp);

    public static int SpCost(int[]? spCosts, int level)
        => spCosts == null || spCosts.Length == 0 ? 1 : Math.Max(1, spCosts[Math.Clamp(level, 1, spCosts.Length) - 1]);

    /// <summary>Auto-attacks one cast replaces when the target has &lt;paramref name="hp"/&gt; left: swings to kill without it,
    /// minus the cast's own slot, minus the swings still needed after it. 0 or less = the cast doesn't speed up the kill.</summary>
    public static int HitsSaved(int hp, double autoDmg, double skillDmg)
    {
        if (hp <= 0 || autoDmg <= 0) return 0;
        var without = (int)Math.Ceiling(hp / autoDmg);
        var after = (int)Math.Ceiling(Math.Max(0, hp - skillDmg) / autoDmg);
        return without - 1 - after;
    }

    /// <summary>Standing SP regen per second, Player.SpRegenTick: one tick every 6 s.</summary>
    public static double SpRegenPerSecond(int maxSp, int intStat)
        => (1 + (maxSp / 100 + intStat / 6) * (200 + intStat) / 200) / 6.0;

    /// <summary>SP at or above this is "free": regen would be capped and wasted, so any cast that saves a swing is worth it.
    /// One regen tick below max.</summary>
    public static bool SpIsFree(int sp, int maxSp, int intStat)
        => maxSp > 0 && sp >= maxSp - SpRegenPerSecond(maxSp, intStat) * 6;

    /// <summary>Level to cast (0 = don't). &lt;paramref name="maxLevel"/&gt; caps the search (the script's level, or the known level).
    /// &lt;paramref name="pinLevel"/&gt; = the script named an exact level: only decide yes/no at that level.
    /// - Pure debuff (no damage, e.g. Provoke) on a target without it: cast (that's what it's for).
    /// - Damage + debuff (Envenom) on a target without it: a damage skill whose hit carries the debuff's expected ticks
    ///   and DEF drop (&lt;paramref name="landChance"/&gt;: level → chance it sticks), held to the same ≥1 swing / per-SP bar.
    /// - Damage skill: needs to save ≥1 swing. With free SP pick the level saving the most swings, otherwise the best
    ///   swings-per-SP. (A "save SP for fresh mobs" bar was tried: with server multipliers a finishing cast scores about
    ///   the same per SP as an opener, so the 1-swing rule already does the work.)
    /// - No auto-attack (autoDmg ≤ 0, a pure caster): the skill IS the damage, always cast.</summary>
    public static int ChooseLevel(CharacterSkill skill, int maxLevel, bool pinLevel, int[]? spCosts,
        int targetHp, bool targetHasDebuff, double autoDmg, int sp, bool spFree,
        Func<int, double>? landChance = null, double attackInterval = 1.5, double poisonedAutoDmg = 0)
    {
        if (maxLevel <= 0) return 0;
        var debuff = AppliedDebuff(skill);
        if (debuff != null && !targetHasDebuff && DamageMultiplier(skill, 1) <= 0) return maxLevel;
        if (DamageMultiplier(skill, 1) <= 0) return debuff != null ? 0 : maxLevel; // debuff already up / not modeled: leave it to the script
        if (autoDmg <= 0) return maxLevel;

        int Cost(int lv) => SpCost(spCosts, lv);
        var land = debuff != null && !targetHasDebuff ? landChance : null;
        int Saved(int hp, int lv) => land != null
            ? PoisonHitsSaved(hp, autoDmg, autoDmg * DamageMultiplier(skill, lv), land(lv), attackInterval, poisonedAutoDmg)
            : HitsSaved(hp, autoDmg, autoDmg * DamageMultiplier(skill, lv));
        double Score(int hp, int lv) => spFree ? Saved(hp, lv) : (double)Saved(hp, lv) / Cost(lv);

        var (lo, hi) = pinLevel ? (maxLevel, maxLevel) : (1, maxLevel);
        int best = 0; double bestScore = 0;
        for (var lv = lo; lv <= hi; lv++)
        {
            if (Cost(lv) > sp || Saved(targetHp, lv) < 1) continue;
            var s = Score(targetHp, lv);
            if (s >= bestScore) { bestScore = s; best = lv; } // ties go to the higher level: same efficiency, faster kill
        }
        return best;
    }
}
