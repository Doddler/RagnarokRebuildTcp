using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public class LightingConfigurationSettings : EditorWindow
{
    Editor editorSetup;
    LightingConfiguration lightSettings;

    private bool fileExists;
    private Vector2 scrollPosition;

    public string lightingConfigurationPath = "Assets/BakeManager/Presets/MyLightingConfiguration.asset";

    [MenuItem("Tools/Lightmaps/Lighting Preset")]
    public static void ShowWindow()
    {
        LightingConfigurationSettings window = GetWindow<LightingConfigurationSettings>();
        window.LightingConfigurationExists();

        Texture icon = AssetDatabase.LoadAssetAtPath<Texture>("Assets/BakeManager/Icon/BakeIcon.png");
        GUIContent titleContent = new GUIContent("Lighting Preset", icon);
        window.titleContent = titleContent;
    }

    void OnDestroy()
    {
        editorSetup = null;
    }

    void OnGUI()
    {
        lightingConfigurationPath = EditorGUILayout.TextField(lightingConfigurationPath);

        if (lightSettings != null)
        {
            if (GUILayout.Button("Unload current Lighting Preset"))
            {
                if (lightSettings != null)
                {
                    lightSettings = null;
                    editorSetup = null;
                }
            }
            else
                if (GUILayout.Button("Save current Lighting Preset to Project"))
                {
                    LightingConfigurationExists();
                    if(!fileExists)
                    {
                        AssetDatabase.CreateAsset (lightSettings, lightingConfigurationPath);
                    }
                    else
                    {
                        AssetDatabase.SaveAssets();
                    }
                
                    if (lightSettings != null)
                        editorSetup = Editor.CreateEditor(lightSettings);
                }


            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            if (editorSetup != null)
            {
                editorSetup.Repaint();
                editorSetup.OnInspectorGUI();
            }

            EditorGUILayout.EndScrollView();
        }
        else
        {
            if (fileExists && GUILayout.Button("Load Lighting Preset from Project path"))
            {
                lightSettings = AssetDatabase.LoadAssetAtPath<LightingConfiguration>(lightingConfigurationPath);
                if (lightSettings != null)
                    editorSetup = Editor.CreateEditor(lightSettings);
            }
            else{
                if (GUILayout.Button("Create a Lighting Preset by copying current Scene lighting settings"))
                {
                    lightSettings = new LightingConfiguration();
                    lightSettings.Save();
                    editorSetup = Editor.CreateEditor(lightSettings);
                }
            }
        }
    }

    public void LightingConfigurationExists()
    {
        if (System.IO.File.Exists(lightingConfigurationPath))
        {
            fileExists = true;
        }
        else
        {
            fileExists = false;
        }
    }
}