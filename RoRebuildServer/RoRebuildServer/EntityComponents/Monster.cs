using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.Data.Map;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Monsters;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Pathfinding;
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

    private float nextAiUpdate;
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

    public void AdjustAiUpdateIfShorter(float time)
    {
        if (time < nextAiUpdate)
            nextAiUpdate = time;
    }

    private float nextMoveUpdate;
    private float nextAiSkillUpdate;
    private float timeofLastStateChange;
    //private float timeEnteredCombat;
    private float timeLastCombat;
    private float timeSinceLastDamage;
    private float createTime;

    public void UpdateStateChangeTime() => timeofLastStateChange = Time.ElapsedTimeFloat;
    public float TimeInCurrentAiState => Time.ElapsedTimeFloat - timeofLastStateChange;
    //private float durationInCombat => Target.IsAlive() ? Time.ElapsedTimeFloat - timeEnteredCombat : -1f;
    public float DurationOutOfCombat => !Target.IsAlive() ? Time.ElapsedTimeFloat - timeLastCombat : -1;
    public float TimeSinceLastDamage => Time.ElapsedTimeFloat - timeSinceLastDamage;
    public float TimeAlive => Time.ElapsedTimeFloat - createTime;

    //private float randomMoveCooldown;

    private const float minIdleWaitTime = 3f;
    private const float maxIdleWaitTime = 6f;

    //private bool hasTarget;

    public Entity Target;
    private Entity Master;
    private EntityList? Children;

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

    public bool LockMovementToSpawn;
    public bool GivesExperience;

    public MonsterAiState CurrentAiState;
    public MonsterAiState PreviousAiState;
    public CharacterSkill LastDamageSourceType;
    public int LastAttackRange;
    public bool WasAttacked;
    public bool WasMagicLocked;
    private bool canResetAttackedState;

    //private WorldObject searchTarget = null!;

    private float deadTimeout;
    //private float allyScanTimeout;
    private bool inAdjustMove;
#if DEBUG
    private bool dbgFlag;
