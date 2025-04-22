using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.Data.Map;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.Database.Domain;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.EntityComponents.Monsters;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Items;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Simulation.Skills;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.EntityComponents;

[EntityComponent(EntityType.Monster)]
public partial class Monster : IEntityAutoReset
{
    public Entity Entity;
    public WorldObject Character = null!;
    public CombatEntity CombatEntity = null!;

    private float aiTickRate;
    //private float aiCooldown;

    private double nextAiUpdate;
    //private float NextAiUpdate
    //{
    //    get => nextAiUpdate;
    //    set
    //    {
    //        if (dbgFlag && value > Time.ElapsedTimeFloat)
    //            ServerLogger.Debug($"Updated AI update time to {value - Time.ElapsedTimeFloat} (previous value was {nextAiUpdate - Time.ElapsedTimeFloat})");
    //        if (NetworkManager.PlayerCount == 0)
    //            dbgFlag = false;
    //        nextAiUpdate = value;
    //    }
    //}
    //private float nextAiUpdate;

    public void ResetAiUpdateTime() => nextAiUpdate = 0;
    public void ResetAiSkillUpdateTime() => nextAiSkillUpdate = 0;

    public void AdjustAiUpdateIfShorter(double time)
    {
        if (time < nextAiUpdate)
            nextAiUpdate = time;
    }

    private float nextMoveUpdate;
    private double nextAiSkillUpdate;
    private float timeofLastStateChange;
    //private float timeEnteredCombat;
    private float timeLastCombat;
    private float timeSinceLastDamage;
    private float timeOfStartChase;
    private float createTime;

    public void UpdateStateChangeTime() => timeofLastStateChange = Time.ElapsedTimeFloat;
    public float TimeInCurrentAiState => Time.ElapsedTimeFloat - timeofLastStateChange;
    //private float durationInCombat => Target.IsAlive() ? Time.ElapsedTimeFloat - timeEnteredCombat : -1f;
    public float DurationOutOfCombat => !Target.IsAlive() ? Time.ElapsedTimeFloat - timeLastCombat : -1;
    public float TimeSinceLastDamage => Time.ElapsedTimeFloat - timeSinceLastDamage;
    public float TimeSinceStartChase => Time.ElapsedTimeFloat - timeOfStartChase;

    public float TimeAlive => Time.ElapsedTimeFloat - createTime;

    public int ChaseSight = 12;
    public int AttackSight = 9;

    //private float randomMoveCooldown;

    private const float minIdleWaitTime = 3f;
    private const float maxIdleWaitTime = 6f;

    //private bool hasTarget;

    public Entity Target;
    private Entity Master;
    public EntityList? Children;

    public int ChildCount => Children?.Count ?? 0;

    private WorldObject? targetCharacter => Target.GetIfAlive<WorldObject>();

    public MonsterDatabaseInfo MonsterBase = null!;
    //public MapSpawnEntry SpawnEntry;
    public string SpawnMap = null!;
    public MapSpawnRule? SpawnRule;
    private MonsterAiType aiType;
    private List<MonsterAiEntry> aiEntries = null!;

    private MonsterSkillAiState skillState = null!;
    public Action<MonsterSkillAiState>? CastSuccessEvent = null;
    private MonsterSkillAiBase? skillAiHandler;
    private ItemReference[]? monsterInventory;
    private int inventoryCount;
    private int inventoryIndex;
    public bool IsInventoryFull => inventoryCount >= InventorySize;
    public int InventoryCount => inventoryCount;
    private const int InventorySize = 20;
    
    public bool LockMovementToSpawn;
    public bool GivesExperience;
    public bool IsAiActive;

    public MonsterAiState CurrentAiState;
    public MonsterAiState PreviousAiState;
    public CharacterSkill LastDamageSourceType;
    public int LastAttackRange;
    public bool WasAttacked;
    public bool WasRudeAttacked;
    public bool LastAttackPhysical;
    public bool WasMagicLocked;
    private bool canResetAttackedState;

    private EntityValueList<int>? TotalDamageReceived;

    //private WorldObject searchTarget = null!;

    private float deadTimeout;
    //private float allyScanTimeout;
    //private bool inAdjustMove;
#if DEBUG
    public bool DebugLogging;


