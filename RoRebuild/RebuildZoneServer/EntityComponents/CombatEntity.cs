using System;
using System.Collections.Generic;
using Leopotam.Ecs;
using RebuildData.Server.Data.Character;
using RebuildData.Server.Logging;
using RebuildData.Server.Pathfinding;
using RebuildData.Shared.Data;
using RebuildData.Shared.Enum;
using RebuildZoneServer.Data.Management;
using RebuildZoneServer.Networking;
using RebuildZoneServer.Util;

namespace RebuildZoneServer.EntityComponents
{
	public class CombatEntity : IStandardEntity
	{
		public Character Character;

		public EcsEntity Entity;

		public BaseStats BaseStats; //base stats before being modified
		public CombatStats Stats; //modified stats

		[EcsIgnoreNullCheck]
		public List<DamageInfo> DamageQueue;


		public void Reset()
		{
			Character = null;
			Entity = EcsEntity.Null;
			BaseStats = null;
			Stats = null;
			
			if(DamageQueue == null)
				DamageQueue = new List<DamageInfo>(16);

			//bad! This will cause gen2 garbage
			BaseStats = null;
			Stats = null;
		}

		public void QueueDamage(DamageInfo info)
		{
			DamageQueue.Add(info);
			if (DamageQueue.Count > 1)
				DamageQueue.Sort((a, b) => a.Time.CompareTo(b.Time));
		}

		public bool IsValidTarget(CombatEntity source)
		{
			if (this == source)
				return false;
			if (Entity.IsNull() || !Entity.IsAlive())
				return false;
			if (!Character.IsActive || Character.State == CharacterState.Dead)
				return false;
			if (Character.Map == null)
				return false;
			if (source.Character.Map != Character.Map)
				return false;
			if (Character.SpawnImmunity > 0f)
				return false;
			if (source.Character.ClassId == Character.ClassId)
				return false;
			if (Character.ClassId == 1000)
				return false; //hack

			return true;
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
			
			Character.Map.GatherPlayersInRange(Character, 18, list);
			foreach (var e in list)
			{
				if (e.IsNull() || !e.IsAlive())
					continue;
				var ce = e.Get<CombatEntity>();
                
				if (ce == null || !ce.Character.IsActive)
					continue;

                if (ce.BaseStats.Level >= 99)
                    continue;

                var player = e.Get<Player>();

				ce.BaseStats.Experience += exp;
				
                var requiredExp = DataManager.ExpChart.ExpRequired[ce.BaseStats.Level];

                CommandBuilder.SendExpGain(player, exp);

				if (ce.BaseStats.Experience < requiredExp)
					continue;
				
                Character.Map.GatherPlayersForMultiCast(ref Entity, Character);

				while (ce.BaseStats.Experience >= requiredExp && ce.BaseStats.Level < 99)
                {
                    ce.BaseStats.Experience -= requiredExp;

					player.LevelUp();

					if(ce.BaseStats.Level < 99)
						requiredExp = DataManager.ExpChart.ExpRequired[ce.BaseStats.Level];
				}
				CommandBuilder.LevelUp(player.Character, ce.BaseStats.Level);
				CommandBuilder.SendHealMulti(player, 0, HealType.None);
            }

            CommandBuilder.ClearRecipients();

			EntityListPool.Return(list);
		}

		public void PerformMeleeAttack(CombatEntity target)
		{
#if DEBUG
			if(!target.IsValidTarget(this))
				throw new Exception("Entity attempting to attack an invalid target! This should be checked before calling PerformMeleeAttack.");
#endif
			if (Character.AttackCooldown + Time.DeltaTimeFloat + 0.005f < Time.ElapsedTimeFloat)
				Character.AttackCooldown = Time.ElapsedTimeFloat + Stats.AttackMotionTime; //they are consecutively attacking
			else
				Character.AttackCooldown += Stats.AttackMotionTime;

			Character.FacingDirection = DistanceCache.Direction(Character.Position, target.Character.Position);

			var atk1 = Stats.Atk;
			var atk2 = Stats.Atk2;
			if (atk1 <= 0)
				atk1 = 1;
			if (atk2 < atk1)
				atk2 = atk1;

			var baseDamage = (short)GameRandom.Next(atk1, atk2);

            var defCut = 2f;
            if (target.Stats.Def > 99)
                defCut = 1f;

            var damage = (short) (baseDamage * (1 - (target.Stats.Def / 100f / defCut)) - target.Stats.Vit * 0.7f);
            if (damage < 1)
                damage = 1;
			
			var di = new DamageInfo()
			{
				Damage = damage,
				HitCount = 1,
				KnockBack = 0,
				Source = Entity,
				Target = target.Entity,
				Time = Time.ElapsedTimeFloat + Stats.SpriteAttackTiming
			};

			//ServerLogger.Log($"{aiCooldown} {character.AttackCooldown} {angle} {dir}");

			Character.Map.GatherPlayersForMultiCast(ref Entity, Character);
			CommandBuilder.AttackMulti(Character, target.Character, di);
			CommandBuilder.ClearRecipients();
			
			target.QueueDamage(di);
			
			if (target.Character.Type == CharacterType.Monster && di.Damage > target.Stats.Hp)
			{
				var mon = target.Entity.Get<Monster>();
				mon.AddDelay(Stats.SpriteAttackTiming); //make sure it stops acting until it dies
			}

			if (Character.Type == CharacterType.Player && di.Damage > target.Stats.Hp)
			{
				var player = Entity.Get<Player>();
				player.ClearTarget();
			}
		}

		public void Init(ref EcsEntity e, Character ch)
		{
			Entity = e;
			Character = ch;
			BaseStats = new BaseStats();
			Stats = new CombatStats();
			DamageQueue.Clear();

			Stats.Range = 2;
		}

		private void AttackUpdate()
		{
			while (DamageQueue.Count > 0 && DamageQueue[0].Time < Time.ElapsedTimeFloat)
			{
				var di = DamageQueue[0];
				DamageQueue.RemoveAt(0);
				if (di.Source.IsNull() || !di.Source.IsAlive())
					continue;
				var enemy = di.Source.Get<Character>();
				if (enemy == null)
					continue;
				if (!enemy.IsActive || enemy.Map != Character.Map)
					continue;
				if (enemy.Position.SquareDistance(Character.Position) > 31)
					continue;
				
				if (Character.State == CharacterState.Dead || enemy.State == CharacterState.Dead)
					break;

				if (Character.State == CharacterState.Sitting)
					Character.State = CharacterState.Idle;
			
				//inform clients the player was hit and for how much
				Character.Map.GatherPlayersForMultiCast(ref Entity, Character);
                if(Character.AddMoveDelay(Stats.HitDelayTime))
					CommandBuilder.SendHitMulti(Character, Stats.HitDelayTime, di.Damage);
				else
                    CommandBuilder.SendHitMulti(Character, -1, di.Damage);
				CommandBuilder.ClearRecipients();
			

				if(!di.Target.IsNull() && di.Source.IsAlive())
					Character.LastAttacked = di.Source;

				var ec = di.Target.Get<CombatEntity>();
				ec.Stats.Hp -= di.Damage;

                if (ec.Stats.Hp <= 0)
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
                        ec.Stats.Hp = 0;
						player.Die();
                        return;
                    }
				}
			}
		}

		public void Update()
		{
			Profiler.Event(ProfilerEvent.CombatEntityUpdate);

			if (Character == null || !Character.IsActive)
				return;
			if (DamageQueue.Count > 0)
				AttackUpdate();
		}
	}
}
