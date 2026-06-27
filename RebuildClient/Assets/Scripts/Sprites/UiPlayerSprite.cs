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
        private int actionOverride = -1;
        private int currentFrame;
        private int frameCount;
        private SpriteMotion currentMotion = SpriteMotion.Idle;
        private Color color = Color.white;
        private bool scaleToRectTransform;

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

        public void SetColor(Color newColor)
        {
            color = newColor;

            for (var i = 0; i < sprites.Count; i++)
            {
                sprites[i].SpriteRenderer.SetColor(color);
                sprites[i].SpriteRenderer.SetVerticesDirty();
            }
        }

        public void DisplaySprite(string spritePath, bool scaleToFit = false, SpriteMotion motion = SpriteMotion.Idle, int frame = 0)
        {
            DisplayLayers(new[] { spritePath }, scaleToFit, motion, frame);
        }

        public void DisplayLayers(IReadOnlyList<string> spritePaths, bool scaleToFit = false, SpriteMotion motion = SpriteMotion.Idle, int frame = 0)
        {
            BeginDisplay(spritePaths.Count, scaleToFit, motion, frame);

            for (var i = 0; i < spritePaths.Count; i++)
                LoadSpriteIntoSlot(spritePaths[i], i);

            if (loadCount == 0)
                Clear();
        }

        public void PrepareDisplayPlayerCharacter(int jobId, int headId, int hairColor, int headgear1, int headgear2, int headgear3, bool isMale, bool scaleToFit = false)
        {
            Debug.Log($"PrepareDisplayPlayerCharacter job:{jobId} head:{headId} hairColor:{hairColor} headgear1:{headgear1} headgear2:{headgear2} headgear3:{headgear3} isMale:{isMale}");

            var d = ClientDataLoader.Instance;
            var spritePaths = new List<string>(5)
            {
                d.GetPlayerBodySpriteName(jobId, isMale),
                d.GetPlayerHeadSpriteName(headId, hairColor, isMale)
            };

            if (headgear3 > 0) spritePaths.Add(d.GetHeadgearSpriteName(headgear3, isMale));
            if (headgear2 > 0) spritePaths.Add(d.GetHeadgearSpriteName(headgear2, isMale));
            if (headgear1 > 0) spritePaths.Add(d.GetHeadgearSpriteName(headgear1, isMale));

            DisplayLayers(spritePaths, scaleToFit);
        }

        public void DisplayEntity(int classId, bool scaleToFit = false)
        {
            var entityData = ClientDataLoader.Instance.GetMonsterData(classId);
            if (entityData == null)
            {
                Debug.LogWarning($"Unable to display entity: No monster or NPC data exists for class ID {classId}.");
                return;
            }

            if (entityData.SpriteName.EndsWith(".prefab"))
            {
                Debug.LogWarning($"Unable to display entity {entityData.Name}: UiPlayerSprite cannot render prefab sprites.");
                return;
            }

            var basePath = classId < 4000 ? "Assets/Sprites/Npcs/" : "Assets/Sprites/Monsters/";
            DisplaySprite(basePath + entityData.SpriteName, scaleToFit);
        }

        private void BeginDisplay(int layerCount, bool scaleToFit, SpriteMotion motion, int frame)
        {
            scaleToRectTransform = scaleToFit;
            currentMotion = motion;
            currentFrame = frame;
            actionOverride = -1;
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
                spriteRenderer.Color = color;
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

            if (scaleToRectTransform)
                LayoutScaledSprite(attachmentOffsets);
            else
                LayoutDefaultSprite(attachmentOffsets);

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
            var action = actionOverride >= 0
                ? actionOverride
                : RoAnimationHelper.GetMotionIdForSprite(spriteRenderer.SpriteData.Type, currentMotion);

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
            spriteRenderer.SetColor(color);
        }

        private static int GetFrameCount(RoSpriteRendererUI spriteRenderer)
        {
            var actionIndex = spriteRenderer.ActionId + (int)spriteRenderer.Direction;
            return actionIndex < spriteRenderer.SpriteData.Actions.Length
                ? spriteRenderer.SpriteData.Actions[actionIndex].Frames.Length
                : 0;
        }

        private void LayoutDefaultSprite(Vector2[] attachmentOffsets)
        {
            const float defaultSize = 100f;

            for (var i = 0; i < sprites.Count; i++)
            {
                if (!sprites[i].IsActive)
                    continue;

                var offset = attachmentOffsets[i] * defaultSize;
                var spriteRect = sprites[i].SpriteRenderer.rectTransform;
                spriteRect.sizeDelta = Vector2.one * defaultSize;
                spriteRect.localPosition = new Vector3(offset.x, offset.y, -i * 0.01f);
            }
        }

        private void LayoutScaledSprite(Vector2[] attachmentOffsets)
        {
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

            var availableSize = ((RectTransform)transform).rect.size;
            var characterSize = max - min;
            if (availableSize.x <= 0 || availableSize.y <= 0 || characterSize.x <= 0 || characterSize.y <= 0)
                return;

            var renderSize = Mathf.Min(availableSize.x / characterSize.x, availableSize.y / characterSize.y);
            var centeredOffset = -(min + max) * 0.5f * renderSize;

            for (var i = 0; i < sprites.Count; i++)
            {
                if (!sprites[i].IsActive)
                    continue;

                var spriteRect = sprites[i].SpriteRenderer.rectTransform;
                spriteRect.anchorMin = Vector2.one * 0.5f;
                spriteRect.anchorMax = Vector2.one * 0.5f;
                spriteRect.pivot = Vector2.one * 0.5f;
                spriteRect.sizeDelta = Vector2.one * renderSize;
                spriteRect.anchoredPosition = centeredOffset + attachmentOffsets[i] * renderSize;
                spriteRect.localPosition = new Vector3(spriteRect.localPosition.x, spriteRect.localPosition.y, -i * 0.01f);
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (scaleToRectTransform && loadCount > 0 && loadedCount >= loadCount)
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
            actionOverride = -1;
            RefreshDisplay();
        }

        public void SetAction(int action)
        {
            actionOverride = action;
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
