using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Hunter;

[MonsterSkillHandler(CharacterSkill.ClaymoreTrap, SkillClass.Physical, SkillTarget.Trap)]
[SkillHandler(CharacterSkill.ClaymoreTrap, SkillClass.Physical, SkillTarget.Ground)]
public class ClaymoreTrapHandler : SkillHandlerTrap
{
    protected override string GroundUnitType() => nameof(ClaymoreTrapEvent);
    protected override CharacterSkill SkillType() => CharacterSkill.ClaymoreTrap;
    public override int GetSkillRange(CombatEntity source, int lvl) => 3;
    protected override int Catalyst() => 1065;
    protected override int CatalystCount() => 2;
}

//claymore trap is kinda messed up because we're overriding almost everything on the TrapBaseEvent
//but it still has to be a TrapBaseEvent because that's how we identify it as a trap
public class ClaymoreTrapEvent : TrapBaseEvent
{
    protected override CharacterSkill SkillSource() => CharacterSkill.ClaymoreTrap;
    protected override NpcEffectType EffectType() => NpcEffectType.ClaymoreTrap;
    protected override float Duration(int skillLevel) => 50f;
    public override bool OnNaturalExpiration(Npc npc) => ActivateTrapWithoutTouchEvent(npc);
    protected override bool AllowAutoAttackMove => false;
    protected override bool Attackable => true;
    protected override bool BlockMultipleActivations => true;
    protected override bool InheritOwnerFacing => false;
    public override bool CanBeAutoAttacked => true;

    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        base.InitEvent(npc, param1, param2, param3, param4, paramString);

        var ce = npc.Character.CombatEntity;
        ce.SetStat(CharacterStat.MaxHp, 600);
        ce.SetStat(CharacterStat.Hp, 600);
    }

    public override bool CanBeAttacked(Npc npc, BattleNpc battleNpc, CombatEntity attacker, CharacterSkill skill = CharacterSkill.None)
    {
        if (!Attackable)
            return false;
        if (attacker.Character.Type == CharacterType.Monster)
            return false;
        if (battleNpc.Character.State == CharacterState.Activated)
            return false;
        if (skill == CharacterSkill.None)
            return true;

        var attr = SkillHandler.GetSkillAttributes(skill);
        return attr.SkillClassification == SkillClass.Physical;
    }

    public override void OnCalculateDamage(Npc npc, BattleNpc battleNpc, CombatEntity attacker, ref DamageInfo di)
    {
        if (npc.Character.State == CharacterState.Activated)
            di.Result = AttackResult.Invisible;
    }

    public override void OnApplyDamage(Npc npc, BattleNpc battleNpc, ref DamageInfo di)
    {
        if (npc.Character.State == CharacterState.Activated)
            return;

        if (battleNpc.CombatEntity.GetStat(CharacterStat.Hp) <= di.Damage * di.HitCount)
        {
            ChangeToActivatedState(npc);
            di.Damage = 0;
        }
    }

    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        if (npc.Character.State == CharacterState.Activated)
        {
            if (lastTime < 0.2f && newTime >= 0.2f)
            {
                using var targetList = EntityListPool.Get();

                if (!npc.Owner.TryGet<CombatEntity>(out var owner))
                    return;

                var srcLevel = owner.GetStat(CharacterStat.Level);
                var statInt = owner.GetEffectiveStat(CharacterStat.Int);
                var statDex = owner.GetEffectiveStat(CharacterStat.Dex);
                var skillLevel = SkillLevel(npc);

                var flags = AttackFlags.IgnoreEvasion | AttackFlags.IgnoreDefense | AttackFlags.IgnoreWeaponRefine | AttackFlags.NoTriggerOnAttackEffects;

                var atk = new AttackRequest(CharacterSkill.LandMine, 1f, 1, flags, AttackElement.Fire);
                atk.MinAtk = skillLevel * (int)((50 + statDex / 2f) * (1 + statInt / 30f));
                atk.MaxAtk = atk.MinAtk;

                npc.Character.Map?.GatherTargetableEntitiesInRange(npc.Character.Position, 3, targetList, true);
                foreach (var e in targetList)
                {
                    if (!e.TryGet<CombatEntity>(out var ce) || ce.Character.Map == null)
                        continue;

                    if (ce.Character.Type == CharacterType.BattleNpc)
                    {
                        if (ce.Character.Npc.Behavior is ClaymoreTrapEvent trap) //both claymore traps
                            trap.ActivateTrapWithoutTouchEvent(ce.Character.Npc);

                        continue;
                    }

                    if (!ce.IsValidTarget(owner, false, false))
                        continue;

                    var res = owner.CalculateCombatResultUsingSetAttackPower(ce, atk);
                    res.IsIndirect = true;
                    res.Time = 0;

                    CommandBuilder.SkillExecuteIndirectAutoVisibility(npc.Character, ce.Character, res);
                    owner.ExecuteCombatResult(res, false);
                }

                npc.TimerEnd = 0; //expire immediately
            }
        }

        base.OnTimer(npc, lastTime, newTime);
    }

    public override bool TriggerTrap(Npc npc, CombatEntity src, CombatEntity? target, int skillLevel)
    {
        if (target != null && target.IsFlying() && ServerConfig.OperationConfig.FliersIgnoreTraps)
            return false;

        if (npc.Character.Map == null)
            return true;

        npc.Character.Map.AddVisiblePlayersAsPacketRecipients(npc.Character);
        var id = DataManager.EffectIdForName["ClaymoreExplosion"];
        CommandBuilder.SendEffectAtLocationMulti(id, npc.Character.Position, 0);
        CommandBuilder.ClearRecipients();

        ChangeToActivatedState(npc, 1f);

        return true;
    }
}