#endif

    public bool HasMaster => Master.IsAlive();
    public Entity GetMaster() => Master;
    public Entity SetMaster(Entity master) => Master = master;

    public static float MaxSpawnTimeInSeconds = 180;

    public void SetStat(CharacterStat type, int val) => CombatEntity.SetStat(type, val);
    public int GetStat(CharacterStat type) => CombatEntity.GetStat(type);
    public void SetTiming(TimingStat type, float val) => CombatEntity.SetTiming(type, val);

    public void CallDeathEvent() => skillAiHandler?.OnDie(skillState);

    public void ChangeAiSkillHandler(string newHandler)
    {
        if (DataManager.MonsterSkillAiHandlers.TryGetValue(newHandler, out var handler))
            skillAiHandler = handler;
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
        timeSinceLastDamage = Time.ElapsedTimeFloat;
        LastDamageSourceType = di.AttackSkill;
        if (di.Source.TryGet<WorldObject>(out var src))
            LastAttackRange = Character.Position.DistanceTo(src.Position);
        else
            LastAttackRange = 0;
        ResetAiUpdateTime();
        Character.StopMovingImmediately();
        WasAttacked = true;
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
        aiEntries = null!;
        //SpawnEntry = null;
        CombatEntity = null!;
        //searchTarget = null!;
        aiTickRate = 0.1f;
        nextAiUpdate = Time.ElapsedTimeFloat + GameRandom.NextFloat(0, aiTickRate);
        SpawnRule = null;
        MonsterBase = null!;
        SpawnMap = null!;
        Children = null;
        skillState = null!; //really should pool these?
        skillAiHandler = null;
        CastSuccessEvent = null;
        WasAttacked = false;
        WasMagicLocked = false;
        canResetAttackedState = false;
        timeSinceLastDamage = 0;
        LastAttackRange = 0;

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
        nextAiUpdate = Time.ElapsedTimeFloat + 1f;
        nextAiSkillUpdate = Time.ElapsedTimeFloat + 1f;
        aiTickRate = 0.05f;
        LastDamageSourceType = CharacterSkill.None;
        GivesExperience = true;

        if (SpawnRule != null)
        {
            LockMovementToSpawn = SpawnRule.LockToSpawn;
        }

        //if (Character.ClassId >= 4000 && spawnEntry == null)
        //    throw new Exception("Monster created without spawn entry"); //remove when arbitrary monster spawning is added

        InitializeStats();

        character.Name = $"{monData.Name} {e}";

        CurrentAiState = MonsterAiState.StateIdle;
        skillState = new MonsterSkillAiState(this);

        if (DataManager.MonsterSkillAiHandlers.TryGetValue(monData.Code, out var handler))
            skillAiHandler = handler;

        timeofLastStateChange = Time.ElapsedTimeFloat;
        timeLastCombat = Time.ElapsedTimeFloat;
        createTime = Time.ElapsedTimeFloat;
        //timeEnteredCombat = float.NegativeInfinity;
    }

    public void AddChild(ref Entity child, MonsterAiType newAiType = MonsterAiType.AiEmpty)
    {
        if (!child.IsAlive() && child.Type != EntityType.Monster)
            ServerLogger.LogError($"Cannot AddChild on monster {Character.Name} when child entity {child} is not alive or not a monster.");

        var childMon = child.Get<Monster>();

        Children ??= new EntityList();
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

    private void InitializeStats()
    {
        var magicMin = MonsterBase.Int + MonsterBase.Int / 7 * MonsterBase.Int / 7;
        var magicMax = MonsterBase.Int + MonsterBase.Int / 5 * MonsterBase.Int / 5;

        if (magicMin < MonsterBase.AtkMin / 2)
            magicMin = MonsterBase.AtkMin / 2;
        if (magicMax < MonsterBase.AtkMax / 2)
            magicMax = MonsterBase.AtkMax / 2;

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
        var aspdBonus = 100f / (GetStat(CharacterStat.AspdBonus) + 100);

        var recharge = MonsterBase.RechargeTime * aspdBonus;
        var motionTime = MonsterBase.AttackLockTime;
        var spriteTime = MonsterBase.AttackDamageTiming;
        if (recharge < motionTime)
        {
            var ratio = recharge / motionTime;
            motionTime *= ratio;
            spriteTime *= ratio;
        }

        SetTiming(TimingStat.AttackDelayTime, recharge);
        SetTiming(TimingStat.AttackMotionTime, motionTime);
        SetTiming(TimingStat.SpriteAttackTiming, spriteTime);

        var moveBonus = 100f / (100f + GetStat(CharacterStat.MoveSpeedBonus));
        if (CombatEntity.HasStatusEffectOfType(CharacterStatusEffect.Curse))
            moveBonus = 0.1f;
        var moveSpeed = MonsterBase.MoveSpeed * moveBonus;
        SetTiming(TimingStat.MoveSpeed, moveSpeed);
        Character.MoveSpeed = moveSpeed;
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

    /// <summary>
    /// This kills the monster.
    /// </summary>
    /// <param name="giveExperience">Whether killing this monster should reward experience to contributing players.</param>
    /// <param name="isMasterCommand">Is the issuer of this die command the master of this monster? If so, set this to suppress the RemoveChild callback.</param>
    public void Die(bool giveExperience = true, bool isMasterCommand = false, CharacterRemovalReason reason = CharacterRemovalReason.Dead)
    {
        if (CurrentAiState == MonsterAiState.StateDead)
            return;

        CombatEntity.OnDeathClearStatusEffects();

        CurrentAiState = MonsterAiState.StateDead;
        Character.State = CharacterState.Dead;

        if (giveExperience && GivesExperience)
            CombatEntity.DistributeExperience();

        Character.IsActive = false;
        Character.QueuedAction = QueuedAction.None;
        CombatEntity.IsCasting = false;
        WasAttacked = false;
        WasMagicLocked = false;
        canResetAttackedState = false;
        timeSinceLastDamage = 0;
        LastAttackRange = 0;
        CombatEntity.SetStat(CharacterStat.Disabled, 0); //if it's disabled in death it might not be able to TryRespawn

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
        if (deadTimeout > MaxSpawnTimeInSeconds)
            deadTimeout = MaxSpawnTimeInSeconds;
        nextAiUpdate = Time.ElapsedTimeFloat + deadTimeout + 0.1f;
        deadTimeout += Time.ElapsedTimeFloat;

        Character.ClearVisiblePlayerList(); //make sure this is at the end or the player may not be notified of the monster's death
    }

    public void ResetDelay()
    {
        nextAiUpdate = 0f;
    }

    public void AddDelay(float delay)
    {
        //usually to stop a monster from acting after taking fatal damage, but before the damage is applied
        //Character.DebugMessage($"{Character.Name} set AI update time to {nextAiUpdate}");
        nextAiUpdate += delay;
        if (nextAiUpdate < Time.ElapsedTimeFloat)
            nextAiUpdate = Time.ElapsedTimeFloat + delay;
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
        if (CastSuccessEvent != null)
            CastSuccessEvent(skillState);
        //else
        //    ServerLogger.Debug("Cast success");
        CastSuccessEvent = null;
    }

    private void ResetFlagsAfterSkillHandler()
    {
        WasAttacked = false;
        WasMagicLocked = false;
        Character.LastAttacked = Entity.Null;
    }

    public bool AiSkillScanUpdate()
    {
        skillState.SkillCastSuccess = false;
        if (CurrentAiState != MonsterAiState.StateAttacking)
            nextAiSkillUpdate = Time.ElapsedTimeFloat + 0.85f + GameRandom.NextFloat(0f, 0.3f); //we'd like to desync mob skill updates if possible

        skillAiHandler?.RunAiSkillUpdate(CurrentAiState, skillState);
        LastDamageSourceType = CharacterSkill.None; //clear this flag after doing a skill update

        canResetAttackedState = true;
        
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

        canResetAttackedState = false;

        if (skillAiHandler != null
            && nextAiSkillUpdate < Time.ElapsedTimeFloat
            && Character.State != CharacterState.Dead
            && !CombatEntity.IsCasting
            && !Character.InAttackCooldown
            && CurrentAiState != MonsterAiState.StateAttacking //we handle this check on every attack in OnPerformAttack in Monster.Ai.cs
            && Character.QueuedAction != QueuedAction.Cast)
        {
            AiSkillScanUpdate();
            if (Character.State == CharacterState.Dead) //if we died during our own skill handler, bail
                return;
        }
        
        for (var i = 0; i < aiEntries.Count; i++)
        {
            var entry = aiEntries[i];

            if (entry.InputState != CurrentAiState)
                continue;

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
            if (ServerConfig.DebugConfig.DebugMapOnly)
                ServerLogger.Debug($"{Entity}: AI state change from {CurrentAiState}: {entry.InputCheck} -> {entry.OutputCheck} = {entry.OutputState}");
#endif

            PreviousAiState = CurrentAiState;
            CurrentAiState = entry.OutputState;
            timeofLastStateChange = Time.ElapsedTimeFloat;
        }


        if (canResetAttackedState)
            ResetFlagsAfterSkillHandler();
        
        if (Character.Map != null && Character.Map.PlayerCount == 0)
        {
            nextAiUpdate = Time.ElapsedTimeFloat + 2f + GameRandom.NextFloat(0f, 1f);
        }
        else
        {
            if (nextAiUpdate < Time.ElapsedTimeFloat)
            {
                if (nextAiUpdate + Time.DeltaTimeFloat < Time.ElapsedTimeFloat)
                    nextAiUpdate = Time.ElapsedTimeFloat + aiTickRate;
                else
                    nextAiUpdate += aiTickRate;

                if (!Character.HasVisiblePlayers())
                    nextAiUpdate += 0.5f;
            }
        }
    }

    private bool InCombatReadyState => Character.State == CharacterState.Idle && !CombatEntity.IsCasting &&
                                       Character.AttackCooldown < Time.ElapsedTimeFloat;

    public void Update()
    {
        if (Character.Map?.PlayerCount == 0)
            return;

        if (nextAiUpdate > Time.ElapsedTimeFloat)
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

        //if(GameRandom.Next(4000) == 42)
        //	Die();
    }
}