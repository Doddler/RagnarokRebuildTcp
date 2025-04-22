using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Assets.Scripts.UI.Utility
{
    public class ButtonRightClickEventHandler : MonoBehaviour, IPointerClickHandler
    {
        public UnityEvent OnRightClick;
        public void OnPointerClick(PointerEventData eventData)
        {
            if(eventData.button == PointerEventData.InputButton.Right)
                OnRightClick.Invoke();
        }
    }
}