using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills.Crusader
{
    [RoEffect("GrandCross")]
    public class GrandCrossEffect : IEffectHandler
    {
        public static Ragnarok3dEffect Create(GameObject target, bool isDark = false)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.GrandCross);
            effect.FollowTarget = target;
            effect.SetDurationByTime(2f);
            effect.UpdateOnlyOnFrameChange = true;
            effect.DestroyOnTargetLost = false;

            AudioManager.Instance.OneShotSoundEffect(-1, $"cru_grand cross.ogg", effect.transform.position);

            var mat = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.CastWind);
            var prim = effect.LaunchPrimitive(PrimitiveType.GrandCross, mat, 9999f);

            prim.CreateParts(2);

            for (var i = 0; i < 2; i++)
            {
                var part = prim.Parts[i];

                part.Active = true;
                part.Step = 0;
                part.MaxHeight = 60f / 5f;
                part.RotStart = i * 90;
                part.Alpha = 0;
                part.Heights[0] = 24f / 5f;
                part.Heights[1] = 3.2f / 5f;
                part.Heights[2] = 0f;
                part.Color = new Color32(255, 255, 255, 255);
            }

            mat = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.WhiteRing); //we need a different mat for dark
            prim = effect.LaunchPrimitive(PrimitiveType.Aura3, mat, 2f);

            prim.CreateParts(4);

            for (var i = 0; i < 4; i++)
            {
                var part = prim.Parts[i];
                part.Active = true;
                part.Step = 0;
                part.CoverAngle = 90;
                part.MaxHeight = 60f;
                part.Alpha = 0;
                part.Distance = 19.9f;
                part.RiseAngle = 90;
                part.Flags[4] = 0; //1 for dark type grand cross
            }

            prim.Parts[0].RotStart = 180;
            prim.Parts[0].Flags[0] = 23;
            prim.Parts[0].Flags[1] = 23;

            prim.Parts[1].RotStart = 90;
            prim.Parts[1].Flags[0] = 23;
            prim.Parts[1].Flags[1] = -23;

            prim.Parts[2].RotStart = 0;
            prim.Parts[2].Flags[0] = -23;
            prim.Parts[2].Flags[1] = -23;

            prim.Parts[3].RotStart = 270;
            prim.Parts[3].Flags[0] = -23;
            prim.Parts[3].Flags[1] = 23;

            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if(step > 120)
                effect.EndEffect();

            return effect.IsTimerActive;
        }
    }
}