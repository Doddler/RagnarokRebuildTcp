using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Networking;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents.Character;

namespace RoRebuildServer.Networking.PacketHandlers.Admin;

[AdminClientPacketHandler(PacketType.AdminLevelUp)]
public class PacketAdminLevelUp : IClientPacketHandler
{
    public void Process(NetworkConnection connection, InboundMessage msg)
    {
        if (!connection.IsOnlineAdmin)
            return;

        var lvTarget = (int)msg.ReadSByte();
        var isJobLevel = msg.ReadBoolean();
        var character = connection.Character!;
        var player = character.Player;

        if (!isJobLevel)
        {
            var level = character.CombatEntity.GetStat(CharacterStat.Level);

            if (lvTarget == 0)
                lvTarget = 1;

            var newLevel = Math.Clamp(level + lvTarget, 1, 99);

            character.Player.JumpToLevel(newLevel);

            //for (var i = level; i < lvTarget; i++)
            //{
            //    character.Player.LevelUp();
            //}

            if (player.Party != null)
                CommandBuilder.NotifyPartyOfChange(player.Party, player.PartyMemberId, PartyUpdateType.UpdatePlayer);

            character.Map!.AddVisiblePlayersAsPacketRecipients(character);
            CommandBuilder.LevelUp(character, newLevel);
            CommandBuilder.SendHealMulti(character, 0, HealType.None);
            CommandBuilder.ClearRecipients();

            CommandBuilder.SendExpGain(character.Player, 0);
            CommandBuilder.ChangeSpValue(character.Player, character.Player.GetStat(CharacterStat.Sp),
                character.Player.GetStat(CharacterStat.MaxSp));
        }
        else
        {
            var jobLevel = player.GetData(PlayerStat.JobLevel);
            var maxJob = player.GetData(PlayerStat.Job) == 0 ? 10 : 70;

            if (lvTarget == 0)
                lvTarget = 1;

            var newJob = int.Clamp(jobLevel + lvTarget, 1, maxJob);

            player.SetData(PlayerStat.JobLevel, newJob);
            player.SetData(PlayerStat.JobExp, 0);
            
            character.Map?.AddVisiblePlayersAsPacketRecipients(character);
            CommandBuilder.SendEffectOnCharacterMulti(character, DataManager.EffectIdForName["JobUp"]);
            CommandBuilder.ClearRecipients();

            player.UpdateStats(false);
        }
    }
}