    public void DebugLog(string msg)
    {
        ServerLogger.Debug($"{MonsterBase.Name}:{Character.Id} State: {CurrentAiState} Pos:{Character.Position} Target:{Character.TargetPosition} Move:{Character.IsMoving} AMotion: {Character.AttackCooldown - Time.ElapsedTimeFloat} | {msg}");
    }
#endif


    public bool HasMaster => Master.IsAlive();
    public Entity GetMaster() => Master;
    public Entity SetMaster(Entity master) => Master = master;

    //public static float MaxSpawnTimeInSeconds = 180;

    public void SetStat(CharacterStat type, int val) => CombatEntity.SetStat(type, val);
    public int GetStat(CharacterStat type) => CombatEntity.GetStat(type);
    public void SetTiming(TimingStat type, float val) => CombatEntity.SetTiming(type, val);

    public void CallDeathEvent() => skillAiHandler?.OnDie(skillState);
    public void ResetIdleWaitTime() => nextMoveUpdate = Time.ElapsedTimeFloat + GameRandom.NextFloat(4f, 6f);

    public void ChangeAiSkillHandler(string newHandler)
    {
        if (DataManager.MonsterSkillAiHandlers.TryGetValue(newHandler, out var handler))
        {
            skillAiHandler = handler;
            handler.OnInit(skillState);
        }
        else
            ServerLogger.LogWarning($"Could not change monster {Character} Ai Skill handler to handler with name {newHandler}");
    }

    public void ChangeAiStateMachine(MonsterAiType type)
    {
        aiType = type;
        aiEntries = DataManager.GetAiStateMachine(type);
    }

    public void NotifyOfAttack(ref DamageInfo di)
    {
        var hasSrc = di.Source.IsAlive();

#if DEBUG
        if (DebugLogging)
            DebugLog($"Monster received {di.HitCount}x{di.Damage} damage from {di.Source} skill {di.AttackSkill}");
#endif

        if (!di.Flags.HasFlag(DamageApplicationFlags.SkipOnHitTriggers))
        {
            timeSinceLastDamage = Time.ElapsedTimeFloat;
            LastDamageSourceType = di.AttackSkill;
            if (hasSrc && di.Source.TryGet<WorldObject>(out var src))
                LastAttackRange = Character.Position.DistanceTo(src.Position);
            else
                LastAttackRange = 0;
            ResetAiUpdateTime();
            Character.StopMovingImmediately();
            WasAttacked = true;
            LastAttackPhysical = di.Flags.HasFlag(DamageApplicationFlags.PhysicalDamage);
        }

        if (!hasSrc)
            return;

        TotalDamageReceived ??= EntityValueListPool<int>.Get();
        TotalDamageReceived.AddOrIncreaseValue(ref di.Source, di.Damage);
    }

    public void MagicLock(CombatEntity src)
    {
        if (WasAttacked)
            return; //we don't want to overwrite an attacked state
        Character.LastAttacked = src.Entity;
        WasMagicLocked = true;
    }

    public void Reset()
    {
        Entity = Entity.Null;
        Master = Entity.Null;
        Character = null!;
        IsAiActive = false;
        aiEntries = null!;
        //SpawnEntry = null;
        CombatEntity = null!;
        //searchTarget = null!;
        aiTickRate = 0.1f;
        nextAiUpdate = Time.ElapsedTime + GameRandom.NextFloat(0, aiTickRate);
        SpawnRule = null;
        MonsterBase = null!;
        SpawnMap = null!;
        Children = null;
        skillState.Reset();
        skillAiHandler = null;
        CastSuccessEvent = null;
        WasAttacked = false;
        WasRudeAttacked = false;
        WasMagicLocked = false;
        LastAttackPhysical = false;
        canResetAttackedState = false;
        timeSinceLastDamage = 0;
        LastAttackRange = 0;
        AttackSight = 9;
        ChaseSight = 12;
        if(monsterInventory != null)
            ArrayPool<ItemReference>.Shared.Return(monsterInventory, true);
        monsterInventory = null;
        inventoryCount = 0;
        inventoryIndex = 0;
        TotalDamageReceived?.Dispose();
        TotalDamageReceived = null;

        Target = Entity.Null;
    }

