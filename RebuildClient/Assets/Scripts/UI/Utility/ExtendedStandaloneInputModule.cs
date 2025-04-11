using UnityEngine.EventSystems;

namespace Assets.Scripts.UI.Utility
{
    public class ExtendedStandaloneInputModule : StandaloneInputModule
    {
        public static PointerEventData GetPointerEventData(int pointerId = -1)
        {
            PointerEventData eventData;
            _instance.GetPointerData(pointerId, out eventData, true);
            return eventData;
        }

        private static ExtendedStandaloneInputModule _instance;

        protected override void Awake()
        {
            base.Awake();
            _instance = this;
        }
    }

}