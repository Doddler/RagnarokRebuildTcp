namespace RoBotClient.Bot.Behavior;

/// <summary>Combat inputs for one side of a fight (player or monster).</summary>
public readonly record struct Combatant(
    int Level, int AtkMin, int AtkMax, int Def, int Vit, int Dex, int Agi,
    int AddHit, int AddFlee, double AttackInterval, int Hp, int MaxHp);

/// <summary>Optional modifiers layered on top of the raw <see cref="Combatant"/> stats: element + size +
/// race + weapon class for proper damage scaling, plus crit chance and an extra-skill-DPS pass-through so
/// a mage's expected FireBolt rotation can feed into the forecast without re-implementing the skill engine
/// inside the simulator. All fields default to "no effect" so existing callers using the old signature
/// keep working.</summary>
public readonly record struct CombatModifiers(
    SimElement AttackerElement = SimElement.Neutral,
    SimElement DefenderElement = SimElement.None,
    int DefenderElementLevel = 1,
    SimSize DefenderSize = SimSize.None,
    SimRace DefenderRace = SimRace.None,
    SimWeaponClass AttackerWeapon = SimWeaponClass.OneHandSword,
    int AttackerLuk = 0,
    int AttackerAddCrit = 0,        // server CharacterStat.AddCrit — in % units (gets ×10 to per-mille). +1 = +10 per-mille crit.
    int AttackerAddCritDamage = -1, // server-side AddCritDamage stat (%). < 0 = use the default 40%.
    int AttackerDemonBaneLevel = 0, // player's max-learned DemonBane level (+3 per lvl vs Demon/Undead, pre-DEF).
    int AttackerBeastBaneLevel = 0, // player's max-learned BeastBane level (+5 per lvl vs Beast/Insect).
    double ExtraSkillDps = 0,
    double SkillBurstDamage = 0,
    bool DefenderIsBoss = false,    // bosses get specific resists; flag exposed to callers, not yet used in damage scale.
    // --- range model (added for ranged-fight forecasting) ---
    int AttackerAttackRange = 1,    // player's effective attack range in tiles (bow = ~9, staff bolt = ~9, melee = 1).
    int DefenderAttackRange = 1,    // monster's attack range from MonsterDb.Range.
    bool DefenderIsImmobile = false)// monster can't chase (MoveSpeed <= 0 OR AiAggressiveImmobile). Used by Forecast to
                                    // zero incoming damage when AttackerAttackRange > DefenderAttackRange — the bot can
                                    // stand outside the mob's reach and shoot freely. Mandragora (R5, plant) and Geographer
                                    // (R3, AiAggressiveImmobile) are the canonical examples; both are free kills for any
                                    // ranged class.
{
    public static readonly CombatModifiers Default = new();
}

public readonly record struct BattleForecast(
    bool CanWin,
    int MyDamagePerHit, int MonsterDamagePerHit,
    double MyHitChance, double MonsterHitChance,
    double SecondsToKillMonster, double SecondsToKillMe,
    double WinChancePercent,
    int ElementMod = 100,
    int SizeMod = 100,
    double CritChance = 0,
    double EffectiveDpsWithSkills = 0);

/// <summary>Outcome of fighting a whole pack at once (assist mobs / aggressive swarms).</summary>
public readonly record struct GroupForecast(
    bool CanWin, int Count,
    double TotalSecondsToKill, double TotalDamageTaken, int MyHp,
    double WinChancePercent,
    double AverageElementMod = 100,
    double AverageSizeMod = 100,
    double EffectiveDpsWithSkills = 0);

