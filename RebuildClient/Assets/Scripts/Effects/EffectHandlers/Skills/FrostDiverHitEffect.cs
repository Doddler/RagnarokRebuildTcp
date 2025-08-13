using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("FrostDiverHit")]
    public class FrostDiverHitEffect : IEffectHandler
    {
        public static void LaunchFrostDiverHit(ServerControllable target)
        {
            AudioManager.Instance.OneShotSoundEffect(target.Id, $"ef_frostdiver2.ogg", target.transform.position);
            
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.FrostDiverHit);
            effect.SetDurationByFrames(60);
            effect.SourceEntity = target;
            effect.FollowTarget = null;
            effect.DestroyOnTargetLost = false;
            effect.UpdateOnlyOnFrameChange = true;
            effect.transform.position = target.transform.position.SnapToWorldHeight();
            effect.transform.localScale = new Vector3(1f, 1f, 1f);
            effect.Material = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.IceMaterial);

            for (var i = 0; i < 8; i++)
            {
                
                //spawn a new spike
                var prim = effect.LaunchPrimitive(PrimitiveType.Spike3D, effect.Material, 0.667f);
                var data = prim.GetPrimitiveData<Spike3DData>();

                //var height = RoWalkDataProvider.Instance.GetHeightForPosition(effect.transform.position);
                var dist = Random.Range(0.1f, 0.5f) / 5f;
                var angle = Random.Range(0, 360f) * Mathf.Deg2Rad;
                //var height = RoWalkDataProvider.Instance.GetHeightForPosition(pos2D.x, pos2D.y);
                prim.transform.localPosition = new Vector3(Mathf.Sin(angle) * dist, -6f, Mathf.Cos(angle) * dist);
                prim.transform.localScale = Vector3.one;
                prim.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360), Random.Range(0, 10));

                data.Height = Random.Range(20f, 30f) / 5f;
                data.Size = Random.Range(1f, 2.5f) / 5f;
                data.Alpha = 0f;
                data.AlphaSpeed = 50 * 60f;
                data.AlphaMax = 200;
                data.Speed = 3f / 5f;
                data.Acceleration = -(data.Speed / 0.667f) * 2f;
                data.Flags = Spike3DFlags.SpeedLimit;
                data.StopStep = 20;
                data.FadeOutLength = 0.167f;

            }
        }
        
        
    }
}