using System.Collections.Generic;
using Assets.Scripts.Objects;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.UI.Hud
{
    public class CharacterOverlayManager : MonoBehaviour
    {
        public TextMeshProUGUI NamePlateTemplate;
        public SliderBar CastBarTemplate;
        public SliderBar HpBarTemplate;
        public SliderBar MpBarTemplate;
        public CharacterChat TextBubbleTemplate;
        public CharacterFloatingDisplay DisplayRootTemplate;
        
        public Transform PoolRoot;

        //per element, as a display holds one of each; pools grow past this
        public int PrewarmCount = 50;

        //grace period an emptied display stays attached before releasing back to the pool
        public float LingerDuration = 5f;

        private readonly Dictionary<Component, Stack<Component>> pools = new();
        private readonly List<CharacterFloatingDisplay> activeDisplays = new();
        private CharacterFloatingDisplay mainCharacterDisplay;
        private TargetMarker targetMarker;

        //kept on top of all other displays; only a newly created display can end up above it
        public void RegisterMainCharacterDisplay(CharacterFloatingDisplay display) => mainCharacterDisplay = display;

        public TextMeshProUGUI AttachNamePlate(GameObject parent) => Attach(NamePlateTemplate, parent);
        public SliderBar AttachCastBar(GameObject parent) => Attach(CastBarTemplate, parent);
        public SliderBar AttachHpBar(GameObject parent) => Attach(HpBarTemplate, parent);
        public SliderBar AttachMpBar(GameObject parent) => Attach(MpBarTemplate, parent);
        public CharacterChat AttachChatBubble(GameObject parent) => Attach(TextBubbleTemplate, parent);

        public void ReturnNamePlate(TextMeshProUGUI namePlate) => Return(NamePlateTemplate, namePlate);
        public void ReturnCastBar(SliderBar castBar) => Return(CastBarTemplate, castBar);
        public void ReturnHpBar(SliderBar hpBar) => Return(HpBarTemplate, hpBar);
        public void ReturnMpBar(SliderBar mpBar) => Return(MpBarTemplate, mpBar);
        public void ReturnChatBubble(CharacterChat chatBubble) => Return(TextBubbleTemplate, chatBubble);

        //the previous target returns it before the next one asks for it
        public TargetMarker AttachTargetMarker(GameObject parent)
        {
            var t = targetMarker.transform;
            t.SetParent(parent.transform, false);
            t.localScale = Vector3.one; //the layout pass applies the zoom scale
            targetMarker.gameObject.SetActive(true);
            return targetMarker;
        }

        public void ReturnTargetMarker()
        {
            targetMarker.transform.SetParent(PoolRoot, false);
            targetMarker.gameObject.SetActive(false);
        }

        private void Return(Component template, Component element)
        {
            element.transform.SetParent(PoolRoot, false);
            element.gameObject.SetActive(false);
            pools[template].Push(element);
        }

        private T Attach<T>(T template, GameObject parent, bool enable = true) where T : Component
        {
            var element = pools[template].TryPop(out var pooled) ? (T)pooled : Instantiate(template);

            var t = element.transform;
            t.SetParent(parent.transform, false);
            t.localPosition = template.transform.localPosition;
            t.localScale = Vector3.one; //the layout pass applies the zoom scale
            if (enable)
                element.gameObject.SetActive(true);
            return element;
        }

        public CharacterFloatingDisplay GetNewFloatingDisplay()
        {
            var display = Attach(DisplayRootTemplate, gameObject, false);
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
            Return(DisplayRootTemplate, display);
        }

        private void Awake()
        {
            foreach (var template in new Component[] { DisplayRootTemplate, NamePlateTemplate, CastBarTemplate, HpBarTemplate, MpBarTemplate, TextBubbleTemplate })
            {
                //templates are left on in the scene for editing; copies of a switched-off one start off
                template.gameObject.SetActive(false);
                var pool = pools[template] = new Stack<Component>(PrewarmCount);
                for (var i = 0; i < PrewarmCount; i++)
                    pool.Push(Instantiate(template, PoolRoot));
            }

            //made up front so its sprite and size are ready before the first target
            targetMarker = TargetMarker.Create(PoolRoot);
        }
    }
}