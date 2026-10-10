using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.PlayerControl;
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

        private bool isPlayer;

        private float castStart;
        private float castEnd;
        private float chatEnd;
        private bool chatShowsCastName;

        //keeps the chat bubble from dropping into the cast bar's spot once the cast bar itself is gone;
        //cleared only when the bubble that was up alongside the cast is itself cleared
        private bool castBarGapReserved;

        private bool isHovering;
        private bool isTargeting;
        private bool hasContent;
        private float emptyAt = -1f;
        private Transform ownerTransform;
        private float cachedGlueScale = -1f;
        private float cachedZoomScale = -1f;
        private bool belowFeetDirty = true;
        private bool aboveHeadDirty = true;
        private float rawStandingHeightPx;
        private float rawSittingHeightPx;
        private float rawSitDepthPx;

        public void Close()
        {
            if (Manager == null || controllable == null) //already pooled, or orphaned during teardown
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

            //stale handles must not reach a pooled display
            if (controllable != null && controllable.FloatingDisplay == this)
                controllable.FloatingDisplay = null;
            controllable = null;
            ownerTransform = null;

            isHovering = false;
            isTargeting = false;
            hasContent = false;
            emptyAt = -1f;
            chatShowsCastName = false;
            castBarGapReserved = false;
            cachedGlueScale = -1f;
            cachedZoomScale = -1f;
            belowFeetDirty = true;
            aboveHeadDirty = true;
        }

        public void AttachTo(ServerControllable owner)
        {
            controllable = owner;
            ownerTransform = owner.transform;
            isPlayer = owner.CharacterType == CharacterType.Player;
            Rect = (RectTransform)transform;
            cachedGlueScale = -1f;
            cachedZoomScale = -1f;

            if (owner.IsMainCharacter)
                Manager.RegisterMainCharacterDisplay(this); //drawn over everyone else's
        }

        public RectTransform Rect { get; private set; }

        // Driven by CharacterOverlayManager so camera, canvas and scale are resolved once per frame.
        public void Tick(Camera camera, RectTransform canvasRect, float glueScale, float zoomScale)
        {
            AdvanceTimers();
            if (controllable == null)
                return; //expiring its last element released this display

            if (!hasContent)
            {
                if (Time.timeSinceLevelLoad - emptyAt >= Manager.LingerDuration)
                    Close(); //stayed empty past the linger grace period
                return;
            }

            UpdateScreenPosition(camera, canvasRect);
            RefreshPositionsIfChanged(glueScale, zoomScale);
        }

        private void UpdateScreenPosition(Camera camera, RectTransform canvasRect)
        {
            var screenPos = camera.WorldToScreenPoint(ownerTransform.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out var localPoint);
            Rect.anchoredPosition = localPoint;
        }

        public void HoverNamePlate(string name)
        {
            ShowNamePlate(name);
            isHovering = true;
        }

        public void TargetingNamePlate(string name)
        {
            ShowNamePlate(name);
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

        private void ShowNamePlate(string name)
        {
            if (namePlate != null)
                return; //already visible, keeps its existing text
            namePlate = Manager.AttachNamePlate(gameObject);
            namePlate.text = name;
            InvalidateBelowFeet();
        }

        private void HideNamePlate()
        {
            if (namePlate == null)
                return;
            Manager.ReturnNamePlate(namePlate.gameObject);
            namePlate = null;
            InvalidateBelowFeet();
        }

        public void StartCasting(float castTime)
        {
            if (castBar == null)
                castBar = Manager.AttachCastBar(gameObject);

            castBar.SetProgress(0);
            castStart = Time.timeSinceLevelLoad;
            castEnd = castStart + castTime;
            castBar.gameObject.SetActive(true);
            castBarGapReserved = true;
            InvalidateAboveHead();
        }

        public void CancelCasting()
        {
            if (castBar != null)
            {
                Manager.ReturnCastBar(castBar.gameObject);
                castBar = null;
            }

            //a cast-name bubble goes away with the cast; regular chat stays (and keeps the gap reserved)
            if (chatBubble != null && chatShowsCastName)
                ClearChatBubble();

            InvalidateAboveHead();
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
        }

        public void HideChatBubbleMessage()
        {
            if (chatBubble == null)
                return;

            ClearChatBubble();
            InvalidateAboveHead();
        }

        //removes the chat bubble and resets everything that describes it - anywhere the bubble goes
        //away should go through here so those fields can't drift out of sync with each other
        private void ClearChatBubble()
        {
            Manager.ReturnChatBubble(chatBubble.gameObject);
            chatBubble = null;
            chatShowsCastName = false;
            castBarGapReserved = false;
        }

        // Clears a leftover above-head slot reserved for a since-finished cast; call before showing an
        // unrelated message (e.g. regular chat). No-op while a cast is active, since the message is
        // then sitting above a real cast bar and should keep the slot once that bar disappears too.
        public void ClearCastReservation()
        {
            if (castBar == null)
                castBarGapReserved = false;
        }

        public void ShowChatBubbleMessage(string message, float visibleTime = 5f, bool isCastName = false)
        {
            if (chatBubble == null)
            {
                chatBubble = Manager.AttachChatBubble(gameObject);
                InvalidateAboveHead(); //activate before SetText so TMP can measure the text
            }

            chatShowsCastName = isCastName;
            chatBubble.SetText(message);
            chatEnd = Time.timeSinceLevelLoad + visibleTime;
            InvalidateAboveHead();
        }

        public void ForceMpBarOn()
        {
            if (mpBar != null)
                return;

            mpBar = Manager.AttachMpBar(gameObject);
            SetBarSize(mpBar);
            InvalidateBelowFeet();
        }

        public void UpdateMp(int mp)
        {
            if (mpBar == null)
                ForceMpBarOn();

            mpBar.SetProgress(Ratio(mp, controllable.MaxSp));
        }

        private static float Ratio(int value, int max) => max > 0 ? (float)value / max : 0f;

        // Set once at attach; writing sizeDelta on every hp update would dirty the canvas layout.
        private void SetBarSize(SliderBar bar) =>
            ((RectTransform)bar.transform).sizeDelta = new Vector2(isPlayer ? 100f : 90f, 10f);
        
        public void HideHpBar()
        {
            if (hpBar == null)
                return;
            Manager.ReturnHpBar(hpBar.gameObject);
            hpBar = null;
            InvalidateBelowFeet();
        }

        public void ForceHpBarOn()
        {
            if (hpBar != null)
                return;

            hpBar = Manager.AttachHpBar(gameObject);
            SetBarSize(hpBar);
            UpdateHp(controllable.Hp, controllable.Hp, false);
            InvalidateBelowFeet();
        }

        public void UpdateHp(int oldHp, int hp, bool animate = true)
        {
            var maxHp = controllable.MaxHp;
            if (hpBar == null)
            {
                if ((hp == maxHp && oldHp == hp) || (!isPlayer && !GameConfig.Data.ShowMonsterHpBars))
                    return;
                hpBar = Manager.AttachHpBar(gameObject);
                SetBarSize(hpBar);
                hpBar.SetProgress(Ratio(oldHp, maxHp));
                InvalidateBelowFeet();
            }

            var progress = Ratio(hp, maxHp);
            hpBar.SetProgress(progress, !animate);

            RefreshHpBarDetails();
        }

        // Below-feet stack offsets, in sprite pixels. Negative gap = overlap.
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

        // Lifecycle gate for the below-feet stack (HP/MP bar, name plate); every attach/detach of those
        // comes through here. Above-head has its own independent counterpart - see InvalidateAboveHead.
        public void InvalidateBelowFeet()
        {
            belowFeetDirty = true;
            InvalidateContent();
        }

        // Lifecycle gate for the above-head stack (cast bar, chat bubble); see InvalidateBelowFeet.
        public void InvalidateAboveHead()
        {
            aboveHeadDirty = true;
            InvalidateContent();
        }

        // Going empty starts a linger timer (checked in Tick) instead of releasing immediately; gaining
        // content activates the display and relayouts whichever stack was just marked dirty.
        private void InvalidateContent()
        {
            if (controllable == null)
                return; //already released

            hasContent = namePlate != null || castBar != null || hpBar != null || mpBar != null || chatBubble != null;
            if (!hasContent)
            {
                if (emptyAt < 0f)
                    emptyAt = Time.timeSinceLevelLoad; //bridges brief gaps without releasing back to the pool
                return;
            }

            emptyAt = -1f;
            var cf = CameraFollower.Instance;
            if (!gameObject.activeSelf)
            {
                //position immediately so it can't draw a frame wherever the pool left it
                gameObject.SetActive(true);
                UpdateScreenPosition(cf.Camera, (RectTransform)cf.UiCanvas.transform);
            }
            RefreshPositionsIfChanged(cf.OverlayGlueScale, cf.OverlayRootScale);
        }

        // Anchors use the glue scale to stay pinned to the sprite's feet/head; elements themselves scale
        // by the zoom factor (1 unless ScalePlayerDisplayWithZoom is on). A scale change relayouts both
        // stacks; otherwise each stack only relayouts if something was actually attached/detached from it.
        public void RefreshPositionsIfChanged(float glueScale, float zoomScale)
        {
            var scaleChanged = !Mathf.Approximately(cachedGlueScale, glueScale) || !Mathf.Approximately(cachedZoomScale, zoomScale);
            if (!scaleChanged && !belowFeetDirty && !aboveHeadDirty)
                return;

            cachedGlueScale = glueScale;
            cachedZoomScale = zoomScale;
            CaptureSpriteHeights();

            if (scaleChanged || belowFeetDirty)
            {
                LayoutBelowFeet(glueScale, zoomScale);
                belowFeetDirty = false;
            }

            if (scaleChanged || aboveHeadDirty)
            {
                LayoutAboveHead(glueScale, zoomScale);
                aboveHeadDirty = false;
            }
        }

        // HP bar, MP bar, name plate stacked downward below the feet; the topmost present one sits at the anchor.
        private void LayoutBelowFeet(float glueScale, float zoomScale)
        {
            var offset = HpBarOffsetPx;
            if (IsSitting && GameConfig.Data.AdjustOverlayWhenSitting) //clear the seated body's dip below the feet
                offset = Mathf.Max(offset, rawSitDepthPx * 1.5f + 5f);
            var anchorY = -offset * glueScale;
            var cursor = 0f;
            var first = true;

            PlaceStacked(hpBar?.transform, 0f, anchorY, -1f, zoomScale, ref cursor, ref first);
            PlaceStacked(mpBar?.transform, HpToMpGap, anchorY, -1f, zoomScale, ref cursor, ref first);
            PlaceStacked(namePlate?.transform, MpToNameGap, anchorY, -1f, zoomScale, ref cursor, ref first);
        }

        // Cast bar then chat bubble stacked upward above the head (mirror of LayoutBelowFeet).
        private void LayoutAboveHead(float glueScale, float zoomScale)
        {
            if (castBar == null && chatBubble == null) return;

            var anchorY = ComputeAboveHeadPx() * glueScale;
            var cursor = 0f;
            var first = true;

            if (castBar != null)
                PlaceStacked(castBar.transform, 0f, anchorY, 1f, zoomScale, ref cursor, ref first);
            else if (castBarGapReserved && chatBubble != null)
            {
                //cast bar is gone but its slot stays reserved until the bubble above it clears
                var reservedHeight = ((RectTransform)Manager.CastBarTemplate.transform).sizeDelta.y * zoomScale;
                cursor = anchorY + reservedHeight;
                first = false;
            }

            PlaceStacked(chatBubble?.transform, 0f, anchorY, 1f, zoomScale, ref cursor, ref first);
        }

        // Stacks one element in the given direction (+1 up, -1 down): the first sits at the anchor, each
        // later one is offset by its gap from the previous element's far edge.
        private static void PlaceStacked(Transform t, float gap, float anchorY, float direction, float zoomScale, ref float cursor, ref bool first)
        {
            if (t == null) return;
            var rt = (RectTransform)t;
            rt.localScale = new Vector3(zoomScale, zoomScale, zoomScale);
            var height = rt.sizeDelta.y * zoomScale;
            var nearEdge = first ? anchorY : cursor + direction * gap * zoomScale;
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
                hpBar.SetColor(controllable.IsPartyMember || controllable.IsMainCharacter
                    ? AllyHpColor : OtherPlayerHpColor);
            else if (GameConfig.Data.ShowMonsterHpBars)
                hpBar.SetColor(MonsterHpColor);
            else
            {
                Manager.ReturnHpBar(hpBar.gameObject);
                hpBar = null;
                InvalidateBelowFeet();
            }
        }

        private void AdvanceTimers()
        {
            if (chatBubble != null)
            {
                if (Time.timeSinceLevelLoad > chatEnd)
                {
                    ClearChatBubble();
                    InvalidateAboveHead();
                }
                else if (chatBubble.RefreshBorderIfNeeded())
                    InvalidateAboveHead();
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