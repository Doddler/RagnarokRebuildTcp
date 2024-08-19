using System;
using System.Diagnostics;
using System.Numerics;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Simulation.Util;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace RoRebuildServer.EntityComponents;

[EntityComponent(EntityType.Player)]
public class Player : IEntityAutoReset
{
    public Entity Entity;
    public WorldObject Character = null!;
    public CombatEntity CombatEntity = null!;

    public NetworkConnection Connection = null!;

    public Guid Id { get; set; }
    public string Name { get; set; } = "Uninitialized Player";
    public HeadFacing HeadFacing;
    //public PlayerData Data { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsInNpcInteraction { get; set; }
    public int HeadId => GetData(PlayerStat.Head);
    public bool IsMale => GetData(PlayerStat.Gender) == 0;

    [EntityIgnoreNullCheck] public NpcInteractionState NpcInteractionState = new();
    [EntityIgnoreNullCheck] public int[] CharData = new int[(int)PlayerStat.PlayerStatsMax];
    [EntityIgnoreNullCheck] public SavePosition SavePosition { get; set; } = new();
    public Dictionary<CharacterSkill, int> LearnedSkills = null!;

    public bool DoesCharacterKnowSkill(CharacterSkill skill, int level) => LearnedSkills.TryGetValue(skill, out var learned) && learned >= level;

    public Entity Target { get; set; }

    public bool AutoAttackLock
    {
        get; 
        set;
    }
    private float regenTickTime { get; set; }
    private int _weaponClass = -1;
    public int WeaponClass => _weaponClass != -1 ? _weaponClass : DefaultWeaponForJob(GetData(PlayerStat.Job));
    public void SetWeaponClassOverride(int id) => _weaponClass = id;

#if DEBUG
    private float currentCooldown;
    public float CurrentCooldown
    {
        get => currentCooldown;
        set
        {
            currentCooldown = value;
            if (currentCooldown > 5f)
                ServerLogger.LogWarning($"Warning! Attempting to set player cooldown to time exceeding 5s! Stack Trace:\n" + Environment.StackTrace);
        }
    }
#else
    public float CurrentCooldown;
#endif

    public float LastEmoteTime; //we'll probably need to have like, a bunch of timers at some point...


    public int GetData(PlayerStat type) => CharData[(int)type];
    public void SetData(PlayerStat type, int val) => CharData[(int)type] = val;
    public int GetStat(CharacterStat type) => CombatEntity.GetStat(type);
    public float GetTiming(TimingStat type) => CombatEntity.GetTiming(type);
    public void SetStat(CharacterStat type, int val) => CombatEntity.SetStat(type, val);
    public void SetStat(CharacterStat type, float val) => CombatEntity.SetStat(type, (int)val);
    public void SetTiming(TimingStat type, float val) => CombatEntity.SetTiming(type, val);

    //this will get removed when we have proper job levels
    private static readonly int[] statsByLevel = new[]
    {
        0, 1, 2, 3, 4, 5, 6, 7, 8, 10, 11, 13, 14, 16, 17, 19, 20, 22, 23, 25,
        26, 28, 29, 31, 32, 34, 35, 37, 38, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50,
        50, 51, 51, 52, 52, 53, 53, 54, 54, 55, 55, 56, 56, 57, 57, 58, 58, 59, 59, 60,
        60, 61, 61, 62, 62, 63, 63, 64, 64, 65, 65, 66, 66, 67, 67, 68, 68, 69, 69, 69,
        69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69, 69
    };

    public void Reset()
    {
        Entity = Entity.Null;
        Target = Entity.Null;
        Character = null!;
        CombatEntity = null!;
        Connection = null!;
        CurrentCooldown = 0f;
        HeadFacing = HeadFacing.Center;
        AutoAttackLock = false;
        Id = Guid.Empty;
        Name = "Uninitialized Player";
        //Data = new PlayerData(); //fix this...
        regenTickTime = 0f;
        NpcInteractionState.Reset();
        IsAdmin = false;
        for (var i = 0; i < CharData.Length; i++)
            CharData[i] = 0;
        _weaponClass = -1;
        LastEmoteTime = 0;
        LearnedSkills = null!;

        SavePosition.Reset();
    }