/// <summary>
/// 1v1 fight forecast. Mirrors the server's <c>CombatEntity.CalculateCombatResultUsingSetAttackPower</c>
/// (in RoRebuildServer/.../DamageHandling.cs) as faithfully as the client-side stat sync allows.
///
/// EXACT MATCHES (down to integer rounding):
/// - DEF cut: <c>MathHelper.DefValueLookup(def)</c> — same piecewise formula.
/// - Hit chance: <c>(Level + Dex + AddHit) + 75 - (Level + Agi + AddFlee)</c> clamped to [5, 100].
/// - Level cuts: 1.5%/level (cap 0.1) player→monster, 0.25%/level (cap 0.5) monster→player.
/// - Element chart: full <c>ServerData/Db/ElementalChart.csv</c> inlined in <see cref="ElementChart"/>.
/// - Crit chance: <c>(1 + Luk/3 + AddCrit) * 10 + Level/5</c> per-mille — same as <c>GetBaseCritRate</c>.
/// - Crit damage: rolls atk2, ignores DEF, multiplies by <c>1 + AddCritDamage/100</c>.
/// - subDef mean: matches the expected value of the server's randomized formula for both player and
///   monster defenders.
/// - DemonBane / BeastBane add-damage: pulled from KnownSkills, applied pre-DEF, gated by monster race.
///
/// REMAINING DEVIATIONS (stats not synced to the client via PlayerUpdateStats — would need a server
/// packet expansion to close):
/// - <c>AddAttackRace[race]</c> / <c>AddResistRace[race]</c>: 0 by default. Gear like Mantis Card affects
///   this but we can't see it.
/// - <c>AddAttackSize</c> / <c>AddResistSize</c>: 0 by default. Skoll Card etc.
/// - <c>AddAttackRangedAttack</c> / <c>AddResistRangedAttack</c>: 0 default. Archer Skeleton Card etc.
/// - <c>AddAttackSpecialBoss</c> / <c>AddResistSpecialBoss</c>: 0 default. Drops/Boss-killer gear.
/// - <c>AddRefineAttackPower</c>: 0 default. Refine bonus damage post-DEF.
/// - <c>IgnoreDefRace</c> / <c>IgnoreDefSize</c> (defMod): defaults to 100 (no DEF reduction).
/// - <c>AddSoftDefPercent</c>: 0 default. Buffs like Magnum Break that buff own subdef temporarily.
/// - <c>WeaponMastery</c>: server adds the buff-driven mastery total to addDamage. We approximate via
///   KnownSkills (DemonBane/BeastBane only) — weapon-specific masteries (Sword/Spear/etc.) need weapon-
///   class detection first.
/// - Random variance: the simulator computes the EXPECTED value (mean across the uniform damage roll
///   and the bernoulli crit roll). A single live combat tick deviates ±50%; the agent should treat
///   forecast as the expectation, not a guarantee.
///
/// Magic damage path (Mage / Wizard / Priest offensive) is approximated via <see cref="CombatModifiers.ExtraSkillDps"/>
/// — caller blends in (MagicAtk + INT) / cast-interval. The simulator doesn't run a separate magic
/// damage forecast yet.
/// </summary>
public static class BattleSimulator
{
    private const double DefaultMonsterAttackInterval = 1.6; // seconds; TODO export real RechargeTime
    // (CritDamageMultiplier removed — the simple "1.4x post-DEF" model has been replaced by the proper
    // crit pipeline in Forecast/ForecastGroup that mirrors the server: atk2 roll, ignore DEF, scale by
    // 1 + AddCritDamage/100. See DefaultAddCritDamagePct below.)

    // Fraction of damage that gets through DEF, mirroring the server's DefValueLookup curve.
    public static double DefCut(int def)
    {
        if (def <= 0) return 1.0;
        if (def < 30) return 1.0 - def / 100.0;
        return Math.Max(0.1, Math.Pow(0.99, def - 30) - 0.3);
    }

    // Expected flat VIT soft-def (mean of the server's randomized formulas).
    private static double PlayerSubDef(int vit) =>
        0.3 * vit + 0.5 * Math.Max(0, vit * vit / 150.0 - 0.3 * vit) + vit / 2.0;

    private static double MonsterSubDef(int vit)
    {
        var r = vit / 20.0;
        return vit + 0.5 * r * r;
    }

    private static int HitStat(Combatant c) => c.Level + c.Dex + c.AddHit;
    private static int FleeStat(Combatant c) => c.Level + c.Agi + c.AddFlee;

    public static double HitChance(Combatant attacker, Combatant defender)
    {
        var rate = HitStat(attacker) + 75 - FleeStat(defender);
        return Math.Clamp(rate, 5, 100) / 100.0;
    }

    /// <summary>Mirror of the server's pre-modifier damage line:
    /// <c>(baseDamage + addDamage) * attackMultiplier * defCut - subDef + addFinalDamage</c>.
    /// We omit attackMultiplier (caller folds buff multipliers into atk1/atk2) and addFinalDamage (the
    /// server's <c>AddRefineAttackPower</c> stat isn't synced to the client). levelCut is applied last
    /// per the server's post-mod sequence; commutes mathematically because it's a scalar.</summary>
    private static double DamagePerHit(Combatant attacker, Combatant defender, double defenderSubDef, double levelCut, int addDamage = 0)
    {
        var baseAtk = (attacker.AtkMin + attacker.AtkMax) / 2.0; // expected roll over uniform[atk1, atk2]
        var dmg = (baseAtk + addDamage) * DefCut(defender.Def) - defenderSubDef;
        if (dmg < 1) dmg = 1;
        dmg *= levelCut;
        return Math.Max(1, dmg);
    }

