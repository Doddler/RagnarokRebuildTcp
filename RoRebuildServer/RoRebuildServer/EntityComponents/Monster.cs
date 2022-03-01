using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.Data.Map;
using RoRebuildServer.Data.Monster;
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

    public MonsterAiState CurrentAiState;

    private WorldObject searchTarget;

    private float deadTimeout;
    private float allyScanTimeout;

    public static float MaxSpawnTimeInSeconds = 60000; //1 minute // 604800; //one week

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

        Target = Entity.Null;
    }
    
    public void Initialize(ref Entity e, WorldObject character, CombatEntity combat, MonsterDatabaseInfo monData, MonsterAiType type, MapSpawnRule spawnEntry, string mapName)
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

        if (Character.ClassId >= 4000 && spawnEntry == null)
            throw new Exception("Monster created without spawn entry"); //remove when arbitrary monster spawning is added

        UpdateStats();

        character.Name = $"{monData.Name} {e}";

        CurrentAiState = MonsterAiState.StateIdle;
    }


	private void UpdateStats()
	{
		var b = CombatEntity.BaseStats;
		var s = CombatEntity.Stats;

		b.Level = MonsterBase.Level;
		b.MaxHp = s.MaxHp = s.Hp = MonsterBase.HP;
		b.MaxSp = s.MaxHp = MonsterBase.HP;
		b.Atk = s.Atk = (short)MonsterBase.AtkMin;
		b.Atk2 = s.Atk2 = (short)MonsterBase.AtkMax;
		b.MoveSpeed = s.MoveSpeed = MonsterBase.MoveSpeed;
		b.Range = s.Range = MonsterBase.Range;
		b.SpriteAttackTiming = s.SpriteAttackTiming = MonsterBase.SpriteAttackTiming;
		b.HitDelayTime = s.HitDelayTime = MonsterBase.HitTime;
		b.AttackMotionTime = s.AttackMotionTime = MonsterBase.RechargeTime;

		s.Def = MonsterBase.Def;
		s.Vit = (short)MonsterBase.Vit;

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
			ServerLogger.LogWarning("Attempting to remove entity without spawn data! How?? " + Character.ClassId);
            Character.Map.RemoveEntity(ref Entity, CharacterRemovalReason.Dead, true);
			//World.Instance.FullyRemoveEntity(ref Entity);
		}
		else
		{
            Character.Map.RemoveEntity(ref Entity, CharacterRemovalReason.Dead, false);
			deadTimeout = GameRandom.NextFloat(SpawnRule.MinSpawnTime / 1000f, SpawnRule.MaxSpawnTime / 1000f);
			if (deadTimeout < 0.4f)
				deadTimeout = 0.4f; //minimum respawn time
			if (deadTimeout > MaxSpawnTimeInSeconds)
				deadTimeout = MaxSpawnTimeInSeconds;
			nextAiUpdate = Time.ElapsedTimeFloat + deadTimeout + 0.1f;
			deadTimeout += Time.ElapsedTimeFloat;
		}
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

		Character.Map.GatherMonstersOfTypeInRange(Character, distance, list, MonsterBase);

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

		Character.Map.GatherPlayersInRange(Character, distance, list, true, true);

		if (list.Count == 0)
		{
			EntityListPool.Return(list);
			newTarget = Entity.Null;
			return false;
		}

		newTarget = list.Count == 1 ? list[0] : list[GameRandom.Next(0, list.Count - 1)];

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

		for (var i = 0; i < aiEntries.Count; i++)
		{
			var entry = aiEntries[i];

			if (entry.InputState != CurrentAiState)
				continue;

			if (!InputStateCheck(entry.InputCheck))
				continue;

			if (!OutputStateCheck(entry.OutputCheck))
				continue;

			//ServerLogger.Log($"Monster from {entry.InputState} to state {entry.OutputState} (via {entry.InputCheck}/{entry.OutputCheck})");

			//Profiler.Event(ProfilerEvent.MonsterStateMachineChangeSuccess);

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