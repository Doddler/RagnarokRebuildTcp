using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.Data.Player;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Simulation.Skills;
using RoRebuildServer.Simulation.Util;
using System;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace RoRebuildServer.EntityComponents;

[EntityComponent(new[] { EntityType.Player, EntityType.Monster })]
public class CombatEntity : IEntityAutoReset
{
    public Entity Entity;
    public WorldObject Character = null!;
    public Player Player = null!;

    public int Faction;
    public int Party;
    public bool IsTargetable;
    public float CastingTime;
    public bool IsCasting;

    public SkillCastInfo CastingSkill { get; set; }
    public SkillCastInfo QueuedCastingSkill { get; set; }

    [EntityIgnoreNullCheck]
    private int[] statData = new int[(int)CharacterStat.CharacterStatsMax];

    [EntityIgnoreNullCheck]
    private float[] timingData = new float[(int)TimingStat.TimingStatsMax];

    [EntityIgnoreNullCheck]
    private Dictionary<CharacterSkill, float> skillCooldowns = new();

    [EntityIgnoreNullCheck]
    public List<DamageInfo> DamageQueue { get; set; } = null!;

    public int GetStat(CharacterStat type) => statData[(int)type];
    public float GetTiming(TimingStat type) => timingData[(int)type];
    public void SetStat(CharacterStat type, int val) => statData[(int)type] = val;
    public void SetTiming(TimingStat type, float val) => timingData[(int)type] = val;

    public bool IsSkillOnCooldown(CharacterSkill skill) => skillCooldowns.TryGetValue(skill, out var t) && t > Time.ElapsedTimeFloat;
    public void SetSkillCooldown(CharacterSkill skill, float val) => skillCooldowns[skill] = Time.ElapsedTimeFloat + val;
    public void ResetSkillCooldowns() => skillCooldowns.Clear();

    public void Reset()
    {
        Entity = Entity.Null;
        Character = null!;
        Player = null!;
        Faction = -1;
        Party = -1;
        IsTargetable = true;
        IsCasting = false;
        skillCooldowns.Clear();
        
        for (var i = 0; i < statData.Length; i++)
            statData[i] = 0;
    }

    public void Heal(int hp, int hp2, bool showValue = false)
    {
        if (hp2 != -1 && hp2 > hp)
            hp = GameRandom.NextInclusive(hp, hp2);

        var curHp = GetStat(CharacterStat.Hp);
        var maxHp = GetStat(CharacterStat.MaxHp);

        hp += (int)(maxHp * 0.06f); //kinda cheat and force give an extra 6% hp back

        if (curHp + hp > maxHp)
            hp = maxHp - curHp;

        SetStat(CharacterStat.Hp, curHp + hp);

        if (Character.Map == null)
            return;

        Character.Map.GatherPlayersForMultiCast(Character);
        CommandBuilder.SendHealMulti(Character, hp, HealType.Item);
        CommandBuilder.ClearRecipients();
    }

    public void FullRecovery(bool hp, bool mp)
    {
        if (hp)
            SetStat(CharacterStat.Hp, GetStat(CharacterStat.MaxHp));
        if (mp)
            SetStat(CharacterStat.Mp, GetStat(CharacterStat.MaxMp));
    }

    public bool IsValidAlly(CombatEntity source)
    {
        if (this == source)
            return false;
        if (Entity.IsNull() || !Entity.IsAlive())
            return false;
        if (!Character.IsActive || Character.State == CharacterState.Dead)
            return false;
        if (source.Character.Map != Character.Map)
            return false;

        if (source.Entity.Type != Character.Entity.Type)
            return false;

        if (Character.ClassId >= 1000 && Character.ClassId < 4000)
            return false; //hack

        return true;
    }

    public bool IsValidTarget(CombatEntity? source)
    {
        if (this == source)
            return false;
        if (Entity.IsNull() || !Entity.IsAlive())
            return false;
        if (!Character.IsActive || Character.State == CharacterState.Dead)
            return false;
        if (Character.Map == null)
            return false;
        if (Character.Hidden)
            return false;

        if (Character.IsTargetImmune)
            return false;
        //if (source.Character.ClassId == Character.ClassId)
        //    return false;
        if (source != null)
        {
            if (source.Character.Map != Character.Map)
                return false;
            if (source.Entity.Type == EntityType.Player && Character.Entity.Type == EntityType.Player)
                return false;
            if (source.Entity.Type == EntityType.Monster && Character.Entity.Type == EntityType.Monster)
                return false;
        }

        //if (source.Entity.Type == EntityType.Player)
        //    return false;

        if (Character.ClassId >= 1000 && Character.ClassId < 4000)
            return false; //hack
        //if (source.Party == Party)
        //    return false;
        //if (source.Faction == Faction)
        //    return false;

        return true;
    }

