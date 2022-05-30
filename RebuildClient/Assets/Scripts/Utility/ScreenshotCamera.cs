using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

[ExecuteInEditMode]
public class ScreenshotCamera : MonoBehaviour
{
    public bool TakeScreenshot;
    public int Width = 4096;
    public int Height = 4096;
    public string FileName;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if(TakeScreenshot)
            TakeScreenshotCoroutine();
        TakeScreenshot = false;
    }

    private Texture2D ScaleTexture2(Texture2D source, int targetWidth, int targetHeight)
    {
        Texture2D result = new Texture2D(targetWidth, targetHeight, source.format, true);
        Color[] rpixels = result.GetPixels(0);
        float incX = (1.0f / (float)targetWidth);
        float incY = (1.0f / (float)targetHeight);
        for (int px = 0; px < rpixels.Length; px++)
        {
            rpixels[px] = source.GetPixelBilinear(incX * ((float)px % targetWidth), incY * ((float)Mathf.Floor(px / targetWidth)));
        }
        result.SetPixels(rpixels, 0);
        result.Apply();
        return result;
    }
    
    //stolen graciously from http://answers.unity.com/answers/890986/view.html
    private Texture2D ScaleTexture(Texture2D source, int targetWidth, int targetHeight)
    {
        Texture2D result = new Texture2D(targetWidth, targetHeight, source.format, false);
        float incX = (1.0f / (float)targetWidth);
        float incY = (1.0f / (float)targetHeight);
        for (int i = 0; i < result.height; ++i)
        {
            for (int j = 0; j < result.width; ++j)
            {
                Color newColor = source.GetPixelBilinear((float)j / (float)result.width, (float)i / (float)result.height);
                result.SetPixel(j, i, newColor);
            }
        }

        result.Apply();
        return result;
    }

    //from https://gamedev.stackexchange.com/a/114768
    public static Texture2D Resize(Texture2D source, int newWidth, int newHeight)
    {
        source.filterMode = FilterMode.Point;
        RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
        rt.filterMode = FilterMode.Point;
        RenderTexture.active = rt;
        Graphics.Blit(source, rt);
        Texture2D nTex = new Texture2D(newWidth, newHeight);
        nTex.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        nTex.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        return nTex;
    }

    [ContextMenu("Take Screenshot")]
    public void TakeScreenshotCoroutine()
    {
        //yield return new WaitForEndOfFrame();

        var cam = GetComponent<Camera>();
        var rt = new RenderTexture(Width*8, Height*8, 0, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        
        cam.Render();

        var prevRT = RenderTexture.active;
        RenderTexture.active = cam.targetTexture;

        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, true);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, true);
        tex.Apply();

        var tex2 = Resize(tex, Width, Height);
        DestroyImmediate(tex);
        tex = tex2;

        var bytes = tex.EncodeToPNG();


        if (!Directory.Exists("Assets/Maps/minimap"))
            Directory.CreateDirectory("Assets/Maps/minimap");
        File.WriteAllBytes($@"Assets/Maps/minimap/{FileName}.png", bytes);

        cam.targetTexture = null;
        RenderTexture.active = prevRT;
        
        DestroyImmediate(rt);
        DestroyImmediate(tex);
    }

}
