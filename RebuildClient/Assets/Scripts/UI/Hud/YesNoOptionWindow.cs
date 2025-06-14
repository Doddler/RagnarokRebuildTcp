using System;
using Assets.Scripts.Objects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Hud
{
    public class YesNoOptionWindow : WindowBase
    {
        public TextMeshProUGUI TextTitle;
        public TextMeshProUGUI YesButtonText;
        public TextMeshProUGUI NoButtonText;
        private Transform container;
        private Action onYesAction;
        private Action onNoAction;
        private bool runNoActionOnHide;
        private bool useSound;

        public void Awake()
        {
            container = transform.parent;
        }

        public void HideInputWindow()
        {
            if(runNoActionOnHide)
                ClickNo(false);
            
            CameraFollower.Instance.InYesNoPrompt = false;
            onYesAction = null;
            onNoAction = null;
            HideWindow();
        }

        public void BeginPrompt(string description, string yesText, string noText, Action onYes, Action onNo, bool runNoActionOnHide, bool playClickSound = true)
        {
            onYesAction = onYes;
            onNoAction = onNo;
            YesButtonText.text = yesText;
            NoButtonText.text = noText;
            this.runNoActionOnHide = runNoActionOnHide;
            useSound = playClickSound;
            
            transform.SetAsLastSibling();
            TextTitle.text = description;
            CameraFollower.Instance.InYesNoPrompt = true;
            
            CenterWindow();
            ShowWindow();
            
            TextTitle.ForceMeshUpdate();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
        }

        public void ClickOk()
        {
            if(useSound)
                AudioManager.Instance.PlaySystemSound("버튼소리.ogg");
            if(onYesAction != null)
                onYesAction();
            HideInputWindow();
        }

        public void ClickNo(bool hide = true)
        {
            if (onNoAction != null)
                onNoAction();
            if(hide)
                HideInputWindow();
        }
        
        public void Update()
        {
            if (transform != container.GetChild(container.childCount - 1))
            {
                if(runNoActionOnHide)
                    ClickNo();
                else
                    HideInputWindow();
            }
        }
    }
}