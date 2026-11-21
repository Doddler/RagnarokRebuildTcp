using System.Collections.Generic;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Utility;
using RebuildSharedData.Enum;
using UnityEngine;
using RebuildSharedData.Data;

namespace Assets.Scripts.Sprites
{
    public enum UiSpriteScaling
    {
        //fits inside the rect
        FitInside,
        //fills the rect's width; a mask clips anything taller
        FitWidth,
        //always at Scale
        Fixed,
    }

    //fixed values: prefabs store them as numbers
    public enum UiSpriteAnchor
    {
        Body = 0,
        //the sprite's own world origin
        SpriteOrigin = 2,
        //where the head joins the body; characters without a head are fit and centered instead
        HeadAttachPoint = 3,
    }

    public class UiPlayerSprite : MonoBehaviour
    {
        public Material Material;
        public Direction ViewDirection;

        [Header("Size")]
        public UiSpriteScaling Scaling = UiSpriteScaling.FitInside;

        [Tooltip("UI pixels per sprite pixel; the most the fit modes scale up to.")]
        public float Scale = 2f;

        [Header("Position")]
        [Tooltip("The part of the character placed at Anchor Position.")]
        public UiSpriteAnchor Anchor = UiSpriteAnchor.Body;

        [Tooltip("Where the anchor sits in the rect, (0,0) bottom-left to (1,1) top-right. Head Attach Point is set facing east and mirrors facing west.")]
        public Vector2 AnchorPosition = new(0.5f, 0f);

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
        private int headLayer = -1;
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
            DisplayLayers(new[] { spritePath }, -1, motion, frame);
        }

        //repeat requests do nothing, as sprites load asynchronously
        private void DisplayLayers(IReadOnlyList<string> spritePaths, int headSlot, SpriteMotion motion, int frame)
        {
            if (IsDisplaying(spritePaths, headSlot, motion, frame))
                return;

            BeginDisplay(spritePaths.Count, motion, frame);
            headLayer = headSlot;

            for (var i = 0; i < spritePaths.Count; i++)
                LoadSpriteIntoSlot(spritePaths[i], i);

            if (loadCount == 0)
                Clear();
        }

        private bool IsDisplaying(IReadOnlyList<string> spritePaths, int headSlot, SpriteMotion motion, int frame)
        {
            if (loadCount == 0 || headSlot != headLayer || motion != currentMotion || frame != currentFrame || spritePaths.Count > sprites.Count)
                return false;

            for (var i = 0; i < sprites.Count; i++)
                if (sprites[i].SpriteName != (i < spritePaths.Count ? spritePaths[i] : null))
                    return false;
            return true;
        }

        public void PrepareDisplayPlayerCharacter(PlayerAppearance appearance)
        {
            const int headSlot = 1;
            var d = ClientDataLoader.Instance;
            var isMale = appearance.IsMale;
            var spritePaths = new List<string>(5)
            {
                d.GetPlayerBodySpriteName(appearance.JobId, isMale),
                d.GetPlayerHeadSpriteName(appearance.HairStyleId, appearance.HairColorId, isMale)
            };

            if (appearance.Headgear3 > 0) spritePaths.Add(d.GetHeadgearSpriteName(appearance.Headgear3, isMale));
            if (appearance.Headgear2 > 0) spritePaths.Add(d.GetHeadgearSpriteName(appearance.Headgear2, isMale));
            if (appearance.Headgear1 > 0) spritePaths.Add(d.GetHeadgearSpriteName(appearance.Headgear1, isMale));

            DisplayLayers(spritePaths, headSlot, SpriteMotion.Idle, 0);
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
                    attachmentOffsets[i] = new Vector2(diff.x, -diff.y) / SpriteMeshBuilder.PixelsPerUnit;
                }
            }

            if (activeSpriteCount == 0)
            {
                frameCount = 0;
                return;
            }

            if (frameCount == int.MaxValue)
                frameCount = 0;

            Vector2? attachPoint = hasRootAnchor ? new Vector2(rootAnchor.x, -rootAnchor.y) / SpriteMeshBuilder.PixelsPerUnit : null;
            LayoutSprites(attachmentOffsets, attachPoint);

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

        private void LayoutSprites(Vector2[] attachmentOffsets, Vector2? attachPoint)
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

            var scale = Scale;
            var scaling = Scaling;
            var anchorPosition = AnchorPosition;
            var anchor = Anchor;
            //set facing east, mirrored facing west
            if (anchor == UiSpriteAnchor.HeadAttachPoint && ViewDirection is Direction.SouthWest or Direction.West or Direction.NorthWest)
                anchorPosition.x = 1f - anchorPosition.x;
            var hasHead = headLayer >= 0 && headLayer < sprites.Count && sprites[headLayer].IsActive;
            if (anchor == UiSpriteAnchor.HeadAttachPoint && (!hasHead || attachPoint == null)) // fallback (monsters, NPCs)
            {
                scale = 1.5f;
                scaling = UiSpriteScaling.FitInside;
                anchor = UiSpriteAnchor.Body;
                anchorPosition = new Vector2(0.5f, 0.5f);
            }

            var maxSize = scale * SpriteMeshBuilder.PixelsPerUnit;
            var widthFit = availableSize.x / characterSize.x;
            var renderSize = scaling switch
            {
                UiSpriteScaling.Fixed => maxSize,
                UiSpriteScaling.FitWidth => Mathf.Min(widthFit, maxSize),
                _ => Mathf.Min(widthFit, availableSize.y / characterSize.y, maxSize),
            };

            var target = (anchorPosition - Vector2.one * 0.5f) * availableSize;
            var anchorPoint = anchor switch
            {
                UiSpriteAnchor.SpriteOrigin => Vector2.zero,
                UiSpriteAnchor.HeadAttachPoint => attachPoint.Value,
                _ => Vector2.Scale(Vector2.one - anchorPosition, min) + Vector2.Scale(anchorPosition, max),
            };
            var placementOffset = target - anchorPoint * renderSize;

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

#if UNITY_EDITOR
        //play mode only: re-lays out while size and position are tuned
        private void OnValidate()
        {
            if (!Application.isPlaying)
                return;

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && loadCount > 0 && loadedCount >= loadCount)
                    AssembleCompleteSprite();
            };
        }
#endif

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
