using System;
using Assets.Scripts.Network;
using Assets.Scripts.UI.ConfigWindow;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Scripts.UI.Hud
{
    public class VendTitleBox : MonoBehaviour, IPointerClickHandler
    {
        public int VendOwnerId;
        [NonSerialized] public GameObject FollowObject;
        
        public RectTransform Parent;
        public RectTransform Self;
        public TextMeshProUGUI Text;

        private float height = 0;
        
        public void SnapDialog()
        {
            var cf = CameraFollower.Instance;
            var rect = Self;
            var canvasRect = cf.UiCanvas.transform as RectTransform;

            var screenPos = cf.Camera.WorldToScreenPoint(FollowObject.transform.position);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPos,
                null,
                out var localPoint
            );

            rect.anchoredPosition = localPoint;

            var d = 70 / cf.Distance;
            
            if (!GameConfig.Data.ScalePlayerDisplayWithZoom)
                d = 1f;
            
            rect.localScale = new Vector3(d, d, d);
        }

        public void Update()
        {
            if(FollowObject != null)
                SnapDialog();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount >= 2)
            {
                NetworkManager.Instance.VendingOpenStore(VendOwnerId);
            }
        }
    }
}
