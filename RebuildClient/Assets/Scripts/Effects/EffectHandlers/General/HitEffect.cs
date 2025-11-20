using System.Collections.Generic;
using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using UnityEngine;
using UnityEngine.U2D;

namespace Assets.Scripts.Effects.EffectHandlers
{
    public enum HitEffectType
    {
        Default,
        Normal,
        Critical,
        Pierce,
        SpearBoomerang
    }

    [RoEffect("HitEffect")]
    public class HitEffect : IEffectHandler
    {
        private static readonly Dictionary<string, Material> Materials = new();
        private static Sprite[] lensSprite;

        private static void LaunchHitParticles(Vector3 src, Vector3 target, Color32 color, int particleId = 0)
        {
            var dir = (src - target).normalized;
            for (var i = 0; i < 4; i++)
            {
                var pVelocity = Quaternion.Euler(0, Random.Range(-30, 30), Random.Range(-50, 50)) * dir;
                // if (pVelocity.y < 0)
                //     pVelocity.y = -pVelocity.y;
                pVelocity *= Random.Range(6f, 15f) * 1.5f;
                var gravity = 0f;
                if (i >= 2)
                {
                    gravity = Random.Range(10f, 40f);
                    pVelocity = -pVelocity;
                }

                var duration = 0.2f + Random.Range(0, 0.3f);
                EffectParticleManager.Instance.AddTrailParticle(Random.Range(6f, 16f) / 10f, target, pVelocity,
                    duration, color, -pVelocity.magnitude / (1 / duration) / 2f, gravity, particleId);
            }
        }
        //
        // public static Ragnarok3dEffect DirectionalHit(HitEffectType type, Vector3 src, Vector3 target, Color32 color, int particleId)
        // {
        //     if (type == HitEffectType.SpearBoomerang)
        //         HitSpearBoomerang(src, target, color, particleId);
        //     else
        //         Hit1(src, target, color, particleId);
        // }

        public static Ragnarok3dEffect DirectionalHit(HitEffectType type, Vector3 src, Vector3 target)
        {
            if (type == HitEffectType.SpearBoomerang)
                return HitSpearBoomerang(src, target, new Color32(255, 255, 255, 80), 0);

            return Hit1(src, target, new Color32(255, 255, 255, 80), 0);
        }

        public static Ragnarok3dEffect Hit1(Vector3 src, Vector3 target) => Hit1(src, target, new Color32(255, 255, 255, 80), 0);

        public static Ragnarok3dEffect Hit1(Vector3 src, Vector3 target, Color32 color, int particleId)
        {
            //generate hit particles
            LaunchHitParticles(src, target, color, particleId);
            var dir = (src - target).normalized;
            //
            // //generate ring effect
            // if (!Materials.TryGetValue("ring_blue", out var mat))
            // {
            //     mat = new Material(ShaderCache.Instance.PerspectiveAlphaShader);
            //     mat.mainTexture = Resources.Load<Texture2D>("ring_blue");
            //     mat.renderQueue = 3001;
            //     Materials.Add("ring_blue", mat);
            // }

            var mat = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.BluePerspectiveCylinder);

            var effect = RagnarokEffectPool.Get3dEffect(EffectType.HitEffect);
            effect.SetDurationByFrames(9);
            // Debug.Log(effect.Duration);

            var prim = effect.LaunchPrimitive(PrimitiveType.Cylinder3D, mat, effect.Duration);
            prim.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
            prim.transform.position = target + dir / 5f + Vector3.up * 0.5f;
            prim.transform.localScale = Vector3.one * 0.2f;

            var data = prim.GetPrimitiveData<CylinderData>();

            var speed = (0.7f * 60) / 5f;

            data.Velocity = dir * speed;
            data.Acceleration = -(speed / effect.Duration) / 2f;
            data.Height = 3.5f;
            data.InnerRadius = 5f;
            data.OuterRadius = 10f;
            data.Height = 3.5f;
            data.Alpha = 255;
            data.MaxAlpha = 255;
            data.FadeOutLength = effect.Duration / 2f;

            return null;
        }

