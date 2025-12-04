using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Simulation;
using System.Diagnostics;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents.Character;

namespace RoRebuildServer.Networking.PacketHandlers.Character;

[ClientPacketHandler(PacketType.Attack)]
public class PacketAttack : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        var id = msg.ReadInt32();

        if (!connection.IsPlayerAlive)
            return;

        Debug.Assert(connection.Player != null);
        Debug.Assert(connection.Character != null);
        Debug.Assert(connection.Character.Map != null);

        if (!connection.Player.CanPerformCharacterActions())
            return;

        var character = connection.Character;
        if (character.State == CharacterState.Sitting || character.CombatEntity.HasBodyState(BodyStateFlags.Hidden))
            return;

        var player = character.Player;

        if (player.Inventory != null && player.Inventory.BagWeight > player.GetStat(CharacterStat.WeightCapacity) && !player.IsAdmin)
        {
            CommandBuilder.ErrorMessage(player, $"You cannot attack while over 100% weight limit.");
            return;
        }

        character.Player.AddInputActionDelay(InputActionCooldownType.Click);

        var target = World.Instance.GetEntityById(id);

        if (target.IsNull() || !target.IsAlive())
            return;

        var targetCharacter = target.Get<WorldObject>();

        if (targetCharacter.Map != character.Map || !targetCharacter.HasCombatEntity)
            return;

        if (targetCharacter.Type == CharacterType.BattleNpc && !targetCharacter.BattleNpc.CanBeTargeted(player.CombatEntity, CharacterSkill.None))
            return;

        if (character.Position.SquareDistance(targetCharacter.Position) > ServerConfig.MaxViewDistance)
            return;

        var ce = target.Get<CombatEntity>();
        if (!ce.IsValidTarget(character.Player.CombatEntity))
            return;

        character.Player.TargetForAttack(targetCharacter);
    }
}