    public void Init()
    {
        LearnedSkills ??= new Dictionary<CharacterSkill, int>();

        if (GetData(PlayerStat.Status) == 0)
        {
            SetData(PlayerStat.Level, 1);
            SetData(PlayerStat.Head, GameRandom.NextInclusive(0, 31));
            SetData(PlayerStat.Gender, GameRandom.NextInclusive(0, 1));
            SetData(PlayerStat.Status, 1);
        }

        UpdateStats();

        SetStat(CharacterStat.Level, GetData(PlayerStat.Level));

        IsAdmin = true; //for now
    }

    public float GetJobBonus(CharacterStat stat)
    {
        var job = GetData(PlayerStat.Job);
        switch (stat)
        {
            case CharacterStat.Attack:
                if (job == DataManager.JobIdLookup["Mage"])
                    return 0.4f;
                return 1f;
            case CharacterStat.MagicAtkMin:
                if (job != DataManager.JobIdLookup["Mage"])
                    return 0.4f;
                return 1f;
            case CharacterStat.MaxHp:
                if (job == DataManager.JobIdLookup["Swordsman"] || job == DataManager.JobIdLookup["Merchant"])
                    return 1.4f;
                if (job == DataManager.JobIdLookup["Thief"])
                    return 1.2f;
                if (job == DataManager.JobIdLookup["Mage"])
                    return 0.8f;
                return 1f;
        }

        return 1;
    }
    
    public void UpdateStats()
    {
        var level = GetData(PlayerStat.Level);
        var job = GetData(PlayerStat.Job);
        var jobInfo = DataManager.JobInfo[job];

        if (level > 99 || level < 1)
        {
            ServerLogger.LogWarning($"Woah! The player '{Name}' has a level of {level}, that's not normal. We'll lower the level down to the cap.");
            level = Math.Clamp(level, 1, 99);
            SetData(PlayerStat.Level, level);
        }

        //if (Character.ClassId != job)
        {
            Character.ClassId = job; //there should be more complex checks here to prevent GM and mounts from being lost but we'll deal with it later
        }

        var jobAspd = 1f;
        if (job == 5)
            jobAspd = 40f;
        if (job == 4)
            jobAspd = 20f;
        var aspdBonus = 100f / (GetStat(CharacterStat.AspdBonus) + 100 + jobAspd);


        var recharge = (1.4f - level * 0.008f) * aspdBonus;
        var motionTime = 0.8f;
        var spriteTime = 0.6f;
        if (job == 2)
        {
            motionTime = recharge * (6f / 8f);
            spriteTime = recharge * (6f / 8f);
        }

        if (recharge < motionTime)
        {
            var ratio = recharge / motionTime;
            motionTime *= ratio;
            spriteTime *= ratio;
        }

        SetTiming(TimingStat.AttackDelayTime, recharge);
        SetTiming(TimingStat.AttackMotionTime, motionTime);
        SetTiming(TimingStat.SpriteAttackTiming, spriteTime);


        //var aMotionTime = 0.5f;
        //var delayTime = 1.1f - level * 0.004f * aspdBonus;
        //if (delayTime < aMotionTime)
        //    delayTime = aMotionTime;
        //var spriteAttackTiming = 0.6f;

        //if (spriteAttackTiming > aMotionTime)
        //    spriteAttackTiming = aMotionTime;

        //SetTiming(TimingStat.AttackMotionTime, aMotionTime);
        //SetTiming(TimingStat.SpriteAttackTiming, 0.5f);
        //SetTiming(TimingStat.AttackDelayTime, delayTime);
        SetTiming(TimingStat.HitDelayTime, 0.288f);
        //SetTiming(TimingStat.MoveSpeed, 0.15f);
        if (GetData(PlayerStat.Job) == 2)
            SetStat(CharacterStat.Range, 9);
        else
            SetStat(CharacterStat.Range, 1);

        var atk = (level / 3f) * (level / 3f) + level * (int)(level / 20) + 28 + level;
        atk *= 1.2f;


        //lower damage below lv 60, raise above
        var proc = 0.5f + Math.Clamp(level, 0, 99f) / 120f;
        atk *= proc;

        var atk1 = (int)(atk * 0.90f - 1);
        var atk2 = (int)(atk * 1.10f + 1);

        var atkBonus = GetJobBonus(CharacterStat.Attack);
        var matkBonus = GetJobBonus(CharacterStat.MagicAtkMin);
        var hpBonus = GetJobBonus(CharacterStat.MaxHp);

        SetStat(CharacterStat.Attack, atk1 * atkBonus);
        SetStat(CharacterStat.Attack2, atk2 * atkBonus);
        SetStat(CharacterStat.MagicAtkMin, atk1 * matkBonus);
        SetStat(CharacterStat.MagicAtkMax, atk2 * matkBonus);
        SetStat(CharacterStat.Def, level * 0.7f);
        SetStat(CharacterStat.MDef, level * 0.4f);
        SetStat(CharacterStat.Vit, 3 + level * 0.5f);
        SetStat(CharacterStat.Int, 3 + level * 0.5f);
        //SetStat(CharacterStat.MaxHp, 50 + 100 * level * hpBonus);

        //var newMaxHp = (level * level * level) / 20 + 80 * level;

        var newMaxHp = (level * level) / 2 + (level * level * level) / 300 + 42 * level;
        var updatedMaxHp = newMaxHp * hpBonus;// (int)(newMaxHp * multiplier) + 70;

        SetStat(CharacterStat.MaxHp, updatedMaxHp);
        if (GetStat(CharacterStat.Hp) <= 0)
            SetStat(CharacterStat.Hp, updatedMaxHp);
        if (GetStat(CharacterStat.Hp) > updatedMaxHp)
            SetStat(CharacterStat.Hp, updatedMaxHp);

        var moveSpeed = 0.15f - (0.001f * level / 5f);
        SetTiming(TimingStat.MoveSpeed, moveSpeed);
        Character.MoveSpeed = moveSpeed;

        var pointEarned = statsByLevel[level - 1];

        if (job == 0 && pointEarned > 9)
            pointEarned = 9;

        var pointsUsed = 0;
        foreach (var skill in LearnedSkills)
            pointsUsed += skill.Value;

        if (pointEarned < pointsUsed)
            SetData(PlayerStat.SkillPoints, 0);
        else
            SetData(PlayerStat.SkillPoints, pointEarned - pointsUsed);

        if (Connection.IsConnectedAndInGame)
            CommandBuilder.SendUpdatePlayerData(this);
    }

