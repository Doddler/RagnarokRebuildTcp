using RebuildSharedData.Networking;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Networking.PacketHandlers.Admin;

[AdminClientPacketHandler(PacketType.AdminServerAction)]
public class PacketAdminServerAction : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        var type = (AdminAction)msg.ReadByte();

        if (type == AdminAction.ForceGC)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        if(type == AdminAction.ReloadScripts)
            World.Instance.TriggerReloadServerScripts();

        if (type == AdminAction.KillMobs)
        {
            var chara = connection.Character;
            if (!connection.IsConnectedAndInGame || chara == null || chara.Map == null)
                return;

            var el = EntityListPool.Get();

            var isMapWide = msg.ReadBoolean();

            chara.Map.GatherMonstersInArea(chara.Position, isMapWide ? 1000 : ServerConfig.MaxViewDistance, el);

            foreach (var m in el)
            {
                if (m.Type != EntityType.Monster)
                    continue;

                if (!m.TryGet<WorldObject>(out var mon))
                    continue;
                
                if(!mon.Monster.HasMaster)
                    mon.Monster.Die(false);
            }

        }
    }
}