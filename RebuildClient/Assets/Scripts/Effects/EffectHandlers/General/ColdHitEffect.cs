using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Effects.PrimitiveHandlers;
using Assets.Scripts.Sprites;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.General
{
    [RoEffect("ColdHit")]
    public class ColdHitEffect : IEffectHandler
    {
        private static Material coldHitMaterial;
        private static Material smokeMaterial;
        
        public static void ColdHit(GameObject target)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.ColdHit);
            effect.SetDurationByFrames(30);
            effect.FollowTarget = target.gameObject;
            effect.DestroyOnTargetLost = false;
            effect.UpdateOnlyOnFrameChange = true;
            effect.SetBillboardMode(BillboardStyle.Normal);
            effect.PositionOffset = new Vector3(0, 0.6f, 0);
            effect.transform.localScale = new Vector3(0.02f, 0.02f, 0.02f);
            effect.SetSortingGroup("FrontEffect", 10); //appear in front of damage indicators
            
            IceArrow.InitIceArrowResources();

            if (coldHitMaterial == null)
            {
                coldHitMaterial = new Material(ShaderCache.Instance.AlphaBlendNoZTestShader)
                {
                    mainTexture = Resources.Load<Texture2D>("ice"),
                    renderQueue = 3015
                };
                
                smokeMaterial = new Material(ShaderCache.Instance.AlphaBlendNoZTestShader)
                {
                    mainTexture = Resources.Load<Texture2D>("smoke"),
                    renderQueue = 3015
                };
            }
            
            //breaking ice effect
            var tmpObj = 0;
            for (var i = 0; i < 360; i += 40, tmpObj++)
            {
                var angle = i * 40 + Random.Range(-15f, 45f);
                var duration = 0.25f;
                var prim = effect.LaunchPrimitive(PrimitiveType.Texture2D, coldHitMaterial, duration);

                var width = 4f;
                var height = Random.Range(0, 5) + 45f;
                var speed = Random.Range(0f, 4.5f) + 4.8f;
                var accel = -(speed / duration) / 1.5f;
                // var widthSpeed = -(width / duration);
                // var heightSpeed = 1.5f / 2.5f * 60f;
                // var heightAccel = 0.25f / 5f * 60f;
                var startDistance = Random.Range(0f, 0.5f);

                if (tmpObj % 3 == 0)
                {
                    width = 10;
                    height = Random.Range(0, 10) + 100f;
                    speed = (Random.Range(0, 4.5f) + 12.8f);
                }

                var x = Mathf.Sin(angle * Mathf.Deg2Rad);
                var y = Mathf.Cos(angle * Mathf.Deg2Rad);
                var position = new Vector2(x, y) * startDistance;
                var data = prim.GetPrimitiveData<Texture2DData>();
                // data.MinSize = Vector2.negativeInfinity;
                // data.MaxSize = Vector2.positiveInfinity;
                data.Size = new Vector2(width, height); 
                // data.ScalingSpeed = new Vector2(widthSpeed, heightSpeed);
                // data.ScalingAccel = new Vector2(0, heightAccel);
                data.Alpha = 200f;
                data.AlphaSpeed = 0f;
                data.FadeOutLength = 0.17f;
                data.Speed = position.normalized * (speed * 60f);
                data.Acceleration = position.normalized * accel;
                
                // Debug.Log($"Shatter Speed: {data.Speed}");

                prim.transform.localPosition = new Vector3(position.x, position.y, 0f);
                prim.transform.localRotation = Quaternion.Euler(0, 0, -angle);
                prim.RenderHandler = Texture2DPrimitive.RenderPointyQuad2D;
            }
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (step == 0 || step == 7)
            {
                var duration = 0.416f;
                var prim = effect.LaunchPrimitive(PrimitiveType.Texture2D, smokeMaterial, duration);
                var data = prim.GetPrimitiveData<Texture2DData>();

                var angle = Random.Range(-20, 20);
                var fadeOutLength = duration / 3f;
                var speed = (Quaternion.Euler(0, 0, angle) * Vector3.up).normalized * 0.85f;

                data.Alpha = 255;
                data.AlphaSpeed = 0;
                data.Size = Vector2.zero;
                data.MinSize = Vector2.zero;
                data.MaxSize = new Vector2(9999, 9999);
                data.FadeOutLength = fadeOutLength;
                data.ScalingSpeed = new Vector2(6f, 6f) * 60f;
                data.ChangedScalingSpeed = new Vector2(0.5f, 0.5f) * 60f;
                data.ScalingChangeStep = 10;
                data.ScalingAccel = Vector2.zero;
                data.Speed = speed * 60f;
                data.Acceleration = Vector2.zero;
                
                // Debug.Log($"Smoke Speed: {data.Speed} {angle}");
                
                prim.transform.localPosition = Vector3.zero;
                prim.transform.localRotation = Quaternion.identity;


            }
            
            return step < effect.DurationFrames;
        }
    }
}