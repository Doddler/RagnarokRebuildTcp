using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.StatusEffects.MonsterEffects;

[StatusEffectHandler(CharacterStatusEffect.Stop, StatusClientVisibility.Everyone, StatusEffectFlags.NoSave, "StopGroup")]
public class StopStatus : StatusEffectBase
{
    public override StatusUpdateMode UpdateMode => StatusUpdateMode.OnUpdate;

    public override StatusUpdateResult OnUpdateTick(CombatEntity ch, ref StatusEffectState state)
    {
        var e = World.Instance.GetEntityById(state.Value1);
        if (!e.TryGet<CombatEntity>(out var src))
            return StatusUpdateResult.EndStatus;

        if (ch.Character.Position.DistanceTo(src.Character.Position) > ServerConfig.MaxViewDistance)
            return StatusUpdateResult.EndStatus;

        return StatusUpdateResult.Continue;
    }

    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        ch.SetBodyState(BodyStateFlags.Stopped);
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        var e = World.Instance.GetEntityById(state.Value1);
        if (e.TryGet<CombatEntity>(out var caster))
            caster.ExpireStatusOfTypeIfExists(CharacterStatusEffect.StopOwner);

        ch.RemoveBodyState(BodyStateFlags.Stopped);
    }
}

[StatusEffectHandler(CharacterStatusEffect.StopOwner, StatusClientVisibility.None, StatusEffectFlags.NoSave, "StopGroup")]
public class StopOwnerStatus : StatusEffectBase
{
    public override void OnApply(CombatEntity ch, ref StatusEffectState state)
    {
        ch.SetBodyState(BodyStateFlags.Stopped);
    }

    public override void OnExpiration(CombatEntity ch, ref StatusEffectState state)
    {
        var e = World.Instance.GetEntityById(state.Value1);
        if (e.TryGet<CombatEntity>(out var target))
            target.ExpireStatusOfTypeIfExists(CharacterStatusEffect.Stop);

        ch.RemoveBodyState(BodyStateFlags.Stopped);
    }
}