    public void Initialize(ref Entity e, WorldObject character, CombatEntity combat, MonsterDatabaseInfo monData, MonsterAiType type, MapSpawnRule? spawnEntry, string mapName)
    {
        Entity = e;
        Character = character;
        SpawnRule = spawnEntry;
        CombatEntity = combat;
        MonsterBase = monData;
        aiType = type;
        SpawnMap = mapName;
        aiEntries = DataManager.GetAiStateMachine(aiType);
        nextAiUpdate = Time.ElapsedTime + 1f;
        nextAiSkillUpdate = Time.ElapsedTimeFloat + 1f;
        aiTickRate = 0.05f;
        LastDamageSourceType = CharacterSkill.None;
        GivesExperience = true;
        ChaseSight = monData.ChaseDist;
        AttackSight = monData.ScanDist;

        if (SpawnRule != null)
        {
            LockMovementToSpawn = SpawnRule.LockToSpawn;
        }

        //if (Character.ClassId >= 4000 && spawnEntry == null)
        //    throw new Exception("Monster created without spawn entry"); //remove when arbitrary monster spawning is added

        InitializeStats();

        character.Name = $"{monData.Name} {e}";

        CurrentAiState = MonsterAiState.StateIdle;
        if(skillState == null!)
            skillState = new MonsterSkillAiState(this);

        timeofLastStateChange = Time.ElapsedTimeFloat;
        timeLastCombat = Time.ElapsedTimeFloat;
        createTime = Time.ElapsedTimeFloat;
        timeOfStartChase = float.MaxValue;
        //timeEnteredCombat = float.NegativeInfinity;

        if (DataManager.MonsterSkillAiHandlers.TryGetValue(monData.Code, out var handler))
        {
            skillAiHandler = handler;
            handler.OnInit(skillState);
        }
    }

    public void ChangeMonsterClass(string name, MonsterDatabaseInfo info, MonsterAiType type, bool isMetamorphosis)
    {
        if (!Character.IsActive || Character.Map == null)
            return;

        Character.ClassId = info.Id;
        MonsterBase = info;
        aiType = info.AiType;
        aiEntries = DataManager.GetAiStateMachine(aiType);
        
        Character.Name = info.Name;
        CurrentAiState = MonsterAiState.StateIdle;

        if (DataManager.MonsterSkillAiHandlers.TryGetValue(info.Code, out var handler))
        {
            skillAiHandler = handler;
            handler.OnInit(skillState);
        }
        else
            skillAiHandler = null;

        InitializeStats();
        nextAiUpdate = Time.ElapsedTime + 1;

        Character.Map.RefreshEntity(Character, isMetamorphosis ? CharacterRemovalReason.Metamorphosis : CharacterRemovalReason.Refresh);
    }

    public void AddChild(ref Entity child, MonsterAiType newAiType = MonsterAiType.AiEmpty)
    {
        if (!child.IsAlive() && child.Type != EntityType.Monster)
            ServerLogger.LogError($"Cannot AddChild on monster {Character.Name} when child entity {child} is not alive or not a monster.");

        var childMon = child.Get<Monster>();

        Children ??= EntityListPool.Get();
        Children.Add(child);

        if(newAiType != MonsterAiType.AiEmpty)
            childMon.MakeChild(ref Entity);
        else
            childMon.MakeChild(ref Entity, newAiType);
    }

    public void MakeChild(ref Entity parent, MonsterAiType newAiType = MonsterAiType.AiMinion)
    {
        if (!parent.IsAlive() && parent.Type != EntityType.Monster)
            ServerLogger.LogError($"Cannot MakeChild on monster {Character.Name} when parent entity {parent} is not alive or not a monster.");

        Master = parent;
        float speed = parent.Get<WorldObject>().MoveSpeed;
        SetTiming(TimingStat.MoveSpeed, speed);
        Character.MoveSpeed = speed;

        if (newAiType != MonsterAiType.AiEmpty)
            aiEntries = DataManager.GetAiStateMachine(newAiType);
    }

    public void RemoveChild(ref Entity child)
    {
        Children?.Remove(ref child);
    }

    public Entity GetRandomChild()
    {
        var childCount = Children?.Count ?? 0;

        if (childCount == 0)
            return Entity.Null;

        var rnd = GameRandom.Next(0, childCount);
        return Children![rnd];
    }

