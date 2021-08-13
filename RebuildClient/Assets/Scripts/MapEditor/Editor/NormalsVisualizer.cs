using System.Collections.Generic;

using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MeshFilter)), CanEditMultipleObjects]
public class NormalsVisualizer : Editor
{
    private static bool showNormals;

    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("Show Normals"))
        {
            showNormals = !showNormals;
            Repaint();
        }

        base.OnInspectorGUI();
    }

    void OnSceneGUI()
    {
        if (Event.current.type != EventType.Repaint || !showNormals)
            return;

        var lineList = new List<Vector3>();

        var mf = (MeshFilter)target;
        if (mf == null)
            return;

        var mesh = mf.sharedMesh;
        if (mesh == null)
            return;
        
        for (int i = 0; i < mesh.vertexCount; i++)
        {
            lineList.Add(mesh.vertices[i]);
            lineList.Add(mesh.vertices[i] + mesh.normals[i]);
            //Handles.DrawLine(
            //    mesh.vertices[i],
            //    mesh.vertices[i] + mesh.normals[i]);
        }

        if (lineList.Count > 0)
        {
            Handles.matrix = (target as MeshFilter).transform.localToWorldMatrix;
            Handles.color = Color.yellow;
            Handles.DrawLines(lineList.ToArray());
        }
    }
}
