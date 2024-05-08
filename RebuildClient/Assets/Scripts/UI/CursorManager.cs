using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

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

        private int cursorIndex => Mathf.FloorToInt(Time.timeSinceLevelLoad * frameRate) % AnimFrames.Count;
        public bool HasAnimation => AnimFrames.Count > 1;
        public Sprite FetchCursorFrame => !HasAnimation ? AnimFrames[0] : AnimFrames[cursorIndex];

    }

    [CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/CursorManager")]
    public class CursorManager : ScriptableObject
    {
        public List<CursorConfig> CursorSettings;

        private Dictionary<GameCursorMode, CursorConfig> lookup;
        private Sprite lastCursor;

        private Vector2 hotspot;
        private Texture2D cursorTexture;
        private Texture2D old;
        
        //private float cursorAnimTime;

        public void UpdateCursor(GameCursorMode mode)
        {
            if (lookup == null)
            {
                lookup = new Dictionary<GameCursorMode, CursorConfig>();
                for(var i = 0; i < CursorSettings.Count; i++)
                    lookup.Add(CursorSettings[i].CursorType, CursorSettings[i]);
            }

            if (!lookup.TryGetValue(mode, out var cursor))
                cursor = CursorSettings[0];

            var cTexture = cursor.FetchCursorFrame;
            if (cTexture == lastCursor)
                return;

            if(old != null)
                Destroy(old);
            if(cursorTexture != null)
                old = cursorTexture;


            var w = (int)cTexture.textureRect.width;
            var h = (int)cTexture.textureRect.height;
            var x = (int)cTexture.textureRect.x;
            var y = (int)cTexture.textureRect.y;
            
            cursorTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            //I think SetPixels is better here because it's done in cpu, where as copytexture might require a read from the gpu
            //Graphics.CopyTexture(cTexture.texture, 0, 0, x, y, w, h,cursorTexture,0,0,0,0);
            cursorTexture.SetPixels(0, 0, w, h, cTexture.texture.GetPixels(x, y, w, h, 0));
            cursorTexture.Apply();
            hotspot = cursor.HotSpot;
            
            
            Debug.Log($"{cTexture} {x} {y} {w} {h}");

            
            
            lastCursor = cTexture;
        }

        //call this in late update
        public void ApplyCursor()
        {
            Cursor.SetCursor(cursorTexture, hotspot, CursorMode.Auto);
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