    public void AddItemToInventory(ItemReference item)
    {
        if (monsterInventory == null)
            monsterInventory = ArrayPool<ItemReference>.Shared.Rent(InventorySize);
        monsterInventory[inventoryIndex] = item;
        inventoryIndex++;
        if(inventoryIndex > inventoryCount)
            inventoryCount = inventoryIndex;
        if (inventoryIndex >= InventorySize)
            inventoryIndex = 0;
    }

    private void InitializeStats()
    {
        var magicMin = MonsterBase.Int + MonsterBase.Int / 7 * MonsterBase.Int / 7;
        var magicMax = MonsterBase.Int + MonsterBase.Int / 5 * MonsterBase.Int / 5;

        if (magicMin < MonsterBase.AtkMin / 6)
            magicMin = MonsterBase.AtkMin / 6;
        if (magicMax < MonsterBase.AtkMax / 6)
            magicMax = MonsterBase.AtkMax / 6;

        SetStat(CharacterStat.Level, MonsterBase.Level);
        SetStat(CharacterStat.Hp, MonsterBase.HP);
        SetStat(CharacterStat.MaxHp, MonsterBase.HP);
        SetStat(CharacterStat.Attack, MonsterBase.AtkMin);
        SetStat(CharacterStat.Attack2, MonsterBase.AtkMax);
        SetStat(CharacterStat.MagicAtkMin, magicMin);
        SetStat(CharacterStat.MagicAtkMax, magicMax);
        SetStat(CharacterStat.Attack2, MonsterBase.AtkMax);
        SetStat(CharacterStat.Range, MonsterBase.Range);
        SetStat(CharacterStat.Def, MonsterBase.Def);
        SetStat(CharacterStat.MDef, MonsterBase.MDef);
        SetStat(CharacterStat.Str, MonsterBase.Str);
        SetStat(CharacterStat.Agi, MonsterBase.Agi);
        SetStat(CharacterStat.Vit, MonsterBase.Vit);
        SetStat(CharacterStat.Int, MonsterBase.Int);
        SetStat(CharacterStat.Dex, MonsterBase.Dex);
        SetStat(CharacterStat.Luk, MonsterBase.Luk);
        SetTiming(TimingStat.MoveSpeed, MonsterBase.MoveSpeed);
        SetTiming(TimingStat.SpriteAttackTiming, MonsterBase.AttackDamageTiming);
        SetTiming(TimingStat.HitDelayTime, MonsterBase.HitTime);
        SetTiming(TimingStat.AttackMotionTime, MonsterBase.AttackLockTime);
        SetTiming(TimingStat.AttackDelayTime, MonsterBase.RechargeTime);
        Character.MoveSpeed = MonsterBase.MoveSpeed;
    }

    public void UpdateStats()
    {
        var aspdBonus = 100f / (float.Clamp(GetStat(CharacterStat.AspdBonus), -99, 5000) + 100);
        
        var recharge = MonsterBase.RechargeTime * aspdBonus;
        var motionTime = MonsterBase.AttackLockTime;
        var spriteTime = MonsterBase.AttackDamageTiming;
        var updateTime = MathF.Max(recharge, MonsterBase.AttackLockTime * aspdBonus);
        if (motionTime > updateTime)
        {
            var ratio = updateTime / motionTime;
            motionTime *= ratio;
            spriteTime *= ratio;
        }

        SetTiming(TimingStat.AttackDelayTime, recharge);
        SetTiming(TimingStat.AttackMotionTime, motionTime);
        SetTiming(TimingStat.SpriteAttackTiming, spriteTime);

        var oldMoveSpeed = Character.MoveSpeed;
        var moveBonus = 100f / (100f + float.Clamp(GetStat(CharacterStat.MoveSpeedBonus), -99, 5000));
        if (CombatEntity.HasBodyState(BodyStateFlags.Curse))
            moveBonus = 1 / 0.1f;
        var moveSpeed = MonsterBase.MoveSpeed * moveBonus;
        SetTiming(TimingStat.MoveSpeed, moveSpeed);
        Character.MoveSpeed = moveSpeed;

        if (Character.IsMoving && Math.Abs(oldMoveSpeed - Character.MoveSpeed) > 0.03)
            Character.TryMove(Character.TargetPosition, 0);
    }

    private bool ValidateTarget()
    {
        if (Target.IsNull() || !Target.IsAlive())
            return false;
        var ce = Target.Get<CombatEntity>();
        if (!ce.IsValidTarget(CombatEntity))
            return false;
        return true;
    }