    public bool CanAttackTarget(WorldObject target, int range = -1)
    {
        if (range == -1)
            range = GetStat(CharacterStat.Range);
        if (!target.CombatEntity.IsTargetable)
            return false;
        if (DistanceCache.IntDistance(Character.Position, target.Position) > range)
            return false;
        if (Character.Map == null || !Character.Map.WalkData.HasLineOfSight(Character.Position, target.Position))
            return false;
        return true;
    }

    public bool CanAttackTargetFromPosition(WorldObject target, Position position)
    {
        if (!target.CombatEntity.IsTargetable)
            return false;
        if (DistanceCache.IntDistance(position, target.Position) > GetStat(CharacterStat.Range))
            return false;
        if (Character.Map == null || !Character.Map.WalkData.HasLineOfSight(position, target.Position))
            return false;
        return true;
    }

    public void QueueDamage(DamageInfo info)
    {
        DamageQueue.Add(info);
        if (DamageQueue.Count > 1)
            DamageQueue.Sort((a, b) => a.Time.CompareTo(b.Time));
    }

    public void ClearDamageQueue()
    {
        DamageQueue.Clear();
    }

    public void DistributeExperience()
    {
        var list = EntityListPool.Get();

        var mon = Character.Entity.Get<Monster>();
        var exp = mon.MonsterBase.Exp;

        if (Character.Map == null)
            return;

        Character.Map.GatherPlayersInRange(Character, 12, list, false);
        Character.Map.GatherPlayersForMultiCast(Character);

        foreach (var e in list)
        {
            if (e.IsNull() || !e.IsAlive())
                continue;

            var player = e.Get<Player>();

            var level = player.GetData(PlayerStat.Level);

            if (level >= 99)
                continue;

            var curExp = player.GetData(PlayerStat.Experience);
            curExp += exp;

            var requiredExp = DataManager.ExpChart.ExpRequired[level];

            CommandBuilder.SendExpGain(player, exp);

            if (curExp < requiredExp)
            {
                player.SetData(PlayerStat.Experience, curExp);
                continue;
            }

            while (curExp >= requiredExp && level < 99)
            {
                curExp -= requiredExp;

                player.LevelUp();
                level++;

                if (level < 99)
                    requiredExp = DataManager.ExpChart.ExpRequired[level];
            }

            player.SetData(PlayerStat.Experience, curExp);

            CommandBuilder.LevelUp(player.Character, level);
            CommandBuilder.SendHealMulti(player.Character, 0, HealType.None);
        }

        CommandBuilder.ClearRecipients();

        EntityListPool.Return(list);
    }

    private void FinishCasting()
    {
        SkillHandler.ExecuteSkill(CastingSkill, this);
        IsCasting = false;
        if(Character.Type == CharacterType.Monster)
            Character.Monster.RunCastSuccessEvent();
    }
    
    public void ResumeQueuedSkillAction()
    {
        Character.QueuedAction = QueuedAction.None;

        if (QueuedCastingSkill.Level <= 0)
            return;

        if (QueuedCastingSkill.TargetedPosition != Position.Invalid)
        {
            AttemptStartGroundTargetedSkill(QueuedCastingSkill.TargetedPosition, QueuedCastingSkill.Skill, QueuedCastingSkill.Level, QueuedCastingSkill.CastTime);
            return;
        }

        var target = QueuedCastingSkill.TargetEntity.GetIfAlive<CombatEntity>();
        if (target == null) return;
        
        if (target == this)
        {
            AttemptStartSelfTargetSkill(QueuedCastingSkill.Skill, QueuedCastingSkill.Level, QueuedCastingSkill.CastTime);
            return;
        }

        AttemptStartSingleTargetSkillAttack(target, QueuedCastingSkill.Skill, QueuedCastingSkill.Level, QueuedCastingSkill.CastTime);
    }

