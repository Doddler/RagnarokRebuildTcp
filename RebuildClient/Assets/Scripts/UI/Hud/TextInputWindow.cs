using System;
using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace Assets.Scripts.UI.Hud
{
    public class TextInputWindow : MonoBehaviour
    {
        public TextMeshProUGUI TextTitle;
        public TMP_InputField TextInput;
        private Transform container;
        private Action<string> onSubmitAction;

        public void Awake()
        {
            container = transform.parent;
        }

        public void HideInputWindow()
        {
            gameObject.SetActive(false);
            CameraFollower.Instance.InTextInputBox = false;
            onSubmitAction = null;
        }

        public void BeginTextInput(string description, Action<string> onSubmit)
        {
            gameObject.SetActive(true);
            onSubmitAction = onSubmit;
            transform.SetAsLastSibling();
            TextTitle.text = description;
            TextInput.text = $"";
            TextInput.ActivateInputField();
            CameraFollower.Instance.InTextInputBox = true;
        }

        public void Submit()
        {
            if(onSubmitAction != null)
                onSubmitAction(TextInput.text);
            HideInputWindow();
        }
        
        public void Update()
        {
            if (transform != container.GetChild(container.childCount - 1))
                HideInputWindow();
        }
    }
}