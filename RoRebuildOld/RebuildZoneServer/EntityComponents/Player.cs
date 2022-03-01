using System;
using Leopotam.Ecs;
using RebuildData.Server.Data.Character;
using RebuildData.Server.Logging;
using RebuildData.Server.Pathfinding;
using RebuildData.Shared.Data;
using RebuildData.Shared.Enum;
using RebuildZoneServer.Data.Management;
using RebuildZoneServer.Data.Management.Types;
using RebuildZoneServer.Networking;
using RebuildZoneServer.Sim;
using RebuildZoneServer.Util;

namespace RebuildZoneServer.EntityComponents
{
    public class Player : IStandardEntity
    {
        public EcsEntity Entity;
        public Character Character;
        public CombatEntity CombatEntity;

        public NetworkConnection Connection;
        public string Name { get; set; }
        public float CurrentCooldown;
        public HeadFacing HeadFacing;
        //public PlayerData Data { get; set; }
        public byte HeadId;
        public bool IsMale;

        public float regenTickTime { get; set; }
        
        public EcsEntity Target { get; set; }

        public bool QueueAttack;

        public void Reset()
        {
            Entity = EcsEntity.Null;
            Target = EcsEntity.Null;
            Character = null;
            CombatEntity = null;
            Connection = null;
            CurrentCooldown = 0f;
            HeadId = 0;
            HeadFacing = HeadFacing.Center;
            IsMale = true;
            QueueAttack = false;
            Name = "Player";
            //Data = new PlayerData(); //fix this...
            regenTickTime = 0f;
        }

        public void Init()
        {
            UpdateStats();
        }

        private void UpdateStats()
        {
            var s = CombatEntity.Stats;
            
            s.AttackMotionTime = 0.9f;
            s.HitDelayTime = 0.5f;
            s.SpriteAttackTiming = 0.6f;
            Character.MoveSpeed -= 0.003f;
            
            s.Range = 2;

            CombatEntity.BaseStats.Level = 0;
            LevelUp(); //fixes attack stats and all that
        }

        public void LevelUp()
        {
            var bs = CombatEntity.BaseStats;
            var s = CombatEntity.Stats;
            
            bs.Level++;
            s.AttackMotionTime -= 0.004f;
            if (s.SpriteAttackTiming > s.AttackMotionTime)
                s.SpriteAttackTiming = s.AttackMotionTime;

            if(bs.Level < 50)
                Character.MoveSpeed -= 0.0006f;

            var atk = (bs.Level / 2f) * (bs.Level / 2f) + bs.Level * (bs.Level/10) + 7 + bs.Level;
            s.Atk = (short) (atk * 0.90f - 1);
            s.Atk2 = (short) (atk * 1.10f + 1);
            s.Def++;
            s.Vit++;
            s.MaxHp = 50 + 100 * bs.Level;

            var multiplier = 0.1f + bs.Level / 10f;
            if (multiplier > 1f)
                multiplier = 1f;
            

            var newMaxHp = (bs.Level * bs.Level * bs.Level) / 20 + 80 * bs.Level;

            s.MaxHp = bs.MaxHp = (int) (newMaxHp * multiplier) + 70;

            s.Hp = s.MaxHp;
        }

        public void UpdateSit(bool isSitting)
        {
            if (!isSitting)
            {
                regenTickTime += 4f;
                if (regenTickTime > Time.ElapsedTimeFloat + 8f)
                    regenTickTime = Time.ElapsedTimeFloat + 8f;
            }
            else
            {
                if (regenTickTime > Time.ElapsedTimeFloat + 4f)
                    regenTickTime = Time.ElapsedTimeFloat + 4;
            }
        }

        public void RegenTick()
        {
            var hp = CombatEntity.Stats.Hp;
            var maxHp = CombatEntity.Stats.MaxHp;

            if (hp < maxHp)
            {
                var regen = maxHp / 10;
                if (Character.State == CharacterState.Sitting)
                    regen *= 2;
                if (regen + hp > maxHp)
                    regen = maxHp - hp;


                CombatEntity.Stats.Hp += regen;
                
                CommandBuilder.SendHealSingle(this, regen, HealType.None);
            }
        }
        
        public void Die()
        {
            if (Character.State == CharacterState.Dead)
                return; //we're already dead!

            ClearTarget();
            Character.StopMovingImmediately();
            Character.State = CharacterState.Dead;
            
            Character.Map.GatherPlayersForMultiCast(ref Entity, Character);
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

            var ce = Target.Get<CombatEntity>();
            if (ce == null || !ce.IsValidTarget(CombatEntity))
                return false;

            return true;
        }
        
