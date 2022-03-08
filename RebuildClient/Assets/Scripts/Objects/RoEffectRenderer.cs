using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Scripts.Objects
{



	public class RoEffectRenderer : MonoBehaviour
    {
        public AudioSource AudioSource;
        public StrAnimationFile Anim;
        private bool isInit;

        private List<GameObject> layerObjects = new List<GameObject>();
        private List<MeshRenderer> layerRenderers;
        private List<MeshFilter> layerFilters;
        private List<Mesh> layerMeshes;

        private static Vector3[] tempPositions = new Vector3[4];
        private static Vector2[] tempPositions2 = new Vector2[4];
        private static Vector2[] tempUvs = new Vector2[4];
        private static Vector2[] tempUvs2 = new Vector2[4];
        private static Vector3[] tempNormals = new Vector3[4];
        private static int[] tempTris = new int[6];
        private float[] angles;

        private Dictionary<string, Material> materials = new Dictionary<string, Material>(8);

        private float time;
        private int frame;

        private bool hasAudio;

        private Material GetEffectMaterial(int layer, int srcBlend, int destBlend)
        {
            var hash = $"{Anim.name}-{layer}-{srcBlend.ToString()}-{destBlend.ToString()}";
            if (materials.TryGetValue(hash, out var val))
                return val;

            var mat = new Material(Shader.Find("Ragnarok/EffectShader"));
            mat.SetFloat("_SrcBlend", (float)BlendMode.One);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0);
            mat.SetFloat("_Cull", 0);

            mat.SetTexture("_MainTex", Anim.Atlas);
            mat.SetColor("_Color", Color.white);

            return mat;
        }

        public void Initialize(StrAnimationFile animation)
        {
            Anim = animation;

            layerObjects = new List<GameObject>(Anim.LayerCount);
            layerRenderers = new List<MeshRenderer>(Anim.LayerCount);
            layerMeshes = new List<Mesh>(Anim.LayerCount);
            layerFilters = new List<MeshFilter>(Anim.LayerCount);
            angles = new float[Anim.LayerCount];

            for (var i = 0; i < Anim.LayerCount; i++)
            {
                var go = new GameObject("Layer " + i);
                var mr = go.AddComponent<MeshRenderer>();
                var mf = go.AddComponent<MeshFilter>();
                go.transform.SetParent(transform, false);

                var mesh = new Mesh();

                layerObjects.Add(go);
                layerRenderers.Add(mr);
                layerFilters.Add(mf);
                layerMeshes.Add(mesh);
                angles[i] = -1;
            }

            time = 0;
            frame = -1;
            isInit = true;
        }

        // Use this for initialization
        private void Awake()
        {
            if(Anim != null)
                Initialize(Anim);

            AudioSource = GetComponent<AudioSource>();
            if (AudioSource.clip != null)
                hasAudio = true;
        }

        private void UpdateMesh(MeshFilter mf, Mesh mesh, Vector2[] pos, Vector2[] uvs, float angle, int imageId)
        {
            if(pos.Length > 4 || uvs.Length > 4)
                Debug.LogError("WHOA! Animation " + Anim.name + " has more than 4 verticies!");

            var bounds = Anim.AtlasRects[imageId];

            for (var i = 0; i < 4; i++)
            {
                var p = VectorHelper.Rotate(pos[i], -angle * Mathf.Deg2Rad);
                tempPositions[i] = new Vector3(p.x, p.y, 0) / 35f;
                
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
            go.transform.localPosition = new Vector3((pos.x - 320f)/35f, -(pos.y-320f)/35f, 0);
            //if (!Mathf.Approximately(angle, angles[layerNum]))
            //{
            //    go.transform.rotation = Quaternion.Euler(0, 0, -angle);
            //    angles[layerNum] = angle;
            //}

            go.transform.localScale = new Vector3(1f, 1f, 1f);
            mat.SetColor("_Color", color);
        }

        private bool UpdateAnimationLayer(int layerNum)
        {
            var layer = Anim.Layers[layerNum];

            var lastFrame = 0;
            var lastSource = 0;
            var startAnim = -1;
            var nextAnim = -1;

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
                    lastSource = Mathf.Max(lastSource, a.Frame);
            }

            if (startAnim < 0 || (nextAnim < 0 && lastFrame < frame))
                return false;

            var from = layer.Animations[startAnim];
            StrAnimationEntry to = null;

            if(nextAnim >= 0)
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

                var fixedFrame = layer.Textures[(int) from.Aniframe];
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
                return;

            time += Time.deltaTime;
            var newFrame = Mathf.FloorToInt(time * Anim.FrameRate);
            if (newFrame == frame)
                return;

            //Debug.Log(frame);

            frame = newFrame;

            if (frame > Anim.MaxKey)
            {
                if (hasAudio && AudioSource.isPlaying)
                {
                    for (var i = 0; i < layerObjects.Count; i++)
                        layerObjects[i].SetActive(false);

                    return;
                }

                Destroy(gameObject);
                return;
            }

            UpdateAnimationFrame();
        }

        void OnDestroy()
        {
            if (!isInit)
                return;
            foreach (var m in layerMeshes)
                Destroy(m);
        }
    }
}
