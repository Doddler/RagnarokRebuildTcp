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

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Alchemist;

[MonsterSkillHandler(CharacterSkill.Demonstration, SkillClass.Physical, SkillTarget.Enemy)] //demonstration from monsters is targeted
[SkillHandler(CharacterSkill.Demonstration)]
public class DemonstrationHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 1f;
    public override int GetSkillRange(CombatEntity source, int lvl) => 9;

    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position,
        int lvl, bool isIndirect, bool isItemSource)
    {
        if (source.Character.Type == CharacterType.Player && source.Character.Position.SquareDistance(position) < 3)
            return SkillValidationResult.TooClose;

        return base.ValidateTarget(source, target, position, lvl, false, false);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        var map = source.Character.Map;
        Debug.Assert(map != null);

        if (target != null)
        {
            position = target.Character.Position;
            if (target.Character.IsMoving)
            {
                //monsters will lead the player 1 tile with firewall if they're moving in order to block them
                var forwardPosition = position.AddDirectionToPosition(target.Character.FacingDirection);
                if (map.WalkData.IsCellWalkable(forwardPosition))
                    position = forwardPosition;
            }
        }

        var ch = source.Character;

        var di = source.PrepareTargetedSkillResult(null, CharacterSkill.Demonstration);

        //var throwDistance =  (int)((di.AttackMotionTime + target.Character.WorldPosition.DistanceTo(position) * 0.25f) * 1000);
        
        var e = World.Instance.CreateEvent(source.Entity, map, "DemonstrationObjectEvent", position, lvl, (int)(di.AttackMotionTime * 1000), 0, 0, null);
        ch.AttachEvent(e);
        source.ApplyCooldownForSupportSkillAction();

        if (!isIndirect)
            CommandBuilder.SkillExecuteAreaTargetedSkillAutoVis(ch, position, CharacterSkill.Demonstration, lvl, di.AttackMotionTime);
    }
}


public class DemonstrationObjectEvent : NpcBehaviorBase
{
    //value0 = skillLevel
    //value1 = time in ms before activation
    //value2 = is skill activated?
    //value 3 = time before expiration (in seconds)

    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        Debug.Assert(npc.Character.Map != null);
        npc.ValuesInt[0] = param1;
        npc.ValuesInt[1] = param2; // time before activation
        npc.ValuesInt[2] = 0; //is ground aoe created
        npc.ValuesInt[3] = 30 + 5 * npc.ValuesInt[0]; // (int)(30 + 5 * float.Ceiling(npc.ValuesInt[0] + npc.ValuesInt[1] * 0.001f));
        npc.StartTimer(50);

        var position = npc.SelfPosition;
        using var targetList = EntityListPool.Get();
    }

    private void CreateDemonstrationZone(Npc npc)
    {
        if (!npc.Owner.TryGet<CombatEntity>(out var parent))
        {
            ServerLogger.LogWarning($"Failed to init Demonstration object event as it has no owner or source entity!");
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

        var aoe = World.Instance.GetNewAreaOfEffect();
        aoe.Init(npc.Character, Area.CreateAroundPoint(npc.Character.Position, 1), AoeType.DamageAoE, targeting, 35 + npc.ValuesInt[0] * 5, 0.1f, 0, 0);
        aoe.CheckStayTouching = true;
        aoe.SkillSource = CharacterSkill.Demonstration;

        npc.AreaOfEffect = aoe;
        npc.Character.Map!.CreateAreaOfEffect(aoe);
        
        npc.RevealAsEffect(NpcEffectType.Demonstration);
    }

    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        if (npc.ValuesInt[2] == 0 && newTime > npc.ValuesInt[1] * 0.001f) //value1 is activation time in MS
        {
            CreateDemonstrationZone(npc);
            npc.TimerUpdateRate = 0.2f;
            npc.ValuesInt[2] = 1;
        }

        if (newTime > npc.ValuesInt[3])
            npc.EndEvent();
    }

    public override void OnAoEInteraction(Npc npc, CombatEntity target, AreaOfEffect aoe)
    {
        if (!aoe.TargetingInfo.SourceEntity.TryGet<CombatEntity>(out var src))
            return;

        if (src.Character.Map != npc.Character.Map)
            return;

        if (!target.IsValidTarget(src) || target.IsInSkillDamageCooldown(CharacterSkill.Demonstration))
            return;

        var ratio = 1f + 0.2f * npc.ValuesInt[0];

        var flags = AttackFlags.Physical | AttackFlags.IgnoreEvasion | AttackFlags.IgnoreNullifyingGroundMagic;
        var res = src.CalculateCombatResult(target, ratio, 1, flags, CharacterSkill.Demonstration, AttackElement.Fire);
        res.AttackMotionTime = 0;
        res.Time = Time.ElapsedTimeFloat;
        res.IsIndirect = true;

        CommandBuilder.SkillExecuteIndirectAutoVisibility(npc.Character, target.Character, res);

        target.SetSkillDamageCooldown(CharacterSkill.Demonstration, 1f);
        src.ExecuteCombatResult(res, false);
    }
}

public class NpcLoaderDemonstrationEvent : INpcLoader
{
    public void Load()
    {
        DataManager.RegisterEvent("DemonstrationObjectEvent", new DemonstrationObjectEvent());
    }
}
