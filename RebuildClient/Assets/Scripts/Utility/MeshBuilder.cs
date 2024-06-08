using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Assets.Scripts.Utility
{

    public class MeshBuilder
    {
        private List<Vector3>  vertices = new();
        private List<Vector3> normals = new();
        private List<Vector2> uvs = new();
        private List<Vector3> uv3s = new();
        private List<int> triangles = new();
        private List<Color> colors = new();
        private List<Color32> color32s = new();

        private int startIndex = 0;
        private bool useUv3 = false;

        public bool HasData => vertices.Count > 0;
        public bool RequiresMultipleMeshes => vertices.Count > 65000;
        public void StartTriangle() => startIndex = vertices.Count;

        //public int VertexCount => vertices.Count;

        public void AddColor(Color c) => colors.Add(c);
        public void AddVertex(Vector3 v) => vertices.Add(v);
        public void AddNormal(Vector3 n) => normals.Add(n);
        public void AddUV(Vector2 v) => uvs.Add(v);
        public void AddTriangle(int i) => triangles.Add(i);
        public void ClearVertex() => vertices.Clear();

        public bool HasMesh() => triangles.Count > 0;

        public void Clear()
        {
            vertices.Clear();
            normals.Clear();
            uvs.Clear();
            triangles.Clear();
            colors.Clear();
            color32s.Clear();
            uv3s.Clear();
            useUv3 = false;
        }

        public void AddFullTriangle(Vector3[] vertArray, Vector3[] normalArray, Vector2[] uvArray, Color32[] colorArray, int[] triangleArray)
        {
            StartTriangle();
            AddVertices(vertArray);
            AddNormals(normalArray);
            AddUVs(uvArray);
            if(colors != null)
                AddColor32s(colorArray);

            AddTriangles(triangleArray);
        }
        
        public void AddQuad(Vector3[] vertArray, Vector3[] normalArray, Vector2[] uvArray, Color32[] colorArray)
        {
            var tri = vertices.Count;

#if DEBUG
            if(vertArray.Length != 4 || normalArray.Length != 4 || uvArray.Length != 4)
                throw new Exception("AddQuad was passed incorrect parameters! Oh no!");
#endif

            AddVertices(vertArray);
            AddNormals(normalArray);
            AddUVs(uvArray);
            AddColor32s(colorArray);
            triangles.Add(tri);
            triangles.Add(tri+1);
            triangles.Add(tri+2);
            triangles.Add(tri+1);
            triangles.Add(tri+3);
            triangles.Add(tri+2);
        }
        
        public void AddPerspectiveQuad(Vector3[] vertArray, Vector3[] normalArray, Vector3[] uvArray, Color32[] colorArray)
        {
            var tri = vertices.Count;

#if DEBUG
            if(vertArray.Length != 4 || normalArray.Length != 4 || uvArray.Length != 4)
                throw new Exception("AddPerspectiveQuad was passed incorrect parameters! Oh no!");
#endif

            AddVertices(vertArray);
            AddNormals(normalArray);
            AddUV3s(uvArray);
            AddColor32s(colorArray);
            triangles.Add(tri);
            triangles.Add(tri+1);
            triangles.Add(tri+2);
            triangles.Add(tri+1);
            triangles.Add(tri+3);
            triangles.Add(tri+2);
        }
        
        public void AddVertices(Vector3[] vertArray)
        {
            foreach(var v in vertArray)
                vertices.Add(v);
        }

        public void AddNormals(Vector3[] normalArray)
        {
            foreach(var n in normalArray)
                normals.Add(n);
        }

        public void AddUVs(Vector2[] uvArray)
        {
#if UNITY_EDITOR
            if (uv3s.Count > 0)
                throw new Exception("Cannot use UV2 and UV3 in the same mesh!");
#endif
            foreach(var uv in uvArray)
                uvs.Add(uv);
        }
        
        public void AddUV3s(Vector3[] uvArray)
        {
#if UNITY_EDITOR
            if (uvs.Count > 0)
                throw new Exception("Cannot use UV2 and UV3 in the same mesh!");
#endif
            foreach(var uv in uvArray)
                uv3s.Add(uv);

            useUv3 = true;
        }


        public void AddTriangles(int[] triArray)
        {
            foreach(var t in triArray)
                triangles.Add(startIndex + t);
        }

        public void AddColors(Color[] colorArray)
        {
            if (colorArray == null)
                return;
            foreach(var c in colorArray)
                color32s.Add(c);
        }
        
        
        public void AddColor32s(Color32[] colorArray)
        {
            if (colorArray == null)
                return;
            foreach(var c in colorArray)
                color32s.Add(c);
        }

        
        public Mesh Build(string name = "Mesh", bool buildSecondaryUVs = false)
        {
            if (!HasMesh())
                return new Mesh();

            var mesh = new Mesh();
            mesh.name = name;
            
            if(vertices.Count != color32s.Count && colors.Count == 0)
                Console.WriteLine("AAAAA");

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            if(!useUv3)
                mesh.SetUVs(0, uvs);
            else
                mesh.SetUVs(0, uv3s);
            if(colors.Count > 0)
                mesh.SetColors(colors);
            if(color32s.Count > 0)
                mesh.SetColors(color32s);

            //mesh.vertices = vertices.ToArray();
            //mesh.normals = normals.ToArray();
            //mesh.uv = uvs.ToArray();
            //mesh.triangles = triangles.ToArray();
            //mesh.colors = colors.ToArray();

            mesh.RecalculateBounds();
            //mesh.RecalculateTangents();
            mesh.Optimize();
            mesh.OptimizeIndexBuffers();
            mesh.OptimizeReorderVertexBuffer();
#if UNITY_EDITOR
            if(buildSecondaryUVs)
                Unwrapping.GenerateSecondaryUVSet(mesh, MeshBuilder.GetUnwrapParam());
#endif
            return mesh;
        }

        public Mesh[] BuildMulti(string name = "Mesh", bool buildSecondaryUVs = false)
        {
            if (!HasMesh())
                return new Mesh[] {};

            var meshCount = Mathf.CeilToInt(vertices.Count / 40000f);
            var meshes = new Mesh[meshCount];
            for (var i = 0; i < meshCount; i++)
            {
                var mesh = new Mesh();
                mesh.name = name;

                var start = i * 40000;
                var count = Mathf.Clamp(vertices.Count - start, 0, 40000);
                var triCount = count / 4 * 6;
                Debug.Log($"{start} {count} {triCount}");
            
                if(vertices.Count != color32s.Count && colors.Count == 0)
                    Console.WriteLine("AAAAA");

                mesh.SetVertices(vertices.GetRange(start, count));
                mesh.SetNormals(normals.GetRange(start, count));
                mesh.SetTriangles(triangles.GetRange(0, triCount), 0);
                if(!useUv3)
                    mesh.SetUVs(0, uvs.GetRange(start, count));
                else
                    mesh.SetUVs(0, uv3s.GetRange(start, count));
                if(colors.Count > 0)
                    mesh.SetColors(colors.GetRange(start, count));
                if(color32s.Count > 0)
                    mesh.SetColors(color32s.GetRange(start, count));

                mesh.RecalculateBounds();
                mesh.Optimize();
                mesh.OptimizeIndexBuffers();
                mesh.OptimizeReorderVertexBuffer();
                meshes[i] = mesh;
            }

            return meshes;
        }

        public Mesh ApplyToMesh(Mesh mesh, bool buildSecondaryUVs = false)
        {
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            if(!useUv3)
                mesh.SetUVs(0, uvs);
            else
                mesh.SetUVs(0, uv3s);
            if(colors.Count > 0)
                mesh.SetColors(colors);
            if(color32s.Count > 0)
                mesh.SetColors(color32s);
            
            mesh.RecalculateBounds();
            //mesh.RecalculateTangents();
            //mesh.Optimize();
            //mesh.OptimizeIndexBuffers();
            //mesh.OptimizeReorderVertexBuffer();

#if UNITY_EDITOR
            if (buildSecondaryUVs)
                Unwrapping.GenerateSecondaryUVSet(mesh);
#endif

            return mesh;
        }

#if UNITY_EDITOR
        public static UnwrapParam GetUnwrapParam(float hardAngle = 88f, float packMargin = 20f, float angleError = 8f, float areaError = 15f)
        {
            return new UnwrapParam()
            {
                angleError = Mathf.Clamp(angleError, 1f, 75f) * .01f,
                areaError = Mathf.Clamp(areaError, 1f, 75f) * .01f,
                hardAngle = Mathf.Clamp(hardAngle, 0f, 180f),
                packMargin = Mathf.Clamp(packMargin, 1f, 64) * .001f
            };
        }
#endif

        public MeshBuilder()
        {

        }
    }
}
