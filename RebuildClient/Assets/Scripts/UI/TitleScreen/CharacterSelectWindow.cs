using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.ConfigWindow;
using RebuildSharedData.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.TitleScreen
{
    public class CharacterSelectWindow : WindowBase
    {
        public TitleScreen Parent;
        public List<CharacterSelectPlayerButton> CharacterSlots;
        public GameObject DisplayPane;
        
        public GameObject InfoArea1;
        public GameObject InfoArea2;
        
        public TextMeshProUGUI OkButtonText;
        public TextMeshProUGUI CharacterName;
        public TextMeshProUGUI Job;
        public TextMeshProUGUI Location;
        public TextMeshProUGUI Level;
        public TextMeshProUGUI CharacterHp;
        public TextMeshProUGUI CharacterSp;
        public TextMeshProUGUI CharacterStr;
        public TextMeshProUGUI CharacterAgi;
        public TextMeshProUGUI CharacterInt;
        public TextMeshProUGUI CharacterVit;
        public TextMeshProUGUI CharacterDex;
        public TextMeshProUGUI CharacterLuk;

        
        private TitleScreen parent;
        private List<ClientCharacterSummary> summaries;
        private int selectedSlot;

        public void ShowPane()
        {
            DisplayPane.SetActive(true);
        }
        
        public void HidePane()
        {
            DisplayPane.SetActive(false);
        }

        public void ClickOk()
        {
            AudioManager.Instance.PlaySystemSound(Parent.ButtonSound);
            var summary = summaries[selectedSlot];
            if (summary != null)
            {
                GameConfig.Data.LastUsedCharacterSlot = selectedSlot;
                GameConfig.SaveConfig();
                NetworkManager.Instance.SendEnterServerMessage(summary.Name);
                Parent.TitleState = TitleScreen.TitleScreenState.Waiting;
                gameObject.SetActive(false);
            }
            else
            {
                Parent.OpenCharacterCreator(selectedSlot);
            }
        }

        public void ClickCancel()
        {
            NetworkManager.Instance.TitleScreen.DisconnectAndReturnToLogin();
        }
        
        public void PrepareSelectWindow(TitleScreen titleScreen, List<ClientCharacterSummary> characters)
        {
            parent = titleScreen;
            summaries = characters;
            
            DisplayPane.SetActive(true);
            
            var charCount = characters.Count;
            // if (charCount > 1)
            //     charCount = 1; //hack

            for (var i = 0; i < 3; i++)
            {
                if(i >= characters.Count || characters[i] == null)
                    CharacterSlots[i].PrepareEmptySlot(i == 0);
                else
                    CharacterSlots[i].PrepareCharacterEntry(characters[i], i == 0);
            }
            //
            // CharacterSlots[1].SetAsUnavailable();
            // CharacterSlots[2].SetAsUnavailable();
            //

            var lastSlot = GameConfig.Data.LastUsedCharacterSlot;
            if (characters.Count <= lastSlot || characters[lastSlot] == null)
                lastSlot = 0;
                
            SetCharacterInfo(lastSlot);
        }

        private void UpdateSlotSelection(int slot)
        {
            CharacterSlots[0].Button.interactable = slot != 0;
            CharacterSlots[1].Button.interactable = slot != 1;
            CharacterSlots[2].Button.interactable = slot != 2;

            GameConfig.Data.LastUsedCharacterSlot = slot;
        }

        public void SetCharacterInfo(int slot)
        {
            selectedSlot = slot;
            UpdateSlotSelection(slot);
            
            if (slot >= summaries.Count || summaries[slot] == null)
            {
                // InfoArea1.SetActive(false);
                // InfoArea2.SetActive(false);
                CharacterName.text = "";
                Job.text = "";
                Location.text = "";
                Level.text = "";
                CharacterHp.text = "";
                CharacterSp.text = "";
                CharacterStr.text = "";
                CharacterAgi.text = "";
                CharacterVit.text = "";
                CharacterInt.text = "";
                CharacterDex.text = "";
                CharacterLuk.text = "";
                OkButtonText.text = "Create";
                return;
            }
            
            OkButtonText.text = "OK";
            
            InfoArea1.SetActive(true);
            InfoArea2.SetActive(true);

            var data = ClientDataLoader.Instance;
            
            var s = summaries[slot];
            if (s.SummaryData != null)
            {
                CharacterName.text = s.Name;
                Job.text = data.GetJobNameForId(s.SummaryData[(int)PlayerSummaryData.JobId]);
                Location.text = data.GetFullNameForMap(s.Map);
                Level.text = s.SummaryData[(int)PlayerSummaryData.Level].ToString();
                CharacterHp.text = $"{s.SummaryData[(int)PlayerSummaryData.Hp]} / {s.SummaryData[(int)PlayerSummaryData.MaxHp]}";
                CharacterSp.text = $"{s.SummaryData[(int)PlayerSummaryData.Sp]} / {s.SummaryData[(int)PlayerSummaryData.MaxSp]}";
                CharacterStr.text = s.SummaryData[(int)PlayerSummaryData.Str].ToString();
                CharacterAgi.text = s.SummaryData[(int)PlayerSummaryData.Agi].ToString();
                CharacterVit.text = s.SummaryData[(int)PlayerSummaryData.Vit].ToString();
                CharacterInt.text = s.SummaryData[(int)PlayerSummaryData.Int].ToString();
                CharacterDex.text = s.SummaryData[(int)PlayerSummaryData.Dex].ToString();
                CharacterLuk.text = s.SummaryData[(int)PlayerSummaryData.Luk].ToString();
            }
            else
            {
                CharacterName.text = s.Name;
                Job.text = "<i>Unavailable";
                Location.text = data.GetFullNameForMap(s.Map);
                Level.text = "";
                CharacterHp.text = "";
                CharacterSp.text = "";
                CharacterStr.text = "";
                CharacterAgi.text = "";
                CharacterVit.text = "";
                CharacterInt.text = "";
                CharacterDex.text = "";
                CharacterLuk.text = "";
            }
        }

        public void Update()
        {
            if (Parent.TitleState == TitleScreen.TitleScreenState.CharacterSelect)
            {
                if(Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    ClickOk();
                if (Input.GetKeyDown(KeyCode.RightArrow))
                    SetCharacterInfo(selectedSlot.AddAndWrapValue(1, 0, 2));
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                    SetCharacterInfo(selectedSlot.AddAndWrapValue(-1, 0, 2));
                if (Input.GetKeyDown(KeyCode.Tab))
                {
                    var isShift = Input.GetKey(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
                    SetCharacterInfo(selectedSlot.AddAndWrapValue(isShift ? -1 : 1, 0, 2));
                }
            }

        }
    }
}