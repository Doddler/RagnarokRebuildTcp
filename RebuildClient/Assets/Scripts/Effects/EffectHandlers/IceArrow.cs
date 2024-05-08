using System;
using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using UnityEngine;
using UnityEngine.U2D;
using Random = UnityEngine.Random;

namespace Assets.Scripts.Effects.EffectHandlers
{
    [RoEffect("IceArrow")]
    public class IceArrow : IEffectHandler
    {
        private static SpriteAtlas iceArrowAtlas;
        private static Material coldboltMaterial;
        private static Material coldboltRingMaterial;
        private static Func<RagnarokPrimitive, bool> groundHitTrigger;
        
        private static string[] SpriteNames = { "icearrow" };


        public static Ragnarok3dEffect Create(ServerControllable source, ServerControllable target, int count)
        {
            if (coldboltMaterial == null)
            {
                coldboltMaterial = new Material(ShaderCache.Instance.AlphaBlendParticleShader);
                coldboltMaterial.renderQueue = 3001;
            }

            if (coldboltRingMaterial == null)
            {
                var tex = Resources.Load<Texture2D>("ring_blue");
                coldboltRingMaterial = new Material(ShaderCache.Instance.PerspectiveAlphaShader);
                coldboltRingMaterial.renderQueue = 3001;
                coldboltRingMaterial.mainTexture = tex;
            }

            if (iceArrowAtlas == null)
                iceArrowAtlas = Resources.Load<SpriteAtlas>("SkillAtlas");

            var effect = RagnarokEffectPool.Get3dEffect(EffectType.IceArrow);
            effect.SourceEntity = source;
            effect.SetDurationByFrames(12 + count * 10 + 60);
            effect.FollowTarget = target.gameObject;
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
                var file = $"ef_icearrow{id}.ogg";
                if (id == 0)
                    file = $"ef_icearrow.ogg";
                AudioManager.Instance.OneShotSoundEffect(effect.SourceEntityId, file, effect.transform.position);
            }

            if (step >= 12 && (step - 12) % 10 == 0 && step < effect.DurationFrames && (step - 12) / 10 <= effect.ObjCount)
            {
                var prim = effect.LaunchPrimitive(PrimitiveType.DirectionalBillboard, coldboltMaterial, 1.2f);
                var data = prim.GetPrimitiveData<EffectSpriteData>();

                data.Atlas = iceArrowAtlas;
                data.FrameRate = 12;
                data.Style = BillboardStyle.AxisAligned;
                data.Width = 11.5f / 5f;
                data.Height = 3.8f / 5f;
                data.SpriteList = SpriteNames;
                data.BaseRotation = new Vector3(0, 0, -90);

                var startPos = new Vector3(30f + Random.Range(-5f, 5f), 60f, 20f + Random.Range(-5f, 5f)) / 5f;
                var targetPos = new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f)) / 5f;

                prim.Velocity = (targetPos - startPos).normalized * 33f; //-2.75f;
                prim.transform.position = effect.transform.position + startPos;
                prim.EventTrigger = groundHitTrigger;
            }

            var delay = 26;
            if (step > delay && (step - delay) % 12 == 0 && (step - delay) <= effect.ObjCount * 12)
            {
                //this actually should be on the damage code...
                // if(effect.FollowTarget) //no more bolts if our follow target ends
                //     CameraFollower.Instance.AttachEffectToEntity("firehit1", effect.FollowTarget, effect.SourceEntityId);
            }

            return step < effect.DurationFrames;
        }

        public void OnEvent(Ragnarok3dEffect effect, RagnarokPrimitive sender)
        {
            var prim = effect.LaunchPrimitive(PrimitiveType.Circle, coldboltRingMaterial, 0.5f);
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
            
            sender.IsActive = false;
            sender.gameObject.SetActive(false);
        }
    }
}