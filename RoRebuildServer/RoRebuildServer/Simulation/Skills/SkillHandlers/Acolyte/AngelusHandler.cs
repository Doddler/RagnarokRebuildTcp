using System.Diagnostics;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using RoRebuildServer.Simulation.Util;
using static System.Net.Mime.MediaTypeNames;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Acolyte;

[SkillHandler(CharacterSkill.Angelus, SkillClass.Unique, SkillTarget.Self)]
public class AngelusHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 0.5f;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        Debug.Assert(source.Character.Map != null);
        var ch = source.Character;
        var angelusEffect = DataManager.EffectIdForName["Angelus"];
        source.ApplyCooldownForSupportSkillAction();
        source.ApplyAfterCastDelay(1f);

        var dp = 0;
        if (ch.Type == CharacterType.Player)
            dp = source.Player.MaxLearnedLevelOfSkill(CharacterSkill.DivineProtection);
        
        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Angelus, 30f + 30 * lvl, lvl, dp);
        source.AddStatusEffect(status);
        ch.Map?.AddVisiblePlayersAsPacketRecipients(source.Character);
        CommandBuilder.SkillExecuteSelfTargetedSkill(ch, CharacterSkill.Angelus, lvl, isIndirect);
        CommandBuilder.SendEffectOnCharacterMulti(ch, angelusEffect);
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
            CommandBuilder.SendEffectOnCharacterMulti(ally.Character, angelusEffect);
            CommandBuilder.ClearRecipients();
        }

        ////this should only target party members, but, well...
        //using var entities = EntityListPool.Get();
        //source.Character.Map.GatherAlliesInRange(source.Character, 9, entities, false, false);
        //foreach (var e in entities)
        //{
        //    if (!e.TryGet<CombatEntity>(out var ally))
        //        continue;
        //    if (!ally.IsValidAlly(source))
        //        continue;
        //    ally.AddStatusEffect(status);
        //    ally.Character.Map?.AddVisiblePlayersAsPacketRecipients(source.Character);
        //    CommandBuilder.SendEffectOnCharacterMulti(ally.Character, angelusEffect);
        //    CommandBuilder.ClearRecipients();
        //}
    }
}