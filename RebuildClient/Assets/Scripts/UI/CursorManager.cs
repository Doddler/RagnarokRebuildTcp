using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Assets.Scripts.UI
{
    public enum GameCursorMode
    {
        Normal,
        Dialog,
        Interact,
        SpinCamera,
        Attack,
        Enter,
        Unavailable,
        PickUp,
        SkillTarget,
    }

    [Serializable]
    public class CursorConfig
    {
        [HideInInspector] public string name;
        public GameCursorMode CursorType;
        public List<Sprite> AnimFrames;
        public Vector2 HotSpot;
        public int frameRate;

        private int cursorIndex(float timeInAnimation) => Mathf.FloorToInt(timeInAnimation * frameRate) % AnimFrames.Count;
        public bool HasAnimation => AnimFrames.Count > 1;

        public Sprite FetchCursorFrame(float timeInAnimation) =>
            !HasAnimation ? AnimFrames[0] : AnimFrames[cursorIndex(timeInAnimation)];
    }

    [CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/CursorManager")]
    public class CursorManager : ScriptableObject
    {
        public List<CursorConfig> CursorSettings;
        public List<Sprite> NumberSprites;
        public static bool UseSoftwareCursor = false;

        private Dictionary<GameCursorMode, CursorConfig> lookup; //game cursor type -> cursor data lookup

        // private Dictionary<Vector2Int, Texture2D> targetTextures; //we need a different temporary texture for different size cursors
        //
        private int lastCursor;
        private GameCursorMode lastMode;
        private int lastLevel;

        private float timeInAnimation;
        private float frameTime;
        private int frameCount;
        private string lastFrame;
        private int lastCursorType;
        private Dictionary<int, List<Texture2D>> cursorTextures;
        private Vector2 hotSpot;
        private List<Texture2D> activeCursor;

        private void CopySpriteIntoTexture(Texture2D target, Sprite source, Rect src, Rect dest)
        {
            var w = (int)src.width;
            var h = (int)src.height;
            var x = (int)src.x;
            var y = (int)src.y;

            var pixels = source.texture.GetPixels(x, y, w, h);
            target.SetPixels((int)dest.x, (int)dest.y, w, h, pixels);
        }

        private void Init()
        {
            if (lookup == null)
            {
                //build a mapping so that we can quickly find details for the selected cursor 
                lookup = new Dictionary<GameCursorMode, CursorConfig>();
                for (var i = 0; i < CursorSettings.Count; i++)
                    lookup.TryAdd(CursorSettings[i].CursorType, CursorSettings[i]);
                cursorTextures = new Dictionary<int, List<Texture2D>>();
            }

            foreach (var cursor in CursorSettings)
            {
                var maxCursorLevel = 0;
                if (cursor.CursorType == GameCursorMode.SkillTarget)
                    maxCursorLevel = 10;
                var rSize = 27;
                var size = cursor.AnimFrames[0].rect.size;
                size = new Vector2(Mathf.Max(size.x, 48), Mathf.Max(size.y, 48));

                var blankColors = new Color32[(int)size.x * (int)size.y];
                for (var i = 0; i < blankColors.Length; i++)
                    blankColors[i] = new Color32(0, 0, 0, 0);

                for (var level = 0; level <= maxCursorLevel; level++)
                {
                    var targetId = level * 100 + (int)cursor.CursorType;
                    var existing = new Dictionary<string, Texture2D>();
                    var textureList = new List<Texture2D>();
                    foreach (var frame in cursor.AnimFrames)
                    {
                        if (existing.TryGetValue(frame.name, out var value))
                        {
                            textureList.Add(value);
                            continue;
                        }

                        var tex = new Texture2D((int)size.x, (int)size.y, TextureFormat.RGBA32, false);
                        tex.SetPixels32(blankColors);
                        tex.name = frame.name + "_" + level;

                        var srcRect = new Rect(frame.textureRect.x, frame.textureRect.y, frame.textureRect.width, frame.textureRect.height);
                        var destRect = new Rect(0, 0, frame.textureRect.width, frame.textureRect.height);
                        CopySpriteIntoTexture(tex, frame, srcRect, destRect);

                        if (level > 0)
                        {
                            var sprite = NumberSprites[level - 1];
                            tex.name = $"{frame.name}_{sprite.name}";
                            srcRect = new Rect(sprite.textureRect.x + sprite.textureRect.width - rSize, sprite.textureRect.y, rSize, sprite.textureRect.height);
                            destRect = new Rect(tex.width - rSize, 0, srcRect.width, srcRect.height);
                            CopySpriteIntoTexture(tex, sprite, srcRect, destRect);
                        }

                        tex.Apply();
                        textureList.Add(tex);
                        existing.Add(frame.name, tex);
                    }

                    cursorTextures.Add(targetId, textureList);
                }
            }

            SwitchCursor(GameCursorMode.Normal);
            var cursorType = (int)GameCursorMode.Normal;
            activeCursor = cursorTextures[cursorType];
            Cursor.SetCursor(activeCursor[0], hotSpot, CursorMode.Auto);
        }

        public void SwitchCursor(GameCursorMode mode)
        {
            var data = lookup[mode];
            frameCount = data.AnimFrames.Count;
            frameTime = data.frameRate;
            hotSpot = data.HotSpot;
        }

        public void UpdateCursor(GameCursorMode mode, int level = 0)
        {
            if (cursorTextures == null)
                Init();

            timeInAnimation += Time.deltaTime;

            if (level > 0 && mode != GameCursorMode.SkillTarget)
            {
                Debug.LogWarning($"Cannot use level for cursor type {mode}!");
                level = 0;
            }

            var cursorType = level * 100 + (int)mode;

            if (lastCursorType != cursorType)
            {
                SwitchCursor(mode);
                activeCursor = cursorTextures[cursorType];
            }

            var frame = Mathf.FloorToInt(timeInAnimation * frameTime) % frameCount;

            lastCursorType = cursorType;
            if (lastFrame == activeCursor[frame].name)
                return;

            // Debug.Log($"CursorType:{cursorType} Mode:{mode} TimeInAnimation:{timeInAnimation} Frame:{frame} Texture:{activeCursor[frame]} Hotspot:{hotSpot}");

            lastFrame = activeCursor[frame].name;

            Cursor.SetCursor(activeCursor[frame], hotSpot, CursorMode.Auto);
            return;
            /*

            if (lookup == null)
            {
                //build a mapping so that we can quickly find details for the selected cursor
                lookup = new Dictionary<GameCursorMode, CursorConfig>();
                for(var i = 0; i < CursorSettings.Count; i++)
                    lookup.TryAdd(CursorSettings[i].CursorType, CursorSettings[i]);
                targetTextures = new Dictionary<Vector2Int, Texture2D>();
            }

            if (!lookup.TryGetValue(mode, out var cursor))
                cursor = CursorSettings[0];

            if (lastMode != mode)
                timeInAnimation = 0;
            else
                timeInAnimation += Time.deltaTime;

            lastMode = mode;
            level = Math.Clamp(level, 0, 10);

            var cTexture = cursor.FetchCursorFrame(timeInAnimation);

            var hash = HashCode.Combine(cTexture.name, level);

            //there is no reason to change our cursor if it hasn't changed from last time
            if (lastCursor == hash)
                return;

            var w = (int)cTexture.textureRect.width;
            var h = (int)cTexture.textureRect.height;
            var x = (int)cTexture.textureRect.x;
            var y = (int)cTexture.textureRect.y;

            //  So here's the deal, we have a texture atlas with all our cursor sprites on it.
            // Unity however can't accept a sprite, you need to feed it a Texture2D.
            // To solve this, we copy the selected sprite's data into a texture and submit that.
            //
            //  But wait, not all the cursors are the same size you say, and you are right.
            // For this reason, we have a different temporary texture for each size of cursor.

            if (!targetTextures.TryGetValue(new Vector2Int(w, h), out cursorTexture))
            {
                cursorTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
                cursorTexture.filterMode = FilterMode.Point;
                targetTextures.Add(new Vector2Int(w, h), cursorTexture);
            }

            //this requires a unique name for some reason because unity hates you and wants you to suffer
            cursorTexture.name = hash.ToString();

            if (cachedTextureData == null)
                cachedTextureData = new Dictionary<string, Color[]>();

            if (level == 0)
            {
                var pixels = cachedTextureData.GetValueOrDefault(hash.ToString(), null);
                if (pixels == null)
                {
                    pixels = cTexture.texture.GetPixels(x, y, w, h);
                    cachedTextureData.Add(hash.ToString(), pixels);
                }

                cursorTexture.SetPixels(0, 0, w, h, pixels);
                cursorTexture.Apply();
            }
            else
            {

                var t1 = $"{hash}TextureA";
                var t2 = $"{hash}TextureB";
                var rSize = 27; //the size of the right half of the texture to copy in
                // Debug.Log($"Cursor skill level {level}: {NumberSprites[level-1]}");
                Color[] pixels = cachedTextureData.GetValueOrDefault(t1, null);
                if (pixels == null)
                {
                    pixels = cTexture.texture.GetPixels(x, y, w - rSize, h);
                    cachedTextureData.Add(t1, pixels);
                }

                cursorTexture.SetPixels(0, 0, w-rSize, h, pixels);

                var offset = w - rSize;
                var rect = NumberSprites[level - 1].textureRect;

                var pixels2 = cachedTextureData.GetValueOrDefault(t2, null);
                if (pixels2 == null)
                {
                    pixels2 = cTexture.texture.GetPixels((int)rect.x + offset, (int)rect.y, rSize, 64);
                    cachedTextureData.Add(t2, pixels2);
                }

                cursorTexture.SetPixels(offset, 0, rSize, 64, pixels2);
                cursorTexture.Apply();
            }

            var hotspot = cursor.HotSpot;
            // if (cursorTexture.width < 64)
            // {
            //     cursorTexture = Resize(cursorTexture, cursorTexture.width * 2, cursorTexture.height * 2);
            //     hotspot *= 2;
            // }

            Cursor.SetCursor(cursorTexture, hotspot, CursorMode.Auto);

            lastCursor = hash;
            */
        }

        Texture2D Resize(Texture2D texture2D, int targetX, int targetY)
        {
            //Debug.Log($"{texture2D} {texture2D.width},{texture2D.height}");
            RenderTexture rt = new RenderTexture(targetX, targetY, 24);
            RenderTexture.active = rt;
            Graphics.Blit(texture2D, rt);
            Texture2D result = new Texture2D(targetX, targetY, TextureFormat.RGBA32, false);
            result.filterMode = FilterMode.Point;
            result.ReadPixels(new Rect(0, 0, targetX, targetY), 0, 0);
            result.Apply();
            return result;
        }

        //call this in late update because... it works better, I guess?
        public void ApplyCursor()
        {
            //Cursor.SetCursor(cursorTexture, hotspot, CursorMode.Auto);
        }

        private void OnValidate()
        {
            if (CursorSettings == null)
                return;
            foreach (var c in CursorSettings)
                c.name = c.HasAnimation
                    ? $"{c.CursorType} Cursor ({c.AnimFrames.Count} frames at {c.frameRate}fps)"
                    : $"{c.CursorType} Cursor (No Animation)";
        }
    }
}