using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Scripts.UI.Hud
{
    //Shown only while typing; closes when it loses focus. A press on the log gives the focus back.
    [RequireComponent(typeof(TMP_InputField))]
    public class ChatInput : MonoBehaviour, IDeselectHandler
    {
        public ChatLog ChatLog;
        [SerializeField] private TMP_InputField field;

        private bool selectAllOnFocus;

        private void Awake() => selectAllOnFocus = field.onFocusSelectAll;

        private void OnEnable() => ChatLog.SetExpanded(true);

        private void OnDisable()
        {
            field.onFocusSelectAll = selectAllOnFocus;
            //the log can be destroyed first when the scene unloads
            if (ChatLog != null)
                ChatLog.SetExpanded(false);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            var pressed = (eventData as PointerEventData)?.pointerPressRaycast.gameObject;
            if (pressed != null && pressed.transform.IsChildOf(ChatLog.transform))
            {
                //the field takes the focus back on its next update
                field.onFocusSelectAll = false;
                field.ActivateInputField();
                return;
            }

            gameObject.SetActive(false);
        }
    }
}
