using UnityEditor;
using UnityEngine;

namespace Utility.Editor
{
    public static class JigglePosition
    {
        [MenuItem("GameObject/Jiggle/XYZ", false, 0)]
        public static void JiggleXYZ(MenuCommand menuCommand) => JiggleTransformPosition(menuCommand.context as GameObject, true, true, true, true, true, true);
        
        [MenuItem("GameObject/Jiggle/XY(+)Z", false, 0)]
        public static void JiggleXYPlusZ(MenuCommand menuCommand) => JiggleTransformPosition(menuCommand.context as GameObject,true, true, false, true, true, true);

        [MenuItem("GameObject/Jiggle/XZ", false, 0)]
        public static void JiggleXZ(MenuCommand menuCommand) => JiggleTransformPosition(menuCommand.context as GameObject,true, true, false, false, true, true);

        
        [MenuItem("GameObject/Jiggle/Y", false, 0)]
        public static void JiggleYPlus(MenuCommand menuCommand) => JiggleTransformPosition(menuCommand.context as GameObject,false, false, true, true, false, false);

        
        private static void JiggleTransformPosition(GameObject go, bool useXMin, bool useXMax, bool useYMin, bool useYMax, bool useZMin, bool useZMax)
        {
                var x = 0f;
                var y = 0f;
                var z = 0f;
                var dist = 0.008f;
                if (useXMin || useXMax) x = Random.Range(useXMin ? -1f : 0f, useXMax ? 1 : 0f) * dist;
                if (useYMin || useYMax) y = Random.Range(useYMin ? -1f : 0f, useYMax ? 1 : 0f) * dist;
                if (useZMin || useZMax) z = Random.Range(useZMin ? -1f : 0f, useZMax ? 1 : 0f) * dist;
                Undo.RecordObject(go.transform, "Jiggle position");
                go.transform.localPosition += new Vector3(x, y, z);
            
        }
        
    }
}