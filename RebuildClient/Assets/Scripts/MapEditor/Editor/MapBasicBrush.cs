using Assets.Editor;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.MapEditor.Editor
{
    class MapBasicBrush : IMapBrush, IMapEditor
    {
        public RoMapEditorWindow Window;
        public RoMapEditor Editor;
        public RoMapData Data;

        protected float VerticalDragHandle(Vector3 previousPosition, Vector3 position)
        {
            var dragOffset = Vector3.up * 2f;

            Handles.color = Color.magenta;
            Handles.DrawLine(position, position + dragOffset);
            var newDragPosition = Handles.Slider(position + dragOffset, Vector3.up, 0.5f, Handles.ConeHandleCap, Editor.HeightSnap);
            newDragPosition -= dragOffset;

            var totalMove = MathHelper.SnapValue(newDragPosition.y - previousPosition.y, Editor.HeightSnap * RoMapData.YScale);

            return totalMove;
        }
        
        public virtual void OnEnable(RoMapEditorWindow window, RoMapEditor editor)
        {
            Window = window;
            Editor = editor;
            Data = editor.MapData;

            Editor.CurrentBrush = this;

            //Debug.Log("OnEnable");

            OnEnable();
        }

        public void RepaintScene()
        {
            var scene = SceneView.currentDrawingSceneView;
            if (scene != null)
            {
                scene.Repaint();
                return;
            }

            var window = EditorWindow.GetWindow<SceneView>();
            if(window != null)
                window.Repaint();
        }

        //interface stuff

        public virtual void OnEnable() { }
        public virtual void EditorUI() { }
        public virtual void OnSceneGUI() { }
        public virtual void OnSelectionChange() {  }

        public virtual void OnDisable()
        {
            //Debug.Log("OnDisable");
            Window = null;
            Editor = null;
            Data = null;
        }

        public virtual bool IsEnabled()
        {
            return Editor != null;
        }

        public virtual void Repaint()
        {

        }

    }
}
