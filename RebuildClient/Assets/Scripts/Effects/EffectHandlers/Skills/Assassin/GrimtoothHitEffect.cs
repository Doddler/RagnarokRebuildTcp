using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Data;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills.Assassin
{
    [RoEffect("GrimtoothHit")]
    public class GrimtoothHitEffect : IEffectHandler
    {
        public static void Create(ServerControllable target)
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
            effect.Material = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.StoneMaterial);

            var rot = Random.Range(0f, 360f);

            for (var i = 0; i < 3; i++)
            {

                //spawn a new spike
                var prim = effect.LaunchPrimitive(PrimitiveType.Spike3D, effect.Material, 0.667f);
                var data = prim.GetPrimitiveData<Spike3DData>();

                //var height = RoWalkDataProvider.Instance.GetHeightForPosition(effect.transform.position);
                var dist = Random.Range(0.1f, 0.5f) / 5f;
                var angle = Random.Range(0, 360f) * Mathf.Deg2Rad;
                //var height = RoWalkDataProvider.Instance.GetHeightForPosition(pos2D.x, pos2D.y);

                var rotation = Quaternion.Euler(0, rot + i * 120, -35);

                var offset = prim.transform.localRotation * new Vector3(0f, -9f, 0f);
                offset = new Vector3(offset.x, 1f, offset.z);
                
                prim.transform.localRotation = rotation * Quaternion.Euler(0, GameRandom.NextFloat(-3f, 3f), 0f);
                prim.transform.localPosition = rotation * new Vector3(0f, -9f, 0f) + offset;// new Vector3(Mathf.Sin(angle) * dist, -6f, Mathf.Cos(angle) * dist);
                prim.transform.localScale = Vector3.one;
                

                data.Height = 25f / 5f;
                data.Size = 0.9f / 5f;
                data.Alpha = 0f;
                data.AlphaSpeed = 50 * 60f;
                data.AlphaMax = 255f;
                data.Speed = 3.5f / 5f / 2f;
                data.Acceleration = 0.001f / 5f;
                data.Flags = Spike3DFlags.SpeedLimit;
                data.StopStep = 20;
                data.FadeOutLength = 0.167f;
            }
        }
            

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            return effect.IsTimerActive;
        }
    }
}