using System.IO;
using UnityEngine;

namespace Assets.Scripts.MapEditor.Editor
{
    public static class MinimapCapture
    {
        //supersampling: rendered pixels per output pixel, per side
        private const int Supersample = 4;

        public static void Capture(Camera camera, int width, int height, string path)
        {
            var rt = new RenderTexture(width * Supersample, height * Supersample, 0, RenderTextureFormat.ARGB32);
            var prevTarget = camera.targetTexture;
            var prevActive = RenderTexture.active;
            camera.targetTexture = rt;
            camera.Render();

            RenderTexture.active = rt;
            var rendered = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            rendered.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
            rendered.Apply();

            camera.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            Object.DestroyImmediate(rt);

            var result = Downsample(rendered, Supersample);
            Object.DestroyImmediate(rendered);

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, result.EncodeToPNG());
            Object.DestroyImmediate(result);
        }

        //alpha-weighted linear average, so transparent pixels don't darken the edges
        private static Texture2D Downsample(Texture2D source, int factor)
        {
            var toLinear = new float[256];
            for (var i = 0; i < 256; i++)
                toLinear[i] = Mathf.GammaToLinearSpace(i / 255f);

            var src = source.GetPixels32();
            var srcWidth = source.width;
            var width = srcWidth / factor;
            var height = source.height / factor;
            var dst = new Color32[width * height];
            var samples = factor * factor;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    for (var sy = 0; sy < factor; sy++)
                    {
                        var row = (y * factor + sy) * srcWidth + x * factor;
                        for (var sx = 0; sx < factor; sx++)
                        {
                            var p = src[row + sx];
                            var pa = p.a / 255f;
                            r += toLinear[p.r] * pa;
                            g += toLinear[p.g] * pa;
                            b += toLinear[p.b] * pa;
                            a += pa;
                        }
                    }

                    if (a <= 0f)
                        continue;

                    dst[y * width + x] = new Color(
                        Mathf.LinearToGammaSpace(r / a),
                        Mathf.LinearToGammaSpace(g / a),
                        Mathf.LinearToGammaSpace(b / a),
                        a / samples);
                }
            }

            var result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            result.SetPixels32(dst);
            result.Apply();
            return result;
        }
    }
}
