using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Editor;
using Assets.Scripts.MapEditor.Editor.ObjectEditors;
using Assets.Scripts.Utility;
using NUnit.Framework;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.MapEditor.Editor
{
    class RoLightingManagerWindow : EditorWindow
    {
        public bool IsBaking;
        public bool HasAmbient;
        public bool UseMultiMap;
        public bool ResetMapResources;
        public bool AlwaysRebuildLightprobes;

        public Object[] Scenes; // Object array to store the list of scenes
        private Vector2 scrollPosition;

        private Dictionary<string, byte[]> ambientTextures;
        private List<Light> lights;

        private string mapName;

        private int bakeIndex;


        private const int DirectSamples = 32;
        private const int IndirectSamples = 256;
        private const int EnvironmentSamples = 256;

        private const int NoLightSamples = 1;

        private EditorCoroutine minimapCoroutine;


        [MenuItem("Ragnarok/Lighting Manager")]
        static void Init()
        {
            var window = (RoLightingManagerWindow)GetWindow(typeof(RoLightingManagerWindow), false, "Ro Lighting");
            window.Show();
        }

        private bool TryFindMapEditor(out RoMapEditor mapOut)
        {
            mapOut = null;
            var map = GameObject.FindObjectsOfType<RoMapEditor>();
            foreach (var m in map)
            {
                if (m.gameObject.name.Contains("_walk"))
                    continue;

                mapOut = m;
                return true;
            }

            return false;
        }

        private void ReloadEffects()
        {
            if (!TryFindMapEditor(out var map))
            {
                Debug.LogError("Could not reload resources: could not find map editor in the scene.");
                return;
            }

            var mapName = map.MapData.name;
            var path = $"Assets/Maps/world/{mapName}_world.asset";
            var world = AssetDatabase.LoadAssetAtPath<RagnarokWorld>(path);

            if (world == null)
            {
                Debug.Log($"Could not reload resources: could not find world data at path {path}");
                return;
            }

            var builder = new RagnarokWorldSceneBuilder();
            builder.ReloadEffectsOnly(map.MapData, world);
        }
        
        private void ReloadResources()
        {
            if (!TryFindMapEditor(out var map))
            {
                Debug.LogError("Could not reload resources: could not find map editor in the scene.");
                return;
            }

            var mapName = map.MapData.name;
            var path = $"Assets/Maps/world/{mapName}_world.asset";
            var world = AssetDatabase.LoadAssetAtPath<RagnarokWorld>(path);

            if (world == null)
            {
                Debug.Log($"Could not reload resources: could not find world data at path {path}");
                return;
            }

            var builder = new RagnarokWorldSceneBuilder();
            builder.Load(map.MapData, world);
        }

        private void ReimportMap()
        {
            if (!TryFindMapEditor(out var map))
            {
                Debug.LogError("Could not reload resources: could not find map editor in the scene.");
                return;
            }

            //var mapData = map.MapData;

            var mapName = map.MapData.name;
            var path = $"Assets/Maps/{mapName}.asset";
            //var mapData = AssetDatabase.LoadAssetAtPath<RoMapData>(path);

            var importer = new RagnarokMapDataImporter(path, mapName);
            importer.Import(true, true, true, true, true);
        }

        private void DisableAllLights()
        {
            lights = new List<Light>();

            foreach (var l in GameObject.FindObjectsOfType<Light>())
            {
                if (l.enabled && l.gameObject.activeInHierarchy)
                {
                    lights.Add(l);
                    l.enabled = false;
                }
            }
        }

        private void RestoreLights()
        {
            if (lights == null)
                return;

            foreach (var l in lights)
            {
                if(l)
                    l.enabled = true;
                else
                    Debug.LogWarning($"For some reason, the light {l} on game object {l.name} has been destroyed.");
            }


        }

        private void SetLightSettings()
        {
            Lightmapping.lightingSettings.ao = true;
            Lightmapping.lightingSettings.directSampleCount = DirectSamples;
            Lightmapping.lightingSettings.indirectSampleCount = IndirectSamples;
            Lightmapping.lightingSettings.environmentSampleCount = EnvironmentSamples;
            Lightmapping.lightingSettings.aoExponentDirect = 1f;
            Lightmapping.lightingSettings.aoExponentIndirect = 1f;
            Lightmapping.lightingSettings.prioritizeView = false;
        }

        private void BakeAmbient()
        {
            if (UseMultiMap)
            {
                var path = AssetDatabase.GetAssetPath(Scenes[bakeIndex]);
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }

            if (!TryFindMapEditor(out var map))
                return;

            if (ResetMapResources)
                ReloadResources();

            mapName = map.name;

            RenderSettings.ambientLight = Color.white;
            RenderSettings.ambientIntensity = 5f;

            DisableAllLights();
            SetLightSettings();
            
            if(AlwaysRebuildLightprobes)
                map.RebuildProbes();


            Lightmapping.bakeCompleted -= PostAmbient;
            Lightmapping.bakeCompleted -= BakePost;
            Lightmapping.bakeCompleted += PostAmbient;
            IsBaking = true;
            HasAmbient = true;
            Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.OnDemand;
            Lightmapping.BakeAsync();
        }

        private void PostAmbient()
        {
            ambientTextures = new Dictionary<string, byte[]>();

            foreach (var f in Directory.GetFiles($"Assets/Scenes/Maps/{mapName}/", "*.exr").OrderBy(o => o))
            {
                var baseName = Path.GetFileNameWithoutExtension(f);

                if (baseName.ToLower().Contains("reflection"))
                    continue;

                Debug.Log(f);

                var import = (TextureImporter)TextureImporter.GetAtPath(f);
                import.isReadable = true;
                import.textureCompression = TextureImporterCompression.Uncompressed;

                EditorUtility.SetDirty(import);
                import.SaveAndReimport();

                AssetDatabase.ImportAsset(f);
                AssetDatabase.Refresh();

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(f);
                ambientTextures.Add(baseName, tex.EncodeToPNG());
            }

            RestoreLights();

            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();

            BakeRegular();
        }

        private void BakeRegular()
        {
            if (!TryFindMapEditor(out var map))
                return;

            mapName = map.name;

            var settings = map.gameObject.GetComponent<RoMapRenderSettings>();
            settings.AmbientOcclusionStrength = 0f;

            RenderSettings.ambientLight = Color.black;
            RenderSettings.ambientIntensity = 0f;
            SetLightSettings();

            //Debug.Log("Lights: " + lights.Count);

            if (HasAmbient && lights.Count <= 1) //only directional light
            {
                Lightmapping.lightingSettings.ao = false;
                Lightmapping.lightingSettings.directSampleCount = NoLightSamples;
                Lightmapping.lightingSettings.indirectSampleCount = NoLightSamples;
                Lightmapping.lightingSettings.environmentSampleCount = NoLightSamples;
            }

            Lightmapping.bakeCompleted -= PostAmbient;
            Lightmapping.bakeCompleted -= BakePost;
            Lightmapping.bakeCompleted += BakePost;
            IsBaking = true;

            Lightmapping.lightingSettings.directionalityMode = HasAmbient ? LightmapsMode.CombinedDirectional : LightmapsMode.NonDirectional;
            Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.OnDemand;
            Lightmapping.BakeAsync();
        }

        private void BakePost()
        {
            IsBaking = false;

            if (HasAmbient)
            {
                if (!TryFindMapEditor(out var map))
                    return;

                if (ambientTextures == null || ambientTextures.Count == 0)
                    return;

                foreach (var f in Directory.GetFiles($"Assets/Scenes/Maps/{mapName}/", "*.exr").OrderBy(o => o))
                {
                    var baseName = Path.GetFileNameWithoutExtension(f);
                    var dirName = baseName.Replace("_light", "_dir");
                    var fullDirPath = $"Assets/Scenes/Maps/{mapName}/{dirName}.png";

                    if (!File.Exists(fullDirPath) || !ambientTextures.ContainsKey(baseName))
                        continue;

                    var tex = ambientTextures[baseName];
                    File.WriteAllBytes(fullDirPath, tex);

                    var import = (TextureImporter)TextureImporter.GetAtPath(fullDirPath);
                    import.crunchedCompression = true;

                    EditorUtility.SetDirty(import);
                    import.SaveAndReimport();

                    if (lights.Count <= 1)
                    {
                        //there's no lights, lets shrink the lightmaps a whole bunch as they contain nothing
                        var orig = (TextureImporter)TextureImporter.GetAtPath(f);
                        orig.maxTextureSize = 64;

                        EditorUtility.SetDirty(orig);
                        import.SaveAndReimport();

                        AssetDatabase.ImportAsset(f);
                    }

                    AssetDatabase.ImportAsset(fullDirPath);
                    AssetDatabase.Refresh();
                }

                var settings = map.gameObject.GetComponent<RoMapRenderSettings>();
                settings.AmbientOcclusionStrength = 0.5f;

                ambientTextures = null;
            }

            SetLightSettings();

            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();
            //EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            if (UseMultiMap && bakeIndex + 1 < Scenes.Length)
            {
                bakeIndex++;
                BakeAmbient();
            }
        }
        
        private IEnumerator MakeMinimaps()
        {
            //yield return new EditorWaitForSeconds(1f);


            var mapList = new List<string>();

            foreach (var s in Scenes)
            {
                var path = AssetDatabase.GetAssetPath(s);
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);


                if (!TryFindMapEditor(out var editor))
                    yield break;

                //editor.MapData.RebuildAtlas();
                //editor.UpdateAtlasTexture();

                var lights = FindObjectsOfType<Light>();
                Light dirLight = null;
                var oldStr = 1f;
                foreach (var l in lights)
                {
                    if (l.type == LightType.Directional)
                    {
                        dirLight = l;
                        oldStr = dirLight.shadowStrength;
                        dirLight.shadowStrength = 0f;
                        break;
                    }
                }


                var go = new GameObject("MapCamera");
                var cam = go.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);

                var width = editor.MapData.InitialSize.x;
                var height = editor.MapData.InitialSize.y;

                go.transform.localPosition = new Vector3(width, 210, height);
                go.transform.localRotation = Quaternion.Euler(90, 0, 0);

                cam.orthographic = true;
                cam.orthographicSize = height;

                //yield return new EditorWaitForSeconds(1f);

                //dirLight.shadowStrength = oldStr;

                const int pixelsPerTile = 2;

                var tool = go.AddComponent<ScreenshotCamera>();
                tool.FileName = editor.MapData.name;
                tool.Width = width * pixelsPerTile;
                tool.Height = height * pixelsPerTile;

                tool.TakeScreenshotCoroutine();

                var walk = editor.MapData.WalkData;

                //start mesh building
                var m = new MeshBuilder();
                
                var sharedData = walk.SharedMeshData;
                sharedData.RebuildArea(new RectInt(0, 0, walk.InitialSize.x, walk.InitialSize.y), 1, false);

                //these probably aren't the right UVs but... it won't be textured anyways!
                var uvs = new Vector2[4]
                {
                    new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 0), new Vector2(1, 1)
                };

                var c = new Color(170f / 255f, 170f / 255f, 170f / 255f, 1f);
                var c1 = new Color(1f, 1f, 1f, 1f);

                var colors = new Color[4] { c, c, c, c };

                var count = 0;
                var gos = new List<GameObject>();

                for (var x = 0; x < walk.InitialSize.x; x++)
                {
                    for (var y = 0; y < walk.InitialSize.y; y++)
                    {

                        if (walk.WalkCellData.CellWalkable(x, y))
                            continue;

                        var tVerts = sharedData.GetTileVertices(new Vector2Int(x, y), Vector3.zero);
                        var tNormals = sharedData.GetTileNormals(new Vector2Int(x, y));// topNormals[x1 + y1 * ChunkBounds.width];
                        //var tColors = sharedData.GetTileColors(new Vector2Int(x, y));


                        m.StartTriangle();

                        m.AddVertices(tVerts);
                        m.AddUVs(uvs);
                        m.AddColors(colors);
                        m.AddNormals(tNormals);
                        m.AddTriangles(new[] { 0, 1, 3, 3, 2, 0 });

                        count++;
                        if (count > 8000)
                        {
                            var newGo = BuildMeshIntoObject(m, Shader.Find("UI/Default"));
                            newGo.transform.position = new Vector3(0f, 200f, 0f);
                            newGo.transform.localScale = new Vector3(1f, 0f, 1f);
                            gos.Add(newGo);
                            count = 0;
                        }
                    }
                }

                var newGo2 = BuildMeshIntoObject(m, Shader.Find("UI/Default"));
                newGo2.transform.position = new Vector3(0f, 200f, 0f);
                newGo2.transform.localScale = new Vector3(1f, 0f, 1f);
                gos.Add(newGo2);

                cam.backgroundColor = new Color(66f / 255f, 66f / 255f, 66f / 255f, 1f);
                cam.cullingMask = 1 << LayerMask.NameToLayer("Editor");

                tool.FileName = editor.MapData.name + "_walkmask";
                tool.TakeScreenshotCoroutine();

                mapList.Add(editor.MapData.name);
                foreach (var g in gos)
                    DestroyImmediate(g);


                //start mesh building -------------------------------------------------
                //this one for combined
                m = new MeshBuilder();

                c = new Color(0f, 0f, 0f, 0.5f);
                colors = new Color[4] { c, c, c, c };

                count = 0;
                gos = new List<GameObject>();

                for (var x = 0; x < walk.InitialSize.x; x++)
                {
                    for (var y = 0; y < walk.InitialSize.y; y++)
                    {

                        if (walk.WalkCellData.CellWalkable(x, y))
                            continue;

                        var tVerts = sharedData.GetTileVertices(new Vector2Int(x, y), Vector3.zero);
                        var tNormals = sharedData.GetTileNormals(new Vector2Int(x, y));// topNormals[x1 + y1 * ChunkBounds.width];
                        //var tColors = sharedData.GetTileColors(new Vector2Int(x, y));


                        m.StartTriangle();

                        m.AddVertices(tVerts);
                        m.AddUVs(uvs);
                        m.AddColors(colors);
                        m.AddNormals(tNormals);
                        m.AddTriangles(new[] { 0, 1, 3, 3, 2, 0 });

                        count++;
                        if (count > 8000)
                        {
                            var newGo = BuildMeshIntoObject(m, Shader.Find("Unlit/BlendingTestShader"));
                            newGo.transform.position = new Vector3(0f, 200f, 0f);
                            newGo.transform.localScale = new Vector3(1f, 0f, 1f);
                            gos.Add(newGo);
                            count = 0;
                        }
                    }
                }

                newGo2 = BuildMeshIntoObject(m, Shader.Find("Unlit/BlendingTestShader"));
                newGo2.transform.position = new Vector3(0f, 200f, 0f);
                newGo2.transform.localScale = new Vector3(1f, 0f, 1f);
                gos.Add(newGo2);

                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.cullingMask = -1;

                tool.FileName = editor.MapData.name + "_combined";
                tool.TakeScreenshotCoroutine();

                mapList.Add(editor.MapData.name);
                //yield break;
                foreach (var g in gos)
                    DestroyImmediate(g);

                //end light baking


                DestroyImmediate(go);


                if (dirLight != null)
                    dirLight.shadowStrength = 1f; //oldStr;

                EditorSceneManager.MarkAllScenesDirty();
                EditorSceneManager.SaveOpenScenes();

                //Debug.Log(path);
            }

            AssetDatabase.Refresh();

            //foreach (var path in Directory.GetFiles($"Assets/Maps/minimap/", "*.png"))
            foreach(var mapName in mapList)
            {
                var path = $"Assets/Maps/minimap/{mapName}.png";
                var path2 = $"Assets/Maps/minimap/{mapName}_walkmask.png";

                var tImporter = AssetImporter.GetAtPath(path) as TextureImporter;
                if (tImporter != null)
                {
                    //var tmp = new TextureImporterSettings();
                    //tImporter.ReadTextureSettings(tmp);

                    tImporter.crunchedCompression = true;
                    tImporter.textureType = TextureImporterType.Sprite;
                    tImporter.alphaSource = TextureImporterAlphaSource.FromInput;
                    //tmp.spriteMeshType = SpriteMeshType.FullRect;
                    //tmp.textureType = TextureImporterType.Sprite;
                    tImporter.spriteImportMode = SpriteImportMode.Single;
                    //tImporter.SetTextureSettings(tmp);
                    EditorUtility.SetDirty(tImporter);
                    tImporter.SaveAndReimport();
                    //AssetDatabase.ImportAsset(path);
                }

                tImporter = AssetImporter.GetAtPath(path2) as TextureImporter;
                if (tImporter != null)
                {

                    //var tmp = new TextureImporterSettings();
                    //tImporter.ReadTextureSettings(tmp);

                    tImporter.crunchedCompression = true;
                    tImporter.textureType = TextureImporterType.Sprite;
                    tImporter.alphaSource = TextureImporterAlphaSource.FromInput;
                    tImporter.spriteImportMode = SpriteImportMode.Single;
                    //tmp.spriteMeshType = SpriteMeshType.FullRect;
                    //tmp.spriteMode = 1;
                    //tmp.textureType = TextureImporterType.Sprite;
                    //tImporter.SetTextureSettings(tmp);
                    EditorUtility.SetDirty(tImporter);
                    tImporter.SaveAndReimport();
                    //AssetDatabase.ImportAsset(path);
                }
            }

            AssetDatabase.Refresh();
        }

        private GameObject BuildMeshIntoObject(MeshBuilder m, Shader shader)
        {

            var go2 = new GameObject("MapBlackout");
            var mf = go2.AddComponent<MeshFilter>();
            var mr = go2.AddComponent<MeshRenderer>();
            var mat = new Material(shader);
            go2.layer = LayerMask.NameToLayer("Editor");
            mr.material = mat;
            mat.color = Color.white;

            mf.mesh = m.Build();
            m.Clear();

            return go2;
        }

        public void OnDestroy()
        {
            if (minimapCoroutine != null)
                EditorCoroutineUtility.StopCoroutine(minimapCoroutine);
        }


        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "HAA0101:Array allocation for params parameter", Justification = "It's annoying holy shit")]
        public void OnGUI()
        {

            var bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, clipping = TextClipping.Overflow };

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Lighting Manager", bigStyle);
            GUILayout.Space(10);
            EditorGuiLayoutUtility.HorizontalLine();

            EditorGUILayout.LabelField("Map Settings", EditorStyles.boldLabel);


            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload Resources"))
            {
                ReloadResources();
            }

            if (GUILayout.Button("Reload Effects"))
            {
                ReloadEffects();
            }
            
            if (GUILayout.Button("Reimport Map"))
            {
                ReimportMap();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Clear Lights", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Clear Occlusion"))
            {

            }

            if (GUILayout.Button("Clear All Lighting"))
            {

            }
            EditorGUILayout.EndHorizontal();
            
            
            EditorGUILayout.LabelField("Other Options", EditorStyles.boldLabel);
            
            
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Rebuild Lightprobes"))
            {
                if(TryFindMapEditor(out var editor))
                    editor.RebuildProbes();
            }

            EditorGUILayout.EndHorizontal();


            EditorGUILayout.LabelField("Bake", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (!IsBaking)
            {

                if (GUILayout.Button("Bake Without Ambient Occlusion"))
                {
                    HasAmbient = false;
                    UseMultiMap = false;
                    lights?.Clear();
                    BakeRegular();
                }

                if (GUILayout.Button("Bake All"))
                {
                    UseMultiMap = false;
                    lights?.Clear();
                    BakeAmbient();
                }
            }
            else
            {
                if (GUILayout.Button("Cancel"))
                {
                    Lightmapping.Cancel();
                    IsBaking = false;
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGuiLayoutUtility.HorizontalLine();


            ResetMapResources = GUILayout.Toggle(ResetMapResources, "Reset Map Resources on Batch Bake");
            AlwaysRebuildLightprobes = GUILayout.Toggle(AlwaysRebuildLightprobes, "Always rebuild light probes on scene bake");

            EditorGUILayout.BeginHorizontal();

            if (!IsBaking && Scenes != null && Scenes.Length > 0)
            {

                if (GUILayout.Button("Bake All Scenes"))
                {
                    UseMultiMap = true;
                    bakeIndex = 0;
                    BakeAmbient();
                }
                if (GUILayout.Button("Make Minimaps"))
                {
                    if (minimapCoroutine != null)
                        EditorCoroutineUtility.StopCoroutine(minimapCoroutine);

                    minimapCoroutine = EditorCoroutineUtility.StartCoroutine(MakeMinimaps(), this);
                }
            }

            EditorGUILayout.EndHorizontal();

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false);


            ScriptableObject target = this;
            SerializedObject so = new SerializedObject(target);
            SerializedProperty scenesProperty = so.FindProperty("Scenes");

            EditorGUILayout.PropertyField(scenesProperty, true); // True shows children
            so.ApplyModifiedProperties(); // Apply modified properties

            GUILayout.EndScrollView();
        }
    }
}
