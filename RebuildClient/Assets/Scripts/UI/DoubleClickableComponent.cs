using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Assets.Scripts.UI
{
    public class DoubleClickableComponent : MonoBehaviour, IPointerClickHandler
    {
        public UnityEvent OnDoubleClick;
        public bool IsActive;
        
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsActive)
                return;
            if(eventData.button == PointerEventData.InputButton.Left && eventData.clickCount >= 2)
                OnDoubleClick.Invoke();
        }
    }
}