    public void QueueCast(SkillCastInfo skillInfo)
    {
        QueuedCastingSkill = skillInfo;
        Character.QueuedAction = QueuedAction.Cast;
    }

    public bool AttemptStartGroundTargetedSkill(Position target, CharacterSkill skill, int level, float castTime = -1)
    {
        Character.QueuedAction = QueuedAction.None;
        QueuedCastingSkill.Clear();

        if (Character.State == CharacterState.Dead)
        {
            ServerLogger.LogError($"Cannot attempt a skill action {skill} while dead! " + Environment.StackTrace);
            return false;
        }

        var skillInfo = new SkillCastInfo()
        {
            Skill = skill,
            Level = level,
            CastTime = castTime,
            TargetedPosition = target,
            Range = (byte)SkillHandler.GetSkillRange(this, skill, level)
        };

        if (IsCasting) //if we're already casting, queue up the next cast
        {
            QueueCast(skillInfo);
            return true;
        }

        if (Character.State == CharacterState.Moving) //if we are already moving, queue the skill action
        {
            if (Character.Type == CharacterType.Player)
                Character.Player.ClearTarget(); //if we are currently moving we should dequeue attacking so we don't chase after casting

            Character.ShortenMovePath(); //for some reason shorten move path cancels the queued action and I'm too lazy to find out why
            QueueCast(skillInfo);

            return true;
        }

        if (Character.AttackCooldown > Time.ElapsedTimeFloat)
        {
            QueueCast(skillInfo);
            return true;
        }

        CastingSkill = skillInfo;
        Character.FacingDirection = DistanceCache.Direction(Character.Position, target);

        if (castTime < 0f)
            castTime = SkillHandler.GetSkillCastTime(skillInfo.Skill, this, null, skillInfo.Level);
        if (castTime <= 0)
            ExecuteQueuedSkillAttack();
        else
        {
            IsCasting = true;
            CastingTime = Time.ElapsedTimeFloat + castTime;

            Character.Map?.GatherPlayersForMultiCast(Character);
            CommandBuilder.StartCastGroundTargetedMulti(Character, target, skillInfo.Skill, skillInfo.Level, skillInfo.Range, castTime);
            CommandBuilder.ClearRecipients();
        }
        return true;
    }

    public bool AttemptStartSelfTargetSkill(CharacterSkill skill, int level, float castTime = -1f)
    {
        Character.QueuedAction = QueuedAction.None;
        QueuedCastingSkill.Clear();

        if (Character.State == CharacterState.Dead)
        {
            ServerLogger.LogError($"Cannot attempt a skill action {skill} while dead! " + Environment.StackTrace);
            return false;
        }

        var skillInfo = new SkillCastInfo()
        {
            TargetEntity = Entity,
            Skill = skill,
            Level = level,
            CastTime = castTime,
            TargetedPosition = Position.Invalid
        };


        if (IsCasting) //if we're already casting, queue up the next cast
        {
            QueueCast(skillInfo);
            return true;
        }

        if (Character.State == CharacterState.Moving) //if we are already moving, queue the skill action
        {
            if (Character.Type == CharacterType.Player)
                Character.Player.ClearTarget(); //if we are currently moving we should dequeue attacking so we don't chase after casting

            Character.ShortenMovePath(); //for some reason shorten move path cancels the queued action and I'm too lazy to find out why
            QueueCast(skillInfo);

            return true;
        }
        
        if (Character.AttackCooldown > Time.ElapsedTimeFloat)
        {
            QueueCast(skillInfo);
            return true;
        }

        CastingSkill = skillInfo;

        if (castTime < 0f)
            castTime = SkillHandler.GetSkillCastTime(skillInfo.Skill, this, null, skillInfo.Level);
        if (castTime <= 0)
            ExecuteQueuedSkillAttack();
        else
        {
            IsCasting = true;
            CastingTime = Time.ElapsedTimeFloat + castTime;

            Character.Map?.GatherPlayersForMultiCast(Character);
            CommandBuilder.StartCastMulti(Character, null, skillInfo.Skill, skillInfo.Level, castTime);
            CommandBuilder.ClearRecipients();
        }
        return true;
    }
    
