using RebuildSharedData.Networking;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Simulation;
using RoRebuildServer.Simulation.Util;
using System;
using RoRebuildServer.Logging;

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

            using var el = EntityListPool.Get();

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

        if (type == AdminAction.SignalNpc)
        {
            var chara = connection.Character;
            if (!connection.IsConnectedAndInGame || chara == null || chara.Map == null)
                return;

            var signalName = msg.ReadString();
            var signalValue = msg.ReadString();

            if (chara.Map.Instance.NpcNameLookup.TryGetValue(signalName, out var localEntity) && localEntity.TryGet<Npc>(out var npc))
            {
                npc.OnSignal(npc, signalValue);
                return;
            }

            if (World.Instance.GetGlobalSignalTarget(signalName).TryGet<Npc>(out npc))
            {
                npc.OnSignal(npc, signalValue);
                return;
            }

            CommandBuilder.ErrorMessage(connection, $"Could not find an NPC signal by the name of {signalName}");
        }

        if (type == AdminAction.ShutdownServer)
        {
            var seconds = msg.ReadInt32();
            var reason = msg.ReadString();

            if (seconds < 0)
            {
                NetworkManager.IsServerOpen = true;
                World.Instance.CancellationSource.CancelAfter(TimeSpan.FromMilliseconds(4294967294u)); // effectively cancel the shutdown (number is max timespan value CancelAfter will accept)

                ServerLogger.Log($"Server shutdown cancelled.");

                CommandBuilder.AddAllPlayersAsRecipients();
                CommandBuilder.SendServerMessage("Server shutdown has been cancelled. Have a nice day.");
                CommandBuilder.ClearRecipients();

                return;
            }
            
            if (string.IsNullOrWhiteSpace(reason))
                reason = "Server is shutting down for maintenance.";

            NetworkManager.ServerClosedReason = reason;
            NetworkManager.IsServerOpen = false;
            
            var ts = TimeSpan.FromSeconds(seconds);
            
            var clientMsg = $"<b>The server is shutting down. All players will be automatically logged out.</b>";
            if (seconds > 15 && seconds < 120)
                clientMsg = "<b>The server is shutting down shortly. All players will be automatically logged out.</b>";
            if(seconds >= 120)
                clientMsg = $"<b>The server is shutting down in {ts.Minutes} minutes. All players will be automatically logged out.</b>";

            if (seconds > 0)
            {
                ServerLogger.Log($"Server shutdown requested! Shutdown will occur after {seconds} seconds. Shutdown reason: {reason}");
                World.Instance.CancellationSource.CancelAfter(ts);
            }
            else
            {
                ServerLogger.Log($"Server shutdown requested! Shutdown is immediate. Shutdown reason: {reason}.");
                World.Instance.CancellationSource.Cancel();
            }

            CommandBuilder.AddAllPlayersAsRecipients();
            CommandBuilder.SendServerMessage(clientMsg);
            CommandBuilder.ClearRecipients();


        }

#if DEBUG
        if (type == AdminAction.EnableMonsterDebugLogging)
        {
            var chara = connection.Character;
            if (!connection.IsConnectedAndInGame || chara == null || chara.Map == null)
                return;

            using var el = EntityListPool.Get();

            chara.Map.GatherMonstersInArea(chara.Position, ServerConfig.MaxViewDistance, el);

            foreach (var m in el)
            {
                if (m.Type != EntityType.Monster)
                    continue;

                if (!m.TryGet<Monster>(out var mon))
                    continue;

                mon.DebugLogging = true;
            }
        }
#endif
    }
}