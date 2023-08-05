using Assets.Scripts.Sprites;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.MapEditor.Editor
{
    [CustomEditor(typeof(BillboardObject))]
    class BillboardEditor : UnityEditor.Editor
    {
        //public override void OnInspectorGUI()
        //{
        //    GUILayout.Label("Use billboard: " + Billboard.UseOldStyle);
        //    if (GUILayout.Button("Change Style"))
        //        Billboard.UseOldStyle = !Billboard.UseOldStyle;


        //}
    }
}