    /// <summary>Exact server formula from CombatEntity.GetBaseCritRate:
    /// <c>critRate_perMille = (1 + Luk/3 + AddCrit) * 10 + Level/5</c>, rolled against 1000.
    /// Server's <c>CharacterStat.AddCrit</c> stat is in % units (it gets multiplied by 10 to enter the
    /// per-mille pool alongside the LUK contribution). Server bonuses we DON'T have access to via the
    /// PlayerUpdateStats packet — race-specific crit chance, weapon-class doubling for Katar — default
    /// to 0 since reading them would require a server-side stat-sync expansion.</summary>
    private static double CritChanceFromMods(Combatant me, CombatModifiers mods)
    {
        var luk = Math.Max(mods.AttackerLuk, 0);
        var addCritPct = Math.Max(mods.AttackerAddCrit, 0);   // in % units (server's GetStat AddCrit)
        var level = Math.Max(me.Level, 1);
        var perMille = (1 + luk / 3 + addCritPct) * 10 + level / 5;
        return Math.Clamp(perMille / 1000.0, 0.0, 1.0);
    }

    private static double CritDamageBonus(CombatModifiers mods) =>
        mods.AttackerAddCritDamage >= 0 ? mods.AttackerAddCritDamage / 100.0 : DefaultAddCritDamagePct / 100.0;

    /// <summary>Race-bonus add-damage from DemonBane / BeastBane mastery (server's CombatEntity.DamageHandling
    /// adds these flat-pre-DEF: DemonBane*3 vs Demon/Undead, BeastBane*5 vs Beast/Insect/Flying). The skill
    /// levels are tracked client-side via KnownSkills; piping them in lets the simulator catch the bonus
    /// without needing a server-side stat-packet expansion.</summary>
    private static int RaceMasteryBonus(CombatModifiers mods)
    {
        var bonus = 0;
        switch (mods.DefenderRace)
        {
            case SimRace.Demon:
            case SimRace.Undead:
                bonus += mods.AttackerDemonBaneLevel * 3;
                break;
            case SimRace.Beast:
            case SimRace.Insect:
                bonus += mods.AttackerBeastBaneLevel * 5;
                break;
        }
        return bonus;
    }

    /// <summary>Backwards-compatible overload — defaults the modifiers to "none" so old callers see the
    /// same numbers they used to.</summary>
    public static BattleForecast Forecast(Combatant me, Combatant monster, double winMargin, int maxRounds)
        => Forecast(me, monster, CombatModifiers.Default, winMargin, maxRounds);

