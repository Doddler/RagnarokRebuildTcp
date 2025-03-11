
using Assets.Scripts.Objects;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.General
{
    [RoEffect("Entry")]
    public class EntryEffect : IEffectHandler
    {
        public static Ragnarok3dEffect LaunchEntryAtLocation(Vector3 position, float entryEffectVolume = 0.7f)
        {
            var mat = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.TeleportPillar);

            var effect = RagnarokEffectPool.Get3dEffect(EffectType.Exit);
            effect.SetDurationByFrames(100);
            effect.FollowTarget = null;
            effect.DestroyOnTargetLost = false;
            effect.transform.position = position;
            effect.transform.localScale = new Vector3(2, 2, 2);
            effect.Flags[0] = 0;

            if(entryEffectVolume > 0)
                AudioManager.Instance.OneShotSoundEffect(-1, "ef_portal.ogg", effect.transform.position, entryEffectVolume);

            var prim = effect.LaunchPrimitive(PrimitiveType.Heal, mat, 1.667f);
            prim.CreateParts(4);
            //prim.Flags[0] = healStrength;

            prim.Parts[0] = new EffectPart()
            {
                Active = true,
                Step = 0,
                Alpha = 0,
                AlphaRate = 3,
                AlphaTime = 1400,
                CoverAngle = 360,
                MaxHeight = 30,
                Angle = Random.Range(0, 360),
                Distance = 3.7f,
                RiseAngle = 90
            };

            prim.Parts[1] = new EffectPart()
            {
                Active = true,
                Step = 0,
                Alpha = 0,
                AlphaRate = 3,
                AlphaTime = 1400,
                CoverAngle = 360,
                MaxHeight = 30,
                Angle = Random.Range(0, 360),
                Distance = 3.4f,
                RiseAngle = 90
            };

            prim.Parts[2] = new EffectPart()
            {
                Active = true,
                Step = 0,
                Alpha = 0,
                AlphaRate = 3,
                AlphaTime = 1400,
                CoverAngle = 360,
                MaxHeight = 4,
                Angle = Random.Range(0, 360),
                Distance = 3.6f,
                RiseAngle = 10
            };

            prim.Parts[3] = new EffectPart()
            {
                Active = true,
                Step = 0,
                Alpha = 0,
                AlphaRate = 3,
                AlphaTime = 1400,
                CoverAngle = 360,
                MaxHeight = 4,
                Angle = Random.Range(0, 360),
                Distance = 3.7f,
                RiseAngle = 5
            };

            for (var i = 0; i < 4; i++)
            {
                for (var j = 0; j < EffectPart.SegmentCount; j++)
                {
                    prim.Parts[i].Heights[j] = 0;
                    prim.Parts[i].Flags[j] = 2; //1 is spin, 0 is no spin
                }
            }

            return effect;
        }
    }
}