using System;
using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Objects;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Assets.Scripts.Effects.EffectHandlers.Skills.Custom
{
    [RoEffect("EarthShaker")]
    public class EarthShakerEffect  : IEffectHandler
    {
        public static Ragnarok3dEffect Create(Vector3 sourcePosition, Vector3 targetPosition)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.EarthShaker);
            effect.FollowTarget = null;
            effect.DestroyOnTargetLost = false;
            effect.SetDurationByTime(4.2f);
            effect.UpdateOnlyOnFrameChange = true;
            effect.transform.position = sourcePosition; //each child will be placed separately
            effect.transform.localScale = new Vector3(1f, 1f, 1f);
            effect.Material = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.StoneMaterial);

            var vector = new Vector2(targetPosition.x, targetPosition.z) - new Vector2(sourcePosition.x, sourcePosition.z);
            effect.DataValue = Mathf.Atan2(vector.x, vector.y) * Mathf.Rad2Deg;
            //effect.DataValue = Vector2.Angle(Vector2.up, new Vector2(targetPosition.x, targetPosition.z) - new Vector2(sourcePosition.x, sourcePosition.z)); //angle
            
            Debug.Log($"Earthshaker aimed {vector} with angle {effect.DataValue}");

            // var len = 4f;

            //position.SnapToWorldHeight()
            //
            // if (mask.Length < 25)
            //     throw new Exception($"Effect mask for HeavensDrive is too small! Expecting 5x5 masked area.");
            //
            // for (var x1 = 0; x1 < 5; x1++)
            // {
            //     for (var y1 = 0; y1 < 5; y1++)
            //     {
            //         var x = x1 - 2;
            //         var y = y1 - 2;
            //
            //         if (!mask[x1 + y1 * 5])
            //             continue;
            //
            //         var pos = new Vector2Int(x + tilePosition.x, y + tilePosition.y).ToWorldPosition();
            //
            //         var rndLen = Random.Range(0, 30);
            //
            //         //spawn a new spike
            //         var prim = effect.LaunchPrimitive(PrimitiveType.Spike3D, effect.Material, 3.2f + (rndLen / 60f));
            //         var data = prim.GetPrimitiveData<Spike3DData>();
            //
            //         prim.transform.position = new Vector3(pos.x, pos.y - 2f, pos.z);
            //         prim.transform.localScale = Vector3.one;
            //         prim.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360), Random.Range(0, 10));
            //         // prim.DelayTime = startTime;
            //
            //         data.Height = Random.Range(10f, 16f) / 5f;
            //         data.Size = Random.Range(3f, 3.5f) / 5f;
            //         data.Alpha = 255;
            //         data.AlphaSpeed = 0;
            //         data.AlphaMax = 255;
            //         data.Speed = 1f / 5f;
            //         data.Acceleration = 0.01f;
            //         data.Flags = Spike3DFlags.SpeedLimit | Spike3DFlags.ReturnDown;
            //         data.StopStep = 14;
            //         data.ChangeStep = 11;
            //         data.ChangeSpeed = -1.2f / 5f;
            //         data.ChangeAccel = 0;
            //         data.ReturnStep = 180 + rndLen;
            //         data.ReturnSpeed = -1.2f / 5f;
            //         data.ReturnAccel = 0;
            //         data.FadeOutLength = 0.167f;
            //     }
            // }

            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (step == 0 && effect.IsStepFrame)
            {
                //AudioManager.Instance.OneShotSoundEffect(-1, "wizard_earthspike.ogg", effect.transform.position, 1f);
                AudioManager.Instance.OneShotSoundEffect(-1, "earth_quake.ogg", effect.transform.position, 0.7f);
                CameraFollower.Instance.ShakeTime = 0.6f;
            }

            if (step < 35 && effect.IsStepFrame)
            {
                var lastPos = effect.transform.position;
                var rndLen = Random.Range(0, 30);

                for (var i = 0; i <= 1 + step / 10; i++)
                {

                    var angle = effect.DataValue + Random.Range(-15f, 15f);
                    
                    var dir = new Vector2(Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad));
                    var newPos = effect.transform.position + (new Vector3(dir.x, 0, dir.y) * step);
                    
                    //spawn a new spike
                    var prim = effect.LaunchPrimitive(PrimitiveType.Spike3D, effect.Material, 3.2f + (rndLen / 60f));
                    var data = prim.GetPrimitiveData<Spike3DData>();

                    prim.transform.position = newPos.SnapToWorldHeight() + new Vector3(0f, -2f, 0f);
                    prim.transform.localScale = Vector3.one;
                    prim.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360), Random.Range(0, 10));
                    // prim.DelayTime = startTime;

                    data.Height = Random.Range(10f, 16f) / 5f;
                    data.Size = Random.Range(3.5f, 4f) / 5f;
                    data.Alpha = 255;
                    data.AlphaSpeed = 0;
                    data.AlphaMax = 255;
                    data.Speed = 1f / 5f;
                    data.Acceleration = 0.01f;
                    data.Flags = Spike3DFlags.SpeedLimit | Spike3DFlags.ReturnDown;
                    data.StopStep = 14;
                    data.ChangeStep = 11;
                    data.ChangeSpeed = -1.2f / 5f;
                    data.ChangeAccel = 0;
                    data.ReturnStep = 22 + rndLen;
                    data.ReturnSpeed = -1.2f / 5f;
                    data.ReturnAccel = 0;
                    data.FadeOutLength = 0.167f;

                    lastPos = prim.transform.position;
                }
                
                if(step > 0 && step % 10 == 0)
                    AudioManager.Instance.OneShotSoundEffect(-1, "wizard_earthspike.ogg", lastPos, 1f);

            }
            
            return effect.IsTimerActive;
        }
    }
}