    public bool AttemptStartSingleTargetSkillAttack(CombatEntity target, CharacterSkill skill, int level, float castTime = -1f)
    {
        Character.QueuedAction = QueuedAction.None;
        QueuedCastingSkill.Clear();

        if (Character.State == CharacterState.Dead)
        {
            ServerLogger.LogError("Cannot attempt a skill action while dead! " + Environment.StackTrace);
            return false;
        }

        if (Character.Type == CharacterType.Player && !Character.Player.VerifyCanUseSkill(skill, level))
        {
            ServerLogger.Log($"Player {Character.Name} attempted to use skill {skill} lvl {level}, but they cannot use that skill.");
            Character.QueuedAction = QueuedAction.None;
            return false;
        }

        var skillInfo = new SkillCastInfo()
        {
            TargetEntity = target.Entity,
            Skill = skill,
            Level = level,
            CastTime = castTime,
            TargetedPosition = Position.Invalid
        };

        var range = SkillHandler.GetSkillRange(this, skill, level);

        if (IsCasting) //if we're already casting, queue up the next cast
        {
            QueueCast(skillInfo);
            return true;
        }

        if (Character.State == CharacterState.Moving) //if we are already moving, queue the skill action
        {
            if (Character.Type == CharacterType.Player)
                Character.Player.ClearTarget(); //if we are currently moving we should dequeue attacking so we don't chase after casting

            Character.ShortenMovePath(); //for some reason shorten move path cancels the queued action and I'm too lazy to find out why
            QueueCast(skillInfo);

            return true;
        }

        if (Character.Position.DistanceTo(target.Character.Position) > range) //if we are out of range, try to move closer
        {
            if (Character.Type == CharacterType.Player)
            {
                Character.Player.ClearTarget();
                if (Character.MoveLockTime > Time.ElapsedTimeFloat) //we need to queue a cast so we both move and cast once the lock ends
                {
                    QueueCast(skillInfo);
                    return true;
                }
            }

            if (Character.TryMove(target.Character.Position, range))
            {
                QueueCast(skillInfo);
                return true;
            }

            return false;
        }

        var res = SkillHandler.ValidateTarget(skillInfo, this);

        if (res == SkillValidationResult.Success)
        {
            CastingSkill = skillInfo;

            if (Character.AttackCooldown > Time.ElapsedTimeFloat)
            {
                //var duration = Character.ActionDelay - Time.ElapsedTimeFloat;
                //ServerLogger.LogWarning($"Entity {Character.Name} currently in an action delay({duration} => {Character.ActionDelay}/{Time.ElapsedTimeFloat}), it's skill cast will be queued.");
                QueueCast(skillInfo);
                return true;
            }
            
            Character.FacingDirection = DistanceCache.Direction(Character.Position, target.Character.Position);
            //if(Character.Type == CharacterType.Player)
            //    Character.Player.ChangeTarget(target.Character);

            if(castTime < 0f)
                castTime = SkillHandler.GetSkillCastTime(skillInfo.Skill, this, target, skillInfo.Level);
            if (castTime <= 0)
                ExecuteQueuedSkillAttack();
            else
            {
                IsCasting = true;
                CastingTime = Time.ElapsedTimeFloat + castTime;

                Character.Map?.GatherPlayersForMultiCast(Character);
                CommandBuilder.StartCastMulti(Character, target.Character, skillInfo.Skill, skillInfo.Level, castTime);
                CommandBuilder.ClearRecipients();
            }
            return true;
        }
        else
        {
            if (Character.Type == CharacterType.Player)
                CommandBuilder.SkillFailed(Character.Player, res);
        }

        return false;
    }

    public void ExecuteQueuedSkillAttack()
    {
        //if (!QueuedCastingSkill.IsValid)
        //    throw new Exception($"We shouldn't be attempting to execute a queued action when no queued action exists!");
        //CastingSkill = QueuedCastingSkill;
        var res = SkillHandler.ValidateTarget(CastingSkill, this);
        if (res != SkillValidationResult.Success)
        {
#if DEBUG
            ServerLogger.Log($"Character {Character} failed a queued skill attack with the validation result: {res}");
            return;
#endif
        }

        SkillHandler.ExecuteSkill(CastingSkill, this);
        CastingSkill.Clear();
        QueuedCastingSkill.Clear();
        Character.ResetSpawnImmunity();
    }

