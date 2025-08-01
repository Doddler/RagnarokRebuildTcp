using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using System.Diagnostics;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.Suicide, SkillClass.None, SkillTarget.Self)]
public class SuicideHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        var ch = source.Character;
        var map = source.Character.Map;
        Debug.Assert(map != null);
        
        map.AddVisiblePlayersAsPacketRecipients(ch);
        CommandBuilder.SendEffectOnCharacterMulti(ch, DataManager.EffectIdForName["Suicide"]);
        CommandBuilder.ClearRecipients();

        if (ch.Type == CharacterType.Player)
            ch.Player.Die();
        if (ch.Type == CharacterType.Monster)
            ch.Monster.Die(false); //sad, no exp
    }
}