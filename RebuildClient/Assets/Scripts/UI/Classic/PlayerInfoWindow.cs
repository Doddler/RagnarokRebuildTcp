using Assets.Scripts.PlayerControl;
using RO_Flex_UI.Components;
using RO_Flex_UI.Panels;
using System;
using TMPro;
using UnityEditor;
using UnityEngine;

//FIXME GameConfig does not track this window's position

namespace Assets.Scripts.UI.Classic
{
    public class PlayerInfoWindow : Window, IStyledWindow
    {
        #region References
        [SerializeField] private bool minimized = false;
        [Header("Maximized View")]
        [SerializeField] private TMP_Text playerNameMax;
        [SerializeField] private TMP_Text jobNameMax;
        [SerializeField] private RoSlider hpSlider;
        [SerializeField] private TMP_Text hpValueMax;
        [SerializeField] private TMP_Text hpPerc;
        [SerializeField] private RoSlider spSlider;
        [SerializeField] private TMP_Text spValueMax;
        [SerializeField] private TMP_Text spPerc;
        [SerializeField] private TMP_Text baseLvMax;
        [SerializeField] private RoSlider baseExpSlider;
        [SerializeField] private RO_Flex_UI.Components.TooltipTrigger baseExpTooltip;
        [SerializeField] private TMP_Text jobLvMax;
        [SerializeField] private RoSlider jobExpSlider;
        [SerializeField] private RO_Flex_UI.Components.TooltipTrigger jobExpTooltip;
        [SerializeField] private TMP_Text weight;
        [SerializeField] private TMP_Text zeny;

        [Header("Minimized View")]
        [SerializeField] private TMP_Text playerNameMin;
        [SerializeField] private TMP_Text jobNameMin;
        [SerializeField] private TMP_Text baseLvMin;
        [SerializeField] private TMP_Text jobLvMin;
        [SerializeField] private TMP_Text baseExp;
        [SerializeField] private TMP_Text hpValueMin;
        [SerializeField] private TMP_Text spValueMin;

        [Header("Other references")]
        [SerializeField] private Header header;
        [SerializeField] private SwapPanel swapPanel;
        #endregion

        public bool Minimized => minimized;
        private bool subscribed;
        private PlayerState playerState;

        protected override void Awake()
        {
            base.Awake();
            if (header == null)
                Debug.LogWarning($"[{name}] Missing reference for Header.");

            if (swapPanel == null)
                Debug.LogWarning($"[{name}] Missing reference for Swap Panel.");

            playerState = PlayerState.Instance;
            UiManagerV2.Instance.RegisterWindow(WindowID.PLAYER_INFO, this);
        }

        private void Start()
        {
            Subscribe();
            RefreshFromState();
        }

        private void OnDestroy()
        {
            if (header != null)
                header.OnMinButtonClick.RemoveListener(ToggleMinimize);

            var state = PlayerState.Instance;
            if (state == null || !subscribed)
                return;

            state.OnTextChanged -= OnTextChanged;
            state.OnHpSPChanged -= OnHpSpChanged;
            state.OnProgressChanged -= OnProgressChanged;
            state.OnWeightOrCurrencyChanged -= OnWeightAndZenyChanged;
            subscribed = false;
        }

        public void ToggleMinimize()
        {

            minimized = !minimized;
            swapPanel.GetNextGroup();

            OnTextChanged(); // update player name in header
        }

        #region Getter & Setter
        public void SetPlayerNameText(string text)
        {
            if (playerNameMax != null)
                playerNameMax.text = text;

            if (playerNameMin != null)
                playerNameMin.text = minimized ? text : "Player Information";
        }

        public void SetJobNameText(string text)
        {
            if (jobNameMax != null)
                jobNameMax.text = text;
            if (jobNameMin != null)
                jobNameMin.text = text;
        }

        public void SetBaseLevelText(int value)
        {
            if (baseLvMax != null)
                baseLvMax.SetText($"Base Lv: {value}");
            if (baseLvMin != null)
                baseLvMin.SetText($"Lv: {value} /");
        }

        public void SetJobLevelText(int value)
        {
            if (jobLvMax != null)
                jobLvMax.SetText($"Job Lv: {value}");
            if (jobLvMin != null)
                jobLvMin.SetText($"Lv: {value} /");
        }

        public void SetWeightText(string text)
        {
            if (weight != null)
                weight.text = text;
        }

        public void SetZenyText(string text)
        {
            if (zeny != null)
                zeny.text = text;
        }

        public void SetHp(int hp, int maxHp)
        {
            maxHp = Math.Max(1, maxHp);
            var percent = hp / (float)maxHp;

            if (hpPerc != null)
                hpPerc.text = $"{Math.Round(percent * 100)}%";
            if (hpSlider != null)
                hpSlider.value = percent;
            if (hpValueMax != null)
                hpValueMax.text = $"{hp} / {maxHp}";
            if (hpValueMin != null)
                hpValueMin.text = $"HP: {hp} / {maxHp} | ";
        }

        public void SetSp(int sp, int maxSp)
        {
            maxSp = Math.Max(1, maxSp);
            var percent = sp / (float)maxSp;

            if (spPerc != null)
                spPerc.text = $"{Math.Round(percent * 100)}%";
            if (spSlider != null)
                spSlider.value = percent;
            if (spValueMax != null)
                spValueMax.text = $"{sp} / {maxSp}";
            if (spValueMin != null)
                spValueMin.text = $"SP: {sp} / {maxSp}";
        }

        public void SetBaseExp(int exp, int maxExp)
        {
            var percent = maxExp <= 0 ? 0f : exp / (float)Math.Max(1, maxExp);
            if (baseExpSlider != null)
                baseExpSlider.value = percent;

            var text = $"{Mathf.Floor(percent * 10000f) / 100f:0.00}%";
            if (baseExp != null)
                baseExp.text = text;
            if (baseExpTooltip != null)
                baseExpTooltip.SetText(text);
        }

        public void SetJobExp(int exp, int maxExp)
        {
            var percent = maxExp <= 0 ? 0f : exp / (float)Math.Max(1, maxExp);
            if (jobExpSlider != null)
                jobExpSlider.value = percent;
            if (jobExpTooltip != null)
                jobExpTooltip.SetText($"{Mathf.Floor(percent * 10000f) / 100f:0.00}%");
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
            SetBaseLevelText(playerState.BaseLevel);
            SetJobLevelText(playerState.JobLevel);
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
            if (header != null)
                header.OnMinButtonClick.AddListener(ToggleMinimize);

            var state = PlayerState.Instance;
            if (state == null || subscribed)
                return;

            state.OnTextChanged += OnTextChanged;
            state.OnHpSPChanged += OnHpSpChanged;
            state.OnProgressChanged += OnProgressChanged;
            state.OnWeightOrCurrencyChanged += OnWeightAndZenyChanged;
            subscribed = true;
        }

        public void RefreshFromState()
        {
            //FIXME figure out cleaner way to get initial state and swap correctly
            if (minimized) swapPanel.GetNextGroup();

            OnTextChanged();
            OnHpSpChanged();
            OnProgressChanged();
            OnWeightAndZenyChanged();
        }
        #endregion
    }
}