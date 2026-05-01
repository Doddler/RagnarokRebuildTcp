using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.MapEditor;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills.Assassin
{
    [RoEffect("GrimtoothTrail")]
    public class GrimtoothTrailEffect : IEffectHandler
    {
        public static void Create(ServerControllable src, ServerControllable target, float launchDelay)
        {
            var travelTime = (src.transform.position - target.transform.position).magnitude * 0.04f;

            var effect = RagnarokEffectPool.Get3dEffect(EffectType.GrimtoothTrail);
            effect.SetDurationByTime(travelTime);
            effect.SourceEntity = target;
            effect.FollowTarget = null;
            effect.DestroyOnTargetLost = false;
            effect.UpdateOnlyOnFrameChange = true;
            effect.PositionOffset = target.transform.position;
            effect.transform.position = src.transform.position;
            effect.transform.localScale = new Vector3(1f, 1f, 1f);
            effect.ActiveDelay = launchDelay;
            effect.Material = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.StoneMaterial);
            
            AudioManager.Instance.OneShotSoundEffect(target.Id, $"ef_frostdiver.ogg", target.transform.position);
        }
        
        //Flag 0 - Initial state
        //Flag 1 - Target has been hit
        //FLag 2 - Effect reached its destination (2x distance from player to target)

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (effect.Flags[0] > 1)
                return step <= effect.DurationFrames + 60;
            
            var targetPos = effect.PositionOffset;
            if (effect.SourceEntity == null)
                effect.Flags[0] = 1;
            else
            {
                targetPos = effect.SourceEntity.transform.position;
                effect.PositionOffset = targetPos;
            }
            
            if (step >= effect.DurationFrames)
                effect.Flags[0] = 2;
            
            if (effect.Flags[0] == 0 && step >= effect.DurationFrames / 2)
            {
                effect.Flags[0] = 1; //stop making new pillars
                if(effect.SourceEntity != null)
                    GrimtoothHitEffect.Create(effect.SourceEntity);
                
                return true;
            }

            // if (effect.IsStepFrame && step % 3 != 0)
            //     return step <= effect.DurationFrames + 60;
            
            var dist = (effect.SourceEntity.transform.position - effect.transform.position);
            var newPos = dist.normalized * 0.3f + effect.transform.position + dist * 2f / effect.DurationFrames * step;

            for (var i = 0; i < 2; i++)
            {
                //spawn a new spike
                var prim = effect.LaunchPrimitive(PrimitiveType.Spike3D, effect.Material, 0.667f);
                var data = prim.GetPrimitiveData<Spike3DData>();

                var height = RoWalkDataProvider.Instance.GetHeightForPosition(newPos.x, newPos.z);

                //var height = RoWalkDataProvider.Instance.GetHeightForPosition(pos2D.x, pos2D.y);
                prim.transform.position = new Vector3(newPos.x, height - Random.Range(6f, 6.5f), newPos.z);
                prim.transform.localScale = Vector3.one;
                prim.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360), Random.Range(0, 10));

                data.Height = 10f / 5f;
                data.Size = Random.Range(0.6f, 1f) / 5f;
                data.Alpha = 200f;
                //data.AlphaSpeed = 50 * 60f;
                data.AlphaMax = 200;
                data.Speed = 3f / 5f;
                data.Acceleration = -(data.Speed / 0.667f) * 2f;
                data.Flags = Spike3DFlags.SpeedLimit;
                data.StopStep = 20;
                data.FadeOutLength = 0.167f;
            }

            return step <= effect.DurationFrames + 60;
        }
    }
}