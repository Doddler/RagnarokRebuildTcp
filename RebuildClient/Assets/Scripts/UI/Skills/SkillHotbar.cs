using System;
using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.ConfigWindow;
using RebuildSharedData.Enum;
using UnityEngine;
using UnityEngine.Serialization;

namespace Assets.Scripts.UI
{
    public class SkillHotbar : WindowBase
    {
        public Transform SkillBarContainer;
        public GameObject SkillBarRowTemplate;
        [FormerlySerializedAs("DragHandle")] public ResizeHandle ResizeHandle;

        private List<GameObject> HotBarRows = new();
        private List<SkillHotbarEntry> HotBarEntries = new();
        private UiManager manager;

        private bool isOpenForDrop;

        private SkillHotbarEntry pressedEntry;

        public void UpdateDrag() //drag as in resize drag
        {
            // Debug.Log(ResizeHandle.CurrentStepSize);
            for (var i = 0; i < HotBarRows.Count; i++)
            {
                HotBarRows[i].SetActive(i <= ResizeHandle.CurrentStepSize.y);
            }
        }

        public SkillHotbarEntry GetEntryById(int id) => HotBarEntries[id];

        private KeyCode[] HotKeyCode =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
            KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0,
        };

        private string[] HotKeyText =
        {
            "1", "2", "3", "4", "5", "6", "7", "8", "9", "0",
        };

        private void SetUpEntry(SkillHotbarEntry entry, int id)
        {
            entry.Id = id;
            entry.Parent = this;
            entry.enabled = false;
            entry.CanDrag = true;
            entry.UIManager = UiManager.Instance;
            entry.Clear();
            entry.HotkeyText.text = id < HotKeyText.Length ? HotKeyText[id] : "";
            entry.DragItem.Origin = ItemDragOrigin.HotBar;
            entry.DragItem.OriginId = id;
            entry.DragItem.OnDoubleClick = entry.OnDoubleClick;
        }

        private void SetupFirstDeleteMe(SkillHotbarEntry entry, int id)
        {
            entry.Id = id;
            entry.Parent = this;
            entry.enabled = true;
            entry.CanDrag = false;
            entry.UIManager = UiManager.Instance;
            //entry.Clear();
            entry.DragItem.ItemCount = 200;
            entry.HotkeyText.text = id < HotKeyText.Length ? HotKeyText[id] : "";
            entry.DragItem.CountText.text = entry.DragItem.ItemCount.ToString();
            entry.DragItem.Origin = ItemDragOrigin.HotBar;
            entry.DragItem.OriginId = id;
            entry.DragItem.OnDoubleClick = entry.OnDoubleClick;
        }

        //this is cursed and only for the fixed red potion entry. This should be removed later.
        public void UpdateItem1Count(int count)
        {
            HotBarEntries[0].DragItem.UpdateCount(count);
        }
        
        public void Initialize()
        {
            manager = UiManager.Instance;
            for (var i = 0; i < 3; i++)
            {
                var row = GameObject.Instantiate(SkillBarRowTemplate, SkillBarContainer);
                var firstEntry = row.transform.GetChild(0).GetComponent<SkillHotbarEntry>();
                SetupFirstDeleteMe(firstEntry, i * 10);
                HotBarEntries.Add(firstEntry);
                for (var j = 1; j < 10; j++)
                {
                    var go = GameObject.Instantiate(firstEntry, row.transform);
                    var entry = go.GetComponent<SkillHotbarEntry>();
                    SetUpEntry(entry, i * 10 + j);
                    HotBarEntries.Add(entry);
                }

                if (i > 0)
                    row.SetActive(false);
                HotBarRows.Add(row);
            }

            Destroy(SkillBarRowTemplate);
            
            GameConfig.InitializeIfNecessary();
            LoadHotBarData(GameConfig.Data.HotBarSaveData);
        }

