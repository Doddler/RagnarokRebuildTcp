using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("StormGust")]
    public class StormGustEffect: IEffectHandler
    {
        public static Ragnarok3dEffect Create(Vector3 position)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.StormGust);
            effect.FollowTarget = null;
            effect.DestroyOnTargetLost = false;
            effect.SetDurationByTime(10f);
            effect.UpdateOnlyOnFrameChange = true;
            effect.transform.position = position.SnapToWorldHeight();
            effect.transform.localScale = new Vector3(1f, 1f, 1f);
            effect.Material = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.IceMaterial);
            
            var cam = CameraFollower.Instance;
            var id = cam.EffectIdLookup["StormGust"];
            CameraFollower.Instance.CreateEffect(id, effect.transform.position, 0);

            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (step == 0)
            {
                
            }

            if (step % 29 == 0 && step > 30 && step <= 150)
            {
                var duration = (215 - step) / 60f;
                
                for (var i = 0; i < 2; i++)
                {
                    //spawn a new spike
                    var prim = effect.LaunchPrimitive(PrimitiveType.Spike3D, effect.Material, duration);
                    var data = prim.GetPrimitiveData<Spike3DData>();

                    var dist = Random.Range(3f, 28f) / 5f;
                    var angle =  Random.Range(0, 360f) * Mathf.Deg2Rad;

                    prim.transform.localPosition = new Vector3(Mathf.Sin(angle) * dist, -4f, Mathf.Cos(angle) * dist);
                    prim.transform.localScale = Vector3.one;
                    prim.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360f) * Mathf.Rad2Deg, 10);
            
                    data.Height = 20f / 5f;
                    data.Size = Random.Range(4f, 5f) / 5f;
                    data.Alpha = 250;
                    data.AlphaSpeed = 0;
                    data.AlphaMax = 250;
                    data.Speed = 1.5f / 5f;
                    data.Acceleration = 0f;
                    data.Flags = Spike3DFlags.SpeedLimit;
                    data.StopStep = 3;
                    data.FadeOutLength = 0.167f;
                }
            }
            
            return effect.IsTimerActive;
        }
    }
}