using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.MapEditor;
using Assets.Scripts.Network;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.Skills
{
    [RoEffect("FrostDiverTrail")]
    public class FrostDiverTrailEffect : IEffectHandler
    {
        public static void LaunchFrostDiverTrail(ServerControllable src, ServerControllable target, float launchDelay)
        {
            var travelTime = (src.transform.position - target.transform.position).magnitude * 0.04f;
            //Debug.Log($"Launch frost diver trail travel time {travelTime} from {src.transform.localPosition} to {target.transform.localPosition}");
            
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.FrostDiverTrail);
            effect.SetDurationByTime(travelTime);
            effect.SourceEntity = target;
            effect.FollowTarget = null;
            effect.DestroyOnTargetLost = false;
            effect.UpdateOnlyOnFrameChange = true;
            effect.PositionOffset = src.transform.position;
            effect.transform.localPosition = src.transform.localPosition;
            effect.transform.localScale = new Vector3(1f, 1f, 1f);
            effect.ActiveDelay = launchDelay;
            effect.Material = EffectSharedMaterialManager.GetMaterial(EffectMaterialType.IceMaterial);
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (effect.SourceEntity == null)
                effect.Flags[0] = 1;

            if (effect.Flags[0] > 0)
                return step <= effect.DurationFrames + 60;
            
            if (step >= effect.DurationFrames)
            {
                effect.Flags[0] = 1; //stop making new pillars
                FrostDiverHitEffect.LaunchFrostDiverHit(effect.SourceEntity);
                return true;
            }

            //move our tracking position closer to the target
            var jumpSize = 1f / (effect.DurationFrames - step);
            var direction = (effect.SourceEntity.transform.position - effect.PositionOffset);
            
            if (step == 0 && direction.magnitude > 1f)
                effect.PositionOffset += direction.normalized * 0.3f;
            else
                effect.PositionOffset += direction * jumpSize;
            
            // Debug.Log($"Position {effect.PositionOffset} direction:{direction} jumpsize:{jumpSize}");

            //spawn a new spike
            var prim = effect.LaunchPrimitive(PrimitiveType.Spike3D, effect.Material, 0.667f);
            var data = prim.GetPrimitiveData<Spike3DData>();

            var height = RoWalkDataProvider.Instance.GetHeightForPosition(effect.PositionOffset.x, effect.PositionOffset.z);
            var dist = Random.Range(0.5f, 1.5f) / 5f;
            var angle = Random.Range(0, 360f) * Mathf.Deg2Rad;
            //var height = RoWalkDataProvider.Instance.GetHeightForPosition(pos2D.x, pos2D.y);
            prim.transform.position = new Vector3(effect.PositionOffset.x + Mathf.Sin(angle) * dist, height - Random.Range(6f, 7f), effect.PositionOffset.z + Mathf.Cos(angle) * dist);
            prim.transform.localScale = Vector3.one;
            prim.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360), Random.Range(0, 15));

            data.Height = Random.Range(15f, 18f) / 5f;
            data.Size = Random.Range(0.6f, 1f) / 5f;
            data.Alpha = 200f;
            //data.AlphaSpeed = 50 * 60f;
            data.AlphaMax = 200;
            data.Speed = 3f / 5f;
            data.Acceleration = -(data.Speed / 0.667f) * 2f;
            data.Flags = Spike3DFlags.SpeedLimit;
            data.StopStep = 20;
            data.FadeOutLength = 0.167f;


            return step <= effect.DurationFrames + 60;
        }
    }
}