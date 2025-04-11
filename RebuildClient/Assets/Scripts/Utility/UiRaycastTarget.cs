using UnityEngine.UI;

namespace Assets.Scripts.Utility
{
    public class UiRaycastTarget : Graphic
    {
        public override void SetMaterialDirty() { return; }
        public override void SetVerticesDirty() { return; }
    }
}