using System;
using System.Collections.Generic;
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

        private Dictionary<GameCursorMode, CursorConfig> lookup; //game cursor type -> cursor data lookup
        private Dictionary<Vector2Int, Texture2D> targetTextures; //we need a different temporary texture for different size cursors
        
        private int lastCursor;
        private GameCursorMode lastMode;
        private int lastLevel;
        
        private Texture2D cursorTexture;
        private float timeInAnimation;

        public void UpdateCursor(GameCursorMode mode, int level = 0)
        {
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
                targetTextures.Add(new Vector2Int(w, h), cursorTexture);
            }

            //this requires a unique name for some reason because unity hates you and wants you to suffer
            cursorTexture.name = hash.ToString();

            if (level == 0)
            {
                var pixels = cTexture.texture.GetPixels(x, y, w, h);

                cursorTexture.SetPixels(0, 0, w, h, pixels);
                cursorTexture.Apply();
            }
            else
            {
                // Debug.Log($"Cursor skill level {level}: {NumberSprites[level-1]}");
                var pixels = cTexture.texture.GetPixels(x, y, w-26, h);
                cursorTexture.SetPixels(0, 0, w-26, h, pixels);

                var offset = w - 26;
                var rect = NumberSprites[level - 1].textureRect;
                
                var pixels2 = cTexture.texture.GetPixels((int)rect.x + offset, (int)rect.y, 26, 64);
                cursorTexture.SetPixels(offset, 0, 26, 64, pixels2);
                cursorTexture.Apply();
            }

            Cursor.SetCursor(cursorTexture, cursor.HotSpot, CursorMode.Auto);
            
            lastCursor = hash;
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
                c.name = c.HasAnimation ? $"{c.CursorType} Cursor ({c.AnimFrames.Count} frames at {c.frameRate}fps)"
                                        : $"{c.CursorType} Cursor (No Animation)";
        }
        
        
    }
}