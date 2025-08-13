using System.Diagnostics;
using RebuildSharedData.Data;
using RebuildSharedData.Networking;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Simulation;
using RebuildSharedData.Enum;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Networking.PacketHandlers.Admin
{
    [AdminClientPacketHandler(PacketType.AdminSummonMonster)]
    public class PacketAdminSummonMonster : IClientPacketHandler
    {
        public void Process(NetworkConnection connection, InboundMessage msg)
        {
            if (!connection.IsOnlineAdmin)
                return;

            Debug.Assert(connection.Player != null);

            var chara = connection.Entity.Get<WorldObject>();

            if (chara.Map == null)
                return;

            var mobName = msg.ReadString();
            var count = msg.ReadInt16();
            var isBoss = msg.ReadBoolean();

            //try by code, then by name. Name is less accurate as multiple monster could have the same name
            if (!DataManager.MonsterCodeLookup.TryGetValue(mobName.ToUpper(), out var monster))
            {
                if (!DataManager.MonsterNameLookup.TryGetValue(mobName, out monster))
                {
                    CommandBuilder.SendRequestFailed(connection.Player, ClientErrorType.InvalidInput);
                    return;
                }
            }

            if (count > 1000)
                count = 1000; //yeah no

            if (count < 0) //kinda a hack to let you use a non random position
            {
                World.Instance.CreateMonster(chara.Map, monster, Area.CreateAroundPoint(chara.Position, 0), null);
                ServerLogger.Log($"Player '{chara.Name}' using summon monster admin command to summon monster '{mobName}' at fixed location {chara.Position}.");
            }
            else
            {
                var area = Area.CreateAroundPoint(chara.Position, 5, 5);
                area.ClipArea(chara.Map.MapBounds);

                for (int i = 0; i < count; i++)
                {
                    var e = World.Instance.CreateMonster(chara.Map, monster, area, null);
                    if (isBoss)
                    {
                        var mon = e.Get<Monster>();
                        mon.Character.IsImportant = true;
                        mon.Character.DisplayType = CharacterDisplayType.Boss;
                        mon.Character.Map?.RegisterImportantEntity(mon.Character);
                    }
                }

                ServerLogger.Log($"Player '{chara.Name}' using summon monster admin command to summon {count} of monster '{mobName}'.");
            }


        }
    }
}
