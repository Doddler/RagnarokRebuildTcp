using Assets.Scripts.PlayerControl;
using Assets.Scripts.UI.Classic;
using RebuildSharedData.Enum;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Hud
{
    public class CharacterDetailBox : MonoBehaviour, IStyledWindow, IPointerEnterHandler, IPointerExitHandler
    {
        private PlayerState playerState;

        public TextMeshProUGUI CharacterName;
        public TextMeshProUGUI CharacterJob;
        public TextMeshProUGUI CharacterZeny;
        public TextMeshProUGUI CharacterWeight;
        public TextMeshProUGUI HpDisplay;
        public Slider HpSlider;
        public TextMeshProUGUI SpDisplay;
        public Slider SpSlider;
        public TextMeshProUGUI ExpDisplay;
        public Slider ExpSlider;
        public TextMeshProUGUI JobExpDisplay;
        public Slider JobExpSlider;
        public TextMeshProUGUI BaseLvlDisplay;
        public TextMeshProUGUI JobLvlDisplay;
        public TextMeshProUGUI DebugInfo;

        public GameObject OverlayDisplay;

        private void Awake()
        {
            playerState = PlayerState.Instance;
            UiManagerV2.Instance.RegisterWindow(WindowID.PLAYER_INFO_MODERN, this);
        }

        private void Start()
        {
            Subscribe();
            RefreshFromState();
        }

        public void RefreshFromState()
        {
            OnTextChanged();
            OnHpSpChanged();
            OnProgressChanged();
            OnWeightAndZenyChanged();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (CameraFollower.Instance.HasSkillOnCursor)
            {
                var target = CameraFollower.Instance.CursorSkillTarget;
                if (target == SkillTarget.Ally || target == SkillTarget.Any)
                    OverlayDisplay.SetActive(true);
            }

            CameraFollower.Instance.IsHoveringSelfPanel = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            OverlayDisplay.SetActive(false);
            CameraFollower.Instance.IsHoveringSelfPanel = false;
        }

        #region Getter & Setter
        public void SetPlayerNameText(string text)
        {
            if (CharacterName != null)
                CharacterName.text = playerState.IsInParty && playerState.PartyLeader == playerState.PartyMemberId ? $"*{text}" : text;
        }

        public void SetJobNameText(string text)
        {
            if (CharacterJob != null)
                CharacterJob.text = text;
        }

        public void SetBaseLevelText(string text)
        {
            if (BaseLvlDisplay != null)
                BaseLvlDisplay.text = text;
        }

        public void SetJobLevelText(string text)
        {
            if (JobLvlDisplay != null)
                JobExpDisplay.text = text;
        }

        public void SetWeightText(string text)
        {
            if (CharacterWeight != null)
                CharacterWeight.text = text;
        }

        public void SetZenyText(string text)
        {
            if (CharacterZeny != null)
                CharacterZeny.text = text;
        }

        public void SetHp(int hp, int maxHp)
        {
            maxHp = Math.Max(1, maxHp);
            var percent = hp / (float)maxHp;

            if (HpDisplay != null)
                HpDisplay.text = $"HP: {hp} / {maxHp} ({percent * 100f:F1}%)";
            if (HpSlider != null)
                HpSlider.value = percent;
        }

        public void SetSp(int sp, int maxSp)
        {
            maxSp = Math.Max(1, maxSp);
            var percent = sp / (float)maxSp;

            if (SpDisplay != null)
                SpDisplay.text = $"MP: {sp} / {maxSp} ({percent * 100f:F1}%)";
            if (SpSlider != null)
                SpSlider.value = percent;
        }

        public void SetBaseExp(int exp, int maxExp)
        {
            var percent = maxExp <= 0 ? 0f : exp / (float)Math.Max(1, maxExp);

            if (ExpSlider != null)
                ExpSlider.value = percent;
            if (ExpDisplay != null)
                ExpDisplay.text = $"{exp}/{maxExp} ({percent * 100:F1})%";
        }

        public void SetJobExp(int exp, int maxExp)
        {
            var percent = maxExp <= 0 ? 0f : exp / (float)Math.Max(1, maxExp);

            if (JobExpSlider != null)
                JobExpSlider.value = percent;
            if (JobExpDisplay != null)
                JobExpDisplay.SetText($"{exp}/{maxExp} ({percent * 100:F1})%");
        }
        #endregion
        #region Observers
        public void OnHpSpChanged()
        {
            SetHp(playerState.Hp, playerState.MaxHp);
            SetSp(playerState.Sp, playerState.MaxSp);
        }

        public void OnProgressChanged()
        {
            SetBaseLevelText($"Base Lv: {playerState.BaseLevel,3}");
            SetJobLevelText($"Job Lv: {playerState.JobLevel,3}");
            SetBaseExp(playerState.BaseExp, playerState.BaseMaxExp);
            SetJobExp(playerState.JobExp, playerState.JobMaxExp);
        }

        public void OnTextChanged()
        {
            SetPlayerNameText(playerState.PlayerName);
            SetJobNameText(playerState.JobName);
        }

        public void OnWeightAndZenyChanged()
        {
            var state = PlayerState.Instance;
            SetWeightText($"Weight: {state.CurrentWeight / 10}/{state.MaxWeight / 10}");
            SetZenyText($"Zeny: {state.Zeny:N0}");
        }

        private void Subscribe()
        {
            playerState.OnTextChanged += OnTextChanged;
            playerState.OnHpSPChanged += OnHpSpChanged;
            playerState.OnProgressChanged += OnProgressChanged;
            playerState.OnWeightOrCurrencyChanged += OnWeightAndZenyChanged;
        }

        public void ShowWindow()
        {
            gameObject.SetActive(true);
        }

        public void HideWindow()
        {
            gameObject.SetActive(false);
        }

        public void ToggleVisibility()
        {
            if (gameObject.activeInHierarchy)
                HideWindow();
            else
                ShowWindow();
        }

        #endregion
    }
}