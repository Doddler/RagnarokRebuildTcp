using System;
using RebuildSharedData.Util;
using TMPro;
using UnityEngine;

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

        //above 0 limits the input to a name of that length
        public void BeginTextInput(string description, Action<string> onSubmit, int nameLength = 0)
        {
            gameObject.SetActive(true);
            onSubmitAction = onSubmit;
            transform.SetAsLastSibling();
            TextTitle.text = description;
            TextInput.text = $"";

            //the window is shared, so the last prompt's limits are cleared
            TextInput.characterLimit = 0;
            TextInput.onValidateInput = null;
            if (nameLength > 0)
            {
                TextInput.characterLimit = nameLength;
                TextInput.onValidateInput = NameValidator.FilterTyped;
            }

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