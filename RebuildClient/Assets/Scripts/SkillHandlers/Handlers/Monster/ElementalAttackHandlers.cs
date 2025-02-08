using Assets.Scripts.Network;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using UnityStandardAssets.Water;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    public class ElementalAttackHandler : SkillHandlerBase
    {
        protected virtual AttackElement Element => AttackElement.Neutral;
        protected virtual CharacterSkill Skill => CharacterSkill.None;
        
        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            attack.Target?.Messages.SendElementalHitEffect(attack.Src, attack.MotionTime, AttackElement.Fire);
        }
        
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            src.PerformBasicAttackMotion(Skill);
        }
    }

    [SkillHandler(CharacterSkill.DarkAttack)]
    public class DarkAttackHandler : ElementalAttackHandler
    {
        protected override AttackElement Element => AttackElement.Dark;
        protected override CharacterSkill Skill => CharacterSkill.DarkAttack;
    }
    
    [SkillHandler(CharacterSkill.HolyAttack)]
    public class HolyAttackHandler : ElementalAttackHandler
    {
        protected override AttackElement Element => AttackElement.Holy;
        protected override CharacterSkill Skill => CharacterSkill.HolyAttack;
    }
    
    [SkillHandler(CharacterSkill.IceAttack)]
    public class IceAttackHandler : ElementalAttackHandler
    {
        protected override AttackElement Element => AttackElement.Water;
        protected override CharacterSkill Skill => CharacterSkill.IceAttack;
    }
    
    [SkillHandler(CharacterSkill.WaterAttack)]
    public class WaterAttackHandler : ElementalAttackHandler
    {
        protected override AttackElement Element => AttackElement.Water;
        protected override CharacterSkill Skill => CharacterSkill.WaterAttack;
    }
    
    [SkillHandler(CharacterSkill.FireAttack)]
    public class FireAttackHandler : ElementalAttackHandler
    {
        protected override AttackElement Element => AttackElement.Fire;
        protected override CharacterSkill Skill => CharacterSkill.FireAttack;
    }
    
    [SkillHandler(CharacterSkill.EarthAttack)]
    public class EarthAttackHandler : ElementalAttackHandler
    {
        protected override AttackElement Element => AttackElement.Earth;
        protected override CharacterSkill Skill => CharacterSkill.EarthAttack;
    }
    
    [SkillHandler(CharacterSkill.WindAttack)]
    public class WindAttackHandler : ElementalAttackHandler
    {
        protected override AttackElement Element => AttackElement.Wind;
        protected override CharacterSkill Skill => CharacterSkill.WindAttack;
    }
    
    [SkillHandler(CharacterSkill.PoisonAttack)]
    public class PoisonAttackHandler : ElementalAttackHandler
    {
        protected override AttackElement Element => AttackElement.Poison;
        protected override CharacterSkill Skill => CharacterSkill.PoisonAttack;
    }
    
    [SkillHandler(CharacterSkill.UndeadAttack)]
    public class UndeadAttackHandler : ElementalAttackHandler
    {
        protected override AttackElement Element => AttackElement.Undead;
        protected override CharacterSkill Skill => CharacterSkill.UndeadAttack;
    }
    
    [SkillHandler(CharacterSkill.GhostAttack)]
    public class GhostAttackHandler : ElementalAttackHandler
    {
        protected override AttackElement Element => AttackElement.Ghost;
        protected override CharacterSkill Skill => CharacterSkill.GhostAttack;
    }
}