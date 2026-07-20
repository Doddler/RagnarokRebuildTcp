using System.Collections.Generic;
using Assets.Scripts.Objects;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.UI.Hud
{
    public class CharacterOverlayManager : MonoBehaviour
    {
        public GameObject NamePlateTemplate;
        public GameObject CastBarTemplate;
        public GameObject HpBarTemplate;
        public GameObject MpBarTemplate;
        public GameObject TextBubbleTemplate;
        public GameObject DisplayRootTemplate;
        
        public Transform PoolRoot;

        //one pool per template
        private readonly Dictionary<GameObject, Stack<GameObject>> pools = new();
        private readonly List<CharacterFloatingDisplay> activeDisplays = new();
        private CharacterFloatingDisplay mainCharacterDisplay;

        //kept on top of all other displays; only a newly created display can end up above it
        public void RegisterMainCharacterDisplay(CharacterFloatingDisplay display) => mainCharacterDisplay = display;

        public TextMeshProUGUI AttachNamePlate(GameObject parent) => Attach<TextMeshProUGUI>(NamePlateTemplate, parent);
        public SliderBar AttachCastBar(GameObject parent) => Attach<SliderBar>(CastBarTemplate, parent);
        public SliderBar AttachHpBar(GameObject parent) => Attach<SliderBar>(HpBarTemplate, parent);
        public SliderBar AttachMpBar(GameObject parent) => Attach<SliderBar>(MpBarTemplate, parent);
        public CharacterChat AttachChatBubble(GameObject parent) => Attach<CharacterChat>(TextBubbleTemplate, parent);

        public void ReturnNamePlate(GameObject obj) => Return(NamePlateTemplate, obj);
        public void ReturnCastBar(GameObject obj) => Return(CastBarTemplate, obj);
        public void ReturnHpBar(GameObject obj) => Return(HpBarTemplate, obj);
        public void ReturnMpBar(GameObject obj) => Return(MpBarTemplate, obj);
        public void ReturnChatBubble(GameObject obj) => Return(TextBubbleTemplate, obj);

        private Stack<GameObject> PoolFor(GameObject template)
        {
            if (!pools.TryGetValue(template, out var pool))
                pools[template] = pool = new Stack<GameObject>();
            return pool;
        }

        private void Return(GameObject template, GameObject obj)
        {
            obj.transform.SetParent(PoolRoot);
            obj.SetActive(false);
            PoolFor(template).Push(obj);
        }

        private T Attach<T>(GameObject template, GameObject parent, bool enable = true)
        {
            if (!PoolFor(template).TryPop(out var obj))
                obj = Instantiate(template);

            var t = obj.transform;
            t.SetParent(parent.transform);
            t.localPosition = template.transform.localPosition;
            t.localScale = Vector3.one; //the layout pass applies the zoom scale
            if (enable)
                obj.SetActive(true);
            return obj.GetComponent<T>();
        }

        public CharacterFloatingDisplay GetNewFloatingDisplay()
        {
            var display = Attach<CharacterFloatingDisplay>(DisplayRootTemplate, gameObject, false);
            activeDisplays.Add(display);
            if (mainCharacterDisplay != null)
                mainCharacterDisplay.Rect.SetAsLastSibling();
            return display;
        }

        //ticks every display with the camera, canvas and scales resolved once per frame
        private void LateUpdate()
        {
            if (activeDisplays.Count == 0)
                return;

            var cf = CameraFollower.Instance;
            var canvasRect = (RectTransform)cf.UiCanvas.transform;
            var glueScale = cf.OverlayGlueScale;
            var zoomScale = cf.OverlayRootScale;

            //backwards, since a display can return itself to the pool while being updated
            for (var i = activeDisplays.Count - 1; i >= 0; i--)
                activeDisplays[i].Tick(cf.Camera, canvasRect, glueScale, zoomScale);
        }

        public void ReturnFloatingDisplay(CharacterFloatingDisplay display)
        {
            if (mainCharacterDisplay == display)
                mainCharacterDisplay = null;
            activeDisplays.Remove(display);
            display.ReturnToPool();
            Return(DisplayRootTemplate, display.gameObject);
        }

        public void Awake()
        {
            NamePlateTemplate.SetActive(false);
            CastBarTemplate.SetActive(false);
            HpBarTemplate.SetActive(false);
            MpBarTemplate.SetActive(false);
            TextBubbleTemplate.SetActive(false);
            DisplayRootTemplate.SetActive(false);

            DisplayRootTemplate.GetComponent<CharacterFloatingDisplay>().Manager = this;
        }
    }
}