    public void LevelUp()
    {
        var level = GetData(PlayerStat.Level);

        if (level + 1 > 99)
            return; //hard lock levels above 99

        var aMotionTime = 1.1f - level * 0.006f;
        //var spriteAttackTiming = 0.6f;

        level++;

        SetData(PlayerStat.Level, level);
        SetStat(CharacterStat.Level, level);

        UpdateStats();

        CombatEntity.FullRecovery(true, true);
    }

    public void JumpToLevel(int target)
    {
        var level = GetData(PlayerStat.Level);

        if (target < 1 || target > 99)
            return; //hard lock levels above 99

        level = target;

        SetData(PlayerStat.Level, level);
        SetData(PlayerStat.Experience, 0); //reset exp to 0
        SetStat(CharacterStat.Level, level);


        UpdateStats();

        CombatEntity.FullRecovery(true, true);
    }

    public void SaveCharacterToData()
    {
        SetData(PlayerStat.Hp, GetStat(CharacterStat.Hp));
        SetData(PlayerStat.Mp, GetStat(CharacterStat.Sp));
    }

    public void ApplyDataToCharacter()
    {
        SetStat(CharacterStat.Hp, GetData(PlayerStat.Hp));
        SetStat(CharacterStat.Sp, GetData(PlayerStat.Mp));
    }

    public void EndNpcInteractions()
    {
        if (!IsInNpcInteraction)
            return;

        NpcInteractionState.CancelInteraction();
    }

