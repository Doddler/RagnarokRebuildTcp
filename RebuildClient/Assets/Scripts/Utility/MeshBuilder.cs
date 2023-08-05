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
        private List<Vector3>  vertices = new List<Vector3>();
        private List<Vector3> normals = new List<Vector3>();
        private List<Vector2> uvs = new List<Vector2>();
        private List<int> triangles = new List<int>();
        private List<Color> colors = new List<Color>();

        private int startIndex = 0;

        public bool HasData => vertices.Count > 0;
        public void StartTriangle() => startIndex = vertices.Count;

        //public int VertexCount => vertices.Count;

        public void AddColor(Color c) => colors.Add(c);
        public void AddVertex(Vector3 v) => vertices.Add(v);
        public void AddNormal(Vector3 n) => normals.Add(n);
        public void AddUV(Vector2 v) => uvs.Add(v);
        public void AddTriangle(int i) => triangles.Add(i);

        public bool HasMesh() => triangles.Count > 0;

        public void Clear()
        {
            vertices.Clear();
            normals.Clear();
            uvs.Clear();
            triangles.Clear();
            colors.Clear();
        }

        public void AddFullTriangle(Vector3[] vertArray, Vector3[] normalArray, Vector2[] uvArray, Color[] colorArray, int[] triangleArray)
        {
            StartTriangle();
            AddVertices(vertArray);
            AddNormals(normalArray);
            AddUVs(uvArray);
            if(colors != null)
                AddColors(colorArray);

            AddTriangles(triangleArray);
        }

        public void AddQuad(Vector3[] vertArray, Vector3[] normalArray, Vector2[] uvArray, Color[] colorArray)
        {
            var tri = vertices.Count;

#if DEBUG
            if(vertArray.Length != 4 || normalArray.Length != 4 || uvArray.Length != 4)
                throw new Exception("AddQuad was passed incorrect parameters! Oh no!");
#endif

            AddVertices(vertArray);
            AddNormals(normalArray);
            AddUVs(uvArray);
            AddColors(colorArray);
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
            foreach(var uv in uvArray)
                uvs.Add(uv);
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
                colors.Add(c);
        }
        
        public Mesh Build(string name = "Mesh", bool buildSecondaryUVs = false)
        {
            if (!HasMesh())
                return new Mesh();

            var mesh = new Mesh();
            mesh.name = name;

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);

            //mesh.vertices = vertices.ToArray();
            //mesh.normals = normals.ToArray();
            //mesh.uv = uvs.ToArray();
            //mesh.triangles = triangles.ToArray();
            //mesh.colors = colors.ToArray();

            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            mesh.Optimize();
            mesh.OptimizeIndexBuffers();
            mesh.OptimizeReorderVertexBuffer();
#if UNITY_EDITOR
            if(buildSecondaryUVs)
                Unwrapping.GenerateSecondaryUVSet(mesh, MeshBuilder.GetUnwrapParam());
#endif
            return mesh;
        }

        public Mesh ApplyToMesh(Mesh mesh, bool buildSecondaryUVs = false)
        {
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            
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
