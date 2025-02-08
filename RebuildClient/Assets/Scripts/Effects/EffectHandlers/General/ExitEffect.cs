using Assets.Scripts.Objects;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.General
{
    [RoEffect("Exit")]
    public class ExitEffect : IEffectHandler
    {
        public static Ragnarok3dEffect LaunchExitAtLocation(Vector3 position)
        {
            var mat = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.TeleportPillar);
            
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.Exit);
            effect.SetDurationByFrames(200);
            effect.FollowTarget = null;
            effect.DestroyOnTargetLost = false;
            effect.transform.position = position;
            effect.transform.localScale = new Vector3(2, 2, 2);
            effect.Flags[0] = 0;
            
            AudioManager.Instance.OneShotSoundEffect(-1, "ef_teleportation.ogg", effect.transform.position, 0.8f);
            
            var prim = effect.LaunchPrimitive(PrimitiveType.Heal, mat, 3.33f);
            prim.CreateParts(4);
            //prim.Flags[0] = healStrength;

            prim.Parts[0] = new EffectPart()
            {
                Active = true,
                Step = 0,
                AlphaRate = 5,
                AlphaTime = 60,
                CoverAngle = 360,
                MaxHeight = 180,
                Angle = 0,
                Distance = 1.8f,
                RiseAngle = 90
            };

            prim.Parts[1] = new EffectPart()
            {
                Active = true,
                Step = 0,
                AlphaRate = 5,
                AlphaTime = 60,
                CoverAngle = 360,
                MaxHeight = 70,
                Angle = 180,
                Distance = 2f,
                RiseAngle = 88
            };

            prim.Parts[2] = new EffectPart()
            {
                Active = true,
                Step = 0,
                AlphaRate = 5,
                AlphaTime = 60,
                CoverAngle = 360,
                MaxHeight = 45,
                Angle = 90,
                Distance = 2.2f,
                RiseAngle = 86
            };

            prim.Parts[3] = new EffectPart()
            {
                Active = true,
                Step = 0,
                AlphaRate = 5,
                AlphaTime = 60,
                CoverAngle = 360,
                MaxHeight = 20,
                Angle = 0,
                Distance = 2.4f,
                RiseAngle = 84
            };

            for (var i = 0; i < 4; i++)
            {
                for (var j = 0; j < EffectPart.SegmentCount; j++)
                {
                    prim.Parts[i].Heights[j] = 0;
                    prim.Parts[i].Flags[j] = 0; //1 is spin, 0 is no spin
                }
            }
            
            return effect;
        }
    }
}