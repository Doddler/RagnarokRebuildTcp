using Assets.Scripts.UI.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Assets.Scripts.UI.ClientDatabase
{
    [DisallowMultipleComponent]
    public sealed class DatabaseListRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private Image icon;
        [SerializeField] private Button button;
        [SerializeField] private ButtonRightClickEventHandler rightClick;
        [SerializeField] private float labelLeftWithIcon = 32f;
        [SerializeField] private float labelLeftWithoutIcon = 8f;

        public void SetLabel(string value)
        {
            label.text = value;
        }

        public void SetIcon(Sprite sprite)
        {
            var hasIcon = sprite != null;
            icon.sprite = sprite;
            icon.color = Color.white;
            icon.gameObject.SetActive(hasIcon);

            var labelRect = label.rectTransform;
            var offsetMin = labelRect.offsetMin;
            offsetMin.x = hasIcon ? labelLeftWithIcon : labelLeftWithoutIcon;
            labelRect.offsetMin = offsetMin;
        }

        public void SetActions(UnityAction onClick, UnityAction onRightClick)
        {
            button.onClick.RemoveAllListeners();
            rightClick.OnRightClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
            rightClick.OnRightClick.AddListener(onRightClick);
        }
    }
}
