using System;
using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using UnityEngine;
using UnityEngine.U2D;
using Random = UnityEngine.Random;

namespace Assets.Scripts.Effects.EffectHandlers
{
    [RoEffect("FireArrow")]
    public class FireArrow : IEffectHandler
    {
        private static SpriteAtlas fireboltAtlas;
        private static Material fireboltMaterial;
        private static Material fireboltRingMaterial;
        private static Func<RagnarokPrimitive, bool> groundHitTrigger;

        private static string[] SpriteNames =
            { "firebolt1", "firebolt2", "firebolt3", "firebolt4", "firebolt5", "firebolt6", "firebolt7" };

        public static Ragnarok3dEffect Create(GameObject target, int count)
        {
            if (fireboltMaterial == null)
            {
                fireboltMaterial = new Material(ShaderCache.Instance.AlphaBlendParticleShader);
                fireboltMaterial.renderQueue = 3001;
            }

            if (fireboltRingMaterial == null)
            {
                var tex = Resources.Load<Texture2D>("ring_yellow");
                fireboltRingMaterial = new Material(ShaderCache.Instance.PerspectiveAlphaShader);
                fireboltRingMaterial.renderQueue = 3001;
                fireboltRingMaterial.mainTexture = tex;
            }

            if (fireboltAtlas == null)
                fireboltAtlas = Resources.Load<SpriteAtlas>("FireBolt");

            var effect = RagnarokEffectPool.Get3dEffect(EffectType.FireArrow);
            effect.SetDurationByFrames(12 + count * 10 + 60);
            effect.FollowTarget = target;
            effect.UpdateOnlyOnFrameChange = true;
            effect.ObjCount = count;

            groundHitTrigger ??= primitive => primitive.transform.localPosition.y < 0f;

            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (step == 12)
            {
                var id = Random.Range(0, 4);
                AudioManager.Instance.OneShotSoundEffect($"ef_firearrow{id}.ogg", effect.transform.position);
            }

            if (step >= 12 && (step - 12) % 10 == 0 && step < effect.DurationFrames && (step - 12) / 10 <= effect.ObjCount)
            {
                var prim = effect.LaunchPrimitive(PrimitiveType.DirectionalBillboard, fireboltMaterial, 1f);
                var data = prim.GetPrimitiveData<EffectSpriteData>();

                data.Atlas = fireboltAtlas;
                data.AnimateTexture = true;
                data.FrameRate = 12;
                data.Style = BillboardStyle.AxisAligned;
                data.TextureCount = fireboltAtlas.spriteCount;
                data.Width = 14f / 5f;
                data.Height = 3.5f / 5f;
                data.SpriteList = SpriteNames;
                data.BaseRotation = new Vector3(0, 0, -90);

                var startPos = new Vector3(30f + Random.Range(-5f, 5f), 60f, 20f + Random.Range(-5f, 5f)) / 5f;
                var targetPos = new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f)) / 5f;

                prim.Velocity = (targetPos - startPos).normalized * 33f;
                prim.transform.position = effect.transform.position + startPos;
                prim.EventTrigger = groundHitTrigger;
            }

            var delay = 26;
            if (step > delay && (step - delay) % 12 == 0 && (step - delay) <= effect.ObjCount * 12)
            {
                //debug!
                if(effect.FollowTarget) //no more bolts if our follow target ends
                    CameraFollower.Instance.AttachEffectToEntity("firehit1", effect.FollowTarget);
                //CameraFollower.Instance.TargetControllable.SpriteAnimator.State = SpriteState.Standby;
                //CameraFollower.Instance.TargetControllable.SpriteAnimator.ChangeMotion(SpriteMotion.Hit, true);
            }

            return step < effect.DurationFrames;
        }

        public void OnEvent(Ragnarok3dEffect effect, RagnarokPrimitive sender)
        {
            var prim = effect.LaunchPrimitive(PrimitiveType.Circle, fireboltRingMaterial, 0.5f);
            var data = prim.GetPrimitiveData<CircleData>();

            prim.transform.localScale = new Vector3(2f, 2f, 2f);
            prim.transform.localPosition += new Vector3(0f, 0.1f, 0f);

            data.Alpha = 0f;
            data.MaxAlpha = 254;
            data.AlphaSpeed = data.MaxAlpha / 0.166f;
            data.FadeOutLength = 0.166f;
            data.InnerSize = 1f;
            data.Radius = 0f;
            data.RadiusSpeed = 7.2f; //originally 1.2 per frame
            data.RadiusAccel = -14.76f; //(data.RadiusSpeed / (prim.Duration + 0.66f)) * 3f; //original: -(data.RadiusSpeed / (prim.FrameDuration + 40f)) * 2f;
        }
    }
}