        public void ClearTarget()
        {
            QueueAttack = false;

            if(!Target.IsNull())
                CommandBuilder.SendChangeTarget(this, null);

            Target = EcsEntity.Null;
        }
        
        public void ChangeTarget(Character target)
        {
            if (target == null || Target == target.Entity)
                return;

            CommandBuilder.SendChangeTarget(this, target);

            Target = target.Entity;
        }
        
        public void PerformQueuedAttack()
        {
            //QueueAttack = false;
            if (!ValidateTarget())
            {
                QueueAttack = false;
                return;
            }

            var targetCharacter = Target.Get<Character>();
            if (!targetCharacter.IsActive)
            {
                QueueAttack = false;
                return;
            }

            if (targetCharacter.Map != Character.Map)
            {
                QueueAttack = false;
                return;
            }

            if (Character.Position.SquareDistance(targetCharacter.Position) > CombatEntity.Stats.Range)
            {
                TargetForAttack(targetCharacter);
                return;
            }

            PerformAttack(targetCharacter);
        }
        public void PerformAttack(Character targetCharacter)
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

            Character.StopMovingImmediately();

            if (Character.AttackCooldown > Time.ElapsedTimeFloat)
            {
                QueueAttack = true;
                if (Target != targetCharacter.Entity)
                    ChangeTarget(targetCharacter);

                return;
            }

            Character.SpawnImmunity = -1;

            CombatEntity.PerformMeleeAttack(targetEntity);

            QueueAttack = true;

            Character.AttackCooldown = Time.ElapsedTimeFloat + CombatEntity.Stats.AttackMotionTime;
        }

        public void PerformSkill()
        {
            var pool = EntityListPool.Get();
            Character.Map.GatherEntitiesInRange(Character, 7, pool);

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
            }

            Character.AttackCooldown = Time.ElapsedTimeFloat + CombatEntity.Stats.AttackMotionTime;
        }

        public void UpdatePosition(Position nextPos)
        {
            var connector = DataManager.GetConnector(Character.Map.Name, nextPos);

            if (connector != null)
            {
                Character.State = CharacterState.Idle;

                if (connector.Map == connector.Target)
                    Character.Map.MoveEntity(ref Entity, Character, connector.DstArea.RandomInArea());
                else
                    Character.Map.World.MovePlayerMap(ref Entity, Character, connector.Target, connector.DstArea.RandomInArea());

                CombatEntity.ClearDamageQueue();

                return;
            }

            if (!ValidateTarget())
                return;

            var targetCharacter = Target.Get<Character>();

            if (Character.State == CharacterState.Moving)
            {
                if (Character.Position.SquareDistance(targetCharacter.Position) <= CombatEntity.Stats.Range)
                    PerformAttack(targetCharacter);
            }

            if (Character.State == CharacterState.Idle)
            {
                TargetForAttack(targetCharacter);
            }
        }
        
        public void TargetForAttack(Character enemy)
        {
            if (Character.Position.SquareDistance(enemy.Position) <= CombatEntity.Stats.Range)
            {
                ChangeTarget(enemy);
                PerformAttack(enemy);
                return;
            }

            if (!Character.TryMove(ref Entity, enemy.Position, 0))
                return;

            ChangeTarget(enemy);
        }
        
        public bool InActionCooldown() => CurrentCooldown > 1f;
        public void AddActionDelay(CooldownActionType type) => CurrentCooldown += ActionDelay.CooldownTime(type);
        public void AddActionDelay(float time) => CurrentCooldown += CurrentCooldown;

        public void Update()
        {
            Profiler.Event(ProfilerEvent.PlayerUpdate);

            if (QueueAttack)
            {
                if (Character.AttackCooldown < Time.ElapsedTimeFloat)
                    PerformQueuedAttack();
            }

            if (regenTickTime < Time.ElapsedTimeFloat)
            {
                RegenTick();
                if(Character.State == CharacterState.Sitting)
                    regenTickTime = Time.ElapsedTimeFloat + 4f;
                else
                    regenTickTime = Time.ElapsedTimeFloat + 8f;
            }

            CurrentCooldown -= Time.DeltaTimeFloat;

            if (CurrentCooldown < 0)
                CurrentCooldown = 0;
        }
    }
}
