using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.EntityComponents;

[EntityComponent(new [] { EntityType.Player , EntityType.Monster})]
public class CombatEntity : IEntityAutoReset
{
    public Entity Entity;
    public WorldObject Character = null!;

    public int Faction;
    public int Party;
    
    [EntityIgnoreNullCheck]
    private int[] statData = new int[(int)CharacterStat.CharacterStatsMax];
    
    [EntityIgnoreNullCheck]
    private float[] timingData = new float[(int)TimingStat.TimingStatsMax];

    [EntityIgnoreNullCheck]
    public List<DamageInfo> DamageQueue = null!;

    public int GetStat(CharacterStat type) => statData[(int)type];
    public float GetTiming(TimingStat type) => timingData[(int)type];
    public void SetStat(CharacterStat type, int val) => statData[(int)type] = val;
    public void SetTiming(TimingStat type, float val) => timingData[(int)type] = val;

    public void Reset()
    {
        Entity = Entity.Null;
        Character = null!;
        Faction = -1;
        Party = -1;

        for(var i = 0; i < statData.Length; i++)
            statData[i] = 0;
    }

    public void Heal(int hp, int hp2, bool showValue = false)
    {
        if (hp2 != -1 && hp2 > hp)
            hp = GameRandom.NextInclusive(hp, hp2);

        var curHp = GetStat(CharacterStat.Hp);
        var maxHp = GetStat(CharacterStat.MaxHp);

        hp += (int)(maxHp * 0.06f); //kinda cheat and force give an extra 6% hp back

        if(curHp + hp > maxHp)
            hp = maxHp - curHp;

        SetStat(CharacterStat.Hp, curHp + hp);

        if (Character.Map == null)
            return;

        Character.Map.GatherPlayersForMultiCast(ref Entity, Character);
        CommandBuilder.SendHealMulti(Character, hp, HealType.Item);
        CommandBuilder.ClearRecipients();
    }

    public void FullRecovery(bool hp, bool mp)
    {
        if(hp)
            SetStat(CharacterStat.Hp, GetStat(CharacterStat.MaxHp));
        if(mp)
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

        if (Character.SpawnImmunity > 0f)
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
        Character.Map.GatherPlayersForMultiCast(ref Entity, Character);

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

    public void PerformMeleeAttack(CombatEntity target)
    {
#if DEBUG
        if (!target.IsValidTarget(this))
            throw new Exception("Entity attempting to attack an invalid target! This should be checked before calling PerformMeleeAttack.");
#endif
        if (Character.AttackCooldown + Time.DeltaTimeFloat + 0.005f < Time.ElapsedTimeFloat)
            Character.AttackCooldown = Time.ElapsedTimeFloat + GetTiming(TimingStat.AttackMotionTime); //they are consecutively attacking
        else
            Character.AttackCooldown += GetTiming(TimingStat.AttackMotionTime);

        Character.FacingDirection = DistanceCache.Direction(Character.Position, target.Character.Position);
        
        var atk1 = GetStat(CharacterStat.Attack);
        var atk2 = GetStat(CharacterStat.Attack2);
        if (atk1 <= 0)
            atk1 = 1;
        if (atk2 < atk1)
            atk2 = atk1;

        var baseDamage = (short)GameRandom.NextInclusive(atk1, atk2);

        var defCut = MathF.Pow(0.99f, target.GetStat(CharacterStat.Def) - 1);

        var damage = (short)(baseDamage * defCut - GetStat(CharacterStat.Vit) * 0.7f);
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

        damage = (short)(lvCut * damage);

        var di = new DamageInfo()
        {
            Damage = damage,
            HitCount = 1,
            KnockBack = 0,
            Source = Entity,
            Target = target.Entity,
            Time = Time.ElapsedTimeFloat + GetTiming(TimingStat.SpriteAttackTiming)
        };

        //ServerLogger.Log($"{aiCooldown} {character.AttackCooldown} {angle} {dir}");

        Character.Map.GatherPlayersForMultiCast(ref Entity, Character);
        CommandBuilder.AttackMulti(Character, target.Character, di);
        CommandBuilder.ClearRecipients();

        target.QueueDamage(di);

        if (target.Character.Type == CharacterType.Monster && di.Damage > target.GetStat(CharacterStat.Hp))
        {
            var mon = target.Entity.Get<Monster>();
            mon.AddDelay(GetTiming(TimingStat.SpriteAttackTiming) * 2); //make sure it stops acting until it dies
        }

        if (Character.Type == CharacterType.Player && di.Damage > target.GetStat(CharacterStat.Hp))
        {
            var player = Entity.Get<Player>();
            player.ClearTarget();
        }
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

            Character.Map.GatherPlayersForMultiCast(ref Entity, Character);
            if (Character.AddMoveDelay(delayTime))
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
                    monster.Die();
                    DamageQueue.Clear();
                    return;
                }

                if (Character.Type == CharacterType.Player)
                {
                    var player = Character.Entity.Get<Player>();
                    SetStat(CharacterStat.Hp, 0);
                    player.Die();
                    return;
                }
            }
        }
    }

    public void Init(ref Entity e, WorldObject ch)
    {
        Entity = e;
        Character = ch;
        
        if(DamageQueue == null)
            DamageQueue = new List<DamageInfo>(4);

        DamageQueue.Clear();

        SetStat(CharacterStat.Range, 2);
    }

    public void Update()
    {
        if (!Character.IsActive)
            return;
        if (DamageQueue != null && DamageQueue.Count > 0)
            AttackUpdate();
    }
}