using RebuildSharedData.Data;
using RebuildSharedData.Networking;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Simulation;
using System.Xml.Linq;
using RebuildSharedData.Enum;
using System.Numerics;
using RoRebuildServer.Logging;

namespace RoRebuildServer.Networking.PacketHandlers.Admin
{
    [ClientPacketHandler(PacketType.AdminSummonMonster)]
    public class PacketAdminSummonMonster : IClientPacketHandler
    {
        public void Process(NetworkConnection connection, InboundMessage msg)
        {
            if (connection.Character == null || !connection.Character.IsActive || connection.Character.Map == null
                || !connection.Entity.IsAlive() || connection.Character.State == CharacterState.Dead)
                return;

            if (connection.Player?.IsAdmin != true)
            {
                NetworkManager.DisconnectPlayer(connection);
                return;
            }

            var chara = connection.Entity.Get<WorldObject>();
            
            if (chara.Map == null)
                return;

            var mobName = msg.ReadString();
            var count = msg.ReadInt16();

            //try by code, then by name. Name is less accurate as multiple monster could have the same name
            if (!DataManager.MonsterCodeLookup.TryGetValue(mobName.ToUpper(), out var monster))
            {
                if (!DataManager.MonsterNameLookup.TryGetValue(mobName, out monster))
                {
                    CommandBuilder.SendRequestFailed(connection.Player, ClientErrorType.TooManyRequests);
                    return;
                }
            }

            if (count > 1000)
                count = 1000; //yeah no

            var area = Area.CreateAroundPoint(chara.Position, 5, 5);
            area.ClipArea(chara.Map.MapBounds);

            for (int i = 0; i < count; i++)
                World.Instance.CreateMonster(chara.Map, monster, area, null);

            ServerLogger.Log($"Player '{chara.Name}' using summon monster admin command to summon {count} of monster '{mobName}'.");
        }
    }
}