    // Adjust the remaining regen tick time when changing sitting state.
    // Standing up doubles your remaining regen tick time, sitting down halves it.
    public void UpdateSit(bool isSitting)
    {
        var remainingTime = regenTickTime - Time.ElapsedTimeFloat;
        if (remainingTime < 0)
            return;

        if (!isSitting)
        {
            if (remainingTime > 4)
                remainingTime = 4;
            regenTickTime = Time.ElapsedTimeFloat + remainingTime * 2f;
        }
        else
            regenTickTime = Time.ElapsedTimeFloat + remainingTime / 2f;
    }
    public void RegenTick()
    {
        if (!Character.IsActive || Character.State == CharacterState.Dead)
            return;

        var hp = GetStat(CharacterStat.Hp);
        var maxHp = GetStat(CharacterStat.MaxHp);

        if (hp < maxHp)
        {
            var regen = maxHp / 10;
            if (Character.State == CharacterState.Sitting)
                regen *= 2;
            if (regen + hp > maxHp)
                regen = maxHp - hp;


            SetStat(CharacterStat.Hp, hp + regen);

            CommandBuilder.SendHealSingle(this, regen, HealType.None);
        }
    }

    public void Die()
    {
        if (Character.Map == null)
            throw new Exception("Attempted to kill a player, but the player is not attached to any map.");

        if (Character.State == CharacterState.Dead)
            return; //we're already dead!

        ClearTarget();
        EndNpcInteractions();
        Character.StopMovingImmediately();
        Character.State = CharacterState.Dead;
        Character.QueuedAction = QueuedAction.None;
        Character.InMoveLock = false;
        CombatEntity.IsCasting = false;
        CombatEntity.CastingSkill.Clear();
        CombatEntity.QueuedCastingSkill.Clear();

        Character.Map.AddVisiblePlayersAsPacketRecipients(Character);
        CommandBuilder.SendPlayerDeath(Character);
        CommandBuilder.ClearRecipients();
    }

    private bool ValidateTarget()
    {
        if (Target.IsNull() || !Target.IsAlive())
        {
            ClearTarget();
            return false;
        }

        if (!Target.TryGet<CombatEntity>(out var ce))
            return false;

        return ce.IsValidTarget(CombatEntity);
    }

    public void ClearTarget()
    {
        AutoAttackLock = false;

        if (!Target.IsNull())
            CommandBuilder.SendChangeTarget(this, null);

        Target = Entity.Null;
    }

    public void ChangeTarget(WorldObject? target)
    {
        if (target == null || Target == target.Entity)
            return;

        CommandBuilder.SendChangeTarget(this, target);

        Target = target.Entity;
    }

    public int DefaultWeaponForJob(int newJobId) => newJobId switch
    {
        0 => 1, //novice => dagger
        1 => 2, //swordsman => sword
        2 => 12, //archer => bow
        3 => 10, //mage => rod
        4 => 8, //acolyte => mace
        5 => 1, //thief => dagger
        6 => 6, //merchant => axe
        _ => 1, //anything else => dagger
    };

    public void ChangeJob(int newJobId)
    {
        var job = DataManager.JobInfo[newJobId];
        SetData(PlayerStat.Job, newJobId);

        if (Character.ClassId < 100) //we don't want to override special character classes like GameMaster
            Character.ClassId = newJobId;

        //until equipment is real pick weapon based on job
        var weapon = DefaultWeaponForJob(newJobId);

        //WeaponClass = weapon;

        UpdateStats();

        if (Character.Map != null)
            Character.Map.RefreshEntity(Character);
    }

    public void SaveSpawnPoint(string spawnName)
    {
        if (DataManager.SavePoints.TryGetValue(spawnName, out var spawnPosition))
            SavePosition = spawnPosition;
        else
            ServerLogger.LogError($"Npc script attempted to set spawn position to \"{spawnName}\", but that spawn point was not defined.");
    }


