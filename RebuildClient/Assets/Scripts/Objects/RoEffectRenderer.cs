using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Assets.Scripts.Objects
{
    public class RoEffectRenderer : MonoBehaviour
    {
        public EffectAudioSource AudioSource;
        public StrAnimationFile Anim;

        public bool IsLoop;
        public bool UseZTest;
        public bool RandomStart;
        public float LoopDelay;

        private bool isInit;

        private CullingGroup cullingGroup;
        private BoundingSphere boundingSphere;
        private BoundingSphere[] boundingSpheres;

        private List<GameObject> layerObjects = new List<GameObject>();
        private List<MeshRenderer> layerRenderers;
        private List<MeshFilter> layerFilters;
        private List<Mesh> layerMeshes;
        private List<MaterialPropertyBlock> propBlocks;

        private static Vector3[] tempPositions = new Vector3[4];
        private static Vector2[] tempPositions2 = new Vector2[4];
        private static Vector2[] tempUvs = new Vector2[4];
        private static Vector2[] tempUvs2 = new Vector2[4];
        private static Vector3[] tempNormals = new Vector3[4];
        private static int[] tempTris = new int[6];
        private float[] angles;

        private static Dictionary<string, Material> materials = new Dictionary<string, Material>(8);

        private float time;
        private int frame;
        private float waitTime;

        // private bool hasAudio;
        private bool hasDisabledChildren;
        private bool hasLooped;

        private Material GetEffectMaterial(int layer, int srcBlend, int destBlend)
        {
            var hash = $"{Anim.name}-{layer}-{srcBlend.ToString()}-{destBlend.ToString()}";
            if (materials.TryGetValue(hash, out var val))
                return val;

            var mat = new Material(Shader.Find("Ragnarok/EffectShader"));

            if (srcBlend == 2 && destBlend == 1)
            {
                srcBlend = 5;
                destBlend = 10;
            }
            
            if (srcBlend == 5 && destBlend == 6)
                destBlend = 10;

            if (srcBlend == 5 && destBlend == 7)
            {
                srcBlend = 1;
                destBlend = 1;
                mat.EnableKeyword("MULTIPLY_ALPHA");
            }
            else
            {
                //Debug.Log("MultiplyAlpha ON");
                mat.DisableKeyword("MULTIPLY_ALPHA");
            }


            mat.SetFloat("_SrcBlend", (float)srcBlend);
            mat.SetFloat("_DstBlend", (float)destBlend);
            mat.SetFloat("_ZWrite", 0);
            mat.SetFloat("_ZTest", UseZTest ? 1f : 0f);

            mat.SetFloat("_Cull", 0);

            if (UseZTest)
                mat.SetInt("_myCustomCompare", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
            else
                mat.SetInt("_myCustomCompare", (int)UnityEngine.Rendering.CompareFunction.Always);

            mat.SetTexture("_MainTex", Anim.Atlas);
            mat.SetColor("_Color", Color.white);

            materials.Add(hash, mat);

            return mat;
        }

        public void Initialize(StrAnimationFile animation)
        {
            Anim = animation;

            layerObjects = new List<GameObject>(Anim.LayerCount);
            layerRenderers = new List<MeshRenderer>(Anim.LayerCount);
            layerMeshes = new List<Mesh>(Anim.LayerCount);
            layerFilters = new List<MeshFilter>(Anim.LayerCount);
            propBlocks = new List<MaterialPropertyBlock>(Anim.LayerCount);
            angles = new float[Anim.LayerCount];

            for (var i = 0; i < Anim.LayerCount; i++)
            {
                var go = new GameObject("Layer " + i);
                var mr = go.AddComponent<MeshRenderer>();
                var mf = go.AddComponent<MeshFilter>();
                var block = new MaterialPropertyBlock();

                go.transform.SetParent(transform, false);

                var mesh = new Mesh();

                layerObjects.Add(go);
                layerRenderers.Add(mr);
                layerFilters.Add(mf);
                layerMeshes.Add(mesh);
                propBlocks.Add(block);
                angles[i] = -1;
            }

            time = 1f / Anim.FrameRate;
            frame = 0;
            isInit = true;

            if (RandomStart)
                time = Random.Range(0, (float)Anim.MaxKey / (float)Anim.FrameRate + LoopDelay);

            // Debug.Log($"Start Time {time}");

            if (IsLoop)
            {
                cullingGroup = new CullingGroup();
                cullingGroup.targetCamera = Camera.main;
                boundingSpheres = new BoundingSphere[1];
                boundingSphere = new BoundingSphere(transform.position, 5f);
                boundingSpheres[0] = boundingSphere;
                cullingGroup.SetBoundingSpheres(boundingSpheres);
                cullingGroup.SetBoundingSphereCount(1);
            }

            AudioSource = GetComponent<EffectAudioSource>();
            if (AudioSource != null)
            {
                // AudioSource.priority = 60;
                // AudioSource.dopplerLevel = 0;
                // if (AudioSource != null && AudioSource.clip != null)
                AudioSource.Play();
                // hasAudio = true;
            }
        }

        // Use this for initialization
        private void Awake()
        {
            // if (Anim != null)
            //     Initialize(Anim);
        }

        private void UpdateMesh(MeshFilter mf, Mesh mesh, Vector2[] pos, Vector2[] uvs, float angle, int imageId)
        {
            if (pos.Length > 4 || uvs.Length > 4)
                Debug.LogError("WHOA! Animation " + Anim.name + " has more than 4 verticies!");

            var bounds = Anim.AtlasRects[imageId];

            for (var i = 0; i < 4; i++)
            {
                var p = VectorHelper.Rotate(pos[i], -angle * Mathf.Deg2Rad);
                tempPositions[i] = new Vector3(p.x, p.y, 0) / 50f;

                //Debug.Log(tempPositions[i]);

                var uvx = uvs[i].x; //.Remap(0, 1, bounds.xMin, bounds.xMax);
                var uvy = uvs[i].y; //.Remap(0, 1, bounds.yMin, bounds.yMax);

                tempUvs[i] = new Vector2(uvx, uvy);
                tempNormals[i] = Vector3.back;
            }

            tempUvs[2] = new Vector2(bounds.xMin, bounds.yMin);
            tempUvs[3] = new Vector2(bounds.xMax, bounds.yMin);
            tempUvs[0] = new Vector2(bounds.xMin, bounds.yMax);
            tempUvs[1] = new Vector2(bounds.xMax, bounds.yMax);

            tempTris[0] = 0;
            tempTris[1] = 1;
            tempTris[2] = 2;
            tempTris[3] = 1;
            tempTris[4] = 3;
            tempTris[5] = 2;
            //tempTris[3] = 1;
            //tempTris[4] = 3;
            //tempTris[5] = 2;

            mesh.vertices = tempPositions;
            mesh.uv = tempUvs;
            mesh.normals = tempNormals;
            mesh.triangles = tempTris;

            mf.sharedMesh = mesh;
        }

        private void UpdateLayerData(GameObject go, Material mat, Vector2 pos, Color color, int layerNum)
        {
            go.transform.localPosition = new Vector3((pos.x - 320f) / 50f, -(pos.y - 320f) / 50f);
            //if (!Mathf.Approximately(angle, angles[layerNum]))
            //{
            //    go.transform.rotation = Quaternion.Euler(0, 0, -angle);
            //    angles[layerNum] = angle;
            //}
            go.transform.localScale = new Vector3(1f, 1f, 1f);

            var renderer = layerRenderers[layerNum];

            renderer.GetPropertyBlock(propBlocks[layerNum]);
            propBlocks[layerNum].SetColor("_Color", color);
            renderer.SetPropertyBlock(propBlocks[layerNum]);

            //mat.SetColor("_Color", color);
            layerRenderers[layerNum].sortingOrder = layerNum;
        }

        private void RenderLayerFixedFrame(int layerNum, int animationKey)
        {
            var layer = Anim.Layers[layerNum];
            var from = layer.Animations[animationKey];
            var fixedFrame = layer.Textures[(int)from.Aniframe];
            
            var blendSrc = (int)from.SrcAlpha;
            var blendDest = (int)from.DstAlpha;
            var mat = GetEffectMaterial(layerNum, blendSrc, blendDest);
            var go = layerObjects[layerNum];
            var mr = layerRenderers[layerNum];
            var mf = layerFilters[layerNum];
            var mesh = layerMeshes[layerNum];
            mr.material = mat;
            
            UpdateMesh(mf, mesh, from.XY, from.UVs, from.Angle, fixedFrame);
            UpdateLayerData(go, mat, from.Position, from.Color, layerNum);
        }

        private bool UpdateAnimationLayer(int layerNum)
        {
            var layer = Anim.Layers[layerNum];

            var lastFrame = 0;
            var lastSource = 0;
            var startAnim = -1;
            var nextAnim = -1;

            var lastAnim = -1;
            
            for (var i = 0; i < layer.AnimationCount; i++)
            {
                var a = layer.Animations[i];
                if (a.Frame < frame)
                {
                    if (a.Type == 0)
                        startAnim = i;
                    if (a.Type == 1)
                        nextAnim = i;
                }

                lastFrame = Mathf.Max(lastFrame, a.Frame);
                if (a.Type == 0)
                {
                    lastSource = Mathf.Max(lastSource, a.Frame);
                    lastAnim = i;
                }

            }

            if (startAnim < 0 || (nextAnim < 0 && lastFrame < frame))
            {
                if (hasLooped && lastAnim != 0 && lastFrame == lastSource)
                {
                    RenderLayerFixedFrame(layerNum, lastAnim);
                    return true;
                }
                return false;
            }

            var from = layer.Animations[startAnim];
            StrAnimationEntry to = null;

            if (nextAnim >= 0)
                to = layer.Animations[nextAnim];
            var delta = frame - from.Frame;
            var blendSrc = (int)from.SrcAlpha;
            var blendDest = (int)from.DstAlpha;

            var mat = GetEffectMaterial(layerNum, blendSrc, blendDest);
            var go = layerObjects[layerNum];
            var mr = layerRenderers[layerNum];
            var mf = layerFilters[layerNum];
            var mesh = layerMeshes[layerNum];
            mr.material = mat;

            if (nextAnim != startAnim + 1 || to?.Frame != from.Frame)
            {
                if (to != null && lastSource <= from.Frame)
                    return false;

                var fixedFrame = layer.Textures[(int)from.Aniframe];
                UpdateMesh(mf, mesh, from.XY, from.UVs, from.Angle, fixedFrame);
                UpdateLayerData(go, mat, from.Position, from.Color, layerNum);
                return true;
            }

            var prog = Mathf.InverseLerp(from.Frame, to.Frame, frame);

            for (var i = 0; i < 4; i++)
            {
                tempPositions2[i] = from.XY[i] + to.XY[i] * delta;
                tempUvs2[i] = from.UVs[i] + to.UVs[i] * delta;
            }

            //Debug.Log("from: " + from.Position + " to: " + to.Position + " delta:" + delta);
            var pos = from.Position + to.Position * delta;
            var angle = from.Angle + to.Angle * delta;
            var color = from.Color + to.Color * delta;
            //color.a *= 0.5f;
            //Debug.Log(color);

            var frameId = 0;

            switch (to.Anitype)
            {
                case 1:
                    frameId = Mathf.FloorToInt(from.Aniframe + to.Aniframe * delta);
                    break;
                case 2:
                    frameId = Mathf.FloorToInt(Mathf.Min(from.Aniframe + to.Delay * delta, layer.TextureCount - 1));
                    break;
                case 3:
                    frameId = Mathf.FloorToInt((from.Aniframe + to.Delay * delta) % layer.TextureCount);
                    break;
                case 4:
                    frameId = Mathf.FloorToInt((from.Aniframe - to.Delay * delta) % layer.TextureCount);
                    break;
            }

            var texIndex = layer.Textures[frameId];

            UpdateMesh(mf, mesh, tempPositions2, tempUvs2, angle, texIndex);
            UpdateLayerData(go, mat, pos, color, layerNum);

            return true;
        }

        private void UpdateAnimationFrame()
        {
            for (var i = 0; i < Anim.LayerCount; i++)
            {
                if (Anim.Layers[i].AnimationCount == 0)
                    continue;

                var res = UpdateAnimationLayer(i);
                layerObjects[i].SetActive(res);
            }
        }

        // Update is called once per frame
        private void Update()
        {
            if (!isInit)
                Initialize(Anim);


            if (IsLoop)
            {
                //if (CameraFollower.Instance.Target != null && (CameraFollower.Instance.Target.transform.position - transform.position).magnitude > 30)
                if (!cullingGroup.IsVisible(0))
                    return;

                if (frame > Anim.MaxKey)
                    waitTime += Time.deltaTime;
            }

            time += Time.deltaTime;
            var newFrame = Mathf.FloorToInt(time * Anim.FrameRate);
            if (newFrame == frame)
                return;

            //Debug.Log($"frame: {frame} waittime: {waitTime} time: {time}");

            frame = newFrame;

            if (frame > Anim.MaxKey)
            {
                if (IsLoop && waitTime < LoopDelay)
                {
                    if (hasDisabledChildren) return;

                    for (var i = 0; i < layerObjects.Count; i++)
                        layerObjects[i].SetActive(false);
                    hasDisabledChildren = true;

                    return;
                }

                if (IsLoop)
                {
                    //time -= (float)Anim.MaxKey / (float)Anim.FrameRate + LoopDelay;
                    time = 1f / Anim.FrameRate;
                    frame = Mathf.FloorToInt(time * Anim.FrameRate);
                    hasLooped = true;
                    waitTime = 0f;
                    hasDisabledChildren = false;
                }
                else
                {
                    // if (hasAudio && AudioSource.isPlaying)
                    // {
                    //     for (var i = 0; i < layerObjects.Count; i++)
                    //         layerObjects[i].SetActive(false);
                    //
                    //     return;
                    // }

                    //Debug.Log($"Animation {name} finishing.");

                    Destroy(gameObject);
                    return;
                }
            }

            UpdateAnimationFrame();
        }

        void OnDestroy()
        {
            if (!isInit)
                return;
            foreach (var m in layerMeshes)
                Destroy(m);
            if (cullingGroup != null)
                cullingGroup.Dispose();
        }

        public void OnDrawGizmos()
        {
            Gizmos.DrawIcon(transform.position, "effect.png", true);
        }

        // public void OnDrawGizmosSelected()
        // {
        //     if(isInit)
        //         Gizmos.DrawSphere(boundingSphere.position, boundingSphere.radius);
        // }
    }
}