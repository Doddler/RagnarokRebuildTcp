using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.UI.ConfigWindow;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.UI.Hud
{
    public class CharacterFloatingDisplay : MonoBehaviour
    {
        private ServerControllable controllable;
        private TextMeshProUGUI namePlate;
        private SliderBar castBar;
        private SliderBar hpBar;
        private SliderBar mpBar;
        private CharacterChat chatBubble;

        public CharacterOverlayManager Manager;

        private string characterName;
        private bool isPlayer;

        private float castStart;
        private float castEnd;
        private float chatEnd;

        private bool isHovering;
        private bool isTargeting;
        private float cachedUIScale = -1f;
        private bool cachedSitting;
        private float rawStandingHeightPx = 0f;
        private float rawSittingHeightPx = 0f;
        private float rawSitDepthPx = 0f;
        private Canvas overlayCanvas;

        public void Close()
        {
            if (Manager == null)
                return;
            Manager.ReturnFloatingDisplay(this);
        }

        public void ReturnToPool()
        {
            if (Manager == null)
            {
                Destroy(gameObject); //it's all fucked
                return;
            }

            if (namePlate != null) Manager.ReturnNamePlate(namePlate.gameObject);
            if (castBar != null) Manager.ReturnCastBar(castBar.gameObject);
            if (hpBar != null) Manager.ReturnHpBar(hpBar.gameObject);
            if (mpBar != null) Manager.ReturnMpBar(mpBar.gameObject);
            if (chatBubble != null) Manager.ReturnChatBubble(chatBubble.gameObject);

            namePlate = null;
            castBar = null;
            hpBar = null;
            mpBar = null;
            chatBubble = null;
            controllable = null;

            // clear per-owner transient state; displays are pooled and reused
            isHovering = false;
            isTargeting = false;
            cachedUIScale = -1f;
        }

        // Sets the owning entity; called when the display is attached, before any content or visibility update.
        public void AttachTo(ServerControllable owner)
        {
            controllable = owner;
            isPlayer = owner.CharacterType == CharacterType.Player;
        }

        public void UpdateName(string newName) => characterName = newName;

        public void HoverNamePlate()
        {
            ShowNamePlate();
            isHovering = true;
        }

        public void TargetingNamePlate()
        {
            ShowNamePlate();
            isTargeting = true;
        }

        public void EndHoverNamePlate()
        {
            isHovering = false;
            if (isTargeting)
                return; //we still need this plate to show
            HideNamePlate();
        }

        public void EndTargetingNamePlate()
        {
            isTargeting = false;
            if (isHovering)
                return; //we still need this plate to show
            HideNamePlate();
        }

        private void ShowNamePlate()
        {
            if (namePlate != null)
                return;
            namePlate = Manager.AttachNamePlate(gameObject);
            namePlate.text = characterName;
            gameObject.SetActive(true);
            InvalidatePositions();
        }

        private void HideNamePlate()
        {
            if (namePlate == null)
                return;
            Manager.ReturnNamePlate(namePlate.gameObject);
            namePlate = null;
            InvalidatePositions();
        }

        public void StartCasting(float castTime)
        {
            if (castBar == null)
                castBar = Manager.AttachCastBar(gameObject);

            castBar.SetProgress(0);
            castStart = Time.timeSinceLevelLoad;
            castEnd = castStart + castTime;
            castBar.gameObject.SetActive(true);
            gameObject.SetActive(true);
            InvalidatePositions();
        }

        public void CancelCasting()
        {
            if (castBar != null)
            {
                Manager.ReturnCastBar(castBar.gameObject);
                castBar = null;
            }

            //this is the biggest hack I've ever seen. End the chat bubble if it's showing monster cast name
            if (chatBubble != null && chatBubble.TextObject.text.Contains("<color=#FF8888>"))
            {
                Manager.ReturnChatBubble(chatBubble.gameObject);
                chatBubble = null;
            }

            //controllable.StopCastingAnimation();

            InvalidatePositions();
        }

        public void ExtendCasting(float addTime)
        {
            if (castBar == null)
                return;

            var len = castEnd - castStart;
            var pos = Time.timeSinceLevelLoad - castStart;
            var remain = len - pos;
            var passed = len - remain;

            var addPercent = (remain + addTime) / remain;
            var subStart = (passed * addPercent) - passed;

            castStart -= subStart;
            castEnd += addTime;

            //castBar.SetProgress(pos / end);
        }

        public void HideChatBubbleMessage()
        {
            if (chatBubble == null)
                return;

            Manager.ReturnChatBubble(chatBubble.gameObject);
            chatBubble = null;
            InvalidatePositions();
        }

        public void ShowChatBubbleMessage(string message, float visibleTime = 5f)
        {
            if (chatBubble == null)
                chatBubble = Manager.AttachChatBubble(gameObject);

            chatBubble.SetText(message);
            chatEnd = Time.timeSinceLevelLoad + visibleTime;
            gameObject.SetActive(true);
            InvalidatePositions();
        }

        public void HideMpBar()
        {
            if (mpBar == null)
                return;

            Manager.ReturnMpBar(mpBar.gameObject);
            mpBar = null;
            InvalidatePositions();
        }

        public void ForceMpBarOn()
        {
            if (mpBar != null)
                return;

            mpBar = Manager.AttachMpBar(gameObject);
            gameObject.SetActive(true);
            InvalidatePositions();
        }

        public void UpdateMp(int mp)
        {
            if (mpBar == null)
                ForceMpBarOn();

            mpBar.SetProgress(Ratio(mp, controllable.MaxSp));
            SetBarSize(mpBar);
        }

        // Progress as a 0..1 fraction, guarding against a zero max.
        private static float Ratio(int value, int max) => max > 0 ? (float)value / max : 0f;

        private void SetBarSize(SliderBar bar) =>
            ((RectTransform)bar.transform).sizeDelta = new Vector2(isPlayer ? 100f : 90f, 10f);
        
        public void HideHpBar()
        {
            if (hpBar == null)
                return;
            Manager.ReturnHpBar(hpBar.gameObject);
            hpBar = null;
            InvalidatePositions();
        }

        public void ForceHpBarOn()
        {
            if (hpBar != null)
                return;

            hpBar = Manager.AttachHpBar(gameObject);
            gameObject.SetActive(true);
            UpdateHp(controllable.Hp, controllable.Hp, false); // also calls RefreshHpBarDetails
            InvalidatePositions();
        }

        public void UpdateHp(int oldHp, int hp, bool animate = true)
        {
            var maxHp = controllable.MaxHp;
            if (hpBar == null)
            {
                if ((hp == maxHp && oldHp == hp) || (!isPlayer && !GameConfig.Data.ShowMonsterHpBars))
                    return;
                hpBar = Manager.AttachHpBar(gameObject);
                hpBar.SetProgress(Ratio(oldHp, maxHp));
                InvalidatePositions();
            }

            var progress = Ratio(hp, maxHp);
            if (oldHp >= 0)
                hpBar.SetProgress(progress, !animate);
            else
                hpBar.SetProgress(progress);

            // Debug.Log($"Update HP on {characterName}: {hp}/{maxHp}");
            gameObject.SetActive(true);
            RefreshHpBarDetails();
        }

        // Offsets are divided by MasterUIScale (not the canvas scaleFactor) when applied, so they cancel only
        // the UI-scale component and stay glued to the sprite across resolutions. Negative gap = overlap.
        private const float HpBarOffsetPx = 25f; // feet to top of the below-feet stack
        private const float HpToMpGap = -2f;
        private const float MpToNameGap = 1f;

        private const float AboveHeadPaddingPx = 15f;
        private const float AboveHeadMinPx = 40f; // floor so tiny sprites still clear
        private const float PlayerHeadExtraPx = 50f; // body StandingHeight excludes the head/headgear sprites
        private const float SitHeadAdjustPx = 5f; // the head tucks lower when seated, so trim the head clearance a bit

        private bool IsSitting => isPlayer && controllable.SpriteAnimator?.State == SpriteState.Sit;

        private void CaptureSpriteHeights()
        {
            var data = controllable.SpriteAnimator?.SpriteData;
            if (data == null) return;
            rawStandingHeightPx = data.StandingHeight;
            rawSittingHeightPx = data.SittingHeight;
            rawSitDepthPx = data.SitDepth;
        }

        private float ComputeAboveHeadPx()
        {
            // Seated players use their (lower) sitting height; fall back to standing if it wasn't baked.
            var rawHeight = IsSitting && rawSittingHeightPx > 0 ? rawSittingHeightPx : rawStandingHeightPx;
            var px = rawHeight * 1.5f + AboveHeadPaddingPx;
            if (isPlayer)
            {
                px += PlayerHeadExtraPx;
                if (IsSitting) px -= SitHeadAdjustPx;
            }
            else if (px < AboveHeadMinPx)
                px = AboveHeadMinPx;
            return px;
        }

        // Recompute immediately so a newly attached/removed element doesn't render a frame at its template spot.
        private void InvalidatePositions()
        {
            cachedUIScale = -1f;
            RefreshPositionsIfChanged();
        }

        public void RefreshPositionsIfChanged()
        {
            if (overlayCanvas == null) overlayCanvas = Manager.GetComponent<Canvas>();
            // Gate on scaleFactor: it only changes on canvas rebuild (slider release / resolution change).
            var currentScale = overlayCanvas.scaleFactor;
            if (Mathf.Approximately(cachedUIScale, currentScale)) return;
            cachedUIScale = currentScale;
            cachedSitting = IsSitting;
            CaptureSpriteHeights();

            var invScale = 1f / Mathf.Max(GameConfig.Data.MasterUIScale, 0.01f);
            LayoutBelowFeet(invScale);
            LayoutAboveHead(invScale);
        }

        // HP bar, MP bar, name plate stacked downward below the feet; the topmost present one sits at the anchor.
        private void LayoutBelowFeet(float invScale)
        {
            var offset = HpBarOffsetPx;
            if (IsSitting) // only lower enough to clear the seated body's dip below the feet (+5 margin)
                offset = Mathf.Max(offset, rawSitDepthPx * 1.5f + 5f);
            var anchorY = -offset * invScale;
            var cursor = 0f;
            var first = true;

            PlaceStacked(hpBar?.transform, 0f, anchorY, -1f, ref cursor, ref first);
            PlaceStacked(mpBar?.transform, HpToMpGap, anchorY, -1f, ref cursor, ref first);
            PlaceStacked(namePlate?.transform, MpToNameGap, anchorY, -1f, ref cursor, ref first);
        }

        // Cast bar then chat bubble stacked upward above the head (mirror of LayoutBelowFeet).
        private void LayoutAboveHead(float invScale)
        {
            if (castBar == null && chatBubble == null) return;

            var anchorY = ComputeAboveHeadPx() * invScale;
            var cursor = 0f;
            var first = true;

            PlaceStacked(castBar?.transform, 0f, anchorY, 1f, ref cursor, ref first);
            PlaceStacked(chatBubble?.transform, 0f, anchorY, 1f, ref cursor, ref first);
        }

        // Stacks one element in the given direction (+1 = up, -1 = down). The first present element's near edge
        // sits at the anchor; each later one is offset by its gap from the previous element's far edge (negative
        // gap = overlap). Pivot-aware: localPosition maps to the pivot, so we derive it from the bottom edge.
        private static void PlaceStacked(Transform t, float gap, float anchorY, float direction, ref float cursor, ref bool first)
        {
            if (t == null) return;
            var rt = (RectTransform)t;
            var height = rt.sizeDelta.y;
            var nearEdge = first ? anchorY : cursor + direction * gap;
            var farEdge = nearEdge + direction * height;
            rt.localPosition = new Vector3(0, Mathf.Min(nearEdge, farEdge) + rt.pivot.y * height, 0);
            cursor = farEdge;
            first = false;
        }

        private static readonly Color32 AllyHpColor = new Color32(0x6C, 0xEA, 0x45, 255);          // self / party
        private static readonly Color32 OtherPlayerHpColor = new Color32(0xEA, 0xEA, 0x35, 255);  // non-party players
        private static readonly Color32 MonsterHpColor = new Color32(0xC8, 0x45, 0xEA, 255);

        public void RefreshHpBarDetails()
        {
            if (hpBar == null)
                return;

            if (isPlayer)
            {
                hpBar.SetColor(controllable.IsPartyMember || controllable.IsMainCharacter
                    ? AllyHpColor : OtherPlayerHpColor);
                SetBarSize(hpBar);
            }
            else if (GameConfig.Data.ShowMonsterHpBars)
            {
                hpBar.SetColor(MonsterHpColor);
                SetBarSize(hpBar);
            }
            else
            {
                Manager.ReturnHpBar(hpBar.gameObject);
                hpBar = null;
            }
        }

        public void Update()
        {
            if (IsSitting != cachedSitting)
                InvalidatePositions();

            if (chatBubble != null)
            {
                if (Time.timeSinceLevelLoad > chatEnd)
                {
                    Manager.ReturnChatBubble(chatBubble.gameObject);
                    chatBubble = null;
                }
                else
                    chatBubble.RefreshBorder();
            }

            if (castBar != null)
            {
                if (Time.timeSinceLevelLoad > castEnd)
                    CancelCasting();
                else
                {
                    var pos = Time.timeSinceLevelLoad - castStart;
                    var end = castEnd - castStart;
                    castBar.SetProgress(pos / end);
                }
            }
        }
    }
}