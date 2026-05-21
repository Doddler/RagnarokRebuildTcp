using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.UI
{
    public class TmpLinkHover : MonoBehaviour
    {
        private static readonly Regex LinkSpan = new(@"(<link=""[^""]+"">)(.*?)</link>", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex InnerColorSpan = new(@"^(<color=#[0-9A-Fa-f]+>)(.*?)(</color>)$", RegexOptions.Compiled | RegexOptions.Singleline);

        private TMP_Text target;
        private string sourceText;
        private int hoveredIdx = -1;

        private void Awake()
        {
            target = GetComponent<TMP_Text>();
            sourceText = target.text;
        }

        public void RefreshSource()
        {
            if (target == null)
                target = GetComponent<TMP_Text>();
            sourceText = target.text;
            hoveredIdx = -1;
        }

        private void OnDisable()
        {
            if (hoveredIdx >= 0 && target != null && sourceText != null)
                target.text = sourceText;
            hoveredIdx = -1;
        }

        private void Update()
        {
            if (target == null || sourceText == null || !target.gameObject.activeInHierarchy)
                return;

            var idx = TMP_TextUtilities.FindIntersectingLink(target, Input.mousePosition, null);
            if (idx == hoveredIdx)
                return;

            hoveredIdx = idx;
            target.text = idx < 0 ? sourceText : ApplyUnderlineToNthLink(sourceText, idx);
        }

        private static string ApplyUnderlineToNthLink(string source, int targetIdx)
        {
            int counter = 0;
            return LinkSpan.Replace(source, m =>
            {
                if (counter++ != targetIdx) return m.Value;
                var openTag = m.Groups[1].Value;
                var content = m.Groups[2].Value;
                var inner = InnerColorSpan.Match(content);
                //if the link wraps a single <color>...</color>, push <u> inside so the underline inherits the color.
                if (inner.Success)
                    content = $"{inner.Groups[1].Value}<u>{inner.Groups[2].Value}</u>{inner.Groups[3].Value}";
                else
                    content = $"<u>{content}</u>";
                return $"{openTag}{content}</link>";
            });
        }
    }
}
