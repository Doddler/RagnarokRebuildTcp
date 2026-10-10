using System;
using Assets.Scripts.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Hud
{
    //Debug frame time readout and history graph, toggled with F7. Its own nested canvas isolates the label's churn.
    public class FrameStatsOverlay : MonoBehaviour
    {
        private const KeyCode ToggleKey = KeyCode.F7;

        private const float RefreshSeconds = 0.1f;
        private const int WarmupFrames = 2; //the canvas rebuild on show would skew the first window

        private const int GraphWidth = 160; //one column per tick, so 16s of history
        private const int GraphHeight = 100;
        private const float CeilingMs = 100f; //fixed scale, taller frames clamp to the top row
        private const int TraceRadius = 1; //texels either side of the sample, so the line is centred

        private const int NoPreviousSample = -1;

        private static readonly Color32 TraceColor = new Color32(255, 255, 255, 255); //opaque, the tint is the RawImage's

        public Canvas OverlayCanvas;
        public RawImage Graph;
        public TextMeshProUGUI Label;

        private float sum;
        private int count;
        private float bucketMax;
        private float elapsed;
        private int framesSeen;

        private int previousY = NoPreviousSample;

        private Texture2D texture;
        private Color32[] pixels;

        private void Awake() => OverlayCanvas.enabled = false;

        //Script created textures aren't collected with the component.
        private void OnDestroy()
        {
            if (texture != null)
                Destroy(texture);
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
            {
                OverlayCanvas.enabled = !OverlayCanvas.enabled;
                if (OverlayCanvas.enabled)
                    BeginSampling();
            }

            if (!OverlayCanvas.enabled)
                return;

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

        private void BeginSampling()
        {
            if (texture == null)
            {
                texture = new Texture2D(GraphWidth, GraphHeight, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                pixels = new Color32[GraphWidth * GraphHeight];
                Graph.texture = texture;
            }

            sum = 0f;
            count = 0;
            bucketMax = 0f;
            elapsed = 0f;
            framesSeen = 0;
            previousY = NoPreviousSample;

            Label.SetText("-- FPS -- MS\n-- MS");

            Array.Clear(pixels, 0, pixels.Length);
            Commit();
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
            var from = previousY >= 0 ? Mathf.Min(previousY, y) : y;
            var to = previousY >= 0 ? Mathf.Max(previousY, y) : y;

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