        public void ActivateHotBarEntry(SkillHotbarEntry entry)
        {
            if (entry.DragItem.Type == DragItemType.Skill)
            {
                var onCursor = CameraFollower.Instance.PressSkillButton((CharacterSkill)entry.DragItem.ItemId, entry.DragItem.ItemCount);
                if (onCursor && entry.gameObject.activeInHierarchy)
                {
                    if(pressedEntry != null)
                        pressedEntry.ReleaseSkill();
                    pressedEntry = entry;
                    entry.PressKey();
                }
            }

            if (entry.DragItem.Type == DragItemType.Item)
            {
                if (entry.DragItem.ItemCount <= 0)
                    return;
                NetworkManager.Instance.SendUseItem(entry.DragItem.ItemId);
                var cnt = entry.DragItem.ItemCount - 1;
                // if (cnt <= 0)
                //     entry.DragItem.Clear();
                // else
                    entry.DragItem.UpdateCount(cnt);
            }
        }

        public void UpdateHotkeyPresses()
        {
            if (!CameraFollower.Instance.HasSkillOnCursor && pressedEntry != null)
            {
                pressedEntry.ReleaseSkill();
                pressedEntry = null;
            }
            
            for (var i = 0; i < HotKeyCode.Length; i++)
            {
                if (Input.GetKeyDown(HotKeyCode[i]))
                {
                    ActivateHotBarEntry(HotBarEntries[i]);
                }
            }
        }

        public HotBarSaveData[] SaveHotBarData(HotBarSaveData[] data)
        {
            if (data == null)
            {
                data = new HotBarSaveData[HotBarEntries.Count];
                for (var i = 0; i < data.Length; i++)
                    data[i] = new HotBarSaveData() { Type = DragItemType.None };
            }

            for (var i = 0; i < data.Length; i++)
            {
                if(data[i] == null)
                    data[i] = new HotBarSaveData() { Type = DragItemType.None };
                data[i].Type = HotBarEntries[i].DragItem.Type;
                data[i].ItemId = HotBarEntries[i].DragItem.ItemId;
                data[i].ItemCount = HotBarEntries[i].DragItem.ItemCount;
            }

            return data;
        }

        public void LoadHotBarData(HotBarSaveData[] data)
        {
            if (data == null)
                return;

            var len = Mathf.Min(data.Length, HotBarEntries.Count);
            for (var i = 0; i < len; i++)
            {
                if (data[i] == null)
                {
                        data[i] = new HotBarSaveData() { Type = DragItemType.None };
                        continue;
                }
                
                if (data[i].Type == DragItemType.Skill)
                {
                    var spriteName = ClientDataLoader.Instance.GetSkillData((CharacterSkill)data[i].ItemId).Icon;
                    var sprite = ClientDataLoader.Instance.ItemIconAtlas.GetSprite(spriteName);
                    if (sprite != null)
                    {
                        HotBarEntries[i].DragItem.gameObject.SetActive(true);
                        HotBarEntries[i].DragItem.Assign(data[i].Type, sprite, data[i].ItemId, data[i].ItemCount);
                    }
                }
            }
        }

        public void Update()
        {
            if (manager.IsDraggingItem && !isOpenForDrop)
            {
                for (var i = 0; i < HotBarEntries.Count; i++)
                {
                    HotBarEntries[i].enabled = true; //enable the entries, they will report to the UI manager when being hovered
                    if (HotBarEntries[i].DragItem.Type == DragItemType.None)
                        HotBarEntries[i].HotkeyText.gameObject.SetActive(true);
                }

                isOpenForDrop = true;
            }

            if (!manager.IsDraggingItem && isOpenForDrop)
            {
                for (var i = 0; i < HotBarEntries.Count; i++)
                {
                    //HotBarEntries[i].HighlightImage.SetActive(false);
                    HotBarEntries[i].HotkeyText.gameObject.SetActive(false);
                    //HotBarEntries[i].enabled = false; //disable them
                }

                isOpenForDrop = false;
            }
        }
    }
}