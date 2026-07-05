using TMPro;
using UnityEngine;

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
    }
}