    public DamageInfo CalculateCombatResult(CombatEntity target, float attackMultiplier, int hitCount, AttackFlags flags, AttackElement element = AttackElement.None)
    {
#if DEBUG
        if (!target.IsValidTarget(this))
            throw new Exception("Entity attempting to attack an invalid target! This should be checked before calling PerformMeleeAttack.");
#endif

        var atk1 = GetStat(CharacterStat.Attack);
        var atk2 = GetStat(CharacterStat.Attack2);

        if (Character.Type == CharacterType.Monster && flags.HasFlag(AttackFlags.Magical))
        {
            atk1 /= 2;
            atk2 /= 2;
        }

        if (atk1 <= 0)
            atk1 = 1;
        if (atk2 < atk1)
            atk2 = atk1;

        var baseDamage = GameRandom.NextInclusive(atk1, atk2);

        var eleMod = 100;
        if (target.Character.Type == CharacterType.Monster)
        {
            var mon = target.Character.Monster;
            if (element == AttackElement.None)
                element = AttackElement.Neutral; //for now default to neutral, but we should pull from the weapon here if none is set
            eleMod = DataManager.ElementChart.GetAttackModifier(element, mon.MonsterBase.Element);
        }

        var defCut = MathF.Pow(0.99f, target.GetStat(CharacterStat.Def) - 1);

        var damage = (int)(baseDamage * attackMultiplier * (eleMod / 100f) * defCut - GetStat(CharacterStat.Vit) * 0.7f);
        if (damage < 1)
            damage = 1;
        
        var lvCut = 1f;
        if (target.Character.Type == CharacterType.Monster)
        {
            //players deal 1.5% less damage per level they are below a monster, to a max of -90%
            lvCut -= 0.015f * (target.GetStat(CharacterStat.Level) - GetStat(CharacterStat.Level));
            lvCut = Math.Clamp(lvCut, 0.1f, 1f);
        }
        else
        {
            //monsters deal 0.5% less damage per level they are below the player, to a max of -50%
            lvCut -= 0.005f * (target.GetStat(CharacterStat.Level) - GetStat(CharacterStat.Level));
            lvCut = Math.Clamp(lvCut, 0.5f, 1f);
        }
        
        damage = (int)(lvCut * damage);

        if (damage < 1)
            damage = 1;

        if (eleMod == 0)
            damage = 0;
        
        var res = AttackResult.NormalDamage;
        if (damage == 0)
            res = AttackResult.Miss;

        var di = new DamageInfo()
        {
            Result = res,
            Damage = damage,
            HitCount = (byte)hitCount,
            KnockBack = 0,
            Source = Entity,
            Target = target.Entity,
            Time = Time.ElapsedTimeFloat + GetTiming(TimingStat.SpriteAttackTiming),
            AttackMotionTime = GetTiming(TimingStat.SpriteAttackTiming)
        };

        return di;
    }

    public void ApplyCooldownForAttackAction(CombatEntity target)
    {
#if DEBUG
        if (!target.IsValidTarget(this))
            throw new Exception("Entity attempting to attack an invalid target! This should be checked before calling PerformAttackAction.");
#endif
        ApplyCooldownForAttackAction();

        Character.FacingDirection = DistanceCache.Direction(Character.Position, target.Character.Position);
    }

    public void ApplyCooldownForAttackAction(Position target)
    {
        ApplyCooldownForAttackAction();

        Character.FacingDirection = DistanceCache.Direction(Character.Position, target);
    }

    public void ApplyCooldownForAttackAction()
    {
        var attackMotionTime = GetTiming(TimingStat.AttackMotionTime); //time for actual weapon strike to occur
        var delayTime = GetTiming(TimingStat.AttackDelayTime); //time before you can attack again

        if (attackMotionTime > delayTime)
            delayTime = attackMotionTime;

        if (Character.AttackCooldown + Time.DeltaTimeFloat + 0.005f < Time.ElapsedTimeFloat)
            Character.AttackCooldown = Time.ElapsedTimeFloat + delayTime; //they are consecutively attacking
        else
            Character.AttackCooldown += delayTime;

        Character.AddMoveLockTime(attackMotionTime);

        //if(Character.Type == CharacterType.Monster)
        //    Character.Monster.AddDelay(attackMotionTime);
    }