        public static Ragnarok3dEffect Hit2(ServerControllable src, ServerControllable target)
        {
            //generate ring effect
            if (!Materials.TryGetValue("lens", out var mat1))
            {
                mat1 = new Material(ShaderCache.Instance.AlphaBlendNoZTestShader);
                mat1.renderQueue = 3015; //always front
            }

            if (lensSprite == null)
            {
                var skillAtlas = Resources.Load<SpriteAtlas>("SkillAtlas");
                lensSprite = new Sprite[2];
                //lensSprite[0] = skillAtlas.GetSprite("testarrow"); //lens1
                lensSprite[0] = EffectSharedMaterialManager.GetAtlasSprite(skillAtlas, "lens1"); //lens1
                lensSprite[1] = EffectSharedMaterialManager.GetAtlasSprite(skillAtlas, "lens2"); //lens2
            }

            AudioManager.Instance.OneShotSoundEffect(target.Id, $"ef_hit2.ogg", target.transform.position);
            LaunchHitParticles(src.transform.position, target.transform.position + new Vector3(0, 1, 0), new Color32(255, 255, 255, 80));

            var effect = RagnarokEffectPool.Get3dEffect(EffectType.HitEffect);
            effect.SetDurationByFrames(30);
            effect.FollowTarget = target.gameObject;
            effect.DestroyOnTargetLost = false;
            effect.SetBillboardMode(BillboardStyle.Normal);
            effect.PositionOffset = new Vector3(0, 2f, 0f);
            effect.transform.localScale = new Vector3(0.02f, 0.02f, 0.02f);
            effect.SetSortingGroup("FrontEffect", 10); //appear in front of damage indicators

            for (var i = 0; i < 8; i++)
            {
                var angle = i * 45 + Random.Range(-15f, 15f);
                var duration = Random.Range(0.16f, 0.5f);
                var prim = effect.LaunchPrimitive(PrimitiveType.Texture2D, mat1, duration);

                var width = Random.Range(5f, 20f);
                var height = Random.Range(20, 40f);
                var speed = Random.Range(0.5f, 5f);
                var accel = -(speed / duration) / 2f;
                var widthSpeed = -(width / duration);
                var heightSpeed = 1.5f * 60f;
                var heightAccel = 0.25f * 60f;
                var startDistance = Random.Range(0f, 5f);

                var x = Mathf.Sin(angle * Mathf.Deg2Rad);
                var y = Mathf.Cos(angle * Mathf.Deg2Rad);
                var position = new Vector2(x, y) * startDistance;
                var data = prim.GetPrimitiveData<Texture2DData>();
                data.MinSize = Vector2.negativeInfinity;
                data.MaxSize = Vector2.positiveInfinity;
                data.Size = new Vector2(width, height);
                data.ScalingSpeed = new Vector2(widthSpeed, heightSpeed);
                data.ScalingAccel = new Vector2(0, heightAccel);
                data.Alpha = 0f;
                data.AlphaSpeed = 32 * 60;
                data.Speed = position.normalized * (speed * 60);
                data.Acceleration = position.normalized * accel;
                data.Sprite = lensSprite[Random.Range(0, 2)];
                data.PivotFromBottom = true;

                prim.transform.localPosition = new Vector3(position.x, position.y, 0f);
                prim.transform.localRotation = Quaternion.Euler(0, 0, -angle);
            }

            return null;
        }

        public static Ragnarok3dEffect HitPierce(Vector3 src, Vector3 target, Color32 color, int particleId)
        {
            //generate hit particles
            LaunchHitParticles(src, target, color, particleId);
            var dir = (src - target).normalized;
            //
            // //generate ring effect
            // if (!Materials.TryGetValue("ring_blue", out var mat))
            // {
            //     mat = new Material(ShaderCache.Instance.PerspectiveAlphaShader);
            //     mat.mainTexture = Resources.Load<Texture2D>("ring_blue");
            //     mat.renderQueue = 3001;
            //     Materials.Add("ring_blue", mat);
            // }

            var mat = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.FireRing);

            var effect = RagnarokEffectPool.Get3dEffect(EffectType.HitEffect);
            effect.SetDurationByFrames(15);
            // Debug.Log(effect.Duration);

            var prim = effect.LaunchPrimitive(PrimitiveType.Cylinder3D, mat, effect.Duration);
            prim.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
            prim.transform.position = target + dir / 5f + Vector3.up * 2f;
            prim.transform.localScale = Vector3.one * 0.2f;

            var data = prim.GetPrimitiveData<CylinderData>();

            var speed = (0.7f * 60) / 5f;

            data.Velocity = dir * speed;
            data.Acceleration = -(speed / effect.Duration) / 2f;
            data.InnerRadius = 6f;
            data.OuterRadius = 11f;
            data.Height = 3.5f;
            data.Alpha = 255;
            data.MaxAlpha = 255;
            data.FadeOutLength = effect.Duration / 2f;

            return null;
        }


        public static Ragnarok3dEffect HitSpearBoomerang(Vector3 src, Vector3 target, Color32 color, int particleId)
        {
            //particles are generated manually later because they're a bit unique
            //LaunchHitParticles(src, target, color, particleId);
            
            var dir = -(src - target).normalized;
            
            // if(Input.GetKey(KeyCode.Tab))
            //     Debug.Break();

            AudioManager.Instance.OneShotSoundEffect(-1, $"ef_hit2.ogg", target);

            var mat = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.PerspectiveLens2Cylinder);

            var effect = RagnarokEffectPool.Get3dEffect(EffectType.HitEffect);
            effect.SetDurationByFrames(15);

            var prim = effect.LaunchPrimitive(PrimitiveType.Cylinder3D, mat, effect.Duration);
            prim.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
            prim.transform.position = target + dir / 5f + Vector3.up * 0.5f;
            prim.transform.localScale = Vector3.one * 0.2f;

            var data = prim.GetPrimitiveData<CylinderData>();

            var speed = (0.7f * 60) / 5f;

            data.Velocity = dir * speed;
            data.Acceleration = -(speed / effect.Duration) / 2f;
            data.InnerRadius = 0.5f;
            data.OuterRadius = 4f;
            data.Alpha = 255;
            data.MaxAlpha = 255;
            data.FadeOutLength = effect.Duration - effect.Duration / 3f;
            data.Height = 0f;
            data.HeightSpeed = 0.25f * 60f * 3;
            data.HeightAccel = 0.15f * 60f * 3;

            for (var i = 0; i < 5; i++)
            {
                var pVelocity = Quaternion.Euler(0, Random.Range(-40, 40), Random.Range(-50, 50)) * dir;
                pVelocity *= Random.Range(6f, 15f) * 1.5f;

                var gravity = 0f;
                var duration = Random.Range(0.4f, 0.5f);
                var startPos = target + dir * Random.Range(0.8f, 1.6f) + Vector3.up * 0.5f;
                
                EffectParticleManager.Instance.AddTrailParticle(Random.Range(6f, 16f) / 50f, startPos, pVelocity,
                    duration, color, -(speed / duration) / 2f, gravity, particleId, 
                    ParticleDisplayMode.Normal, 0.05f);
            }

            return null;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (effect.DurationFrames == 0)
                Debug.LogWarning("AAFAFASF");
            return effect.CurrentPos < effect.Duration;
        }
    }
}