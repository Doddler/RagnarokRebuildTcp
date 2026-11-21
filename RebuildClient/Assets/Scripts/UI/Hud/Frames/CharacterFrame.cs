using System;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Hud
{
    //Shared by the player, target and party frames. Needs a raycast target graphic on the same object.
    public class CharacterFrame : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        //portrait on the right; applied on Awake and while editing
        public bool Mirrored;
        public RectTransform PortraitArea;
        public RectTransform Bars;
        [SerializeField, Range(0f, 1f)] private float titleSplit = 0.7f;

        public UiPlayerSprite Portrait;
        public TextMeshProUGUI NameText;
        //the level, or a party member's map
        public TextMeshProUGUI InfoText;
        public Slider HpBar;
        public TextMeshProUGUI HpText;
        public Slider SpBar;
        public TextMeshProUGUI SpText;
        public Image HpFill;
        public GameObject LeaderStar;
        //off unless the panel shows exp
        public Slider BaseExpBar;
        public Slider JobExpBar;

        public Color HpHighColor = new Color32(76, 160, 56, 255);
        public Color HpMidColor = new Color32(220, 150, 40, 255);
        public Color HpLowColor = new Color32(205, 55, 55, 255);
        public Image[] Borders;
        public Color HighlightColor = new Color32(255, 214, 80, 255);

        //null while skills can't be cast on the character
        [NonSerialized] public Func<ServerControllable> GetCharacter;
        public event Action RightClicked;

        public ServerControllable Character => GetCharacter?.Invoke();

        //mid color at half or below, low color at a quarter or below
        private const float HpMidThreshold = 0.5f;
        private const float HpLowThreshold = 0.25f;
        private const float TitleInset = 5f;
        private const float CornerInset = 1.5f;
        //the portrait and bars share one border edge
        private const float BorderOverlap = 3f;

        public float TitleSplit
        {
            get => titleSplit;
            set
            {
                titleSplit = value;
                ApplyTitleLayout();
            }
        }

        public void SetName(string characterName) => NameText.text = characterName;

        public void SetInfo(string info) => InfoText.text = info;

        public void SetLeader(bool isLeader) => LeaderStar.SetActive(isLeader);

        private void Awake() => ApplyLayout();

        private void ApplyLayout()
        {
            var side = Mirrored ? 1f : 0f;
            PlaceOnSide(PortraitArea, side, 0.5f, 0f);
            PlaceOnSide(Bars, side, 0.5f, PortraitArea.sizeDelta.x - BorderOverlap);
            foreach (RectTransform row in Bars)
                PlaceOnSide(row, side, 1f, 0f);

            Portrait.ViewDirection = Mirrored ? Direction.SouthWest : Direction.SouthEast;

            var star = (RectTransform)LeaderStar.transform;
            star.anchorMin = star.anchorMax = new Vector2(Mirrored ? 1f : 0f, 1f);
            star.anchoredPosition = new Vector2(Mirrored ? -CornerInset : CornerInset, -CornerInset);

            ApplyTitleLayout();
        }

        //anchors to the parent's side (0 left, 1 right) at a height (0 bottom, 1 top), keeping the vertical offset
        private static void PlaceOnSide(RectTransform rect, float side, float height, float inset)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(side, height);
            rect.pivot = new Vector2(side, height);
            rect.anchoredPosition = new Vector2(side > 0f ? -inset : inset, rect.anchoredPosition.y);
        }

        private void ApplyTitleLayout()
        {
            if (Mirrored)
            {
                PlaceTitleText(InfoText, 0f, 1f - TitleSplit, false);
                PlaceTitleText(NameText, 1f - TitleSplit, 1f, true);
            }
            else
            {
                PlaceTitleText(NameText, 0f, TitleSplit, false);
                PlaceTitleText(InfoText, TitleSplit, 1f, true);
            }
        }

        private static void PlaceTitleText(TextMeshProUGUI text, float from, float to, bool againstRight)
        {
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(from, rect.anchorMin.y);
            rect.anchorMax = new Vector2(to, rect.anchorMax.y);
            rect.pivot = new Vector2(againstRight ? 1f : 0f, rect.pivot.y);
            rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);
            rect.sizeDelta = new Vector2(0f, rect.sizeDelta.y);
            text.horizontalAlignment = againstRight ? HorizontalAlignmentOptions.Right : HorizontalAlignmentOptions.Left;
            text.margin = againstRight ? new Vector4(0f, 0f, TitleInset, 0f) : new Vector4(TitleInset, 0f, 0f, 0f);
        }

        //an overkill can leave hp below 0
        public void SetHp(int hp, int maxHp)
        {
            SetBar(HpBar, HpText, Mathf.Max(hp, 0), maxHp);
            var fraction = HpBar.value;
            HpFill.color = fraction > HpMidThreshold ? HpHighColor : fraction > HpLowThreshold ? HpMidColor : HpLowColor;
        }

        public void SetSp(int sp, int maxSp) => SetBar(SpBar, SpText, sp, maxSp);

        //SetText avoids building a string on every hp change
        private static void SetBar(Slider bar, TextMeshProUGUI text, int value, int max)
        {
            bar.value = max > 0 ? (float)value / max : 0f;
            text.SetText("{0} / {1}", value, max);
        }

        public void SetExp(float? baseProgress, float? jobProgress)
        {
            SetExpBar(BaseExpBar, baseProgress);
            SetExpBar(JobExpBar, jobProgress);
        }

        //the row is hidden at the highest level
        private static void SetExpBar(Slider bar, float? progress)
        {
            bar.transform.parent.gameObject.SetActive(progress.HasValue);
            if (progress.HasValue)
                bar.value = progress.Value;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            var cameraFollower = CameraFollower.Instance;
            cameraFollower.HoveredFrame = this;
            cameraFollower.SkillOnCursorChanged += RefreshHighlight;
            RefreshHighlight();
        }

        public void OnPointerExit(PointerEventData eventData) => EndHover();

        //pooled party rows can be disabled while hovered
        private void OnDisable() => EndHover();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                RightClicked?.Invoke();
        }

        private void EndHover()
        {
            SetHighlighted(false);
            var cameraFollower = CameraFollower.Instance;
            if (cameraFollower == null)
                return;

            cameraFollower.SkillOnCursorChanged -= RefreshHighlight;
            if (cameraFollower.HoveredFrame == this)
                cameraFollower.HoveredFrame = null;
        }

        private void RefreshHighlight() =>
            SetHighlighted(CameraFollower.Instance.HasAllySkillOnCursor && Character != null);

        //the tint multiplies with each border's own color
        private void SetHighlighted(bool highlighted)
        {
            foreach (var border in Borders)
                border.canvasRenderer.SetColor(highlighted ? HighlightColor : Color.white);
        }

        //the layout waits a tick, as OnValidate can't change layout
        private void OnValidate()
        {
            if (HpFill != null)
                HpFill.color = HpHighColor;

#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(this))
                return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && PortraitArea != null && Bars != null)
                    ApplyLayout();
            };
#endif
        }
    }
}
