using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Hud
{
    //Expanded while typing it shows the whole log, scrollable; otherwise recent lines that fade out.
    //A scroll position of 0 is the bottom.
    [RequireComponent(typeof(LayoutElement))]
    public class ChatLog : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public ScrollRect Scroll;
        public TextMeshProUGUI Text;
        public Image Background;
        public CanvasGroup Scrollbar;
        public LayoutElement Layout;
        public RectTransform ChatBox;
        public float ScrollbarFadeTime = 0.2f;
        //shown fully this long, then fades over MessageFadeTime
        public float MessageShowTime = 7f;
        public float MessageFadeTime = 3f;
        public int MaxMessages = 100;

        private const float BottomTolerance = 1f;
        private const float FadeInterval = 1f / 30f;

        //oldest first; shown from firstShown on
        private readonly List<(string Text, float Time)> messages = new();
        private int firstShown;
        private readonly List<int> shownStarts = new();
        private readonly StringBuilder builder = new();
        private bool isExpanded;
        private bool isHovered;
        //rebuilt once in LateUpdate for all of a frame's changes
        private bool needsRefresh;
        private bool refreshToBottom;
        private float nextFadeTime;

        private bool IsAtBottom => Scroll.content.anchoredPosition.y >= -BottomTolerance;
        private float MessageLifetime => MessageShowTime + MessageFadeTime;

        public void Add(string text)
        {
            messages.Add((text, Time.unscaledTime));
            if (messages.Count > MaxMessages)
                messages.RemoveAt(0);
            QueueRefresh(false);
        }

        public void Clear()
        {
            messages.Clear();
            QueueRefresh(true);
        }

        public void SetSize(float width, int lines)
        {
            var face = Text.font.faceInfo;
            //TMP's line spacing is in hundredths of the font size
            var lineHeight = (face.lineHeight / face.pointSize + Text.lineSpacing * 0.01f) * Text.fontSize;
            Layout.preferredHeight = lines * lineHeight + Text.margin.w;
            ChatBox.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        public void SetExpanded(bool expanded)
        {
            isExpanded = expanded;
            Background.enabled = expanded;
            Scroll.enabled = expanded;
            Scrollbar.interactable = Scrollbar.blocksRaycasts = expanded;
            QueueRefresh(true);
        }

        private void QueueRefresh(bool toBottom)
        {
            needsRefresh = true;
            refreshToBottom |= toBottom;
        }

        //a scrolled-up log keeps its place unless told otherwise; a collapsed one stays at the bottom
        private void Refresh(bool toBottom)
        {
            var content = Scroll.content;
            var stayAtBottom = toBottom || !isExpanded || IsAtBottom;
            var oldHeight = content.rect.height;

            var shownAfter = isExpanded ? float.MinValue : Time.unscaledTime - MessageLifetime;
            firstShown = messages.Count;
            while (firstShown > 0 && messages[firstShown - 1].Time > shownAfter)
                firstShown--;
            shownStarts.Clear();
            for (var i = firstShown; i < messages.Count; i++)
            {
                shownStarts.Add(builder.Length);
                builder.AppendLine(messages[i].Text);
            }

            Text.text = builder.ToString();
            builder.Clear();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            //built now so fading lines don't flash fully visible
            Text.ForceMeshUpdate();
            if (!isExpanded)
                ApplyFade();

            if (stayAtBottom)
            {
                Scroll.StopMovement();
                content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
                return;
            }

            //the text grows upward, so the lines being read move down by as much
            content.anchoredPosition -= new Vector2(0f, content.rect.height - oldHeight);
        }

        private void ApplyFade()
        {
            //an empty text keeps its old vertices, which a mesh update would draw again
            var textInfo = Text.textInfo;
            if (textInfo.characterCount == 0)
                return;

            var now = Time.unscaledTime;
            nextFadeTime = now + FadeInterval;
            var message = 0;
            for (var i = 0; i < textInfo.characterCount; i++)
            {
                ref var c = ref textInfo.characterInfo[i];
                while (message + 1 < shownStarts.Count && c.index >= shownStarts[message + 1])
                    message++;
                //newer messages aren't fading yet
                var time = messages[firstShown + message].Time;
                if (time + MessageShowTime > now)
                    break;
                if (!c.isVisible)
                    continue;

                var fade = Mathf.Clamp01((time + MessageLifetime - now) / MessageFadeTime);
                var alpha = (byte)(c.color.a * fade);
                var colors = textInfo.meshInfo[c.materialReferenceIndex].colors32;
                for (var v = 0; v < 4; v++)
                    colors[c.vertexIndex + v].a = alpha;
            }

            Text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        public void OnPointerEnter(PointerEventData eventData) => isHovered = true;

        public void OnPointerExit(PointerEventData eventData) => isHovered = false;

        private void Update()
        {
            var target = isHovered && isExpanded ? 1f : 0f;
            if (Scrollbar.alpha != target)
                Scrollbar.alpha = Mathf.MoveTowards(Scrollbar.alpha, target, Time.unscaledDeltaTime / ScrollbarFadeTime);

            //the oldest shown message fades and expires first
            if (needsRefresh || isExpanded || firstShown == messages.Count)
                return;
            var oldest = messages[firstShown].Time;
            if (Time.unscaledTime >= oldest + MessageLifetime)
                Refresh(true);
            else if (Time.unscaledTime >= oldest + MessageShowTime && Time.unscaledTime >= nextFadeTime)
                ApplyFade();
        }

        private void LateUpdate()
        {
            if (!needsRefresh)
                return;

            needsRefresh = false;
            Refresh(refreshToBottom);
            refreshToBottom = false;
        }
    }
}