    public void PerformQueuedAttack()
    {
        if (Character.State == CharacterState.Sitting
            || Character.State == CharacterState.Dead
            || !ValidateTarget())
        {
            AutoAttackLock = false;
            return;
        }

        var targetCharacter = Target.Get<WorldObject>();
        if (!targetCharacter.IsActive || targetCharacter.Map != Character.Map)
        {
            AutoAttackLock = false;
            return;
        }

        if (DistanceCache.IntDistance(Character.Position, targetCharacter.Position) > GetStat(CharacterStat.Range))
        {
            if (InMoveReadyState)
                Character.TryMove(targetCharacter.Position, 1);
            return;
        }

        if (Character.State == CharacterState.Moving)
        {
            Character.StopMovingImmediately();
            //if (Character.StepsRemaining > 1)
            //    Character.ShortenMovePath(); //no point in shortening a path that is already short

            //return;
        }

        ChangeTarget(targetCharacter);
        PerformAttack(targetCharacter);
    }
    public void PerformAttack(WorldObject targetCharacter)
    {
        if (targetCharacter.Type == CharacterType.NPC)
        {
            ChangeTarget(null);

            return;
        }

        var targetEntity = targetCharacter.Entity.Get<CombatEntity>();
        if (!targetEntity.IsValidTarget(CombatEntity))
        {
            ClearTarget();
            return;
        }

        AutoAttackLock = true;

        if (Character.State == CharacterState.Moving)
        {
            if (Character.QueuedAction == QueuedAction.Move && Character.MoveLockTime > Time.DeltaTimeFloat)
                Character.State = CharacterState.Idle;
            else
                Character.ShortenMovePath();

            if (Target != targetCharacter.Entity)
                ChangeTarget(targetCharacter);

            return;
        }

        //Character.StopMovingImmediately();

        if (Character.AttackCooldown > Time.ElapsedTimeFloat)
        {
            if (Target != targetCharacter.Entity)
                ChangeTarget(targetCharacter);

            return;
        }

        Character.ResetSpawnImmunity();
        CombatEntity.PerformMeleeAttack(targetEntity);
        Character.AddMoveLockTime(GetTiming(TimingStat.AttackDelayTime));

        Character.AttackCooldown = Time.ElapsedTimeFloat + GetTiming(TimingStat.AttackDelayTime);
    }

    public void TargetForAttack(WorldObject enemy)
    {
        if (CombatEntity.IsCasting)
        {
            ChangeTarget(enemy);
            AutoAttackLock = true;
            return;
        }

        if (DistanceCache.IntDistance(Character.Position, enemy.Position) <= GetStat(CharacterStat.Range))
        {
            ChangeTarget(enemy);
            PerformAttack(enemy);
            return;
        }

        if (!Character.TryMove(enemy.Position, 1))
            return;

        ChangeTarget(enemy);
        AutoAttackLock = true;
    }

    public bool VerifyCanUseSkill(CharacterSkill skill, int lvl)
    {
        return true; //lol
    }

    public void PerformSkill()
    {
        Debug.Assert(Character.Map != null, $"Player {Name} cannot perform skill, it is not attached to a map.");

        var pool = EntityListPool.Get();
        Character.Map.GatherEnemiesInRange(Character, 7, pool, true);

        if (Character.AttackCooldown > Time.ElapsedTimeFloat)
            return;

        if (pool.Count == 0)
        {
            EntityListPool.Return(pool);
            return;
        }

        Character.StopMovingImmediately();
        ClearTarget();

        for (var i = 0; i < pool.Count; i++)
        {
            var e = pool[i];
            if (e.IsNull() || !e.IsAlive())
                continue;
            var target = e.Get<CombatEntity>();
            if (target == CombatEntity || target.Character.Type == CharacterType.Player)
                continue;

            CombatEntity.PerformMeleeAttack(target);
            Character.AddMoveLockTime(GetTiming(TimingStat.AttackDelayTime));
        }

        Character.AttackCooldown = Time.ElapsedTimeFloat + GetTiming(TimingStat.AttackDelayTime);
    }

    public bool WarpPlayer(string mapName, int x, int y, int width, int height, bool failIfNotWalkable)
    {
        if (!World.Instance.TryGetWorldMapByName(mapName, out var map))
            return false;

        AddActionDelay(2f); //block character input for 1+ seconds.
        Character.ResetState();
        Character.SetSpawnImmunity();

        CombatEntity.ClearDamageQueue();

        var p = new Position(x, y);

        if (Character.Map != null && (width > 1 || height > 1))
        {
            var area = Area.CreateAroundPoint(x, y, width, height);
            p = Character.Map.GetRandomWalkablePositionInArea(area);
            if (p == Position.Invalid)
            {
                ServerLogger.LogWarning($"Could not warp player to map {mapName} area {area} is blocked.");
                p = new Position(x, y);
            }
        }

        if (Character.Map?.Name == mapName)
            Character.Map.TeleportEntity(ref Entity, Character, p, CharacterRemovalReason.OutOfSight);
        else
            World.Instance?.MovePlayerMap(ref Entity, Character, map, p);

        return true;
    }


