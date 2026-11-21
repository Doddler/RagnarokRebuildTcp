using System;
using Assets.Scripts.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Hud
{
    //Toggled with F7. Its nested canvas isolates the label's churn.
    public class FrameStatsOverlay : MonoBehaviour
    {
        private const float RefreshSeconds = 0.1f;
        private const int WarmupFrames = 2; //the canvas rebuild on show would skew the first window

        private const int GraphWidth = 160; //one column per tick, so 16s of history
        private const int GraphHeight = 100;
        private const float CeilingMs = 100f; //fixed scale, taller frames clamp to the top row
        private const int TraceRadius = 1; //texels either side of the sample, so the line is centred

        private static readonly Color32 TraceColor = new Color32(255, 255, 255, 255); //opaque, the tint is the RawImage's

        public RawImage Graph;
        public TextMeshProUGUI Label;

        private float sum;
        private int count;
        private float bucketMax;
        private float elapsed;
        private int framesSeen;

        //null until the first column
        private int? previousY;

        private Texture2D texture;
        private Color32[] pixels;

        //it has no graphic to tell its layout that it appeared or went away
        public void Toggle()
        {
            gameObject.SetActive(!gameObject.activeSelf);
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
        }

        private void Awake()
        {
            texture = new Texture2D(GraphWidth, GraphHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            pixels = new Color32[GraphWidth * GraphHeight];
            Graph.texture = texture;
        }

        private void OnEnable()
        {
            ResetWindow();
            elapsed = 0f;
            framesSeen = 0;
            previousY = null;

            Label.SetText("-- FPS -- MS\n-- MS");

            Array.Clear(pixels, 0, pixels.Length);
            Commit();
        }

        //Script created textures aren't collected with the component.
        private void OnDestroy()
        {
            if (texture != null)
                Destroy(texture);
        }

        private void Update()
        {
            if (framesSeen < WarmupFrames)
            {
                framesSeen++;
                return;
            }

            var dt = Time.unscaledDeltaTime; //unscaled so the slow motion debug key doesn't distort the readout
            var ms = dt * 1000f;

            sum += ms;
            count++;
            if (ms > bucketMax)
                bucketMax = ms;

            elapsed += dt;
            if (elapsed < RefreshSeconds)
                return;

            elapsed -= RefreshSeconds; //carry the remainder so the cadence doesn't drift

            var meanMs = sum / count;
            SetLabel(meanMs);
            AppendColumn(bucketMax);
            ResetWindow();
        }

        private void ResetWindow()
        {
            sum = 0f;
            count = 0;
            bucketMax = 0f;
        }

        private void SetLabel(float meanMs)
        {
            var network = NetworkManager.Instance;

            //Both rows end in MS, so right alignment stacks the latency directly under the frame time.
            if (network == null || network.Latency.Current < 0)
                Label.SetText("{0:0} FPS {1:1} MS\n-- MS", 1000f / meanMs, meanMs);
            else
                Label.SetText("{0:0} FPS {1:1} MS\n{2:0} MS", 1000f / meanMs, meanMs, network.Latency.Current);
        }

        private void AppendColumn(float ms)
        {
            const int newest = GraphWidth - 1;

            //Rows are contiguous, so shifting the whole buffer one index shifts every row one column. The spill
            //from each row's first pixel lands in the newest column, which is blanked next.
            Array.Copy(pixels, 1, pixels, 0, pixels.Length - 1);

            for (var row = 0; row < GraphHeight; row++)
                pixels[row * GraphWidth + newest] = default; //zeroed is fully transparent

            //The column spans the previous sample's height to its own, joining the points into one line.
            //The shift doesn't change any height, so the carried previousY still lines up.
            var y = Mathf.Clamp(Mathf.RoundToInt(ms / CeilingMs * (GraphHeight - 1)), 0, GraphHeight - 1);
            var previous = previousY ?? y;
            var from = Mathf.Min(previous, y);
            var to = Mathf.Max(previous, y);

            var lo = Mathf.Max(from - TraceRadius, 0);
            var hi = Mathf.Min(to + TraceRadius, GraphHeight - 1);
            for (var i = lo; i <= hi; i++)
                pixels[i * GraphWidth + newest] = TraceColor;

            previousY = y;

            Commit();
        }

        private void Commit()
        {
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }
    }
}
