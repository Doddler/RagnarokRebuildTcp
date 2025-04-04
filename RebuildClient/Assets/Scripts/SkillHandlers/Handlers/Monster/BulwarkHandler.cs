﻿using Assets.Scripts.Network;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.Bulwark)]
    public class BulwarkHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            CameraFollower.Instance.AttachEffectToEntity("StoneSkin", src.gameObject, src.Id);
        }
    }
}