using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Assets.Scripts.UI.TitleScreen
{
    public class CharacterStatAdjust : UIBehaviour, IScrollHandler
    {
        public UnityEvent ScrollUpEvent; 
        public UnityEvent ScrollDownEvent; 
    
        // Start is called before the first frame update
        void Start()
        {
        
        }

        // Update is called once per frame
        void Update()
        {
        
        }

        public void OnScroll(PointerEventData eventData)
        {
            if(eventData.scrollDelta.y > 0)
                ScrollUpEvent.Invoke();
            if(eventData.scrollDelta.y < 0)
                ScrollDownEvent.Invoke();
        }
    }
}
