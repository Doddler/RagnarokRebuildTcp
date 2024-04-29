using System.Linq;
using Assets.Scripts;
using Assets.Scripts.Sprites;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Utility.Editor
{
    [CustomEditor(typeof(FogController))]
    public class FogControllerEditor : UnityEditor.Editor
    {
        private FogController fogController;

        private float oldNear;
        private float oldFar;
        private bool hasFogData;
        private FogInfo mapFog;

        private void OnEnable()
        {
            fogController = (FogController)target;
            var dataFile = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/fogData.json");
            var json = JsonUtility.FromJson<Wrapper<FogInfo>>(dataFile.text);

            var mapName = EditorSceneManager.GetActiveScene().name;
            // Debug.Log(mapName);
            
            mapFog = json.Items.FirstOrDefault(m => m.Map == mapName);
            if (mapFog != null)
            {
                
                hasFogData = true;
                oldNear = RenderSettings.fogStartDistance;
                oldFar = RenderSettings.fogEndDistance;
                
            }
        }
        
        private void OnDisable()
        {
            if (hasFogData)
            {
                RenderSettings.fogStartDistance = oldNear;
                RenderSettings.fogEndDistance = oldFar;
                if (CameraFollower.Instance != null && CameraFollower.Instance.Recorder != null)
                    CameraFollower.Instance.Recorder.GetComponent<Camera>().backgroundColor = Color.black;
                hasFogData = false;
            }
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            
            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.LabelField($"Distance: {fogController.GetFogDistance}");
            }
            
            
            if ((fogController.OverrideDistance || !EditorApplication.isPlaying) 
                && Physics.Raycast(fogController.transform.position, fogController.transform.forward, out var hit, 900, 1 << LayerMask.NameToLayer("Ground")))
            {
                EditorGUILayout.LabelField($"Real distance: {hit.distance}");
            }

            if (mapFog != null)
            {
                EditorGUILayout.LabelField($"Fog Near/Far: {mapFog.NearPlane}/{mapFog.FarPlane}");
                fogController.UpdateFog(mapFog.NearPlane * 1500f / 400f, mapFog.FarPlane * 1500f / 400f, false);
            }
        }
    }
}