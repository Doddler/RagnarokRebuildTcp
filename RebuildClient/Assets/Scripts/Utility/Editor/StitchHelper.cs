using Assets.Scripts.MapEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Utility.Editor
{
    public class StitchHelper
    {
        [MenuItem("GameObject/Copy For Stitch", true)]
        public static bool ValidateCopyMapForStitch()
        {
            if (Selection.objects == null || Selection.gameObjects.Length < 2)
                return false;

            foreach (var obj in Selection.gameObjects)
            {
                if (obj.GetComponent<RoMapEditor>())
                    return true;
            }
            
            return false;
        }
        
        [MenuItem("GameObject/Copy For Stitch", false, 0)]
        private static void CopyMapForStitch(MenuCommand menuCommand)
        {
            if (!ValidateCopyMapForStitch())
                return;

            var mapName = Selection.gameObjects[0].scene.name;
            if (menuCommand.context != Selection.gameObjects[0])
                return;

            Debug.Log("Doing crazy things");
            
            Undo.IncrementCurrentGroup();
            var container = new GameObject(mapName);
            Undo.RegisterCreatedObjectUndo(container, "Stitch Container");
            SceneManager.MoveGameObjectToScene(container, SceneManager.GetActiveScene());
            
            foreach (var obj in Selection.gameObjects)
            {
                var copy = GameObject.Instantiate(obj);
                copy.transform.SetParent(container.transform, false);

                var mapEditor = copy.GetComponent<RoMapEditor>();
                if (mapEditor != null)
                    GameObject.DestroyImmediate(mapEditor);

                var waterAnimator = copy.GetComponent<RoWaterAnimator>();
                if(waterAnimator != null)
                    GameObject.DestroyImmediate(waterAnimator);
                
                var renderSettings = copy.GetComponent<RoMapRenderSettings>();
                if(renderSettings != null)
                    GameObject.DestroyImmediate(renderSettings);
            }
            
            Undo.RegisterFullObjectHierarchyUndo(container, "Move stitch container");
        }
    }
}