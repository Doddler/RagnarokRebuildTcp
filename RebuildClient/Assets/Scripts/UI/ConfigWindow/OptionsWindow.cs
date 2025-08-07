using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.ConfigWindow
{
    public partial class OptionsWindow : WindowBase
    {
        public ScrollRect MainScrollArea;
        public List<GameObject> TabContents;
        public List<Button> TabButtons;
        public GameObject ColorPickerObject;
        public int colorPickerTab;
        private int currentTab;
        private Action uiUpdateEvent;
        private bool isInitialized;

        public void Initialize()
        {
            if (isInitialized || TabContents == null || TabContents.Count <= 0 || TabButtons == null || TabButtons.Count <= 0)
                return;
            

            currentTab = 0;

            TabButtons[0].interactable = false;
            TabContents[0].SetActive(true);
            for (var i = 1; i < TabContents.Count; i++)
            {
                TabContents[i].SetActive(false);
                TabButtons[i].interactable = true;
            }

            ((RectTransform)transform).sizeDelta = new Vector2(500, 400);

            GameConfig.InitializeIfNecessary();
            InitializeAudio();
            InitializeCharacterOptions();
            InitializeUISettings();
            InitializeDisplayOptions();
            isInitialized = true;
            //RefreshGameSettings();
            
            ColorPickerObject.SetActive(currentTab == colorPickerTab);
        }

        public void ChangeTab(int id)
        {
            TabContents[currentTab].SetActive(false);
            TabButtons[currentTab].interactable = true;
            TabContents[id].SetActive(true);
            TabButtons[id].interactable = false;
            currentTab = id;
            MainScrollArea.content = TabContents[currentTab].transform as RectTransform;
            
            ColorPickerObject.SetActive(currentTab == colorPickerTab);
                
        }

        public void Update()
        {
            if (uiUpdateEvent != null && !Input.GetMouseButton(0))
            {
                //the update event might set a new update event, so... we wait
                var update = uiUpdateEvent;
                uiUpdateEvent = null;
                update();
            }
        }

        private new void OnDestroy()
        {
            base.OnDestroy();
            GameConfig.SaveConfig();
        }

    }
}