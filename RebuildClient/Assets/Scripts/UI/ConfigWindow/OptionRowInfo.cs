using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.ConfigWindow
{
    public enum OptionCategory
    {
        Game,
        UI,
        Experimental,
        Audio
    }

    public sealed class OptionRowInfo : MonoBehaviour
    {
        [SerializeField] private OptionCategory category;
        [SerializeField] private TMP_Text label;

        public OptionCategory Category => category;

        public void Bind(OptionCategory value) => category = value;
        public void SetLabel(string text) { if (label != null) label.text = text; }

        // Fixes the label column width; the control takes the rest. Not for headers, whose text holds the row's LayoutElement.
        public void SetLabelWidth(float width)
        {
            var element = label == null ? null : label.GetComponent<LayoutElement>();
            if (element == null)
                return;

            element.minWidth = width;
            element.preferredWidth = width;
        }
    }
}
