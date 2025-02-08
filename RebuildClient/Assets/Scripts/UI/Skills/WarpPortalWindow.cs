using System;
using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.Utility;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
    public class WarpPortalWindow : WindowBase
    {
        public List<Button> WarpEntryButtons;
        public List<TextMeshProUGUI> WarpEntryTexts;
        public List<DoubleClickableComponent> WarpEntryDoubleClickCatches;

        public TextMeshProUGUI UsageText;
        public TextMeshProUGUI OkButtonText;

        public Vector2 PreferredDimensions;

        public static WarpPortalWindow Instance;
        [NonSerialized] public bool InMemoMode = false;
        private int selectedEntry;

        public override void HideWindow()
        {
            base.HideWindow();
            Destroy(gameObject);
        }

        public void OnSubmit()
        {
            if(InMemoMode)
                NetworkManager.Instance.MemoMap(selectedEntry);
            else
                NetworkManager.Instance.SendSelfTargetSkillAction(CharacterSkill.WarpPortal, selectedEntry + 1);
            HideWindow();
            Debug.Log($"On warp portal window submit ({selectedEntry})");
        }

        public void DoubleClickEntry(int id)
        {
            selectedEntry = id;
            Debug.Log($"On warp portal window double click");
            OnSubmit();
        }

        public void ClickEntryButton(int id)
        {
            selectedEntry = id;
            for (var i = 0; i < WarpEntryButtons.Count; i++)
            {
                WarpEntryButtons[i].interactable = i != id;
                WarpEntryDoubleClickCatches[i].IsActive = i == id;
            }
        }

        public static void StartCastWarpPortal()
        {
            var state = PlayerState.Instance;
            
            if (!state.KnownSkills.TryGetValue(CharacterSkill.WarpPortal, out var skillLevel))
                return;
         
            var hasDestinations = false;
            for (var i = 0; i < 4; i++)
            {
                if (!string.IsNullOrWhiteSpace(state.MemoLocations[i].MapName))
                {
                    hasDestinations = true;
                    break;
                }
            }

            if (!hasDestinations)
            {
                CameraFollower.Instance.AppendNotice("To cast warp portal you must first use the /memo command to memorize a destination.");
                return;
            }

            if (Instance != null)
            {
                Instance.Init(false, skillLevel);
                return;
            }
            
            var go = GameObject.Instantiate(UiManager.Instance.WarpMemoWindowPrefab);
            var window = go.GetComponent<WarpPortalWindow>();
            window.AttachToMainUI();
            window.Init(false, skillLevel);
        }

        public static void RunMemoCommand()
        {
            if (!PlayerState.Instance.KnownSkills.TryGetValue(CharacterSkill.WarpPortal, out var skillLevel))
            {
                CameraFollower.Instance.AppendNotice("You cannot use /memo command without knowing the warp portal skill.");
                return;
            }
            
            var mapName = NetworkManager.Instance.CurrentMap;
            var mapInfo = ClientDataLoader.Instance.GetMapInfo(mapName);
            if (mapInfo == null)
            {
                CameraFollower.Instance.AppendNotice("This location is unavailable to use as a warp portal destination.");
                return;
            }

            if (!mapInfo.CanMemo)
            {
                var msg = (MapType)mapInfo.MapMode switch
                {
                    MapType.Indoor => "Indoor locations cannot be used as a warp portal destination.",
                    MapType.Dungeon => "Dungeons cannot be used as a warp portal destination.",
                    MapType.Field => "This location is too far away from a town or settlement to use as a warp portal destination.",
                    _ => "This location is unavailable to use as a warp portal destination."
                };
                
                CameraFollower.Instance.AppendNotice(msg);
                return;
            }

            if (Instance != null)
            {
                Instance.Init(true, skillLevel);
                return;
            }

            var go = GameObject.Instantiate(UiManager.Instance.WarpMemoWindowPrefab);
            var window = go.GetComponent<WarpPortalWindow>();
            window.AttachToMainUI();
            window.Init(true, skillLevel);
        }

        private void Init(bool isMemo, int skillLevel)
        {
            Instance = this;
            InMemoMode = isMemo;
            gameObject.SetActive(true);
            transform.RectTransform().sizeDelta = PreferredDimensions;
            
            if (isMemo)
            {
                UsageText.text = $"Save warp portal location in which slot?";
                OkButtonText.text = $"Save";
                FillInMemoButtons(skillLevel, true);
            }
            else
            {
                UsageText.text = $"Select a destination for your warp portal.";
                OkButtonText.text = $"Cast";
                FillInMemoButtons(skillLevel, false);
            }
            
            ClickEntryButton(0);
            
            MoveToTop();
            CenterWindow(new Vector2(0.5f, 0.6f));
        }

        private void FillInMemoButtons(int skillLevel, bool showEmpty)
        {
            var state = PlayerState.Instance;
            for (var i = 0; i < 4; i++)
            {
                if (i > skillLevel)
                {
                    WarpEntryButtons[i].gameObject.SetActive(false);
                    continue;
                }
                WarpEntryButtons[i].gameObject.SetActive(true);
                var mapName = state.MemoLocations[i].MapName;
                if (string.IsNullOrWhiteSpace(mapName))
                {
                    if(showEmpty)
                        WarpEntryTexts[i].text = $"<color=#474747>- Empty Slot -";
                    else
                        WarpEntryButtons[i].gameObject.SetActive(false);
                }
                else
                {
                    var mapInfo = ClientDataLoader.Instance.GetFullNameForMap(mapName);
                    WarpEntryTexts[i].text = $"[{mapName}] {mapInfo}";
                }
            }
        }
        
        public void Update()
        {
            if (transform != transform.parent.GetChild(transform.parent.childCount - 1))
            {
                HideWindow();
                return;
            }
        }
    }
}