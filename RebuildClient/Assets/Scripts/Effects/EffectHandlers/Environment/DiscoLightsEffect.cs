using Assets.Scripts.Effects.Misc;
using Assets.Scripts.Network;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Assets.Scripts.Effects.EffectHandlers.Environment
{
    [RoEffect("DiscoLights")]
    public class DiscoLightsEffect : IEffectHandler
    {
        private static AsyncOperationHandle<GameObject> lightSpinnerPrefabLoader;
        private static AsyncOperationHandle<GameObject> orbPrefabLoader;
        private static GameObject lightSpinnerPrefab;
        private static GameObject orbPrefab;
        private static bool isLoading;
        
        public static Ragnarok3dEffect LaunchDiscoLights(ServerControllable target)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.DiscoLights);
            effect.SetDurationByTime(60f); //we'll manually end this early probably
            effect.FollowTarget = target.gameObject;
            effect.SourceEntity = target;
            effect.DestroyOnTargetLost = false;
            //target.AttachEffect(effect);
            
            effect.EffectData = new DiscoLightData();

            if (!isLoading && lightSpinnerPrefab == null)
            {
                lightSpinnerPrefabLoader = Addressables.LoadAssetAsync<GameObject>("Assets/Prefabs/LightSpinner.prefab");
                orbPrefabLoader = Addressables.LoadAssetAsync<GameObject>("Assets/Prefabs/WhiteSphere.prefab");
                
                isLoading = true;
            }

            return null;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            if (isLoading)
            {
                // Debug.Log($"Status: {spriteLoadTask.Status}");
                if (lightSpinnerPrefabLoader.Status == AsyncOperationStatus.None || orbPrefabLoader.Status == AsyncOperationStatus.None)
                {
                    effect.ResetStep();
                    return true; //wait until the sprite loads
                }

                lightSpinnerPrefab = lightSpinnerPrefabLoader.Result;
                orbPrefab = orbPrefabLoader.Result;
                
                isLoading = false;
            }

            var data = (DiscoLightData)effect.EffectData;
            
            if (step == 0 && data.Orb == null)
            {
                var orbObj = Object.Instantiate(orbPrefab, effect.transform);
                orbObj.transform.localPosition = Vector3.zero;
                data.Orb = orbObj;
                
                var lightObj = Object.Instantiate(lightSpinnerPrefab, effect.transform);
                lightObj.transform.localPosition = Vector3.zero;
                data.LightControlGroup = lightObj.GetComponent<LightControlGroup>();
            }
            
            if (effect.FollowTarget == null || step > effect.DurationFrames)
            {
                effect.DataValue -= Time.deltaTime;
                if (effect.DataValue < 0)
                {
                    if(data.Orb != null)
                        Object.Destroy(data.Orb);
                    if(data.LightControlGroup != null)
                        Object.Destroy(data.LightControlGroup.gameObject);
                    return false;
                }

                data.LightControlGroup.SetBrightness(effect.DataValue);
                var size = effect.DataValue / 3f;
                data.Orb.transform.localScale = new Vector3(size, size, size);
                return true;
            }

            if (effect.DataValue >= 3)
                return true;
            
            effect.DataValue += Time.deltaTime;
            data.LightControlGroup.SetBrightness(Mathf.Clamp(effect.DataValue, 0, 3f));
            data.Orb.transform.localPosition = Vector3.Lerp(data.Orb.transform.localPosition, new Vector3(0f, 10f, 0f), Time.deltaTime * 3f);

            return true;
        }

        public void OnCleanup(Ragnarok3dEffect effect)
        {
            lightSpinnerPrefab = null;
            orbPrefab = null;
            isLoading = false;
        }
    }
}