    /// <summary>
    /// Applies a damageInfo to the enemy combatant. Use sendPacket = false to suppress sending an attack packet if you plan to send a different packet later.
    /// </summary>
    /// <param name="damageInfo">Damage to apply, sourced from this combat entity.</param>
    /// <param name="sendPacket">Set to true if you wish to automatically send an Attack packet.</param>
    public void ExecuteCombatResult(DamageInfo damageInfo, bool sendPacket = true)
    {
        var target = damageInfo.Target.Get<CombatEntity>();

        if (sendPacket)
        {
            Character.Map?.GatherPlayersForMultiCast(Character);
            CommandBuilder.AttackMulti(Character, target.Character, damageInfo);
            CommandBuilder.ClearRecipients();
        }

        target.QueueDamage(damageInfo);
        target.Character.MoveModifier = 0.5f;
        var hitSlowTime = 0.2f * damageInfo.HitCount;
        if (damageInfo.HitCount > 1 && target.Character.MoveModifierTime < hitSlowTime)
            target.Character.MoveModifierTime = hitSlowTime;

        if (target.Character.Type == CharacterType.Monster && damageInfo.Damage > target.GetStat(CharacterStat.Hp))
        {
            var mon = target.Entity.Get<Monster>();
            mon.AddDelay(GetTiming(TimingStat.SpriteAttackTiming) * 2); //make sure it stops acting until it dies
        }

        if (Character.Type == CharacterType.Player && damageInfo.Damage > target.GetStat(CharacterStat.Hp))
        {
            var player = Entity.Get<Player>();
            player.ClearTarget();
        }
    }

    public void PerformMeleeAttack(CombatEntity target)
    {
        ApplyCooldownForAttackAction(target);

        var di = CalculateCombatResult(target, 1f, 1, AttackFlags.Physical);

        ExecuteCombatResult(di);
    }
    
    private void AttackUpdate()
    {
        while (DamageQueue.Count > 0 && DamageQueue[0].Time < Time.ElapsedTimeFloat)
        {
            var di = DamageQueue[0];
            DamageQueue.RemoveAt(0);
            if (di.Source.IsNull() || !di.Source.IsAlive())
                continue;
            var enemy = di.Source.Get<WorldObject>();
            if (!enemy.IsActive || enemy.Map != Character.Map)
                continue;
            if (enemy.Position.SquareDistance(Character.Position) > 31)
                continue;

            if (Character.State == CharacterState.Dead || enemy.State == CharacterState.Dead)
                break;

            if (Character.State == CharacterState.Sitting)
                Character.State = CharacterState.Idle;

            var damage = di.Damage * di.HitCount;

            //inform clients the player was hit and for how much
            var delayTime = GetTiming(TimingStat.HitDelayTime);

            Character.Map?.GatherPlayersForMultiCast(Character);
            if (Character.AddMoveLockTime(delayTime))
                CommandBuilder.SendHitMulti(Character, delayTime, damage);
            else
                CommandBuilder.SendHitMulti(Character, -1, damage);
            CommandBuilder.ClearRecipients();
            
            if (!di.Target.IsNull() && di.Source.IsAlive())
                Character.LastAttacked = di.Source;

            var ec = di.Target.Get<CombatEntity>();
            var hp = GetStat(CharacterStat.Hp);
            hp -= damage;

            SetStat(CharacterStat.Hp, hp);

            if (hp <= 0)
            {
                if (Character.Type == CharacterType.Monster)
                {
                    Character.ResetState();
                    var monster = Entity.Get<Monster>();

                    if (Entity.IsAlive() && enemy.Type == CharacterType.Player && DataManager.MvpMonsterCodes.Contains(monster.MonsterBase.Code))
                    {
                        //if we're an mvp, give the attacker the effect
                        Character.Map?.GatherPlayersForMultiCast(Character);
                        CommandBuilder.SendEffectOnCharacterMulti(enemy, DataManager.EffectIdForName["MVP"]);
                        CommandBuilder.ClearRecipients();
                    }

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
            }
        }
    }

    public void Init(ref Entity e, WorldObject ch)
    {
        Entity = e;
        Character = ch;
        IsTargetable = true;
        if (e.Type == EntityType.Player)
            Player = ch.Player;

        if (DamageQueue == null!)
            DamageQueue = new List<DamageInfo>(4);

        DamageQueue.Clear();

        SetStat(CharacterStat.Range, 2);
    }

    public void Update()
    {
        if (!Character.IsActive)
            return;

        if (IsCasting && CastingTime < Time.ElapsedTimeFloat)
                FinishCasting();

        if (DamageQueue.Count > 0)
            AttackUpdate();
    }
}