using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Assassin;

[SkillHandler(CharacterSkill.VenomDust, SkillClass.Physical, SkillTarget.Ground)]
public class VenomDustHandler : SkillHandlerBase
{
    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position,
        int lvl, bool isIndirect, bool isItemSource)
    {
        if (!isIndirect && !CheckRequiredGemstone(source, RedGemstone, false))
            return SkillValidationResult.MissingRequiredItem;

        return base.ValidateTarget(source, target, position, lvl, false, false);
    }

    //failing pre-validation prevents sp from being taken
    public override bool PreProcessValidation(CombatEntity source, CombatEntity? target, Position position, int lvl,
        bool isIndirect, bool isItemSource) => isIndirect || CheckRequiredGemstone(source, RedGemstone);

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        if (source.Character.Map == null)
            return;

        if (!isIndirect && !ConsumeGemstoneForSkillWithFailMessage(source, RedGemstone))
            return;

        if (source.Character.Type == CharacterType.Monster)
            position += new Position(GameRandom.NextInclusive(0, 2), GameRandom.NextInclusive(0, 2) - 1);
        
        if (target != null)
            position = target.Character.Position; //monsters and indirect casts will target self, so use that position
        if (position == Position.Invalid)
            position = source.Character.Position; //or self if there's no target (should always have a target though...)

        var ch = source.Character;
        var map = ch.Map;

        Span<Position> posList = stackalloc Position[9];
        var posCount = 0;

        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                if (int.Abs(x) == 1 && int.Abs(y) == 1)
                    continue; //cut out the corners to make a cross shape

                var pos = new Position(position.X + x, position.Y + y);

                if (!map.WalkData.IsCellWalkable(pos))
                    continue;

                posList[posCount++] = pos;
            }
        }

        for (var i = 0; i < posCount; i++)
        {
            if (source.Character.Map!.WalkData.IsCellWalkable(posList[i]))
                source.CreateEvent("VenomDustObjectEvent", posList[i], 5 * lvl, 50 + lvl * 5);
        }
        
        if (!isIndirect)
        {
            source.ApplyCooldownForSupportSkillAction();
            CommandBuilder.SkillExecuteAreaTargetedSkillAutoVis(ch, position, CharacterSkill.VenomDust, lvl);
        }
    }
}

public class VenomDustObjectEvent : NpcBehaviorBase
{
    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        if (!npc.Owner.TryGet<CombatEntity>(out var source))
        {
            ServerLogger.LogWarning($"Failed to init VenomDust object event as it has no owner or source entity!");
            npc.EndEvent();
            return;
        }
        var targeting = new TargetingInfo()
        {
            Faction = source.Character.Type == CharacterType.Player ? 1 : 0,
            Party = 0,
            IsPvp = false,
            SourceEntity = npc.Owner,
            TargetingType = TargetingType.Enemies
        };

        var aoe = World.Instance.GetNewAreaOfEffect();
        aoe.Init(npc.Character, Area.CreateAroundPoint(npc.Character.Position, 1), AoeType.SpecialEffect, targeting, param1, 0.3f, 0, 0);
        aoe.TriggerOnFirstTouch = true;
        aoe.CheckStayTouching = true; //if they die or something in the aoe and lose their status we want to give it back promptly
        aoe.SkillSource = CharacterSkill.VenomDust;

        npc.ValuesInt[0] = param1;
        npc.ValuesInt[1] = param2;
        npc.AreaOfEffect = aoe;
        npc.Character.Map!.CreateAreaOfEffect(aoe);
        npc.StartTimer(1000);

        npc.RevealAsEffect(NpcEffectType.VenomDust, "VenomDust");
    }

    public override void OnAoEInteraction(Npc npc, CombatEntity target, AreaOfEffect aoe)
    {
        if (!aoe.TargetingInfo.SourceEntity.TryGet<CombatEntity>(out var src))
            return;

        if (src.Character.Map != npc.Character.Map || !src.CanPerformIndirectActions())
            return;

        if (target != src && !target.IsValidTarget(src, false, true))
            return;

        if (!npc.Owner.TryGet<CombatEntity>(out var owner))
            return;

        if (target.IsElementBaseType(CharacterElement.Undead1) || target.GetRace() == CharacterRace.Undead)
            return;

        if (target.HasStatusEffectOfType(CharacterStatusEffect.Poison))
            return;

        var (min, max) = src.CalculateAttackPowerRange(false);
        var atk = GameRandom.NextInclusive(min, max) * npc.ValuesInt[1] / 100;
        owner.TryPoisonOnTarget(target, 100_000, true, atk, 24f, 0);
    }

    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        if (newTime > npc.ValuesInt[0])
            npc.EndEvent();
    }
}

public class NpcLoaderVenomDustEvents : INpcLoader
{
    public void Load()
    {
        DataManager.RegisterEvent("VenomDustObjectEvent", new VenomDustObjectEvent());
    }
}
