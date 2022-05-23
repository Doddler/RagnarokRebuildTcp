using System.Collections.Generic;

using UnityEngine;
using UnityEditor;
using UnityEngine.ProBuilder;

[CustomEditor(typeof(MeshRenderer)), CanEditMultipleObjects]
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

        var mr = (MeshRenderer)target;

        var mf = mr.GetComponent<MeshFilter>();
        if (mf == null)
            return;

        var mesh = mf.sharedMesh;
        if (mesh == null)
            return;
        
        for (int i = 0; i < mesh.vertexCount; i++)
        {
            lineList.Add(mesh.vertices[i]);
            lineList.Add(mesh.vertices[i] + mesh.normals[i]*5);
            //Handles.DrawLine(
            //    mesh.vertices[i],
            //    mesh.vertices[i] + mesh.normals[i]);
        }

        if (lineList.Count > 0)
        {
            Handles.matrix = mf.transform.localToWorldMatrix;
            Handles.color = Color.yellow;
            Handles.DrawLines(lineList.ToArray());
        }
    }
}
