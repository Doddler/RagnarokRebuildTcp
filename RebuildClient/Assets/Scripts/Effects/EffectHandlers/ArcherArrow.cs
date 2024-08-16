using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Sprites;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers
{
    [RoEffect("ArcherArrow")]
    public class ArcherArrow : IEffectHandler
    {
        private static Material arrowMaterial;
        private static Texture2D arrowTexture;

        public static Ragnarok3dEffect CreateArrow(GameObject source, GameObject target, float delayTime)
        {
            if (arrowMaterial == null)
            {
                arrowTexture = Resources.Load<Texture2D>("arrow2");
                arrowMaterial = new Material(ShaderCache.Instance.AlphaBlendParticleShader);
                arrowMaterial.renderQueue = 3001;
                arrowMaterial.mainTexture = arrowTexture;
            }

            var startPosition = source.transform.position + new Vector3(0, 2, 0);
            var targetPosition = target.transform.position + new Vector3(0, 2, 0);
            var distance = Vector3.Distance(startPosition, targetPosition);

            var effect = RagnarokEffectPool.Get3dEffect(EffectType.ArcherArrow);
            effect.AimTarget = target;
            effect.Duration = distance / 40f;
            effect.transform.position = startPosition;
            effect.ActiveDelay = delayTime;

            var prim = effect.LaunchPrimitive(PrimitiveType.DirectionalBillboard, arrowMaterial, effect.Duration);
            var data = prim.GetPrimitiveData<EffectSpriteData>();

            data.Texture = arrowTexture;
            data.FrameRate = 12;
            data.Style = BillboardStyle.AxisAligned;
            data.Width = arrowTexture.width / 65f;
            data.Height = arrowTexture.height / 65f;
            data.BaseRotation = new Vector3(0, -90, -90);

            prim.Velocity = (targetPosition - startPosition).normalized * 40f; //-2.75f;
            prim.transform.position = startPosition + prim.Velocity * 0.03f;

            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (effect.Primitives.Count < 1)
                return false;

            var prim = effect.Primitives[0];
            if (!prim.IsActive)
                return false;

            var target = effect.AimTarget.transform.position + new Vector3(0, 2, 0);
            var distance = Vector3.Distance(prim.transform.position, target);

            if (distance < 40f * Time.deltaTime + 0.1f)
            {
                prim.EndPrimitive();
                return false;
            }

            prim.Velocity = (target - prim.transform.position).normalized * 40f;

            return step < effect.DurationFrames;
        }
    }
}