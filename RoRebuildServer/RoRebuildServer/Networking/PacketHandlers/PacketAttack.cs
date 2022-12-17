using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Simulation;

namespace RoRebuildServer.Networking.PacketHandlers;

[ClientPacketHandler(PacketType.Attack)]
public class PacketAttack : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
		var id = msg.ReadInt32();

        var character = connection.Character;

        if (character == null
            || connection.Player == null
            || character.State is CharacterState.Sitting or CharacterState.Dead 
            || character.Player.IsInNpcInteraction)
            return;
        
        if (connection.Player.InActionCooldown())
            return;
        character.Player.AddActionDelay(CooldownActionType.Click);

        var target = World.Instance.GetEntityById(id);

        if (target.IsNull() || !target.IsAlive())
            return;

        var targetCharacter = target.Get<WorldObject>();

        if (targetCharacter.Map != character.Map)
            return;

        if (character.Position.SquareDistance(targetCharacter.Position) > ServerConfig.MaxViewDistance)
            return;

        var ce = target.Get<CombatEntity>();
        if (!ce.IsValidTarget(character.Player.CombatEntity))
            return;

        character.Player.TargetForAttack(targetCharacter);
	}
}