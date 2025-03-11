using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Acolyte;

[SkillHandler(CharacterSkill.AquaBenedicta, SkillClass.Magic, SkillTarget.Self)]
public class AquaBenedictaHandler : SkillHandlerBase
{
    private const int BottleId = 713;
    private const int HolyWaterId = 523;

    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position, int lvl)
    {
        if (source.Character.Type != CharacterType.Player)
            return SkillValidationResult.Failure; //player only skill

        if (source.Character.Map == null || !source.Character.Map.WalkData.IsCellInWater(source.Character.Position))
            return SkillValidationResult.MustBeStandingInWater;

        if (source.Player.Inventory == null || !source.Player.Inventory.HasItem(713))
            return SkillValidationResult.MissingRequiredItem;

        return base.ValidateTarget(source, target, position, lvl);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        if (source.Character.Type != CharacterType.Player || source.Character.Map == null || source.Player.Inventory == null)
            return;

        source.ApplyCooldownForSupportSkillAction(1f);

        var p = source.Player;

        if (!p.TryRemoveItemFromInventory(713, 1, true))
            return;

        p.AddItemToInventory(new ItemReference(523, 1));

        CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(source.Character, CharacterSkill.AquaBenedicta, lvl);
    }
}