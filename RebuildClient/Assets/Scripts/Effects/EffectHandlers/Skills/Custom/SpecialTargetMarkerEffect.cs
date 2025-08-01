using Assets.Scripts.Effects.PrimitiveData;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Assets.Scripts.Effects.EffectHandlers.Skills.Custom
{
    [RoEffect("SpecialTargetMarker")]
    public class SpecialTargetMarkerEffect : IEffectHandler
    {
        private static AsyncOperationHandle<GameObject> earthShakerMarkerPrefabLoader;
        private static GameObject markerPrefab;
        private static bool isLoading;
        
        public static Ragnarok3dEffect Create(ServerControllable target, float duration)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.SpecialTargetMarker);
            effect.FollowTarget = target.gameObject;
            effect.SourceEntity = target;
            effect.SetDurationByTime(duration);
            effect.UpdateOnlyOnFrameChange = true;
            effect.DestroyOnTargetLost = true;
            effect.SetBillboardMode(BillboardStyle.Character);

            effect.EffectData = new SpecialMarkerData();
            
            CastLockOnEffect.Create(duration, target.gameObject);
            
            if (!isLoading && markerPrefab == null)
            {
                earthShakerMarkerPrefabLoader = Addressables.LoadAssetAsync<GameObject>("Assets/Effects/Custom/EarthShaker/EarthShakerMarker.prefab");
                
                isLoading = true;
            }

            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (isLoading)
            {
                // Debug.Log($"Status: {spriteLoadTask.Status}");
                if (earthShakerMarkerPrefabLoader.Status == AsyncOperationStatus.None)
                {
                    effect.ResetStep();
                    return true; //wait until the sprite loads
                }

                markerPrefab = earthShakerMarkerPrefabLoader.Result;
                
                isLoading = false;
            }
            
            var data = (SpecialMarkerData)effect.EffectData;

            if (step == 0 && data.Marker == null)
            {
                var markerObj = Object.Instantiate(markerPrefab, effect.transform);
                markerObj.transform.localPosition = Vector3.zero;
                data.Marker = markerObj;

                var child = markerObj.transform.GetChild(0);
                data.Renderer = child.GetComponent<SpriteRenderer>();
            }

            if (!data.IsEnding && data.Alpha < 1f)
                data.Alpha = Mathf.Clamp(data.Alpha + Time.deltaTime * 5, 0, 1f);
            
            if (data.IsEnding)
            {
                data.Alpha -= Time.deltaTime * 5f;
                if(data.Alpha < 0)
                    return false;
            }

            if(data.Renderer != null)
                data.Renderer.color = new Color(1f, 1f, 1f, data.Alpha);
            // Debug.Log(data.Alpha);

            return effect.FollowTarget != null && data.Renderer != null;
        }
    }
}