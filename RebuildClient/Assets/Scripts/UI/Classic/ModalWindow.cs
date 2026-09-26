using RO_Flex_UI.Components;
using RO_Flex_UI.Panels;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Assets.Scripts.UI.Classic
{
    public class ModalWindow : Window, IStyledWindow
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private GameObject buttonsArea;
        [SerializeField] private RoButton buttonTemplate;

        public void Configure(string title, string body,
            IReadOnlyList<string> buttons,
            IReadOnlyList<UnityAction> events)
        {
            titleText.SetText(title);
            bodyText.SetText(body);

            for (int i = 0; i < buttons.Count; i++)
            {
                var b = Instantiate(buttonTemplate, buttonsArea.transform);
                b.gameObject.SetActive(true);
                b.GetComponentInChildren<TMP_Text>().SetText(buttons[i]);
                b.onClick.AddListener(events[i]);
            }
        }

        public void Awake()
        {
            buttonTemplate.gameObject.SetActive(false);
        }

        public void Start()
        {
            Configure(
                "TEST",
                "BODY TEXT",
                new string[] { "Confirm", "Cancel" },
                new UnityAction[] { Confirm, Cancel }
            );
        }

        public void HideWindow()
        {
            throw new System.NotImplementedException();
        }

        public void ShowWindow()
        {
            throw new System.NotImplementedException();
        }

        public void ToggleVisibility()
        {
            throw new System.NotImplementedException();
        }


        public void Confirm()
        {
            Debug.Log("Confirm");
        }

        public void Cancel()
        {
            Debug.Log("Cancel");
        }
    }
}