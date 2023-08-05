using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using UnityEngine;
using UnityEngine.U2D;

namespace Assets.Scripts.Effects.EffectHandlers
{
    [RoEffect("FireArrow")]
    public class FireArrow : IEffectHandler
    {
        private static SpriteAtlas FireboltAtlas;
        private static Material FireboltMaterial;

        private static string[] SpriteNames =
            { "firebolt1", "firebolt2", "firebolt3", "firebolt4", "firebolt5", "firebolt6", "firebolt7" };

        public static Ragnarok3dEffect Create(GameObject target, int count)
        {
            if (FireboltMaterial == null)
            {
                FireboltMaterial = new Material(ShaderCache.Instance.AlphaBlendParticleShader);
                FireboltMaterial.renderQueue = 3001;
            }
            
            if(FireboltAtlas == null)
                FireboltAtlas = Resources.Load<SpriteAtlas>("FireBolt");

            var effect = RagnarokEffectPool.Get3dEffect(EffectType.FireArrow);
            effect.SetDurationByFrames(12 + count * 10);
            effect.FollowTarget = target;
            effect.UpdateOnlyOnFrameChange = true;
            effect.ObjCount = count;

            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (step == 12)
            {
                var id = Random.Range(0, 4);
                AudioManager.Instance.OneShotSoundEffect($"ef_firearrow{id}.ogg", effect.transform.position);
            }

            if (step >= 12 && (step - 12) % 10 == 0 && step < effect.DurationFrames)
            {
                var prim = effect.LaunchPrimitive(PrimitiveType.DirectionalBillboard, FireboltMaterial, 1f);
                var data = prim.GetPrimitiveData<EffectSpriteData>();
                
                data.Atlas = FireboltAtlas;
                data.AnimateTexture = true;
                data.FrameRate = 12;
                data.Style = BillboardStyle.AxisAligned;
                data.TextureCount = FireboltAtlas.spriteCount;
                data.Width = 14f / 5f;
                data.Height = 3.5f / 5f;
                data.SpriteList = SpriteNames;
                data.BaseRotation = new Vector3(0, 0, -90);

                var startPos = new Vector3(30f + Random.Range(-5f, 5f), 60f, 20f + Random.Range(-5f, 5f)) / 5f;
                var targetPos = new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f)) / 5f;
                
                prim.Velocity = (targetPos-startPos).normalized * 16.5f;
                prim.transform.position = effect.transform.position + startPos;
            }

            return step < effect.DurationFrames;
        }

        public void OnEvent(Ragnarok3dEffect effect, RagnarokPrimitive sender)
        {
            
        }
    }
}