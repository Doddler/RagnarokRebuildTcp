using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Items;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Acolyte;

[SkillHandler(CharacterSkill.AquaBenedicta, SkillClass.Magic, SkillTarget.Self)]
public class AquaBenedictaHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 0.5f;

    private const int BottleId = 713;
    private const int HolyWaterId = 523;

    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position,
        int lvl, bool isIndirect, bool isItemSource)
    {
        if (source.Character.Type != CharacterType.Player)
            return SkillValidationResult.Failure; //player only skill

        if (source.Character.Map == null || !source.Character.Map.WalkData.HasWaterNearby(source.Character.Position, 2))
            return SkillValidationResult.MustBeStandingInWater;

        if (source.Player.Inventory == null || !source.Player.Inventory.HasItem(BottleId))
            return SkillValidationResult.MissingRequiredItem;

        return base.ValidateTarget(source, target, position, lvl, false, false);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        if (source.Character.Type != CharacterType.Player || source.Character.Map == null || source.Player.Inventory == null)
            return;

        source.ApplyCooldownForSupportSkillAction(1f); //if your aspd is >1s, support skills cap cooldown at 1s
        source.ApplyAfterCastDelay(0.5f);

        var p = source.Player;

        if (!p.TryRemoveItemFromInventory(BottleId, 1, true))
            return;

        p.CreateItemInInventory(new ItemReference(HolyWaterId, 1));
        
        CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(source.Character, CharacterSkill.AquaBenedicta, lvl, isIndirect);
    }
}