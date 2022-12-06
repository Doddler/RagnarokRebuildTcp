using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.Data.Map;
using RoRebuildServer.Data.Monster;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.EntityComponents;

[EntityComponent(EntityType.Monster)]
public partial class Monster : IEntityAutoReset
{
    public Entity Entity;
    public WorldObject Character;
    public CombatEntity CombatEntity;
    
    private float aiTickRate;
    //private float aiCooldown;

    private float nextAiUpdate { get; set; }
    private float nextMoveUpdate;

    //private float randomMoveCooldown;

    private const float minIdleWaitTime = 3f;
    private const float maxIdleWaitTime = 6f;

    private bool hasTarget;

    private Entity Target;

    private WorldObject targetCharacter => Target.GetIfAlive<WorldObject>();

    public MonsterDatabaseInfo MonsterBase;
    //public MapSpawnEntry SpawnEntry;
    public string SpawnMap;
    public MapSpawnRule? SpawnRule;
    private MonsterAiType aiType;
    private List<MonsterAiEntry> aiEntries;

    public bool LockMovementToSpawn;

    public MonsterAiState CurrentAiState;

    private WorldObject searchTarget;

    private float deadTimeout;
    private float allyScanTimeout;

    public static float MaxSpawnTimeInSeconds = 180;

    public void SetStat(CharacterStat type, int val) => CombatEntity.SetStat(type, val);
    public void SetTiming(TimingStat type, float val) => CombatEntity.SetTiming(type, val);

	public void Reset()
    {
        Entity = Entity.Null;
        Character = null;
        aiEntries = null;
        //SpawnEntry = null;
        CombatEntity = null;
        searchTarget = null;
        aiTickRate = 0.1f;
        nextAiUpdate = Time.ElapsedTimeFloat + GameRandom.NextFloat(0, aiTickRate);
        SpawnRule = null;
        MonsterBase = null;
        SpawnMap = null;
		
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

        if (SpawnRule != null)
        {
            LockMovementToSpawn = SpawnRule.LockToSpawn;
        }

        //if (Character.ClassId >= 4000 && spawnEntry == null)
        //    throw new Exception("Monster created without spawn entry"); //remove when arbitrary monster spawning is added

        UpdateStats();

        character.Name = $"{monData.Name} {e}";

        CurrentAiState = MonsterAiState.StateIdle;
    }


	private void UpdateStats()
	{
		SetStat(CharacterStat.Level, MonsterBase.Level);
		SetStat(CharacterStat.Hp, MonsterBase.HP);
		SetStat(CharacterStat.MaxHp, MonsterBase.HP);
		SetStat(CharacterStat.Attack, MonsterBase.AtkMin);
		SetStat(CharacterStat.Attack2, MonsterBase.AtkMax);
        SetStat(CharacterStat.Range, MonsterBase.Range);
        SetStat(CharacterStat.Def, MonsterBase.Def);
        SetStat(CharacterStat.Vit, MonsterBase.Vit);
        SetTiming(TimingStat.MoveSpeed, MonsterBase.MoveSpeed);
		SetTiming(TimingStat.SpriteAttackTiming, MonsterBase.SpriteAttackTiming);
		SetTiming(TimingStat.HitDelayTime, MonsterBase.HitTime);
		SetTiming(TimingStat.AttackMotionTime, MonsterBase.RechargeTime);
        Character.MoveSpeed = MonsterBase.MoveSpeed;
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

		if (Target.Type == EntityType.Player)
        {
            var p = newTarget.Get<Player>();
			CommandBuilder.SendMonsterTarget(p, Character);
		}

	}

	public void Die()
	{
		if (CurrentAiState == MonsterAiState.StateDead)
			return;

		CurrentAiState = MonsterAiState.StateDead;
		Character.State = CharacterState.Dead;

		CombatEntity.DistributeExperience();
		
		Character.IsActive = false;
		
		if (SpawnRule == null)
		{
			//ServerLogger.LogWarning("Attempting to remove entity without spawn data! How?? " + Character.ClassId);
            
            World.Instance.FullyRemoveEntity(ref Entity, CharacterRemovalReason.Dead);
            //Character.ClearVisiblePlayerList();
            return;
        }

        Character.Map.RemoveEntity(ref Entity, CharacterRemovalReason.Dead, false);
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
		nextAiUpdate = Time.ElapsedTimeFloat + delay;
	}

	private bool CanAssistAlly(int distance, out Entity newTarget)
	{
		newTarget = Entity.Null;

		//if (Time.ElapsedTimeFloat < allyScanTimeout)
		//             return false;

		var list = EntityListPool.Get();

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
				&& monster.Target.IsAlive()
				&& Character.Map.WalkData.HasLineOfSight(Character.Position, monster.Character.Position))
			{
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

	public void AiStateMachineUpdate()
	{
#if DEBUG
		if (!Character.IsActive && CurrentAiState != MonsterAiState.StateDead)
		{
			ServerLogger.LogWarning($"Monster was in incorrect state {CurrentAiState}, even though it should be dead (character is not active)");
			CurrentAiState = MonsterAiState.StateDead;
		}
#endif

		//Profiler.Event(ProfilerEvent.MonsterStateMachineUpdate);

        //ServerLogger.Debug($"{Entity}: Checking AI for state {CurrentAiState}");

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

            //ServerLogger.Debug($"{Entity}: Met output requirements for {entry.OutputCheck}! Changing state to {entry.OutputState}");
			
			CurrentAiState = entry.OutputState;
		}

		Character.LastAttacked = Entity.Null;

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
			}
		}
	}

	public void Update()
    {
        if (Character.Map?.PlayerCount == 0)
            return;

        if (nextAiUpdate > Time.ElapsedTimeFloat)
            return;

        AiStateMachineUpdate();

		//if(GameRandom.Next(4000) == 42)
		//	Die();
	}
}