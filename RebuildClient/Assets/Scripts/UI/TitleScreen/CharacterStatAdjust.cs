using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Assets.Scripts.UI.TitleScreen
{
    public class CharacterStatAdjust : UIBehaviour, IScrollHandler
    {
        public UnityEvent ScrollUpEvent; 
        public UnityEvent ScrollDownEvent; 

        public void OnScroll(PointerEventData eventData)
        {
            if(eventData.scrollDelta.y > 0)
                ScrollUpEvent.Invoke();
            if(eventData.scrollDelta.y < 0)
                ScrollDownEvent.Invoke();
        }
    }
}
