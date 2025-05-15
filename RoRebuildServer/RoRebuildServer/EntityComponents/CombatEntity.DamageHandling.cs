using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Networking;
using RebuildSharedData.Data;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.EntityComponents;

public partial class CombatEntity
{

    public (int atk1, int atk2) CalculateAttackPowerRange(bool isMagic)
    {
        var atk1 = 0;
        var atk2 = 0;

        if (Character.Type == CharacterType.Player)
        {
            if (!isMagic)
            {
                var str = GetEffectiveStat(CharacterStat.Str);
                var dex = GetEffectiveStat(CharacterStat.Dex);

                var mainStat = Player.WeaponClass == 12 ? dex : str;
                var secondaryStat = Player.WeaponClass == 12 ? str : dex;
                var weaponLvl = Player.Equipment.WeaponLevel;
                var weaponAttack = Player.Equipment.WeaponAttackPower;
                if (Player.WeaponClass == 12)
                    atk1 = (int)(dex * (0.8f + 0.2f * weaponLvl)); //more or less pre-renewal
                else
                    atk1 = (int)(float.Min(weaponAttack * 0.33f, mainStat) + dex * (0.8f + 0.2f * weaponLvl)); //kinda pre-renewal but primary stat makes up 1/3 of min

                atk2 = weaponAttack * (200 + mainStat) / 200; //more like post-renewal, primary stat adds 0.5% weapon atk
                if (atk1 > atk2)
                    atk1 = atk2;

                var statAtk = GetStat(CharacterStat.AddAttackPower) + mainStat + (secondaryStat / 5) + (mainStat / 10) * (mainStat / 10);
                var attackPercent = 100 + GetStat(CharacterStat.AddAttackPercent);
                if (Player.WeaponClass == 12 && Player.Equipment.AmmoId > 0 && Player.Equipment.AmmoType == AmmoType.Arrow) //bow with arrow
                    atk2 += Player.Equipment.AmmoAttackPower; //arrows don't affect min atk, only max

                atk1 = (statAtk + atk1 + Player.Equipment.MinRefineAtkBonus) * attackPercent / 100;
                atk2 = (statAtk + atk2 + Player.Equipment.MaxRefineAtkBonus) * attackPercent / 100;
            }
            else
            {
                var matkStat = GetEffectiveStat(CharacterStat.Int);
                var addMatk = GetStat(CharacterStat.AddMagicAttackPower);
                var statMatkMin = addMatk + matkStat + (matkStat / 7) * (matkStat / 7);
                var statMatkMax = addMatk + matkStat + (matkStat / 5) * (matkStat / 5);
                var magicPercent = 100 + GetStat(CharacterStat.AddMagicAttackPercent);
                atk1 = (statMatkMin + Player.Equipment.MinRefineAtkBonus) * magicPercent / 100;
                atk2 = (statMatkMax + Player.Equipment.MaxRefineAtkBonus) * magicPercent / 100;
            }
        }
        else
        {
            atk1 = !isMagic ? GetStat(CharacterStat.Attack) : GetStat(CharacterStat.MagicAtkMin);
            atk2 = !isMagic ? GetStat(CharacterStat.Attack2) : GetStat(CharacterStat.MagicAtkMax);

            if (!isMagic)
            {
                var attackPercent = 1f + (GetStat(CharacterStat.AddAttackPercent) / 100f);
                atk1 = (int)(atk1 * attackPercent);
                atk2 = (int)(atk2 * attackPercent);
            }
            else
            {
                var magicPercent = 1f + (GetStat(CharacterStat.AddMagicAttackPercent) / 100f);
                atk1 = (int)(atk1 * magicPercent);
                atk2 = (int)(atk2 * magicPercent);
            }
        }

        if (atk1 <= 0)
            atk1 = 1;
        if (atk2 < atk1)
            atk2 = atk1;

        return (atk1, atk2);
    }

