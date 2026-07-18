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

        private Stack<GameObject> namePlatePool = new();
        private Stack<GameObject> castBarPool = new();
        private Stack<GameObject> hpBarPool = new();
        private Stack<GameObject> mpBarPool = new();
        private Stack<GameObject> textBubblePool = new();

        private Stack<GameObject> characterPanelPool = new();
        private readonly List<CharacterFloatingDisplay> activeDisplays = new();

        public TextMeshProUGUI AttachNamePlate(GameObject parent) => AttachObjectFromQueue<TextMeshProUGUI>(namePlatePool, NamePlateTemplate, parent);
        public SliderBar AttachCastBar(GameObject parent) => AttachObjectFromQueue<SliderBar>(castBarPool, CastBarTemplate, parent);
        public SliderBar AttachHpBar(GameObject parent) => AttachObjectFromQueue<SliderBar>(hpBarPool, HpBarTemplate, parent);
        public SliderBar AttachMpBar(GameObject parent) => AttachObjectFromQueue<SliderBar>(mpBarPool, MpBarTemplate, parent);
        public CharacterChat AttachChatBubble(GameObject parent) => AttachObjectFromQueue<CharacterChat>(textBubblePool, TextBubbleTemplate, parent);

        public void ReturnNamePlate(GameObject obj) => ReturnObjectToQueue(namePlatePool, obj);
        public void ReturnCastBar(GameObject obj) => ReturnObjectToQueue(castBarPool, obj);
        public void ReturnHpBar(GameObject obj) => ReturnObjectToQueue(hpBarPool, obj);
        public void ReturnMpBar(GameObject obj) => ReturnObjectToQueue(mpBarPool, obj);
        public void ReturnChatBubble(GameObject obj) => ReturnObjectToQueue(textBubblePool, obj);
        
        private void ReturnObjectToQueue(Stack<GameObject> stack, GameObject obj)
        {
            obj.transform.SetParent(PoolRoot);
            obj.SetActive(false);
            stack.Push(obj);
        }

        private T AttachObjectFromQueue<T>(Stack<GameObject> np, GameObject template, GameObject parent, bool enable = true)
        {
            var lp = template.transform.localPosition;
            if (!np.TryPop(out var obj))
                obj = Instantiate(template);
            obj.transform.SetParent(parent.transform);
            obj.transform.localPosition = lp;
            obj.transform.localScale = Vector3.one;
            if(enable)
                obj.SetActive(true);
            return obj.GetComponent<T>();
        }

        public CharacterFloatingDisplay GetNewFloatingDisplay()
        {
            var display = AttachObjectFromQueue<CharacterFloatingDisplay>(characterPanelPool, DisplayRootTemplate, gameObject, false);
            activeDisplays.Add(display);
            return display;
        }

        //every display is snapped from here so the camera, canvas and overlay scale are resolved once
        //per frame instead of once per entity
        private void LateUpdate()
        {
            if (activeDisplays.Count == 0)
                return;

            var cf = CameraFollower.Instance;
            var canvasRect = (RectTransform)cf.UiCanvas.transform;
            var rootScale = cf.OverlayRootScale;

            //backwards, since a display can return itself to the pool while being updated
            for (var i = activeDisplays.Count - 1; i >= 0; i--)
                activeDisplays[i].Snap(cf.Camera, canvasRect, rootScale);
        }

        public void ReturnFloatingDisplay(CharacterFloatingDisplay display)
        {
            activeDisplays.Remove(display);
            display.ReturnToPool();
            ReturnObjectToQueue(characterPanelPool, display.gameObject);
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