    private void SwapTarget(Entity newTarget)
    {
        if (Target == newTarget)
            return;

        Target = newTarget;

        timeLastCombat = Time.ElapsedTimeFloat;

        if (Target.Type == EntityType.Player)
        {
            var p = newTarget.Get<Player>();
            if (p.Character.State == CharacterState.Dead)
                ServerLogger.LogWarning($"Monster {Character.Name} is attempting to change target to a dead player!");
            CommandBuilder.SendMonsterTarget(p, Character);
        }
    }

    private Position GetNextTileForDrop(int dropId)
    {
        if (dropId == 0)
            return Character.Position;

        var pos = dropId % 3;
        var tile = pos switch
        {
            0 => Character.Position.AddDirectionToPosition(Direction.North),
            1 => Character.Position.AddDirectionToPosition(Direction.SouthEast),
            2 => Character.Position.AddDirectionToPosition(Direction.West),
            _ => throw new Exception("Invalid drop position.")
        };
        var map = Character.Map!;
        if (map.WalkData.IsCellWalkable(tile) && !map.IsTileOccupied(tile, true))
            return tile;
        if (map.FindUnoccupiedAdjacentTile(tile, out var adjustTile, true))
            return adjustTile;
        return Character.Position;
    }

    public void DoMonsterDrops()
    {
        if (Character.Map == null)
            return;

        int dropId = 0;
        if (DataManager.MonsterDropData.TryGetValue(MonsterBase.Code, out var drops))
        {
            for (var i = 0; i < drops.DropChances.Count; i++)
            {
                var d = drops.DropChances[i];
                if (GameRandom.Next(10000) <= d.Chance)
                {
                    var count = 1;
                    if (d.CountMax > 1)
                        count = GameRandom.NextInclusive(d.CountMin, d.CountMax);
                    var dropPos = GetNextTileForDrop(dropId);
                    var item = new GroundItem(dropPos, d.Id, count);
                    Character.Map.DropGroundItem(ref item);
                    dropId++;
                }
            }
        }

        dropId = 3; //bonus drops start north
        if (inventoryCount > 0 && monsterInventory != null)
        {
            for (var i = 0; i < inventoryCount; i++)
            {
                var dropPos = GetNextTileForDrop(dropId);
                var item = new GroundItem(dropPos, ref monsterInventory[i]);
                Character.Map.DropGroundItem(ref item);
                dropId++;
            }

            inventoryIndex = 0;
            inventoryCount = 0;
        }
    }

    public void BoostDamageContributionOfFirstAttacker()
    {
        if (TotalDamageReceived == null || TotalDamageReceived.Count < 1)
            return;

        var dmgValues = TotalDamageReceived.InternalValueList;
        if(dmgValues != null)
            dmgValues[0] = dmgValues[0] * 130 / 100; //give first attacker a bonus contribution
    }

    public void RewardMVP()
    {
        var maxDamage = 0;
        WorldObject? damageDealer = null;
        if (TotalDamageReceived == null || TotalDamageReceived.Count == 0)
            return;

        foreach (var (attacker, damage) in TotalDamageReceived)
        {
            if (damage < maxDamage)
                continue;
            if (!attacker.TryGet<WorldObject>(out var player))
                continue;
            if (player.State == CharacterState.Dead || Character.Map != player.Map)
                continue;
            damageDealer = player;
            maxDamage = damage;
        }

        if (damageDealer == null)
            return;

        //if we're an mvp, give the attacker the effect
        Character.Map?.AddVisiblePlayersAsPacketRecipients(Character);
        CommandBuilder.SendEffectOnCharacterMulti(damageDealer, DataManager.EffectIdForName["MVP"]);
        CommandBuilder.ClearRecipients();
    }

    public void RewardExperience()
    {
        if (TotalDamageReceived == null || Character.Map == null)
            return;

        var exp = MonsterBase.Exp;
        var job = MonsterBase.JobExp;

        if (exp == 0 && job == 0)
            return;

        TotalDamageReceived.ClearDeadAndNotOnMap(Character.Map);
        var dmgValues = TotalDamageReceived.InternalValueList;

        if (TotalDamageReceived.Count == 0 || dmgValues == null || Character.Map == null)
            return;

        var acc = Character.Map.Instance.ExpCalculator;
        var totalDamage = 0;

        for (var i = 0; i < TotalDamageReceived.Count; i++)
            totalDamage += dmgValues[i];

        foreach (var (attacker, damage) in TotalDamageReceived)
        {
            var percent = damage / (float)totalDamage;
            if (attacker.TryGet<Player>(out var player))
                acc.AddExp(player, (int)float.Ceiling(exp * percent), (int)float.Ceiling(job * percent));
        }

        acc.DistributeExp();
    }