    public DamageInfo CalculateCombatResultUsingSetAttackPower(CombatEntity target, AttackRequest req)
    {
        var atk1 = req.MinAtk;
        var atk2 = req.MaxAtk;
        var flags = req.Flags;
        var attackElement = req.Element;
        var attackMultiplier = req.AttackMultiplier;
        var attackerType = Character.Type;
        var defenderType = target.Character.Type;
        var defenderElement = target.GetElement();
        var baseElementType = GetAttackTypeForDefenderElement(defenderElement);

#if DEBUG
        if (!target.IsValidTarget(this, flags.HasFlag(AttackFlags.CanHarmAllies), true))
            throw new Exception("Entity attempting to attack an invalid target! This should be checked before calling CalculateCombatResultUsingSetAttackPower.");
#endif

        var baseDamage = GameRandom.NextInclusive(atk1, atk2);

        var isCrit = false;
        var evade = false;
        var srcLevel = GetStat(CharacterStat.Level);
        var targetLevel = target.GetStat(CharacterStat.Level);

        if (flags.HasFlag(AttackFlags.CanCrit))
        {
            //crit rate: 1%, + 0.3% per luk, + 0.1% per level
            var critRate = 10 + GetEffectiveStat(CharacterStat.Luck) * 3 + srcLevel + GetStat(CharacterStat.AddCrit);
            //counter crit: 0.2% per luck, 0.67% per level
            var counterCrit = target.GetEffectiveStat(CharacterStat.Luck) * 2 + targetLevel * 2 / 3;
            //should double crit for katar here
            if (target.HasBodyState(BodyStateFlags.Sleep) || CheckLuckModifiedRandomChanceVsTarget(target, critRate - counterCrit, 1000))
                isCrit = true;
        }
        else
        {
            if (Character.Type == CharacterType.Monster && target.HasBodyState(BodyStateFlags.Sleep) && req.SkillSource == CharacterSkill.None)
                isCrit = true; //even if crit isn't allowed, monster auto attacks will still guarantee crit if the target is sleeping
        }

        if (flags.HasFlag(AttackFlags.GuaranteeCrit))
            isCrit = true;

        if (isCrit)
        {
            baseDamage = atk2;
            evade = false;
            attackMultiplier *= 1 + GetStat(CharacterStat.AddCritDamage) / 100f;
            flags |= AttackFlags.IgnoreDefense;
        }

        var eleMod = 100;
        if (!flags.HasFlag(AttackFlags.NoElement))
        {
            if (attackElement == AttackElement.None)
            {
                //ghost armor has no effect on monster attacks if they are launched with AttackElement None
                if (defenderElement == CharacterElement.Ghost1 && attackerType == CharacterType.Monster && target.Character.Type == CharacterType.Player)
                    defenderElement = CharacterElement.Neutral1;

                attackElement = AttackElement.Neutral;
                if (Character.Type == CharacterType.Player)
                {
                    attackElement = Player.Equipment.WeaponElement;
                    if (Player.WeaponClass == 12) //bows
                    {
                        var arrowElement = Player.Equipment.AmmoElement;
                        if (arrowElement != AttackElement.None && arrowElement != AttackElement.Neutral)
                            attackElement = arrowElement;
                    }

                    var overrideElement = (AttackElement)GetStat(CharacterStat.EndowAttackElement);
                    if (overrideElement > 0)
                        attackElement = overrideElement;
                }
            }

            if (defenderType == CharacterType.Player && attackElement != AttackElement.Special)
                eleMod -= target.GetStat(CharacterStat.AddResistElementNeutral + (int)attackElement);
            if (attackerType == CharacterType.Player)
            {

                eleMod += GetStat(CharacterStat.AddAttackElementNeutral + (int)baseElementType);
            }

            if (defenderElement != CharacterElement.None)
                eleMod = eleMod * DataManager.ElementChart.GetAttackModifier(attackElement, defenderElement) / 100;
        }

        var racialMod = 100;
        var rangeMod = 100;
        var sizeMod = 100;
        if (!flags.HasFlag(AttackFlags.NoDamageModifiers))
        {
            var isRanged = req.Flags.HasFlag(AttackFlags.Ranged);
            var atkSize = attackerType == CharacterType.Monster ? Character.Monster.MonsterBase.Size : CharacterSize.Medium;
            var defSize = defenderType == CharacterType.Monster ? target.Character.Monster.MonsterBase.Size : CharacterSize.Medium;

            if (!isRanged)
            {
                if (req.SkillSource == CharacterSkill.None)
                    isRanged = GetStat(CharacterStat.Range) >= 4;
                else
                    isRanged = Character.Position.SquareDistance(target.Character.Position) >= 4;
            }

            if (defenderType == CharacterType.Player) //monsters can't get race reductions
            {
                var sourceRace = GetRace();
                racialMod -= target.GetStat(CharacterStat.AddResistRaceFormless + (int)sourceRace);

                if (isRanged)
                    rangeMod -= target.GetStat(CharacterStat.AddResistRangedAttack);

                sizeMod -= target.GetStat(CharacterStat.AddResistSmallSize + (int)atkSize);
            }

            if (attackerType == CharacterType.Player && flags.HasFlag(AttackFlags.Physical)) //only players and physical attacks get these bonuses
            {
                var targetRace = target.GetRace();
                racialMod += GetStat(CharacterStat.AddAttackRaceFormless + (int)targetRace);

                //masteries, demonbane, etc
                if (flags.HasFlag(AttackFlags.Physical) && (target.GetRace() == CharacterRace.Demon || baseElementType == AttackElement.Undead))
                    baseDamage += Player.MaxLearnedLevelOfSkill(CharacterSkill.DemonBane) * 3;
                baseDamage += GetStat(CharacterStat.WeaponMastery);

                if (isRanged)
                    rangeMod += GetStat(CharacterStat.AddAttackRangedAttack);

                sizeMod += GetStat(CharacterStat.AddAttackSmallSize + (int)defSize);
            }
        }

        var attackerPenalty = target.GetAttackerPenalty(Entity);

        if (flags.HasFlag(AttackFlags.Physical) && !flags.HasFlag(AttackFlags.IgnoreEvasion))
            evade = !TestHitVsEvasion(target, req.AccuracyRatio, attackerPenalty * 10);

        if (target.HasBodyState(BodyStateFlags.Hidden) && !flags.HasFlag(AttackFlags.IgnoreEvasion)
                                                            && !flags.HasFlag(AttackFlags.CanAttackHidden)
                                                            && !(attackElement == AttackElement.Earth && flags.HasFlag(AttackFlags.Magical))) //earth magic breaks hide
            evade = true;

        if (target.Character.Type == CharacterType.Player && flags.HasFlag(AttackFlags.Physical) &&
            req.SkillSource == CharacterSkill.None)
        {
            var lucky = target.Player.GetEffectiveStat(CharacterStat.PerfectDodge);
            if (CheckLuckModifiedRandomChanceVsTarget(target, lucky, 1000))
                evade = true; //should have a special result for lucky dodge
        }

        if (!evade && flags.HasFlag(AttackFlags.Physical) && !flags.HasFlag(AttackFlags.IgnoreNullifyingGroundMagic))
        {
            var distance = target.Character.Position.DistanceTo(Character.Position);
            if (distance >= 5)
            {
                if (target.HasStatusEffectOfType(CharacterStatusEffect.Pneuma))
                {
                    evade = true;
                    isCrit = false;
                }
            }
            else
            {
                if (target.HasStatusEffectOfType(CharacterStatusEffect.SafetyWall))
                {
                    if (Character.Map!.TryGetAreaOfEffectAtPosition(target.Character.Position, CharacterSkill.SafetyWall, out var effect) && effect.Value1 > 0)
                    {
                        evade = true;
                        isCrit = false;
                        effect.TriggerNpcEvent(target);
                    }
                }
            }
        }

        var defCut = 1f;
        var subDef = 0;
        var vit = target.GetEffectiveStat(CharacterStat.Vit);
        if (flags.HasFlag(AttackFlags.Physical) && !flags.HasFlag(AttackFlags.IgnoreDefense))
        {
            //armor def
            var def = target.GetEffectiveStat(CharacterStat.Def);
            if (target.Character.Type == CharacterType.Player)
            {
                //+20% bonus (for non-upgrade def), in place of penalty for upgrade def
                def += target.GetStat(CharacterStat.Def) * 20 / 100;
            }

            //soft def
            if (!flags.HasFlag(AttackFlags.IgnoreSubDefense))
            {
                if (target.Character.Type == CharacterType.Player)
                {
                    //this formula is weird, but it is official
                    //your vit defense is a random number between 80% (30% of which steps up every 10 vit)
                    //you also gain a random bonus that kicks in at 46 def and increases at higher values
                    var vit30Percent = 3 * vit / 10;
                    var vitRng = vit * vit / 150 - vit30Percent;
                    if (vitRng < 1) vitRng = 1;
                    subDef = (vit30Percent + GameRandom.NextInclusive(0, 20000) % vitRng + vit / 2);

                    //attacker penalty (players only)
                    if (attackerPenalty > 0)
                    {
                        def -= 5 * def * attackerPenalty / 100;
                        subDef -= 5 * subDef * attackerPenalty / 100;
                    }

                    if (def < 0) def = 0;
                    if (subDef < 0) subDef = 0;
                }
                else
                {
                    //monsters vit defense is also weird
                    var vitRng = (vit / 20) * (vit / 20);
                    if (vitRng <= 0)
                        subDef = vit;
                    else
                        subDef = vit + GameRandom.NextInclusive(0, 20000) % vitRng;
                }


                subDef = subDef * (100 + target.GetStat(CharacterStat.AddSoftDefPercent)) / 100;
            }

            //convert def to damage reduction %
            defCut = MathHelper.DefValueLookup(def);

            if (def >= 200)
                subDef = 999999;
        }

        if ((flags & AttackFlags.Magical) > 0 && (flags & AttackFlags.IgnoreDefense) <= 0) 
        {
            var mDef = target.GetEffectiveStat(CharacterStat.MDef);
            defCut = MathHelper.DefValueLookup(mDef); //for now players have different def calculations
            if (!flags.HasFlag(AttackFlags.IgnoreSubDefense))
                subDef = target.GetEffectiveStat(CharacterStat.Int) + vit / 2;

            if (mDef >= 200)
                subDef = 999999;
        }

        var damage = (int)((baseDamage * attackMultiplier * defCut - subDef) * (eleMod / 100f) * (racialMod / 100f) * (rangeMod / 100f) * (sizeMod / 100f));
        if (damage < 1)
            damage = 1;

        var lvCut = 1f;
        if (target.Character.Type == CharacterType.Monster)
        {
            //players deal 1.5% less damage per level they are below a monster, to a max of -90%
            lvCut -= 0.015f * (targetLevel - srcLevel);
            lvCut = float.Clamp(lvCut, 0.1f, 1f);
        }
        else
        {
            //monsters deal 0.5% less damage per level they are below the player, to a max of -50%
            lvCut -= 0.005f * (targetLevel - srcLevel);
            lvCut = float.Clamp(lvCut, 0.5f, 1f);
        }

        damage = (int)(lvCut * damage);

        if (damage < 1)
            damage = 1;

        if (eleMod == 0 || evade)
            damage = 0;

        var res = AttackResult.NormalDamage;
        if (damage == 0)
            res = AttackResult.Miss;

        if (res == AttackResult.NormalDamage && isCrit)
            res = AttackResult.CriticalDamage;

        var di = PrepareTargetedSkillResult(target, req.SkillSource);
        di.Result = res;
        di.Damage = damage;
        di.HitCount = (byte)req.HitCount;

        //arrow travel time
        if (Character.Type == CharacterType.Player && Player.WeaponClass == 12 && req.SkillSource == CharacterSkill.None)
            di.Time += Character.Position.DistanceTo(target.Character.Position) / ServerConfig.ArrowTravelTime;

        if (damage > 0 && flags.HasFlag(AttackFlags.Physical))
            di.Flags |= DamageApplicationFlags.PhysicalDamage;
        if (damage > 0 && flags.HasFlag(AttackFlags.Magical))
            di.Flags |= DamageApplicationFlags.MagicalDamage;

        if (!flags.HasFlag(AttackFlags.NoTriggerOnAttackEffects))
        {
            if (statusContainer != null)
                statusContainer.OnAttack(ref di);

            if (target.statusContainer != null)
                target.statusContainer.OnCalculateDamageTaken(ref req, ref di);

            if (di.Damage > 0 && (di.Result == AttackResult.NormalDamage || di.Result == AttackResult.CriticalDamage))
            {
                TriggerOnAttackEffects(target, req, ref di);
                target.TriggerWhenAttackedEffects(this, req, ref di);
            }
        }

        return di;
    }

