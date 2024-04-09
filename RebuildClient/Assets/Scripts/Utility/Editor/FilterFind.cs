using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Utility.Editor
{
    public class FilterFind : EditorWindow
    {
        [MenuItem("Tools/Select Non-Static Meshes")]
        private static void DoSelect()
        {
            var selectList = new List<GameObject>();
            
            foreach (var obj in FindObjectsOfType<MeshRenderer>())
            {
                if(!obj.gameObject.isStatic && obj.lightProbeUsage == LightProbeUsage.BlendProbes)
                    selectList.Add(obj.gameObject);
            }

            Selection.objects = selectList.ToArray();
        }
    }
}