using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Util;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace RoRebuildServer.EntityComponents.Npcs
{
    public class NpcPathHandler
    {
        public Npc Npc;
        public int Step;
        public NpcPathUpdateResult LastResult;
        private int speed;
        private float waitEndTime;
        private Entity currentTarget;


        public const int StorageCount = 10;

        public int[] ValuesInt = new int[StorageCount];
        public string?[] ValuesString = new string[StorageCount];

        public void SetSpeed(int newSpeed) => speed = newSpeed; 
        public void Wait(int time) => waitEndTime = time / 1000f + Time.ElapsedTimeFloat;
        public bool FindPlayerNearby(int range)
        {
            Debug.Assert(Npc.Character.Map != null);

            using var entities = EntityListPool.Get();
            Npc.Character.Map.GatherPlayersInRange(Npc.Character.Position, range, entities, true, false);
            if (entities.Count <= 0)
                return false;
            if (entities.Count == 1)
                currentTarget = entities[0];
            else
                currentTarget = entities[GameRandom.Next(0, entities.Count)];

            return true;
        }

        //public void Hide()
        //{
        //    var ch = Npc.Character;
        //    if (ch.Hidden || ch.Map == null)
        //        return;

        //    ch.Map.RemoveEntity(ref ch.Entity, CharacterRemovalReason.OutOfSight, false);
        //    ch.Hidden = true;
        //}

        //public void Reveal()
        //{
        //    var ch = Npc.Character;
        //    if (!ch.Hidden)
        //        return;


        //    if (ch.Map == null)
        //        throw new Exception($"NPC {ch} attempting to execute AdminUnHide, but the npc is not currently attached to a map.");

        //    ch.Hidden = false;
        //    ch.Map.AddEntity(ref ch.Entity, false);
        //}

        public int Random(int min, int max) => GameRandom.Next(min, max);
        
        public void LookAtTarget()
        {
            if (currentTarget.TryGet<WorldObject>(out var targetChara))
                Npc.Character.ChangeLookDirection(targetChara.Position);
        }

        public void Say(string text)
        {
            Npc.Character.Map!.AddVisiblePlayersAsPacketRecipients(Npc.Character);
            CommandBuilder.SendSayMulti(Npc.Character, Npc.Character.Name, text, false);
            CommandBuilder.ClearRecipients();
        }

        public void Emote(int id)
        {
            Npc.Character.Map!.AddVisiblePlayersAsPacketRecipients(Npc.Character);
            CommandBuilder.SendEmoteMulti(Npc.Character, id);
            CommandBuilder.ClearRecipients();
        }

        public void PathTo(int x, int y)
        {
            Debug.Assert(Npc.Character.Map != null);
            Npc.Character.MoveSpeed = speed / 1000f;
            if(!Npc.Character.TryMove(new Position(x, y), 0))
                Npc.Character.Map.TeleportEntity(ref Npc.Entity, Npc.Character, new Position(x, y), CharacterRemovalReason.OutOfSight);
        }


        public void PathToTarget(int distance = 1)
        {
            if (!currentTarget.TryGet<WorldObject>(out var target))
                return;
            Npc.Character.MoveSpeed = speed / 1000f;
            Npc.Character.TryMove(target.Position, distance);
        }

        public void UpdatePath()
        {
            if (Step > 0)
            {
                if (LastResult == NpcPathUpdateResult.WaitForMove && !Npc.Character.IsAtDestination)
                    return;
                if (LastResult == NpcPathUpdateResult.WaitForTime && waitEndTime > Time.ElapsedTimeFloat)
                    return;
            }

            var curStep = Step;

            var res = Npc.Behavior.OnPath(Npc, this);
            
            if (res == NpcPathUpdateResult.EndPath && Step == curStep)
                Npc.EndPath();

            LastResult = res;
        }
    }
}
