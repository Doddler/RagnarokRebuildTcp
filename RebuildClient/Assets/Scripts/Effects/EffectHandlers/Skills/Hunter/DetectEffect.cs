using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills.Hunter
{
    [RoEffect("Detect")]
    public class DetectEffect : IEffectHandler
    {
        public static Ragnarok3dEffect Create(ServerControllable source, Vector2Int targetPos)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.Detect);
            effect.SourceEntity = source;
            effect.transform.position = targetPos.ToWorldPosition();
            effect.SetDurationByTime(1f);
            effect.UpdateOnlyOnFrameChange = true;
            effect.DestroyOnTargetLost = false;

            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (step == 0)
            {
                var mat = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.SkillSpriteAlphaBlended);
                var prim = effect.LaunchPrimitive(PrimitiveType.Texture3D, mat, 0.95f);
                var data = prim.GetPrimitiveData<Texture3DData>();
                prim.transform.localRotation = Quaternion.Euler(90f, Random.Range(0, 360f), 0f);
                prim.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                data.Size = new Vector2(0.01f, 0.01f);
                data.ScalingSpeed = new Vector2(90f / 5f, 90f / 5f);
                data.ScalingAccel = data.ScalingSpeed / 14f;
                data.MaxSize = Vector2.positiveInfinity;
                data.MinSize = Vector2.zero;
                data.Alpha = 255;
                data.AlphaMax = 255;
                data.FadeOutTime = 0.67f;
                data.Sprite = EffectSharedMaterialManager.GetSkillEffectSprite("fashasha");
                data.Color = Color.white;

                var id = -1;
                AudioManager.Instance.OneShotSoundEffect(effect.SourceEntityId, "hunter_detecting.ogg", effect.transform.position, 0.7f);
            }

            if (step == 17 && effect.Primitives.Count > 0)
            {
                var prim = effect.Primitives[0];
                var data = prim.GetPrimitiveData<Texture3DData>();
                data.ScalingSpeed = new Vector2(6 / 5f, 6 / 5f);
                data.ScalingAccel = data.ScalingSpeed / 30f;
            }
            
            return effect.IsTimerActive;
        }
    }
}