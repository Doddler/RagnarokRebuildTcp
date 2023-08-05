using System.Collections.Generic;
using System.IO;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using Assets.Scripts.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.MapEditor.Editor
{
    class RagnarokWorldSceneBuilder
    {
        private RoMapData data;
        private RagnarokWorld world;

        private GameObject baseObject;

        private Dictionary<string, GameObject> modelCache = new Dictionary<string, GameObject>();

        private Color maxLightColor = Color.black;
        private float maxLight = 0f;

        private void SetWorldLighting()
        {
            //RenderSettings.ambientIntensity = 0f;
            RenderSettings.ambientMode = AmbientMode.Flat;

            var c = world.LightSetup.Ambient;
            var ambientIntensity = (c.r + c.g + c.b) / 3;
            ambientIntensity = 1 + (1 - world.LightSetup.Opacity);

            var ng = 1 - world.LightSetup.Opacity;
            var diff = world.LightSetup.Diffuse;

            var factor = 1 + (1 - world.LightSetup.Opacity);

            //Debug.Log(c + " : " + ambientIntensity + " : " + factor);

            c = new Color(c.r + diff.r * ng, c.g + diff.g * ng, c.b + diff.b * ng, c.a);

            RenderSettings.ambientLight = c;

            //alt rendering mode

            RenderSettings.ambientLight = Color.black;

            var map = GameObject.FindObjectsOfType<RoMapEditor>();
            //Debug.Log("AAAAAAAAAAAAAA" + map);

            foreach (var m in map)
            {
                if (m.gameObject.name.Contains("_walk"))
                    continue;

                var go = m.gameObject;
                var light = go.GetComponent<RoMapRenderSettings>();
                if (light == null)
                    light = go.AddComponent<RoMapRenderSettings>();

                light.AmbientColor = world.LightSetup.Ambient;
                light.Diffuse = world.LightSetup.Diffuse;
                light.Opacity = world.LightSetup.Opacity;
                light.UseMapAmbient = world.LightSetup.UseMapAmbient;

                //m.PaintEmptyTileColorsBlack = false;

                break;
            }



            //RenderSettings.ambientIntensity = (1 - world.LightSetup.Opacity) * 2;

            //Debug.Log("Light count: " + world.Lights.Count);

            //if (world.Lights.Count == 0)
            //    RenderSettings.ambientIntensity = 2f;
            //else
            //    RenderSettings.ambientIntensity = 0f;

            //Debug.Log("Ambient intensity: " + RenderSettings.ambientIntensity);

            var lights = GameObject.FindObjectsOfType<Light>();

            Light targetLight = null;

            foreach (var l in lights)
            {
                if (l.type == LightType.Directional)
                    targetLight = l;
            }

            if (targetLight == null)
            {
                var go = new GameObject("DirectionalLight");
                targetLight = go.AddComponent<Light>();
                targetLight.type = LightType.Directional;
                targetLight.shadows = LightShadows.Soft;
            }

            var intensity = 1;// world.LightSetup.Opacity;

            var rotation = Quaternion.Euler(90 - world.LightSetup.Longitude, world.LightSetup.Latitude, 0);

            if(world.MapName == "moc_pryd02" || world.MapName == "moc_pryd03")
                rotation = Quaternion.Euler(0, 0, 0);


            targetLight.transform.rotation = rotation; //Quaternion.Euler(90 - world.LightSetup.Longitude, world.LightSetup.Latitude, 0);
            targetLight.color = Color.white;
            targetLight.intensity = intensity;
            targetLight.shadowBias = 0.5f;
            targetLight.shadowNormalBias = 0f;
            targetLight.lightmapBakeType = LightmapBakeType.Realtime;

            targetLight.shadowStrength = 1;
        }

        private void PlaceLight(GameObject parent, Color color, float range, float intensity)
        {
            //var lobj = new GameObject(name);
            //lobj.isStatic = true;
            //lobj.transform.parent = parent.transform;
            //lobj.transform.localPosition = position;

            var lobj = parent;

            //color = color * 0.9f + new Color(0.1f, 0.1f, 0.1f, 0.1f);

            //color = Color.Lerp(color, Color.white, 0.2f);

            //color = color / 2;

            //var it = range * 5 / 255f;
            ////Debug.Log("LIGHT:"+ it);


            ////color += new Color(it, it, it, 0f);

            var max = Mathf.Max(color.r, color.g, color.b);

            if (max > maxLight)
            {
                maxLight = max;
                maxLightColor = color;
            }

            ////max /= 2;

            //var boostTo = 0.5f;

            //if (max > boostTo)
            //{
            //    color = new Color(color.r / max * boostTo, color.g / max * boostTo, color.b / max * boostTo, color.a);
            //}

            var i = Mathf.Max(3, (range + 1) / 3f);// 3 + Mathf.Pow(range/10f,2); //2 * (1/max);

            //range *= 0.8f;

            //range = range / 2 + range * max;

            //var i = intensity;
            
            var l = lobj.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = range;
            l.color = color;
            l.intensity = i;
            l.lightmapBakeType = LightmapBakeType.Baked;
            l.shadows = LightShadows.Soft;

            var sub = new GameObject("Sub Light");
            sub.transform.SetParent(lobj.transform);
            sub.transform.localPosition = Vector3.zero;
            //sub.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector | HideFlags.NotEditable;

            l = sub.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = range/2f;
            l.color = color;
            l.intensity = i;
            l.lightmapBakeType = LightmapBakeType.Baked;
            l.shadows = LightShadows.Soft;
        }

        private void LoadLights()
        {
            var lightContainer = new GameObject("lights");
            lightContainer.transform.SetParent(baseObject.transform, false);

            maxLight = -1f;

            foreach (var light in world.Lights)
            {
                var position = new Vector3(light.Position.x / 5, -light.Position.y / 5, light.Position.z / 5);

                var lightObj = new GameObject(light.Name);
                lightObj.transform.SetParent(lightContainer.transform, false);
                lightObj.transform.localPosition = position;
                lightObj.isStatic = true;

                //PlaceLight(lightObj, "Gen" + light.Name, Vector3.zero, light.Color, light.Range / 5, 7.5f);

                var r = light.Range / 5f;
                //var b = 1f;

                //var c = 0;

                PlaceLight(lightObj, light.Color, r, 5);

                //for (var i = r; i > 1; i -= r / 10f)
                //{
                //    if (Mathf.Approximately(i, r))
                //        PlaceLight(lightObj, light.Color, i, b);
                //    else
                //    {
                //        var go = new GameObject("Light " + i);
                //        go.isStatic = true;
                //        go.transform.SetParent(lightObj.transform);
                //        go.transform.localPosition = Vector3.zero;
                //        //go.hideFlags = HideFlags.HideInHierarchy;

                //        PlaceLight(go, light.Color, i, b);
                //    }

                //    c++;

                //    if (c > 2)
                //        b += 0.2f;
                //}

                //PlaceLight(lightContainer, light.Name + " normal1", Vector3.zero, light.Color, light.Range / 5f, 1f);
                //PlaceLight(lightContainer, light.Name + " normal2", position, light.Color, light.Range / 5f * 0.9f, 1f);
                //PlaceLight(lightContainer, light.Name + " normal3", position, light.Color, light.Range / 5f * 0.8f, 1f);
                //PlaceLight(lightContainer, light.Name + " normal4", position, light.Color, light.Range / 5f * 0.7f, 1f);
                //PlaceLight(lightContainer, light.Name + " bright1", position, light.Color, light.Range / 5f * 0.6f, 2f);
                //PlaceLight(lightContainer, light.Name + " bright2", position, light.Color, light.Range / 5f * 0.4f, 3f);
                //PlaceLight(lightContainer, light.Name + " bright3", position, light.Color, light.Range / 5f * 0.2f, baseIntensity + inc * 4);
                //PlaceLight(lightContainer, light.Name + " bright2", position, light.Color, light.Range / 5f / 4f, 4f);


                //PlaceLight(light.Name + " Red", position, Color.red, light.Range / 5f, light.Color.r * 5f);
                //PlaceLight(light.Name + " Blue", position, Color.blue, light.Range / 5f, light.Color.g * 5f);
                //PlaceLight(light.Name + " Green", position, Color.green, light.Range / 5f, light.Color.b * 5f);

            }

            Debug.Log($"Brightest scene color value is {maxLight} (color {maxLightColor})");
        }

        private void LoadFog()
        {
            if (world.FogSetup == null)
                return;

            if (Mathf.Approximately(world.FogSetup.FogColor.a, 0))
                return; //fog alpha is 0, so probably shouldn't be visible...

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = world.FogSetup.FogColor;
            RenderSettings.fogStartDistance = world.FogSetup.NearPlane * 55f;
            RenderSettings.fogEndDistance = world.FogSetup.FarPlane * 550f;
        }

        private void LoadWater()
        {
            if (world.Water == null)
                return;



            //var waterContainer = new GameObject("water");
            //waterContainer.transform.SetParent(baseObject.transform, false);

            //var mb = new MeshBuilder();

            //mb.AddVertex(new Vector3(-data.InitialSize.x, -world.Water.Level, data.InitialSize.y));
            //mb.AddVertex(new Vector3(data.InitialSize.x, -world.Water.Level, data.InitialSize.y));
            //mb.AddVertex(new Vector3(-data.InitialSize.x, -world.Water.Level, -data.InitialSize.y));
            //mb.AddVertex(new Vector3(data.InitialSize.x, -world.Water.Level, -data.InitialSize.y));

            //mb.AddNormal(Vector3.up);
            //mb.AddNormal(Vector3.up);
            //mb.AddNormal(Vector3.up);
            //mb.AddNormal(Vector3.up);

            //mb.AddUV(new Vector2(0, data.InitialSize.y/2f));
            //mb.AddUV(new Vector2(data.InitialSize.x/2f, data.InitialSize.y/2f));
            //mb.AddUV(new Vector2(0, 0));
            //mb.AddUV(new Vector2(data.InitialSize.x/2f, 0));

            //mb.AddTriangle(0);
            //mb.AddTriangle(1);
            //mb.AddTriangle(3);
            //mb.AddTriangle(0);
            //mb.AddTriangle(3);
            //mb.AddTriangle(2);

            //var mesh = mb.Build("Water");

            //var mf = waterContainer.AddComponent<MeshFilter>();
            //var mr = waterContainer.AddComponent<MeshRenderer>();

            //var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Textures/WaterTemp.mat");
            //mr.material = material;

            //mr.shadowCastingMode = ShadowCastingMode.Off;
            //mr.receiveShadows = false;
            //mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            //mr.lightProbeUsage = LightProbeUsage.Off;

            //mf.sharedMesh = mesh;
        }

        public void LoadEffectPlaceholders(RoMapData mapData, RagnarokWorld worldData)
        {
            data = mapData;
            world = worldData;

            var findObject = GameObject.Find($"{world.MapName} resources");
            if (findObject != null)
                baseObject = findObject;
            else
            {
                baseObject = new GameObject($"{world.MapName} resources");
                baseObject.transform.position = new Vector3(data.InitialSize.x, 0, data.InitialSize.y);
                baseObject.isStatic = true;
            }

            var effectContainer = new GameObject("effects");
            effectContainer.transform.SetParent(baseObject.transform, false);

            foreach (var effect in world.Effects)
            {
                var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                //var obj2 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Effects/Smoke/SmokeEmitter.prefab");
                //var obj = PrefabUtility.InstantiatePrefab(obj2) as GameObject;
                obj.name = effect.Id + " - " + effect.Name;
                obj.transform.SetParent(effectContainer.transform, false);
                obj.transform.localPosition = new Vector3(effect.Position.x / 5, -effect.Position.y / 5, effect.Position.z / 5);

            }
        }

        public void LoadEffects()
        {
            var effectContainer = new GameObject("effects");
            effectContainer.transform.SetParent(baseObject.transform, false);

            foreach (var effect in world.Effects)
            {
                if (effect.Id == 44) //chimney smoke
                {
                    var obj2 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Effects/Smoke/SmokeEmitter.prefab");
                    var obj = PrefabUtility.InstantiatePrefab(obj2) as GameObject;
                    obj.name = effect.Id + " - " + effect.Name;
                    obj.transform.SetParent(effectContainer.transform, false);
                    obj.transform.localPosition = new Vector3(effect.Position.x / 5, -effect.Position.y / 5, effect.Position.z / 5);
                }

                if (effect.Id == 45) //fireflies
                {
                    var obj2 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Resources/Fireflies.prefab");
                    var obj = PrefabUtility.InstantiatePrefab(obj2) as GameObject;
                    obj.name = effect.Id + " - " + effect.Name;
                    obj.transform.SetParent(effectContainer.transform, false);
                    obj.transform.localPosition = new Vector3(effect.Position.x / 5, -effect.Position.y / 5, effect.Position.z / 5);
                }

                if (effect.Id == 47 && world.MapName == "moc_pryd01")
                {
                    //torch
                    var light = world.Lights[0];
                    var position = new Vector3(effect.Position.x / 5, -effect.Position.y / 5, effect.Position.z / 5);
                    //PlaceLight(effectContainer, light.Name, position + new Vector3(0f, 4f, 0f), light.Color, light.Range / 5f, light.Range / 5f / 4f);
                }

                if (effect.Id == 109) //underwater bubbles
                {
                    var obj2 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Effects/Prefabs/bubble.prefab");
                    var obj = PrefabUtility.InstantiatePrefab(obj2) as GameObject;
                    obj.AddComponent<BillboardObject>();
                    obj.name = effect.Id + " - " + effect.Name;
                    obj.transform.SetParent(effectContainer.transform, false);
                    obj.transform.localPosition = new Vector3(effect.Position.x / 5, -effect.Position.y / 5, effect.Position.z / 5);
                    var renderer = obj.GetComponent<RoEffectRenderer>();
                    renderer.IsLoop = true;
                    renderer.UseZTest = true;
                    renderer.RandomStart = true;
                }
            }
        }

        public void LoadSounds()
        {
            var soundContainer = new GameObject("sounds");
            soundContainer.transform.SetParent(baseObject.transform, false);
            //soundContainer.transform.localPosition = new Vector3(data.InitialSize.x, 0f, data.InitialSize.y);

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Resources/AudioMixer.mixer");
            var envGroup = mixer.FindMatchingGroups("Environment")[0];

            var soundFolder = "Assets/sounds";
            if (!Directory.Exists(soundFolder))
                Directory.CreateDirectory(soundFolder);

            foreach (var sound in world.Sounds)
            {
                //var soundName = Path.GetFileNameWithoutExtension(sound.File);
                var soundAsset = AssetDatabase.LoadAssetAtPath<AudioClip>(Path.Combine(soundFolder, sound.File.Replace(".wav", ".ogg")));
                if (soundAsset == null)
                    Debug.LogWarning("Could not load audio file " + sound.File);

                var go = new GameObject(sound.Name);
                go.transform.parent = soundContainer.transform;
                go.transform.localPosition = new Vector3(sound.Position.x / 5, -sound.Position.y / 5, sound.Position.z / 5f);

                var ac = go.AddComponent<AudioSource>();
                ac.outputAudioMixerGroup = envGroup;
                ac.clip = soundAsset;
                ac.priority = 64;
                ac.minDistance = 3f;
                ac.maxDistance = sound.Range / 5f;
                ac.volume = Mathf.Clamp(sound.Volume, 0, 1);
                ac.rolloffMode = AudioRolloffMode.Linear;
                ac.spatialBlend = 1f;
                ac.loop = true;
                ac.dopplerLevel = 0f;

                var loop = go.AddComponent<AudioLooper>();
                loop.LoopTime = sound.Cycle;
            }
        }

        private void LoadModels()
        {
            var modelContainer = new GameObject("models");
            modelContainer.transform.SetParent(baseObject.transform, false);

            foreach (var model in world.Models)
            {
                var baseFolder = Path.Combine(RagnarokDirectory.GetRagnarokDataDirectory, "model");

                var modelPath = Path.Combine(baseFolder, model.FileName);
                var relative = DirectoryHelper.GetRelativeDirectory(baseFolder, Path.GetDirectoryName(modelPath));
                var baseName = Path.GetFileNameWithoutExtension(model.FileName);

                var prefabFolder = Path.Combine("Assets/models/prefabs/", relative).Replace("\\", "/");
                var prefabPath = Path.Combine(prefabFolder, baseName + ".prefab").Replace("\\", "/");

                if (!Directory.Exists(prefabFolder))
                    Directory.CreateDirectory(prefabFolder);


                GameObject obj;
                if (modelCache.ContainsKey(model.FileName))
                {
                    //obj = GameObject.Instantiate(modelCache[model.FileName]);
                    obj = PrefabUtility.InstantiatePrefab(modelCache[model.FileName], SceneManager.GetActiveScene()) as GameObject;
                }
                else
                {
                    if (File.Exists(prefabPath))
                    {
                        var prefabRef = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                        obj = PrefabUtility.InstantiatePrefab(prefabRef, SceneManager.GetActiveScene()) as GameObject;
                        modelCache.Add(model.FileName, prefabRef);
                    }
                    else
                    {
                        var modelLoader = new RagnarokModelLoader();

                        modelLoader.LoadModel(modelPath, relative);
                        obj = modelLoader.Compile();

                        PrefabUtility.SaveAsPrefabAssetAndConnect(obj, prefabPath, InteractionMode.AutomatedAction);

                        var prefabRef = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                        modelCache.Add(model.FileName, prefabRef);
                    }
                }

                obj.isStatic = true;
                obj.name = model.FileName;
                obj.transform.parent = modelContainer.transform;
                obj.transform.localPosition = new Vector3(model.Position.x / 5, -model.Position.y / 5, model.Position.z / 5) + new Vector3(0f, 0.01f, 0f);
                obj.transform.localScale = model.Scale; //model.Scale * 0.2f;

                var rz = Matrix4x4.Rotate(Quaternion.Euler(0, 0, -model.Rotation.z));
                var rx = Matrix4x4.Rotate(Quaternion.Euler(-model.Rotation.x, 0, 0));
                var ry = Matrix4x4.Rotate(Quaternion.Euler(0, model.Rotation.y, 0));

                var final = rz * rx * ry;

                var rotation = model.Rotation;

                obj.transform.rotation = final.rotation; //Quaternion.Euler(rotation);

                //obj.ChangeStaticRecursive(true);
            }
        }

        public void Load(RoMapData mapData, RagnarokWorld worldData)
        {
            data = mapData;
            world = worldData;

            modelCache = new Dictionary<string, GameObject>();

            var oldBox = GameObject.Find($"{world.MapName} resources");
            if (oldBox != null)
                GameObject.DestroyImmediate(oldBox);

            baseObject = new GameObject($"{world.MapName} resources");
            baseObject.transform.position = new Vector3(data.InitialSize.x, 0, data.InitialSize.y);
            baseObject.isStatic = true;

            SetWorldLighting();
            LoadLights();
            LoadModels();
            LoadEffects();
            LoadSounds();
            LoadWater();
            LoadFog();
        }
    }
}
