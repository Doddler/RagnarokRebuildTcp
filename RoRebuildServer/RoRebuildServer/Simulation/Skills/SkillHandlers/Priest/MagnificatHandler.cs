using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using System.Diagnostics;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Priest;

[SkillHandler(CharacterSkill.Magnificat, SkillClass.Magic, SkillTarget.Self)]
public class MagnificatHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 4f;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        Debug.Assert(source.Character.Map != null);

        var ch = source.Character;
        var effectId = DataManager.EffectIdForName["Magnificat"];
        if (!isIndirect)
        {
            source.ApplyCooldownForSupportSkillAction();
            source.ApplyAfterCastDelay(2f);
        }

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Magnificat, 15f + 15 * lvl, lvl);
        source.AddStatusEffect(status);
        ch.Map?.AddVisiblePlayersAsPacketRecipients(source.Character);

        CommandBuilder.SkillExecuteSelfTargetedSkill(ch, CharacterSkill.Magnificat, lvl, isIndirect);
        CommandBuilder.SendEffectOnCharacterMulti(ch, effectId);
        CommandBuilder.ClearRecipients();

        if (ch.Type != CharacterType.Player || ch.Player.Party == null)
            return;

        var party = ch.Player.Party;

        foreach (var e in party.OnlineMembers)
        {
            if (!e.TryGet<CombatEntity>(out var ally))
                continue;
            if (!ally.IsValidAlly(source))
                continue;
            if (ally.Character.Map != ch.Map)
                continue;
            if (!ally.Character.Position.InRange(ch.Position, 14))
                continue;
            if (ally.IsMagicImmune())
                continue;
            ally.AddStatusEffect(status);
            ally.Character.Map?.AddVisiblePlayersAsPacketRecipients(source.Character);
            CommandBuilder.SendEffectOnCharacterMulti(ally.Character, effectId);
            CommandBuilder.ClearRecipients();
        }
    }
}