    public void UpdatePosition()
    {
        if (!ValidateTarget())
            return;

        var targetCharacter = Target.Get<WorldObject>();

        if (Character.State == CharacterState.Moving)
        {
            if (DistanceCache.IntDistance(Character.Position, targetCharacter.Position) <= GetStat(CharacterStat.Range))
                Character.StopMovingImmediately();
        }

        if (Character.State == CharacterState.Idle)
        {
            TargetForAttack(targetCharacter);
        }
    }


    public bool InActionCooldown() => CurrentCooldown > 1f;
    public void AddActionDelay(CooldownActionType type) => CurrentCooldown += ActionDelay.CooldownTime(type);
    public void AddActionDelay(float time) => CurrentCooldown += CurrentCooldown;

    private bool InCombatReadyState => (Character.State == CharacterState.Idle || Character.State == CharacterState.Moving)
        && !CombatEntity.IsCasting &&
                                       Character.AttackCooldown < Time.ElapsedTimeFloat;

    private bool InMoveReadyState => Character.State == CharacterState.Idle && !CombatEntity.IsCasting;

    public void Update()
    {
        CurrentCooldown -= Time.DeltaTimeFloat; //this cooldown is the delay on how often a player can perform actions
        if (CurrentCooldown < 0)
            CurrentCooldown = 0;

        if (Character.State == CharacterState.Dead || Character.State == CharacterState.Sitting)
        {
            Character.QueuedAction = QueuedAction.None;
            AutoAttackLock = false;
            if (Character.State == CharacterState.Dead)
                return;
        }

        if (regenTickTime < Time.ElapsedTimeFloat)
        {
            RegenTick();
            if (Character.State == CharacterState.Sitting)
                regenTickTime = Time.ElapsedTimeFloat + 4f;
            else
                regenTickTime = Time.ElapsedTimeFloat + 8f;
        }

        if (Character.QueuedAction == QueuedAction.Cast)
        {
            if (CombatEntity.QueuedCastingSkill.TargetEntity.TryGet<WorldObject>(out var targetCharacter))
            {
                var isValid = true;
                var canAttack = CombatEntity.CanAttackTarget(targetCharacter, CombatEntity.QueuedCastingSkill.Range);
                if (Character.State == CharacterState.Moving && canAttack)
                    Character.StopMovingImmediately(); //we've locked in place but we're close enough to attack
                if (Character.State == CharacterState.Idle && !canAttack)
                {
                    var target = CombatEntity.CastingSkill.TargetEntity;
                    isValid = Character.TryMove(targetCharacter.Position, 1);
                }

                if (InCombatReadyState && isValid)
                {
                    if (CombatEntity.QueuedCastingSkill.IsValid)
                        CombatEntity?.ResumeQueuedSkillAction();
                    else
                        Character.QueuedAction = QueuedAction.None;
                }

                if (!isValid)
                    Character.QueuedAction = QueuedAction.None;
            }
            else
            {
                Character.QueuedAction = QueuedAction.None;
                Target = Entity.Null;
            }
        }

        if (Character.QueuedAction == QueuedAction.Move && InMoveReadyState)
        {
            if (Character.InMoveLock)
                return;

            Character.QueuedAction = QueuedAction.None;
            Character.TryMove(Character.TargetPosition, 0);

            return;
        }

        if (AutoAttackLock)
        {
            if (!Target.TryGet<WorldObject>(out var targetCharacter))
            {
                AutoAttackLock = false;
                Target = Entity.Null;
                return;
            }

            if (Character.InMoveLock && !Character.InAttackCooldown && CombatEntity.CanAttackTarget(targetCharacter))
                Character.StopMovingImmediately();

            if (InCombatReadyState)
                PerformQueuedAttack();
        }
    }
}