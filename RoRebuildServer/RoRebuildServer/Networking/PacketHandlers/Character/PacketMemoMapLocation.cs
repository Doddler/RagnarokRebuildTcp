using System.Diagnostics;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.Data;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.MemoMapLocation)]
public class PacketMemoMapLocation : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        //var map = connection.Character?.Map;

        if (!connection.IsConnectedAndInGame)
            return;

        var p = connection.Player!;
        var map = p.Character.Map;

        Debug.Assert(map != null); //IsConnectedAndInGame assures this is true

        var slot = (int)msg.ReadByte();
        var maxLevel = p.MaxLearnedLevelOfSkill(CharacterSkill.WarpPortal);

        if (maxLevel == 0 || slot > maxLevel - 1 || slot < 0 || slot > 3)
            return;

        if (!DataManager.CanMemoMapForWarpPortalUse(map.Name))
        {
            CommandBuilder.SkillFailed(p, SkillValidationResult.MemoLocationInvalid);
            return;
        }

        var pos = p.Character.Position;

        if (!map.WalkData.IsCellWalkable(pos))
        {
            CommandBuilder.SkillFailed(p, SkillValidationResult.MemoLocationUnwalkable);
            return;
        }
        
        var location = new MapMemoLocation()
        {
            MapName = map.Name,
            Position = pos
        };

        p.MemoLocations[slot] = location;

        CommandBuilder.SendServerEvent(p, ServerEvent.MemoLocationSaved, slot);
        CommandBuilder.SendMapMemoLocations(p);
    }
}