    public static BattleForecast Forecast(Combatant me, Combatant monster, CombatModifiers mods, double winMargin, int maxRounds)
    {
        // Level-difference cut (player-vs-monster and monster-vs-player use different slopes/floors).
        var myLevelCut = Math.Clamp(1 - 0.015 * (monster.Level - me.Level), 0.1, 1.0);
        var monLevelCut = Math.Clamp(1 - 0.0025 * (me.Level - monster.Level), 0.5, 1.0);

        // addDamage pool (server adds these BEFORE multiplying by defCut): WeaponMastery (not exposed
        // client-side), DemonBane vs Demon/Undead, BeastBane vs Beast/Insect. Apply what we can read.
        var addDamage = RaceMasteryBonus(mods);

        // Normal (non-crit) damage: mean atk * post-DEF * minus VIT-subdef, then level cut.
        var myDmgNormal = DamagePerHit(me, monster, MonsterSubDef(monster.Vit), myLevelCut, addDamage);
        var monDmg = DamagePerHit(monster, me, PlayerSubDef(me.Vit), monLevelCut, 0);

        // Crit damage: server's crit path is *fundamentally different* from a flat 1.4x multiplier.
        //   * baseDamage is forced to atk2 (max roll, not average).
        //   * `flags |= IgnoreDefense` — DEF percentage cut is skipped entirely.
        //   * subDef (VIT soft-def) is still applied.
        //   * attackMultiplier multiplied by `1 + AddCritDamage/100` (default 40% on this server).
        // Mirror that here so a high-DEF mob (Rocker, Steel Chonchon) shows the correct upside of crit
        // builds instead of being underestimated.
        var critRawAtk = me.AtkMax > 0 ? me.AtkMax : (me.AtkMin + me.AtkMax) / 2.0;
        // Crit ignores DEF but addDamage is still added (pre-DEF in the server formula, but DEF is now 1.0).
        var critPre = (critRawAtk + addDamage) - MonsterSubDef(monster.Vit);
        if (critPre < 1) critPre = 1;
        var myDmgCrit = critPre * myLevelCut * (1 + CritDamageBonus(mods));

        // Apply element to BOTH normal and crit damage (size is disabled in Rebuild — table returns 100).
        var elementMod = ElementChart.GetModifier(mods.AttackerElement, mods.DefenderElement, mods.DefenderElementLevel);
        var sizeMod = WeaponSizeChart.GetModifier(mods.AttackerWeapon, mods.DefenderSize);
        var ratio = elementMod / 100.0 * sizeMod / 100.0;
        myDmgNormal *= ratio;
        myDmgCrit   *= ratio;
        if (myDmgNormal < 1 && elementMod > 0 && sizeMod > 0) myDmgNormal = 1;
        if (myDmgCrit   < 1 && elementMod > 0 && sizeMod > 0) myDmgCrit   = 1;

        var myHitChance = HitChance(me, monster);
        var monHitChance = HitChance(monster, me);

        var myInterval = me.AttackInterval > 0.05 ? me.AttackInterval : 1.5;
        var monInterval = monster.AttackInterval > 0.05 ? monster.AttackInterval : DefaultMonsterAttackInterval;

        // Blend: expected damage per swing = (1-p)·normal·hitChance + p·crit (crits ignore evasion).
        var critChance = CritChanceFromMods(me, mods);
        var expectedDmgPerSwing = (1 - critChance) * myDmgNormal * myHitChance
                                   + critChance     * myDmgCrit;

        var myMeleeDps = expectedDmgPerSwing / myInterval;
        var myDps = myMeleeDps + Math.Max(0, mods.ExtraSkillDps);
        var monDps = monDmg * monHitChance / monInterval;

        // FREE-KILL CASE: monster can't reach me + I can reach it. Mandragora (R5, plant) and Geographer
        // (R3, AiAggressiveImmobile) stand still — if I'm holding a bow (range 9) and standing at range
        // > the monster's range, the monster's attacks miss me entirely. Drop incoming DPS to zero so
        // CanWin becomes trivially true regardless of WinMargin.
        // The bot's POSITIONING still has to honour this (kite FSM keeps it outside the zone) — see
        // MakePathCost which now adds the mob's attack-zone to the hazard overlay so any walk near these
        // mobs prefers cells outside their range.
        if (mods.DefenderIsImmobile && mods.AttackerAttackRange > mods.DefenderAttackRange)
            monDps = 0;

        // hitsToKill: how many discrete "actions" (basic swings OR skill casts) to drop the monster.
        // For a NoAutoAttack caster, AtkMin/Max are zeroed → expectedDmgPerSwing collapses to 1, so
        // basic-attack count would be MaxHp / 1 = thousands — failing the maxRounds cap even when the
        // skill rotation kills in seconds. Use the skill-cast damage proxy (ExtraSkillDps × 2s typical
        // cast cycle) when it's larger than the swing damage; that's the actual "per-action" damage for
        // a caster.
        var perActionDmg = expectedDmgPerSwing;
        if (mods.ExtraSkillDps > 0)
        {
            var skillCastInterval = 2.0;
            var skillPerCast = mods.ExtraSkillDps * skillCastInterval;
            if (skillPerCast > perActionDmg) perActionDmg = skillPerCast;
        }
        var hitsToKill = (int)Math.Ceiling(monster.MaxHp / Math.Max(0.01, perActionDmg));
        var secsToKillMonster = myDps > 0.01 ? Math.Max(0, monster.MaxHp - mods.SkillBurstDamage) / myDps : double.PositiveInfinity;
        var secsToKillMe = monDps > 0.01 ? me.Hp / monDps : double.PositiveInfinity;

        // hitsToKill cap is the "is this fight too tanky for my tools" check — but when the mob can't
        // touch us (free-win case: monDps zeroed), the only real question is "do I deal any damage at
        // all". A patient Mage with a slow rotation against a high-HP plant is fine — it'll get there.
        // The time-based check still rejects "takes longer than I have time for".
        var hitsCapOk = hitsToKill <= maxRounds || monDps <= 0;

        var canWin = monster.MaxHp > 0 && me.Hp > 0
                     && hitsCapOk
                     && secsToKillMonster <= secsToKillMe * winMargin;

        var damageTaken = double.IsInfinity(secsToKillMonster) ? double.MaxValue : secsToKillMonster * monDps;
        var winChance = me.Hp > 0 ? Math.Clamp(100.0 * (me.Hp - damageTaken) / me.Hp, 0, 100) : 0;

        // Report the WEIGHTED-AVERAGE per-hit damage (what the bot actually deals on average) — including
        // crit upside. The previous code reported only the crit-scaled non-crit number; this matches
        // what get_telemetry's DamageDealt sees in practice.
        var avgPerHit = (1 - critChance) * myDmgNormal + critChance * myDmgCrit;
        return new BattleForecast(
            canWin,
            (int)Math.Round(avgPerHit), (int)Math.Round(monDmg),
            myHitChance, monHitChance,
            ClampFinite(secsToKillMonster), ClampFinite(secsToKillMe),
            winChance,
            elementMod, sizeMod, critChance, myDps);
    }

