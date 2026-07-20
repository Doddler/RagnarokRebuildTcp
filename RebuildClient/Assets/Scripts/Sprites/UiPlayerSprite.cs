using System.Collections.Generic;
using Assets.Scripts.Utility;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.Sprites
{
    public enum UiSpriteAlignment
    {
        Center,
        Left,
        Right,
        //ignores the rect entirely; the sprite's own root anchor sits at the rect's local origin
        Origin,
    }

    public class UiPlayerSprite : MonoBehaviour
    {
        public Material Material;
        public Direction ViewDirection;

        //Horizontal placement: the character binds to the chosen rect edge, or centers. Always
        //grounded at the rect bottom. Origin ignores the rect and just anchors to its local origin.
        public UiSpriteAlignment Alignment = UiSpriteAlignment.Center;

        //0 scales the sprite to fit the rect, up to MaxScale; above 0 renders at exactly that scale.
        //50 = one ui pixel per sprite pixel.
        public float FixedScale;
        public float MaxScale = 100f;
        
        private class UiSpriteLayerData
        {
            public RoSpriteRendererUI SpriteRenderer;
            public RoSpriteData SpriteData;
            public string SpriteName;
            public bool IsActive;
        }
        
        private readonly List<UiSpriteLayerData> sprites = new();

        private int loadCount;
        private int loadedCount;
        private int loadVersion;
        private int currentFrame;
        private int frameCount;
        private SpriteMotion currentMotion = SpriteMotion.Idle;

        public int FrameCount => frameCount;

        public void SetMaterial(Material material)
        {
            Material = material;

            for (var i = 0; i < sprites.Count; i++)
            {
                sprites[i].SpriteRenderer.material = material;
                sprites[i].SpriteRenderer.SetMaterialDirty();
            }
        }

        public void DisplaySprite(string spritePath, SpriteMotion motion = SpriteMotion.Idle, int frame = 0)
        {
            DisplayLayers(new[] { spritePath }, motion, frame);
        }

        public void DisplayLayers(IReadOnlyList<string> spritePaths, SpriteMotion motion = SpriteMotion.Idle, int frame = 0)
        {
            BeginDisplay(spritePaths.Count, motion, frame);

            for (var i = 0; i < spritePaths.Count; i++)
                LoadSpriteIntoSlot(spritePaths[i], i);

            if (loadCount == 0)
                Clear();
        }

        public void PrepareDisplayPlayerCharacter(int jobId, int headId, int hairColor, int headgear1, int headgear2, int headgear3, bool isMale)
        {
            var d = ClientDataLoader.Instance;
            var spritePaths = new List<string>(5)
            {
                d.GetPlayerBodySpriteName(jobId, isMale),
                d.GetPlayerHeadSpriteName(headId, hairColor, isMale)
            };

            if (headgear3 > 0) spritePaths.Add(d.GetHeadgearSpriteName(headgear3, isMale));
            if (headgear2 > 0) spritePaths.Add(d.GetHeadgearSpriteName(headgear2, isMale));
            if (headgear1 > 0) spritePaths.Add(d.GetHeadgearSpriteName(headgear1, isMale));

            DisplayLayers(spritePaths);
        }

        private void BeginDisplay(int layerCount, SpriteMotion motion, int frame)
        {
            currentMotion = motion;
            currentFrame = frame;
            frameCount = 0;
            loadVersion++;

            while (sprites.Count < layerCount)
            {
                var go = new GameObject("UiSprite" + sprites.Count);
                go.transform.SetParent(transform);
                go.transform.localPosition = Vector3.zero;
                go.transform.localScale = Vector3.one;
                go.SetActive(false);

                var spriteRenderer = go.AddComponent<RoSpriteRendererUI>();
                go.AddComponent<CanvasRenderer>();
                spriteRenderer.material = Material;
                spriteRenderer.raycastTarget = false;
                sprites.Add(new UiSpriteLayerData { SpriteRenderer = spriteRenderer });
            }

            for (var i = 0; i < sprites.Count; i++)
            {
                sprites[i].IsActive = false;
                sprites[i].SpriteData = null;
                sprites[i].SpriteName = null;
            }

            loadCount = 0;
            loadedCount = 0;
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
            var requestVersion = loadVersion;
            AddressableUtility.LoadRoSpriteData(gameObject, spriteName, sData => OnLoadSpriteData(sData, spriteName, slot, requestVersion));
        }

        private void OnLoadSpriteData(RoSpriteData data, string spriteName, int id, int requestVersion)
        {
            if (requestVersion != loadVersion)
                return;

            loadedCount++;

            if (data == null)
            {
                sprites[id].IsActive = false;
                Debug.LogWarning($"Unable to display sprite: Failed to load {spriteName}.");
            }
            else if (spriteName == sprites[id].SpriteName)
                sprites[id].SpriteData = data;

            if(loadedCount >= loadCount)
                AssembleCompleteSprite();
        }

        public void AssembleCompleteSprite()
        {
            var rootAnchor = Vector2.zero;
            var attachmentOffsets = new Vector2[sprites.Count];
            var activeSpriteCount = 0;
            var hasRootAnchor = false;
            frameCount = int.MaxValue;
            
            for (var i = 0; i < sprites.Count; i++)
            {
                var sr = sprites[i].SpriteRenderer;
                
                if (!sprites[i].IsActive)
                {
                    sr.gameObject.SetActive(false);
                    continue;
                }

                activeSpriteCount++;
                sr.SpriteData = sprites[i].SpriteData;
                ConfigureRenderer(sr);
                frameCount = Mathf.Min(frameCount, GetFrameCount(sr));
                
                var frame = sr.GetActiveRendererFrame();
                if (!hasRootAnchor && frame.Pos.Length > 0)
                {
                    rootAnchor = frame.Pos[0].Position;
                    hasRootAnchor = true;
                }
                else if (frame.Pos.Length > 0)
                {
                    var diff = rootAnchor - frame.Pos[0].Position;
                    attachmentOffsets[i] = new Vector2(diff.x, -diff.y) / 50f;
                }
            }

            if (activeSpriteCount == 0)
            {
                frameCount = 0;
                return;
            }

            if (frameCount == int.MaxValue)
                frameCount = 0;

            LayoutSprites(attachmentOffsets);

            for (var i = 0; i < sprites.Count; i++)
            {
                if (!sprites[i].IsActive)
                    continue;

                var sr = sprites[i].SpriteRenderer;
                sr.gameObject.SetActive(true);
                sr.SetActive(true);
            }
        }

        private void ConfigureRenderer(RoSpriteRendererUI spriteRenderer)
        {
            var action = RoAnimationHelper.GetMotionIdForSprite(spriteRenderer.SpriteData.Type, currentMotion);

            if (action < 0)
                action = 0;

            var direction = ViewDirection;
            if (action + (int)direction >= spriteRenderer.SpriteData.Actions.Length)
            {
                action = 0;
                if ((int)direction >= spriteRenderer.SpriteData.Actions.Length)
                    direction = Direction.South;
            }

            var actionIndex = action + (int)direction;
            var maxFrame = spriteRenderer.SpriteData.Actions[actionIndex].Frames.Length - 1;

            spriteRenderer.ActionId = action;
            spriteRenderer.CurrentFrame = Mathf.Clamp(currentFrame, 0, maxFrame);
            spriteRenderer.Direction = direction;
        }

        private static int GetFrameCount(RoSpriteRendererUI spriteRenderer)
        {
            var actionIndex = spriteRenderer.ActionId + (int)spriteRenderer.Direction;
            return actionIndex < spriteRenderer.SpriteData.Actions.Length
                ? spriteRenderer.SpriteData.Actions[actionIndex].Frames.Length
                : 0;
        }

        private void LayoutSprites(Vector2[] attachmentOffsets)
        {
            var availableSize = ((RectTransform)transform).rect.size;
            if (availableSize.x <= 0 || availableSize.y <= 0)
                return;

            //character bounds in sprite units, relative to the sprite origin
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            for (var i = 0; i < sprites.Count; i++)
            {
                if (!sprites[i].IsActive)
                    continue;

                var vertices = sprites[i].SpriteRenderer.GetMeshForFrame().vertices;
                for (var j = 0; j < vertices.Length; j++)
                {
                    var point = (Vector2)vertices[j] + attachmentOffsets[i];
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }
            }

            var characterSize = max - min;
            if (characterSize.x <= 0 || characterSize.y <= 0)
                return;

            var renderSize = FixedScale > 0
                ? FixedScale
                : Mathf.Min(availableSize.x / characterSize.x, availableSize.y / characterSize.y, MaxScale);

            Vector2 placementOffset;
            if (Alignment == UiSpriteAlignment.Origin)
            {
                //ignores the rect; the sprite's own root anchor sits at the rect's local origin
                placementOffset = Vector2.zero;
            }
            else
            {
                //grounded at the rect bottom; horizontally bound to the chosen edge, or centered
                var half = availableSize * 0.5f;
                var placementX = Alignment switch
                {
                    UiSpriteAlignment.Left => -half.x - min.x * renderSize,
                    UiSpriteAlignment.Right => half.x - max.x * renderSize,
                    _ => -(min.x + max.x) * 0.5f * renderSize,
                };
                placementOffset = new Vector2(placementX, -half.y - min.y * renderSize);
            }

            for (var i = 0; i < sprites.Count; i++)
            {
                if (!sprites[i].IsActive)
                    continue;

                var spriteRect = sprites[i].SpriteRenderer.rectTransform;
                spriteRect.anchorMin = Vector2.one * 0.5f;
                spriteRect.anchorMax = Vector2.one * 0.5f;
                spriteRect.pivot = Vector2.one * 0.5f;
                spriteRect.sizeDelta = Vector2.one * renderSize;
                spriteRect.anchoredPosition = placementOffset + attachmentOffsets[i] * renderSize;
                spriteRect.localPosition = new Vector3(spriteRect.localPosition.x, spriteRect.localPosition.y, -i * 0.01f);
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (loadCount > 0 && loadedCount >= loadCount)
                AssembleCompleteSprite();
        }

        public void ChangeDirection(Direction newDir)
        {
            ViewDirection = newDir;
            RefreshDisplay();
        }

        public void SetMotion(SpriteMotion motion)
        {
            currentMotion = motion;
            currentFrame = 0;
            RefreshDisplay();
        }

        public void SetFrame(int frame)
        {
            currentFrame = frame;
            RefreshDisplay();
        }

        public void Clear()
        {
            loadVersion++;
            loadCount = 0;
            loadedCount = 0;
            frameCount = 0;

            for (var i = 0; i < sprites.Count; i++)
            {
                sprites[i].IsActive = false;
                sprites[i].SpriteData = null;
                sprites[i].SpriteName = null;
                sprites[i].SpriteRenderer.gameObject.SetActive(false);
            }
        }

        private void RefreshDisplay()
        {
            if (loadCount > 0 && loadedCount >= loadCount)
                AssembleCompleteSprite();
        }
    }
}
