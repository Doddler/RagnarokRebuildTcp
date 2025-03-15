using Assets.Scripts.Effects;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Editor
{
    [CustomEditor(typeof(RagnarokEffectPool))]
    public class RagnarokEffectPoolInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement myInspector = new VisualElement();

            if (Application.isPlaying)
            {
                myInspector.Add(new Label($"3D Effect Count: {RagnarokEffectPool.DebugGet3DEffectPoolCount()}"));
                myInspector.Add(new Label($"Primitive Count: {RagnarokEffectPool.DebugGetPrimitivePoolCount()}"));
                myInspector.Add(new Label($"Mesh Count: {EffectPool.DebugGetMeshCount()}"));
                myInspector.Add(new Label($"Mesh Builder Count: {EffectPool.DebugGetBuilderCount()}"));
                myInspector.Add(new Label($"Segment Count: {EffectPool.EffectSegmentCount()}"));
                myInspector.Add(new Label($"Parts Count: {EffectPool.DebugGetPartsCount()}"));
                myInspector.Add(new Label(EffectPool.DebugGetPrimitiveDataCounts()));
            }

            return myInspector;
        }
    }
}