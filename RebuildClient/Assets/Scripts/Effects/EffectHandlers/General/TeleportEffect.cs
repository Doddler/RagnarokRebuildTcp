using Assets.Scripts.Objects;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.General
{
    [RoEffect("Teleport")]
    public class TeleportEffect : IEffectHandler
    {
        public static Ragnarok3dEffect LaunchTeleportAtLocation(Vector3 position)
        {
            var mat = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.TeleportPillar);
            
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.Teleport);
            effect.SetDurationByFrames(200);
            effect.FollowTarget = null;
            effect.DestroyOnTargetLost = false;
            effect.transform.position = position;
            effect.transform.localScale = new Vector3(2, 2, 2);
            effect.Flags[0] = 0;
            
            AudioManager.Instance.OneShotSoundEffect(-1, "ef_teleportation.ogg", effect.transform.position, 0.8f);
            
            var prim = effect.LaunchPrimitive(PrimitiveType.Teleport, mat, 3.33f);
            prim.CreateParts(4);

            prim.Parts[0] = new EffectPart()
            {
                Active = true,
                Step = 0,
                Alpha = 100,
                CoverAngle = 360,
                MaxHeight = 100,
                Angle = 180,
                RiseAngle = 90,
                Distance = 1.5f,
            };

            prim.Parts[1] = new EffectPart()
            {
                Active = true,
                Step = 0,
                Alpha = 80,
                CoverAngle = 360,
                MaxHeight = 70,
                Angle = 270,
                RiseAngle = 89,
                Distance = 3f,
            };

            prim.Parts[2] = new EffectPart()
            {
                Active = true,
                Step = 0,
                Alpha = 60,
                CoverAngle = 360,
                MaxHeight = 40,
                Angle = 0,
                RiseAngle = 88,
                Distance = 4f,
            };

            prim.Parts[3] = new EffectPart()
            {
                Active = true,
                Step = 0,
                Alpha = 40,
                CoverAngle = 360,
                MaxHeight = 15,
                Angle = 90,
                RiseAngle = 87,
                Distance = 5f,
            };

            for (var i = 0; i < 4; i++)
            {
                for (var j = 0; j < EffectPart.SegmentCount; j++)
                {
                    prim.Parts[i].Heights[j] = 0;
                }
                prim.Parts[i].Flags[0] = 0;
                prim.Parts[i].Flags[1] = (int)prim.Parts[i].MaxHeight;
            }
            
            return effect;
        }
    }
}