    /// <summary>JSON has no representation for ±Infinity, so when a forecast computes "effectively
    /// immortal" via Hp / 0 it would crash the MCP serializer. Map ±Infinity (and NaN) to a large
    /// finite sentinel — 999999 reads as "essentially forever" without breaking the wire format. The
    /// canWin decision was already made against secsToKillMe before this clamp, so squashing the
    /// display value doesn't change behavior.</summary>
    private static double ClampFinite(double v)
    {
        if (double.IsNaN(v)) return 0;
        if (double.IsPositiveInfinity(v)) return 999999;
        if (double.IsNegativeInfinity(v)) return -999999;
        return v;
    }

    // Default AddCritDamage stat. The server's CharacterStat.AddCritDamage is configurable per build
    // (gear/skills add to it); 40 reflects an unbuffed default in this codebase. If we later expose the
    // stat through SelfState we can plug it in directly via CombatModifiers.
    private const double DefaultAddCritDamagePct = 40;

    public static GroupForecast ForecastGroup(Combatant me, IReadOnlyList<Combatant> monsters, double winMargin, int maxRounds)
        => ForecastGroup(me, monsters, Array.Empty<CombatModifiers>(), CombatModifiers.Default, winMargin, maxRounds);

    /// <summary>Forecast fighting several monsters at once: the bot kills them one at a time (quickest-to-die
    /// first, to shed incoming damage fastest), but EVERY still-alive monster hits it the whole time. We win
    /// if the total damage taken stays within the same fraction of HP the 1v1 win condition allows. Element
    /// / size mods may differ per monster — pass a parallel array, or empty to default to neutral.</summary>
    public static GroupForecast ForecastGroup(Combatant me, IReadOnlyList<Combatant> monsters,
        IReadOnlyList<CombatModifiers> perMonsterMods, CombatModifiers playerMods,
        double winMargin, int maxRounds)
    {
        var n = monsters.Count;
        if (n == 0) return new GroupForecast(true, 0, 0, 0, me.Hp, 100, 100, 100, 0);

        var killTime = new double[n];
        var monDps = new double[n];
        var eleAcc = 0.0;
        var sizeAcc = 0.0;
        var feasible = me.Hp > 0;

        var critChance = CritChanceFromMods(me, playerMods);
        var myInterval = me.AttackInterval > 0.05 ? me.AttackInterval : 1.5;
        var critRawAtk = me.AtkMax > 0 ? me.AtkMax : (me.AtkMin + me.AtkMax) / 2.0;

        for (var i = 0; i < n; i++)
        {
            var mon = monsters[i];
            var mods = i < perMonsterMods.Count ? perMonsterMods[i] : CombatModifiers.Default;
            // DemonBane/BeastBane add pre-DEF, applied per-monster because each mob's race differs.
            var perMonMods = playerMods with { DefenderRace = mods.DefenderRace };
            var addDamage = RaceMasteryBonus(perMonMods);
            var myLevelCut = Math.Clamp(1 - 0.015 * (mon.Level - me.Level), 0.1, 1.0);
            var monLevelCut = Math.Clamp(1 - 0.0025 * (me.Level - mon.Level), 0.5, 1.0);

            var eleMod = ElementChart.GetModifier(playerMods.AttackerElement, mods.DefenderElement, mods.DefenderElementLevel);
            var szMod = WeaponSizeChart.GetModifier(playerMods.AttackerWeapon, mods.DefenderSize);
            eleAcc += eleMod;
            sizeAcc += szMod;
            var ratio = eleMod / 100.0 * szMod / 100.0;

            // Normal (DEF-applied) vs crit (DEF-ignored, atk2-roll, +AddCritDamage%) — mirrors Forecast.
            var normal = DamagePerHit(me, mon, MonsterSubDef(mon.Vit), myLevelCut, addDamage) * ratio;
            var critPre = (critRawAtk + addDamage) - MonsterSubDef(mon.Vit);
            if (critPre < 1) critPre = 1;
            var crit = critPre * myLevelCut * (1 + CritDamageBonus(playerMods)) * ratio;
            if (normal < 1 && eleMod > 0 && szMod > 0) normal = 1;
            if (crit   < 1 && eleMod > 0 && szMod > 0) crit   = 1;

            var hitChance = HitChance(me, mon);
            var myEffDmg = (1 - critChance) * normal * hitChance + critChance * crit;

            var monDmg = DamagePerHit(mon, me, PlayerSubDef(me.Vit), monLevelCut, 0) * HitChance(mon, me);
            var monInterval = mon.AttackInterval > 0.05 ? mon.AttackInterval : DefaultMonsterAttackInterval;
            var myDpsThis = myEffDmg / myInterval;
            // Free-kill case per the 1v1 path: if the mob can't reach us and we can hit it, it deals zero.
            // Applied per-monster so a mixed pack (one mobile + one immobile) handles each correctly.
            var perMonReachInfo = i < perMonsterMods.Count ? perMonsterMods[i] : CombatModifiers.Default;
            var atkRange = perMonReachInfo.AttackerAttackRange > 0
                ? perMonReachInfo.AttackerAttackRange
                : playerMods.AttackerAttackRange;
            if (perMonReachInfo.DefenderIsImmobile && atkRange > perMonReachInfo.DefenderAttackRange)
                monDps[i] = 0;
            else
                monDps[i] = monDmg / monInterval;
            killTime[i] = myDpsThis > 0.01 ? mon.MaxHp / myDpsThis : double.PositiveInfinity;
            var hits = (int)Math.Ceiling(mon.MaxHp / Math.Max(0.01, myEffDmg));
            if (hits > maxRounds || double.IsInfinity(killTime[i])) feasible = false;
        }

        // Process in ascending kill-time order so incoming DPS drops as quickly as it realistically can.
        var order = new int[n];
        for (var i = 0; i < n; i++) order[i] = i;
        var keys = (double[])killTime.Clone();
        Array.Sort(keys, order);

        var remainingDps = 0.0;
        for (var i = 0; i < n; i++) remainingDps += monDps[i];

        var totalKill = 0.0;
        var dmgTaken = 0.0;
        for (var k = 0; k < n; k++)
        {
            var idx = order[k];
            var t = killTime[idx];
            if (double.IsInfinity(t)) { dmgTaken = double.PositiveInfinity; totalKill = double.PositiveInfinity; break; }
            dmgTaken += remainingDps * t; // all monsters still alive during this kill keep hitting us
            remainingDps -= monDps[idx];
            totalKill += t;
        }

        // Skill DPS shaves total kill time across the whole pack — folded in as a flat extra DPS pool
        // since the bot's skill rotation isn't per-target-aware enough to attribute to one mob.
        var effectiveDps = playerMods.ExtraSkillDps > 0 && totalKill > 0
            ? (n / totalKill) * monsters[0].MaxHp + playerMods.ExtraSkillDps
            : 0;
        if (playerMods.ExtraSkillDps > 0 && !double.IsInfinity(totalKill) && totalKill > 0)
            totalKill *= (effectiveDps - playerMods.ExtraSkillDps) / Math.Max(0.01, effectiveDps);

        var canWin = feasible && dmgTaken <= me.Hp * winMargin;
        var capped = double.IsInfinity(dmgTaken) ? double.MaxValue : dmgTaken;
        var winChance = me.Hp > 0 ? Math.Clamp(100.0 * (me.Hp - capped) / me.Hp, 0, 100) : 0;
        return new GroupForecast(
            canWin, n, ClampFinite(totalKill), ClampFinite(dmgTaken), me.Hp, winChance,
            n > 0 ? eleAcc / n : 100,
            n > 0 ? sizeAcc / n : 100,
            ClampFinite(effectiveDps));
    }
}
