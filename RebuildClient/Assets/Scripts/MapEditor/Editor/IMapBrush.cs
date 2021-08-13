using Assets.Editor;

namespace Assets.Scripts.MapEditor.Editor
{
    public interface IMapBrush
    {
        
        void OnEnable(RoMapEditorWindow window, RoMapEditor editor);
        void OnDisable();
        bool IsEnabled();
        void Repaint();
        void EditorUI();
    }
}