    /// <summary>
    /// This kills the monster.
    /// </summary>
    /// <param name="giveExperience">Whether killing this monster should reward experience to contributing players.</param>
    /// <param name="isMasterCommand">Is the issuer of this die command the master of this monster? If so, set this to suppress the RemoveChild callback.</param>
    public void Die(bool giveExperience = true, bool isMasterCommand = false, CharacterRemovalReason reason = CharacterRemovalReason.Dead)
    {
        if (CurrentAiState == MonsterAiState.StateDead)
            return;

        if (giveExperience && GivesExperience)
        {
            DoMonsterDrops();
            //CombatEntity.DistributeExperience();
            RewardExperience();
        }

        CombatEntity.OnDeathClearStatusEffects();
        Character.OnDeathCleanupEvents();

        CurrentAiState = MonsterAiState.StateDead;
        Character.State = CharacterState.Dead;
        
        Character.IsActive = false;
        Character.QueuedAction = QueuedAction.None;
        CombatEntity.IsCasting = false;
        WasAttacked = false;
        WasMagicLocked = false;
        WasRudeAttacked = false;
        canResetAttackedState = false;
        timeSinceLastDamage = 0;
        LastAttackRange = 0;
        CombatEntity.SetStat(CharacterStat.Disabled, 0); //if it's disabled in death it might not be able to TryRespawn
        TotalDamageReceived?.Dispose();
        TotalDamageReceived = null;

        skillState.ResetAllCooldowns();

        if (Children != null && Children.Count > 0)
        {
            foreach (var child in Children)
            {
                var childMonster = child.Get<Monster>();
                childMonster.Die(false, true);
            }

            Children.Clear();
        }

        if (Master.IsAlive() && !isMasterCommand)
        {
            var monster = Master.Get<Monster>();
            monster.RemoveChild(ref Entity); //they might not be registered as a child but we should try anyways
        }

        if (SpawnRule == null)
        {
            //ServerLogger.LogWarning("Attempting to remove entity without spawn data! How?? " + Character.ClassId);

            World.Instance.FullyRemoveEntity(ref Entity, reason);
            nextAiUpdate = float.MaxValue;
            nextAiSkillUpdate = float.MaxValue;
            //Character.ClearVisiblePlayerList();
            return;
        }

        Character.Map?.RemoveEntity(ref Entity, reason, false);
        deadTimeout = GameRandom.NextFloat(SpawnRule.MinSpawnTime / 1000f, SpawnRule.MaxSpawnTime / 1000f);
        if (deadTimeout < 0.4f)
            deadTimeout = 0.4f; //minimum respawn time
        //if (deadTimeout > MaxSpawnTimeInSeconds)
        //    deadTimeout = MaxSpawnTimeInSeconds;
        nextAiUpdate = Time.ElapsedTime + deadTimeout + 0.1f;
        deadTimeout += Time.ElapsedTimeFloat;

        CombatEntity.ClearDamageQueue();
        Character.ClearVisiblePlayerList(); //make sure this is at the end or the player may not be notified of the monster's death
    }

    public void ResetDelay()
    {
        nextAiUpdate = 0f;
    }

    public void AddDelay(float delay)
    {
        //usually to stop a monster from acting after taking fatal damage, but before the damage is applied
        nextAiUpdate += delay;
        if (nextAiUpdate < Time.ElapsedTime)
            nextAiUpdate = Time.ElapsedTime + delay;

#if DEBUG
        if (DebugLogging)
            DebugLog($"Next AI update in {nextAiUpdate - Time.ElapsedTime}");
#endif
    }

