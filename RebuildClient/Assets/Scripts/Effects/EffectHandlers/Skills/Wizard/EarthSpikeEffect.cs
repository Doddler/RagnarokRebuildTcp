using System;
using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("EarthSpike")]
    public class EarthSpikeEffect: IEffectHandler
    {
        public static Ragnarok3dEffect Create(Vector3 position, float delayTime = 0)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.EarthSpike);
            effect.FollowTarget = null;
            effect.DestroyOnTargetLost = false;
            effect.SetDurationByTime(4.2f);
            effect.UpdateOnlyOnFrameChange = true;
            effect.transform.position = position.SnapToWorldHeight();
            effect.transform.localScale = new Vector3(1f, 1f, 1f);
            effect.ActiveDelay = delayTime;
            effect.Material = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.StoneMaterial);

            // var len = 4f;
            
            // AudioManager.Instance.OneShotSoundEffect(-1, "wizard_earthspike.ogg", effect.transform.position, 0.7f);
            // CameraFollower.Instance.ShakeTime = 0.3f;
            //
            //primary spike
            {
                //spawn a new spike
                var prim = effect.LaunchPrimitive(PrimitiveType.Spike3D, effect.Material, 4f);
                var data = prim.GetPrimitiveData<Spike3DData>();

                prim.transform.localPosition = new Vector3(0, -2f, 0);
                prim.transform.localScale = Vector3.one;
                prim.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360), Random.Range(0, 10));

                data.Height = 18f / 5f;
                data.Size = Random.Range(3f, 3.5f) / 5f;
                data.Alpha = 255;
                data.AlphaSpeed = 0;
                data.AlphaMax = 255;
                data.Speed = 1f / 5f;
                data.Acceleration = 0.01f;
                data.Flags = Spike3DFlags.SpeedLimit | Spike3DFlags.ReturnDown;
                data.StopStep = 15;
                data.ChangeStep = 12;
                data.ChangeSpeed = -1.2f / 5f;
                data.ChangeAccel = 0;
                data.ReturnStep = 210;
                data.ReturnSpeed = -1.2f / 5f;
                data.ReturnAccel = 0;
                data.FadeOutLength = 0.167f;
            }
            
            //secondary spikes
            for (var i = 0; i < 6; i++)
            {
                //spawn a new spike
                var prim = effect.LaunchPrimitive(PrimitiveType.Spike3D, effect.Material, 4f);
                var data = prim.GetPrimitiveData<Spike3DData>();
            
                //var height = RoWalkDataProvider.Instance.GetHeightForPosition(effect.transform.position);
                var dist = 3.5f / 5f;
                var angle = (Random.Range(0, 60f) + 60 * i) * Mathf.Deg2Rad;
                //var height = RoWalkDataProvider.Instance.GetHeightForPosition(pos2D.x, pos2D.y);
                prim.transform.localPosition = new Vector3(Mathf.Sin(angle) * dist, -4f, Mathf.Cos(angle) * dist);
                prim.transform.localScale = Vector3.one;
                prim.transform.localRotation = Quaternion.Euler(0, angle * Mathf.Rad2Deg, 10);
            
                data.Height = 20f / 5f;
                data.Size = Random.Range(4f, 5f) / 5f;
                data.Alpha = 255;
                data.AlphaSpeed = 0;
                data.AlphaMax = 255;
                data.Speed = 1.5f / 5f;
                data.Acceleration = 0f;
                data.Flags = Spike3DFlags.SpeedLimit;
                data.StopStep = 3;
                data.FadeOutLength = 0.167f;
            }
            
            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (step == 0 && effect.IsStepFrame)
            {
                AudioManager.Instance.OneShotSoundEffect(-1, "wizard_earthspike.ogg", effect.transform.position, 0.7f);
                CameraFollower.Instance.ShakeTime = 0.3f;
            }
            
            return effect.IsTimerActive;
        }
    }
}