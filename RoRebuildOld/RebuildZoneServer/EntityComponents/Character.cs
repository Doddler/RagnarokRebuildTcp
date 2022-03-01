using Leopotam.Ecs;
using RebuildData.Server.Logging;
using RebuildData.Server.Pathfinding;
using RebuildData.Shared.Data;
using RebuildData.Shared.Enum;
using RebuildZoneServer.Networking;
using RebuildZoneServer.Sim;
using RebuildZoneServer.Util;

namespace RebuildZoneServer.EntityComponents
{
	public class Character : IStandardEntity
	{
		public int Id { get; set; }
		public EcsEntity Entity;
		public bool IsActive;
		public int ClassId;
		public Direction FacingDirection;
		public CharacterState State;
		public CharacterType Type;
		public Position Position;
		public Position TargetPosition;

		public Position[] WalkPath;

		public EcsEntity LastAttacked;

		public float SpawnImmunity;
		public float AttackCooldown;
		public float MoveSpeed;
		public float MoveCooldown;
		public float HitDelay;
		public int MoveStep;
		public int TotalMoveSteps;
		
		public Map Map;

		public void Reset()
		{
			Id = -1;
			Entity = EcsEntity.Null;
			LastAttacked = EcsEntity.Null;
			IsActive = true;
			Map = null;
			State = CharacterState.Idle;
			MoveCooldown = 0;
			MoveSpeed = 0.15f;
			MoveStep = 0;
			Position = new Position();
			TargetPosition = new Position();
			FacingDirection = Direction.South;
			WalkPath = null;
		}

		public void ResetState()
		{
			MoveCooldown = 0;
			State = CharacterState.Idle;
		}

		public void SitStand(bool isSitting)
		{
			if (Type != CharacterType.Player)
				return;

			if (State == CharacterState.Moving || State == CharacterState.Dead)
				return;

			if (isSitting)
				State = CharacterState.Sitting;
			else
				State = CharacterState.Idle;

            var player = Entity.Get<Player>();
			player.UpdateSit(isSitting);
			
			Map.GatherPlayersForMultiCast(ref Entity, this);
			CommandBuilder.ChangeSittingMulti(this);
			CommandBuilder.ClearRecipients();
		}

		public void ChangeLookDirection(ref EcsEntity entity, Direction direction, HeadFacing facing)
		{
			if (State == CharacterState.Moving || State == CharacterState.Dead)
				return;

			FacingDirection = direction;

			var player = entity.Get<Player>();
			if(player != null)
				player.HeadFacing = facing;

			Map.GatherPlayersForMultiCast(ref entity, this);
			CommandBuilder.ChangeFacingMulti(this);
			CommandBuilder.ClearRecipients();
		}

		public void StopMovingImmediately()
		{
			if (State == CharacterState.Moving)
			{
				Map.GatherPlayersForMultiCast(ref Entity, this);
				CommandBuilder.CharacterStopImmediateMulti(this);
				CommandBuilder.ClearRecipients();
				State = CharacterState.Idle;
			}
		}

		public bool AddMoveDelay(float delay)
		{
			if (HitDelay > Time.ElapsedTimeFloat)
				return false;

			HitDelay = Time.ElapsedTimeFloat + delay;

			return true;
		}

		private void ChangeToActionState()
		{
			SpawnImmunity = -1f;

			if (Type != CharacterType.Player)
				return;
			
			var player = Entity.Get<Player>();
			player.HeadFacing = HeadFacing.Center; //don't need to send this to client, they will assume it resets
		}

		public bool TryMove(ref EcsEntity entity, Position target, int range)
		{
			if (State == CharacterState.Sitting || State == CharacterState.Dead)
				return false;

			if (MoveSpeed <= 0)
				return false;

			if (!Map.WalkData.IsCellWalkable(target))
				return false;

			if(WalkPath == null)
				WalkPath = new Position[17];

			var hasOld = false;
			var oldNext = new Position();
			var oldCooldown = MoveCooldown;
			
			if (MoveStep + 1 < TotalMoveSteps && State == CharacterState.Moving)
			{
				oldNext = WalkPath[MoveStep + 1];
				hasOld = true;
			}
			
			int len;
			
			//we won't interrupt the next step we are currently taking, so append it to the start of our new path.
			if (hasOld)
				len = Pathfinder.GetPathWithInitialStep(Map.WalkData, Position, oldNext, target, WalkPath, range);
			else
				len = Pathfinder.GetPath(Map.WalkData, Position, target, WalkPath, range);

			if (len == 0)
				return false;

			TargetPosition = WalkPath[len-1]; //reset to last point in walkpath
			MoveCooldown = MoveSpeed;
			MoveStep = 0;
			TotalMoveSteps = len;
			FacingDirection = (WalkPath[1] - WalkPath[0]).GetDirectionForOffset();
			
			State = CharacterState.Moving;
			
			if (hasOld)
				MoveCooldown = oldCooldown;

			Map.StartMove(ref entity, this);
			ChangeToActionState();

			return true;
		}

		public void StopAction()
		{
			var needsStop = false;

			//if it's not MoveStep + 2, that means the next step is already the last step.
			if (State == CharacterState.Moving && MoveStep + 2 < TotalMoveSteps)
			{
				TotalMoveSteps = MoveStep + 2;
				TargetPosition = WalkPath[TotalMoveSteps-1];

				//ServerLogger.Log("Stopping player at: " + TargetPosition);
				needsStop = true;
			}

			if (!needsStop)
				return;

			Map.StartMove(ref Entity, this);
		}
		
		public void Update()
		{
			Profiler.Event(ProfilerEvent.CharacterUpdate);

			SpawnImmunity -= Time.DeltaTimeFloat;

			if (State == CharacterState.Idle)
				return;

			if (State == CharacterState.Moving)
			{
				if (HitDelay > Time.ElapsedTimeFloat)
					return;

				Profiler.Event(ProfilerEvent.CharacterMoveUpdate);

				if (FacingDirection.IsDiagonal())
					MoveCooldown -= Time.DeltaTimeFloat * 0.8f;
				else
					MoveCooldown -= Time.DeltaTimeFloat;

				if (MoveCooldown <= 0f)
				{
					FacingDirection = (WalkPath[MoveStep + 1] - WalkPath[MoveStep]).GetDirectionForOffset();

					MoveStep++;
					var nextPos = WalkPath[MoveStep];

					Map.MoveEntity(ref Entity, this, nextPos, true);

					if (nextPos == TargetPosition)
						State = CharacterState.Idle;
					else
					{
						FacingDirection = (WalkPath[MoveStep + 1] - WalkPath[MoveStep]).GetDirectionForOffset();
						MoveCooldown += MoveSpeed;
					}

					if (Type == CharacterType.Player)
					{
						var player = Entity.Get<Player>();
						player.UpdatePosition(nextPos);
					}

					if (Type == CharacterType.Monster)
					{
						var monster = Entity.Get<Monster>();
						monster.ResetDelay();
					}
				}
			}
		}
	}
}