    private bool CanAssistAlly(int distance, out Entity newTarget)
    {
        newTarget = Entity.Null;

        //if (Time.ElapsedTimeFloat < allyScanTimeout)
        //             return false;

        var list = EntityListPool.Get();

        Debug.Assert(Character.Map != null, "Monster must be attached to a map");

        Character.Map.GatherMonstersOfTypeInRange(Character.Position, distance, list, MonsterBase);

        if (list.Count == 0)
        {
            EntityListPool.Return(list);
            return false;
        }

        for (var i = 0; i < list.Count; i++)
        {
            var entity = list[i];
            var monster = entity.Get<Monster>();

            //check if their ally is attacking, the target is alive, and they can see their ally
            if (monster.CurrentAiState == MonsterAiState.StateAttacking
                && Character.Map.WalkData.HasLineOfSight(Character.Position, monster.Character.Position))
            {
                var targetChara = monster.targetCharacter;
                if (targetChara == null || !targetChara.CombatEntity.IsValidTarget(CombatEntity))
                    continue;
                newTarget = monster.Target;
                EntityListPool.Return(list);
                return true;
            }
        }

        EntityListPool.Return(list);
        //allyScanTimeout = Time.ElapsedTimeFloat + 0.25f; //don't scan for allies in combat more than 4 times a second. It's slow.
        return false;
    }

    private bool FindRandomTargetInRange(int distance, out Entity newTarget)
    {
        var list = EntityListPool.Get();

        Character.Map!.GatherValidTargets(Character, distance, MonsterBase.Range, list);

        if (list.Count == 0)
        {
            EntityListPool.Return(list);
            newTarget = Entity.Null;
            return false;
        }

        newTarget = list.Count == 1 ? list[0] : list[GameRandom.NextInclusive(0, list.Count - 1)];

        EntityListPool.Return(list);

        return true;
    }

    public void RunCastSuccessEvent()
    {
        if (CastSuccessEvent != null && CurrentAiState != MonsterAiState.StateDead)
            CastSuccessEvent(skillState);
        //else
        //    ServerLogger.Debug("Cast success");
        CastSuccessEvent = null;
    }

    private void ResetFlagsAfterSkillHandler()
    {
        WasAttacked = false;
        WasRudeAttacked = false;
        WasMagicLocked = false;
        Character.LastAttacked = Entity.Null;
    }

    public void ResetSummonMonsterDeathTime()
    {
        skillState.MinionDeathTime = -1;
    }

    public bool AiSkillScanUpdate()
    {
        skillState.SkillCastSuccess = false;
        if (CurrentAiState != MonsterAiState.StateAttacking)
        {
            nextAiSkillUpdate = Time.ElapsedTimeFloat + 0.9f + GameRandom.NextFloat(0f, 0.2f); //we'd like to desync mob skill updates if possible
            if(!Character.HasVisiblePlayers())
                nextAiSkillUpdate += 2f;
        }

        skillAiHandler?.RunAiSkillUpdate(CurrentAiState, skillState);
        LastDamageSourceType = CharacterSkill.None; //clear this flag after doing a skill update

        canResetAttackedState = true;

#if DEBUG
        if (DebugLogging)
            DebugLog($"AI Skill Scan Update success: {skillState.SkillCastSuccess}");
#endif

        if (!skillState.SkillCastSuccess)
        {
            if (CurrentAiState == MonsterAiState.StateIdle)
                Target = Entity.Null;
            return false;
        }
        
        if (skillState.CastSuccessEvent != null)
        {
            if (skillState.ExecuteEventAtStartOfCast || (Character.QueuedAction == QueuedAction.None && !CombatEntity.IsCasting))
            {
                //we need to execute our event now
                skillState.CastSuccessEvent(skillState);
                CastSuccessEvent = null;
            }
            else
                CastSuccessEvent = skillState.CastSuccessEvent;
        }

        if (CurrentAiState == MonsterAiState.StateIdle)
            Target = Entity.Null;

        return true;
    }

