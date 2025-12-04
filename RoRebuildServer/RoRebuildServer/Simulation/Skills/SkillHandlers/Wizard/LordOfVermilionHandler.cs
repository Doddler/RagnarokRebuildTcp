using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;
using System.Diagnostics;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Wizard;

[SkillHandler(CharacterSkill.LordOfVermilion, SkillClass.Magic, SkillTarget.Ground)]
public class LordOfVermilionHandler : SkillHandlerBase
{
    public override int GetAreaOfEffect(CombatEntity source, Position position, int lvl) => lvl <= 10 ? 6 : 8;

    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 15.5f - 0.5f * lvl;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        var map = source.Character.Map;
        Debug.Assert(map != null);

        if (!position.IsValid()) //monsters will either target pneuma directly on themselves or an ally
            position = target != null ? target.Character.Position : source.Character.Position;

        var ch = source.Character;

        var e = World.Instance.CreateEvent(source.Entity, map, "LordOfVermilionObjectEvent", position, lvl, 0, 0, 0, null);
        ch.AttachEvent(e);
        source.ApplyCooldownForSupportSkillAction();

        if (!isIndirect)
            CommandBuilder.SkillExecuteAreaTargetedSkillAutoVis(ch, position, CharacterSkill.LordOfVermilion, lvl);
    }
}

public class LordOfVermilionObjectEvent : NpcBehaviorBase
{
    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        Debug.Assert(npc.Character.Map != null);
        npc.ValuesInt[0] = param1;
        npc.StartTimer(50);

        var position = npc.SelfPosition;
        using var targetList = EntityListPool.Get();

        //send the thunder aoe
        npc.Character.Map.GatherPlayersInRange(position, ServerConfig.MaxViewDistance + 2, targetList, false, false);
        CommandBuilder.AddRecipients(targetList);
        var id = DataManager.EffectIdForName["LoV"];
        CommandBuilder.SendEffectAtLocationMulti(id, npc.SelfPosition, 0);
        CommandBuilder.ClearRecipients();
    }

    private void CreateLoVZone(Npc npc)
    {
        if (!npc.Owner.TryGet<CombatEntity>(out var parent))
        {
            ServerLogger.LogWarning($"Failed to init LordOfVermilion object event as it has no owner or source entity!");
            npc.EndEvent();
            return;
        }

        var targeting = new TargetingInfo()
        {
            Faction = parent.Character.Type == CharacterType.Player ? 1 : 0,
            Party = 0,
            IsPvp = false,
            SourceEntity = npc.Owner,
            TargetingType = TargetingType.Enemies
        };

        var size = npc.ValuesInt[0] <= 10 ? 6 : 8;

        var aoe = World.Instance.GetNewAreaOfEffect();
        aoe.Init(npc.Character, Area.CreateAroundPoint(npc.Character.Position, size), AoeType.DamageAoE, targeting, 3.5f, 0.1f, 0, 0);
        aoe.CheckStayTouching = true;
        aoe.SkillSource = CharacterSkill.LordOfVermilion;

        npc.AreaOfEffect = aoe;
        npc.Character.Map!.CreateAreaOfEffect(aoe);
    }

    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        if (lastTime < 0.45f && newTime >= 0.45f)
        {
            CreateLoVZone(npc);
            npc.TimerUpdateRate = 0.2f;
        }

        if (newTime > 4f)
            npc.EndEvent();
    }

    public override void OnAoEInteraction(Npc npc, CombatEntity target, AreaOfEffect aoe)
    {
        if (!aoe.TargetingInfo.SourceEntity.TryGet<CombatEntity>(out var src))
            return;

        if (src.Character.Map != npc.Character.Map)
            return;

        if (!target.IsValidTarget(src) || target.IsInSkillDamageCooldown(CharacterSkill.LordOfVermilion))
            return;

        var ratio = 0.8f + 0.2f * int.Clamp(npc.ValuesInt[0], 1, 10);

        var res = src.CalculateCombatResult(target, ratio, 1, AttackFlags.Magical, CharacterSkill.LordOfVermilion, AttackElement.Wind);
        res.AttackMotionTime = 0;
        res.Damage /= 10;
        res.HitCount = 10;
        res.Time = Time.ElapsedTimeFloat;
        res.IsIndirect = true;

        CommandBuilder.SkillExecuteIndirectAutoVisibility(npc.Character, target.Character, res);

        target.SetSkillDamageCooldown(CharacterSkill.LordOfVermilion, 1f);
        src.ExecuteCombatResult(res, false);
    }
}

public class NpcLoaderLordOfVermilionEvent : INpcLoader
{
    public void Load()
    {
        DataManager.RegisterEvent("LordOfVermilionObjectEvent", new LordOfVermilionObjectEvent());
    }
}