    private void ApplyQueuedCombatResult(ref DamageInfo di)
    {

        if (Character.State == CharacterState.Dead || !Entity.IsAlive() || Character.IsTargetImmune || Character.Map == null)
            return;

        if (di.Source.Type == EntityType.Player && !di.Source.IsAlive())
            return;

        //if (di.Source.IsAlive() && di.Source.TryGet<WorldObject>(out var enemy))
        //{
        //    if (!enemy.IsActive || enemy.Map != Character.Map)
        //        continue;
        //    if (enemy.State == CharacterState.Dead)
        //        continue; //if the attacker is dead we bail

        //    //if (enemy.Position.SquareDistance(Character.Position) > 31)
        //    //    continue;
        //}
        //else
        //    continue;

        if (di.Damage < 0 && di.Result == AttackResult.Heal)
        {
            HealHp(-di.Damage, true, HealType.HealSkill);
            return;
        }

        if (Character.State == CharacterState.Sitting)
            Character.State = CharacterState.Idle;

        if (TryGetStatusContainer(out var targetStatus))
            targetStatus.OnTakeDamage(ref di);

        var damage = di.Damage * di.HitCount;

        //inform clients the player was hit and for how much
        var delayTime = GetTiming(TimingStat.HitDelayTime);

        var knockback = di.KnockBack;
        var hasHitStop = !di.Flags.HasFlag(DamageApplicationFlags.NoHitLock);

        if (Character.Type == CharacterType.Monster && delayTime > 0.15f && Character.Monster.CurrentAiState != MonsterAiState.StateIdle)
            delayTime = 0.15f;
        if (!hasHitStop)
            delayTime = 0f;
        if (di.Flags.HasFlag(DamageApplicationFlags.ReducedHitLock))
            delayTime = 0.1f;

        var oldPosition = Character.Position;

        var sendMove = di.Flags.HasFlag(DamageApplicationFlags.UpdatePosition);

        if (Character.Type == CharacterType.Monster && knockback > 0 && Character.Monster.MonsterBase.Special == CharacterSpecialType.Boss)
        {
            knockback = 0;
            delayTime = 0.03f;
            if (Character.IsMoving)
                sendMove = true;
        }

        Character.AddMoveLockTime(delayTime);

        if (knockback > 0)
        {
            var atkSrc = di.AttackPosition;
            if (Character.Position == atkSrc)
                atkSrc = atkSrc.AddDirectionToPosition((Direction)GameRandom.Next(0, 8));
            var pos = Character.Map.WalkData.CalcKnockbackFromPosition(Character.Position, atkSrc, di.KnockBack);
            if (Character.Position != pos)
                Character.Map.ChangeEntityPosition3(Character, Character.WorldPosition, pos, false);

            Character.StopMovingImmediately();
            sendMove = false;
        }

        if (Character.Type == CharacterType.Monster)
            Character.Monster.NotifyOfAttack(ref di);

        Character.Map?.AddVisiblePlayersAsPacketRecipients(Character);
        if (sendMove)
            CommandBuilder.SendMoveEntityMulti(Character);
        CommandBuilder.SendHitMulti(Character, damage, hasHitStop);

        if (IsCasting)
        {
            if ((CastInterruptionMode == CastInterruptionMode.InterruptOnDamage && di.Damage > 0)
                || (CastInterruptionMode != CastInterruptionMode.NeverInterrupt && knockback > 0)
                || (CastInterruptionMode == CastInterruptionMode.InterruptOnSkill && di.AttackSkill != CharacterSkill.None))
            {
                //character casts aren't interrupted by attacks if they are close to executing
                if (Character.Type != CharacterType.Player || CastTimeRemaining > 0.2f)
                {
                    IsCasting = false;
                    CommandBuilder.StopCastMulti(Character);
                }
            }
        }

        CommandBuilder.ClearRecipients();

        if (!di.Target.IsNull() && di.Source.IsAlive())
            Character.LastAttacked = di.Source;

        var ec = di.Target.Get<CombatEntity>();
        var hp = GetStat(CharacterStat.Hp);
        hp -= damage;

        SetStat(CharacterStat.Hp, hp);

        if (hp <= 0)
        {
            ResetSkillDamageCooldowns();

            if (Character.Type == CharacterType.Monster)
            {
                Character.ResetState();
                var monster = Entity.Get<Monster>();

                monster.CallDeathEvent();

                if (GetStat(CharacterStat.Hp) > 0)
                {
                    //death is cancelled!
                    return;
                }

                monster.BoostDamageContributionOfFirstAttacker();

                if (DataManager.MvpMonsterCodes.Contains(monster.MonsterBase.Code))
                    monster.RewardMVP();

                monster.Die();
                DamageQueue.Clear();

                return;
            }

            if (Character.Type == CharacterType.Player)
            {
                SetStat(CharacterStat.Hp, 0);
                Player.Die();
                return;
            }

            return;
        }

        if (oldPosition != Character.Position)
        {
            statusContainer?.OnMove(oldPosition, Character.Position);
            Character.Map?.TriggerAreaOfEffectForCharacter(Character, oldPosition, Character.Position);
        }
    }
}