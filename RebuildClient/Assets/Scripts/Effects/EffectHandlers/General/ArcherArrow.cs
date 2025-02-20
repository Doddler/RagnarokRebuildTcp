using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.General
{
    [RoEffect("ArcherArrow")]
    public class ArcherArrow : IEffectHandler
    {
        private static Material arrowMaterial;
        private static Texture2D arrowTexture;

        public static Ragnarok3dEffect CreateArrow(ServerControllable source, GameObject target, float delayTime, float offset = 0f)
        {
            if (arrowMaterial == null)
            {
                arrowTexture = Resources.Load<Texture2D>("arrow2");
                arrowMaterial = new Material(ShaderCache.Instance.AlphaBlendParticleShader);
                arrowMaterial.renderQueue = 3001;
                arrowMaterial.mainTexture = arrowTexture;
            }

            var speed = 38f + Random.Range(0, 4f);
            
            var startPosition = source.transform.position + new Vector3(0, 2+offset, 0);
            var targetPosition = target.transform.position + new Vector3(0, 2+offset+Random.Range(-0.1f, 0.1f), 0);
            var distance = Vector3.Distance(startPosition, targetPosition);

            var effect = RagnarokEffectPool.Get3dEffect(EffectType.ArcherArrow);
            effect.SourceEntity = source;
            effect.AimTarget = target;
            effect.Duration = distance / speed;
            effect.transform.position = startPosition;
            effect.ActiveDelay = delayTime;

            var prim = effect.LaunchPrimitive(PrimitiveType.DirectionalBillboard, arrowMaterial, effect.Duration);
            var data = prim.GetPrimitiveData<EffectSpriteData>();

            data.Texture = arrowTexture;
            data.FrameRate = 12;
            data.Alpha = 255;
            data.Style = BillboardStyle.AxisAligned;
            data.Width = arrowTexture.width / 65f;
            data.Height = arrowTexture.height / 65f;
            data.BaseRotation = new Vector3(0, -90, -90);

            prim.Velocity = (targetPosition - startPosition).normalized * speed; //-2.75f;
            prim.transform.position = startPosition + prim.Velocity * 0.03f;

            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (effect.Primitives.Count < 1)
                return false;

            var prim = effect.Primitives[0];
            if (!prim.IsActive || effect.SourceEntity == null || effect.AimTarget == null)
                return false;

            var target = effect.AimTarget.transform.position + new Vector3(0, 2, 0);
            var speed = prim.Velocity.magnitude;
            var distToArrow = Vector3.Distance(effect.SourceEntity.transform.position, prim.transform.position);
            var distToTarget = Vector3.Distance(effect.SourceEntity.transform.position, effect.AimTarget.transform.position);
            
            // Debug.Log($"Distance {distToArrow} {distToTarget}");
            
            if (distToArrow >= distToTarget)
            {
                prim.EndPrimitive();
                return false;
            }

            prim.Velocity = (target - prim.transform.position).normalized * speed;

            return step < effect.DurationFrames;
        }
    }
}