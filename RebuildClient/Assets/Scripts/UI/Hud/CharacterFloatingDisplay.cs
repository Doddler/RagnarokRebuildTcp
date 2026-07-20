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

        private bool isHovering;
        private bool isTargeting;
        private bool hasContent;
        private Transform ownerTransform;
        private float cachedGlueScale = -1f;
        private float cachedZoomScale = -1f;
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
            chatShowsCastName = false;
            cachedGlueScale = -1f;
            cachedZoomScale = -1f;
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
                Close(); //created but never given any content
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
            InvalidatePositions();
        }

        public void CancelCasting()
        {
            if (castBar != null)
            {
                Manager.ReturnCastBar(castBar.gameObject);
                castBar = null;
            }

            //a cast-name bubble goes away with the cast; regular chat stays
            if (chatBubble != null && chatShowsCastName)
            {
                Manager.ReturnChatBubble(chatBubble.gameObject);
                chatBubble = null;
            }

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
        }

        public void HideChatBubbleMessage()
        {
            if (chatBubble == null)
                return;

            Manager.ReturnChatBubble(chatBubble.gameObject);
            chatBubble = null;
            InvalidatePositions();
        }

        public void ShowChatBubbleMessage(string message, float visibleTime = 5f, bool isCastName = false)
        {
            if (chatBubble == null)
            {
                chatBubble = Manager.AttachChatBubble(gameObject);
                InvalidatePositions(); //activate before SetText so TMP can measure the text
            }

            chatShowsCastName = isCastName;
            chatBubble.SetText(message);
            chatEnd = Time.timeSinceLevelLoad + visibleTime;
            InvalidatePositions();
        }

        public void ForceMpBarOn()
        {
            if (mpBar != null)
                return;

            mpBar = Manager.AttachMpBar(gameObject);
            SetBarSize(mpBar);
            InvalidatePositions();
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
            InvalidatePositions();
        }

        public void ForceHpBarOn()
        {
            if (hpBar != null)
                return;

            hpBar = Manager.AttachHpBar(gameObject);
            SetBarSize(hpBar);
            UpdateHp(controllable.Hp, controllable.Hp, false);
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
                SetBarSize(hpBar);
                hpBar.SetProgress(Ratio(oldHp, maxHp));
                InvalidatePositions();
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

        // Lifecycle gate; every attach and detach comes through here. A display only exists while it has
        // content: going empty returns it to the pool, gaining content activates and relayouts it.
        public void InvalidatePositions()
        {
            if (controllable == null)
                return; //already released

            hasContent = namePlate != null || castBar != null || hpBar != null || mpBar != null || chatBubble != null;
            if (!hasContent)
            {
                Close();
                return;
            }

            cachedGlueScale = -1f;
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
        // by the zoom factor (1 unless ScalePlayerDisplayWithZoom is on).
        public void RefreshPositionsIfChanged(float glueScale, float zoomScale)
        {
            if (Mathf.Approximately(cachedGlueScale, glueScale) && Mathf.Approximately(cachedZoomScale, zoomScale))
                return;
            cachedGlueScale = glueScale;
            cachedZoomScale = zoomScale;
            CaptureSpriteHeights();

            LayoutBelowFeet(glueScale, zoomScale);
            LayoutAboveHead(glueScale, zoomScale);
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

            PlaceStacked(castBar?.transform, 0f, anchorY, 1f, zoomScale, ref cursor, ref first);
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
                InvalidatePositions();
            }
        }

        private void AdvanceTimers()
        {
            if (chatBubble != null)
            {
                if (Time.timeSinceLevelLoad > chatEnd)
                {
                    Manager.ReturnChatBubble(chatBubble.gameObject);
                    chatBubble = null;
                    InvalidatePositions();
                }
                else if (chatBubble.RefreshBorderIfNeeded())
                    InvalidatePositions();
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