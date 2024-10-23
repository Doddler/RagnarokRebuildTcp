using System.Collections.Generic;
using Assets.Scripts.Utility;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.Sprites
{
    public class UiPlayerSprite : MonoBehaviour
    {
        public Material Material;
        public Direction ViewDirection;
        
        private class UIPlayerSpriteData
        {
            public RoSpriteRendererUI SpriteRenderer;
            public RoSpriteData SpriteData;
            public string SpriteName;
            public bool IsActive;
        }
        
        private List<UIPlayerSpriteData> sprites;

        private int loadCount;
        private int loadedCount;

        public void PrepareDisplayPlayerCharacter(int jobId, int headId, int hairColor, int headgear1, int headgear2, int headgear3, bool isMale)
        {
            Debug.Log($"PrepareDisplayPlayerCharacter job:{jobId} head:{headId} hairColor:{hairColor} headgear1:{headgear1} headgear2:{headgear2} headgear3:{headgear3} isMale:{isMale}");
            
            if (sprites == null)
            {
                sprites = new List<UIPlayerSpriteData>(5);
                for (var i = 0; i < 5; i++)
                {
                    var go = new GameObject("UiSprite" + i);
                    go.transform.SetParent(this.transform);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localScale = Vector3.one;
                    go.SetActive(false);
                    var sr = go.AddComponent<RoSpriteRendererUI>();
                    go.AddComponent<CanvasRenderer>();
                    sr.material = Material;
                    sr.raycastTarget = false;
                    var spData = new UIPlayerSpriteData { SpriteRenderer = sr, IsActive = false, SpriteData = null };
                    sprites.Add(spData);
                }
            }
            else
            {
                foreach (var s in sprites)
                {
                    s.IsActive = false;
                    s.SpriteName = null;
                }
            }

            var d = ClientDataLoader.Instance;

            loadCount = 0;
            loadedCount = 0;
            
            LoadSpriteIntoSlot(d.GetPlayerBodySpriteName(jobId, isMale), 0);
            LoadSpriteIntoSlot(d.GetPlayerHeadSpriteName(headId, hairColor, isMale), 1);

            if (headgear1 > 0) LoadSpriteIntoSlot(d.GetHeadgearSpriteName(headgear1, isMale), 4);
            if (headgear2 > 0) LoadSpriteIntoSlot(d.GetHeadgearSpriteName(headgear2, isMale), 3);
            if (headgear3 > 0) LoadSpriteIntoSlot(d.GetHeadgearSpriteName(headgear3, isMale), 2);
        }

        private void LoadSpriteIntoSlot(string spriteName, int slot)
        {
            if (string.IsNullOrWhiteSpace(spriteName))
            {
                Debug.LogWarning($"Unable to perform LoadSpriteIntoSlot: Not provided a valid sprite name.");
                return;
            }
            loadCount++;
            sprites[slot].IsActive = true;
            sprites[slot].SpriteName = spriteName;
            AddressableUtility.LoadRoSpriteData(gameObject, spriteName, sData => OnLoadSpriteData(sData, spriteName, slot));
        }

        public void OnLoadSpriteData(RoSpriteData data, string spriteName, int id)
        {
            loadedCount++;
            if(spriteName == sprites[id].SpriteName)
                sprites[id].SpriteData = data;
            // else
            //     Debug.Log($"Ignored UiPlayerSprite OnLoad for {spriteName} as the sprite that finished loading was not the one we expected (expecting {sprites[id].SpriteName}");
            if(loadedCount >= loadCount)
                AssembleCompleteSprite();
        }

        public void AssembleCompleteSprite()
        {
            var rootAnchor = Vector2.zero;
            
            for (var i = 0; i < sprites.Count; i++)
            {
                var sr = sprites[i].SpriteRenderer;
                
                if (!sprites[i].IsActive)
                {
                    sr.gameObject.SetActive(false);
                    continue;
                }

                sr.SpriteData = sprites[i].SpriteData;
                sr.ActionId = 0;
                sr.CurrentFrame = 0;
                sr.Direction = ViewDirection;
                
                var frame = sr.SpriteData.Actions[(int)ViewDirection].Frames[0];
                if (i == 0 && frame.Pos.Length > 0)
                    rootAnchor = frame.Pos[0].Position;
                if (i > 0 && frame.Pos.Length > 0)
                {
                    var diff = rootAnchor - frame.Pos[0].Position;
                    sr.gameObject.transform.localPosition = new Vector3(diff.x * 2, -diff.y * 2, -i * 0.01f);
                }
                
                sr.gameObject.SetActive(true);
                sr.SetActive(true);
            }
        }

        public void ChangeDirection(Direction newDir)
        {
            ViewDirection = newDir;
            if(loadCount > 0 && loadedCount >= loadCount)
                AssembleCompleteSprite();
        }
    }
}