    public void AiStateMachineUpdate()
    {
#if DEBUG
        if (!Character.IsActive && CurrentAiState != MonsterAiState.StateDead)
        {
            ServerLogger.LogWarning($"Monster was in incorrect state {CurrentAiState}, even though it should be dead (character is not active)");
            CurrentAiState = MonsterAiState.StateDead;
        }
#endif

        if (Character.State == CharacterState.Dead && CurrentAiState != MonsterAiState.StateDead)
        {
            ServerLogger.LogWarning($"Monster {Character} is attempting an AiStateMachineUpdate while in character state Dead, but their AI state is not StateDead (currently {CurrentAiState}.)");
            CurrentAiState = MonsterAiState.StateDead;
        }

        //if (CurrentAiState == MonsterAiState.StateChase)
        //    CurrentAiState = CurrentAiState;

        canResetAttackedState = false;

        if (skillAiHandler != null
            && nextAiSkillUpdate < Time.ElapsedTimeFloat
            && Character.State != CharacterState.Dead
            && !CombatEntity.IsCasting
            && !Character.InAttackCooldown
            && (CombatEntity.BodyState & BodyStateFlags.NoSkillAttack) == 0
            && CurrentAiState != MonsterAiState.StateAttacking //we handle this check on every attack in OnPerformAttack in Monster.Ai.cs
            && Character.QueuedAction != QueuedAction.Cast)
        {
            AiSkillScanUpdate();
            if (Character.State == CharacterState.Dead) //if we died during our own skill handler, bail
                return;
        }

        if (skillAiHandler == null)
            canResetAttackedState = true;
        
        for (var i = 0; i < aiEntries.Count; i++)
        {
            var entry = aiEntries[i];

            if (entry.InputState != CurrentAiState)
                continue;
#if DEBUG
            if(ServerConfig.DebugConfig.DebugMapOnly || DebugLogging)
                DebugLog($"Input test {entry.InputCheck}");
#endif

            if (!InputStateCheck(entry.InputCheck))
            {
                //ServerLogger.Debug($"{Entity}: Did not meet input requirements for {entry.InputCheck}");
                continue;
            }

            //ServerLogger.Debug($"{Entity}: Met input requirements for {entry.InputCheck}");

            if (!OutputStateCheck(entry.OutputCheck))
            {
                //ServerLogger.Debug($"{Entity}: Did not meet output requirements for {entry.OutputCheck}");
                continue;
            }

#if DEBUG
            if (ServerConfig.DebugConfig.DebugMapOnly || DebugLogging)
                DebugLog($"Transition {entry.InputCheck} -> {entry.OutputCheck} = {entry.OutputState}");
#endif

            PreviousAiState = CurrentAiState;
            CurrentAiState = entry.OutputState;
            timeofLastStateChange = Time.ElapsedTimeFloat;
        }

        if (canResetAttackedState)
            ResetFlagsAfterSkillHandler();

        if (nextAiUpdate < 0)
            return; //we set this to ensure we do an update next frame, so we'll do an update next frame
        
        if (Character.Map != null && Character.Map.PlayerCount == 0)
        {
            nextAiUpdate = Time.ElapsedTime + 2f + GameRandom.NextFloat(0f, 1f);
        }
        else
        {
            if (nextAiUpdate < Time.ElapsedTime)
            {
                if (nextAiUpdate + Time.DeltaTimeFloat < Time.ElapsedTime)
                    nextAiUpdate = Time.ElapsedTime + aiTickRate;
                else
                    nextAiUpdate += aiTickRate;

                if (!Character.HasVisiblePlayers())
                    nextAiUpdate += 0.5f;
            }
        }
    }

    private bool InCombatReadyState => Character.State == CharacterState.Idle && !CombatEntity.IsCasting &&
                                       Character.AttackCooldown < Time.ElapsedTime;

    public void Update()
    {
        if (Character.Map?.PlayerCount == 0)
            return;

        if (!IsAiActive)
        {
            if (Character.HasVisiblePlayers())
                IsAiActive = true;
            else
                return;
        }

        if (nextAiUpdate > Time.ElapsedTime)
            return;

        if (GetStat(CharacterStat.Disabled) > 0 || CombatEntity.IsCasting)
            return;
        
        if (Character.QueuedAction == QueuedAction.None || Character.QueuedAction == QueuedAction.Move)
            AiStateMachineUpdate();

        if (!InCombatReadyState)
            return;

        if (Character.QueuedAction == QueuedAction.Cast)
            CombatEntity.ResumeQueuedSkillAction();

        //a monster really shouldn't be queueing a move, how does that even happen...?
        if (Character.QueuedAction == QueuedAction.Move)
        {
            if (Character.UpdateAndCheckMoveLock())
                return;

            Character.QueuedAction = QueuedAction.None;
            Character.TryMove(Character.TargetPosition, 0);

            return;
        }
    }
}