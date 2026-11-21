using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Data;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.ConfigWindow;
using RebuildSharedData.Enum;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RebuildSharedData.Data;

namespace Assets.Scripts.UI.Hud
{
    public class MinimapController : MonoBehaviour, IScrollHandler, IPointerMoveHandler, IPointerExitHandler
    {
        public RectTransform Viewport;
        public RectTransform ContentContainer;

        public Image MapImage;
        public Image Border;
        public Image Background;
        public Material MapMaterial;

        public CanvasGroup IconLayer;
        //keeps entity icons below the player marker
        public RectTransform EntityIconContainer;

        //also used for party members
        public UiPlayerSprite PlayerIconPrefab;
        public Image PlayerPrefab;
        public Image MonsterPrefab;
        public Image BossPrefab;
        public Image MvpPrefab;
        public Image PortalPrefab;
        public Image WarpNpcPrefab;
        public Image KafraPrefab;
        public Image TalkNpcPrefab;

        public float IconSizeMultiplier = 1f;
        public float MouseWheelZoomStep = 0.2f;
        //the longest the frame gets on either side, for very wide or tall maps
        public float MaxFrameSide = 300f;

        private static MinimapController instance;
        private static readonly int UseMapFog = Shader.PropertyToID("_UseMapFog");

        private RectTransform selfRect;
        private Vector2 layoutSize;

        private Coroutine loadCoroutine;
        private MapType mapType;
        private readonly MinimapArt art = new();

        private float minZoom;
        private float maxZoom;
        private float zoom;

        private readonly Dictionary<int, MinimapEntityData> entities = new();

        private UiPlayerSprite playerIcon;
        private Vector2Int lastIconPosition = new(-1, -1);

        private bool pointerInside;
        private Vector2 pointerPosition;
        private Camera pointerCamera;
        private MinimapEntityData hoveredEntity;

        private float IconScale => IconSizeMultiplier / zoom;

        private class MinimapEntityData
        {
            public RectTransform Icon;
            public UiPlayerSprite Head;
            public Vector2Int Position;
            public CharacterDisplayType Type;
            public string Label;
        }

        public static MinimapController Instance
        {
            get
            {
                if (instance != null)
                    return instance;
                //include inactive: the minimap switches itself off
                instance = FindAnyObjectByType<MinimapController>(FindObjectsInactive.Include);
                return instance;
            }
        }

        //entities for a loading map are tracked but not drawn until LoadMinimap
        public void UnloadMap()
        {
            ResetView();
            entities.Clear();
        }

        private void ResetView()
        {
            MapImage.sprite = null;
            art.Release();
            ClearIconViews();
        }

        private void ClearIconViews()
        {
            foreach (var pair in entities)
                DestroyIcon(pair.Value);

            if (playerIcon != null)
            {
                Destroy(playerIcon.gameObject);
                playerIcon = null;
            }

            //makes the next SetPlayerPosition place the new marker
            lastIconPosition = new Vector2Int(-1, -1);
        }

        public void RemoveEntity(int entityId)
        {
            if (entities.Remove(entityId, out var entity))
                DestroyIcon(entity);
        }

        private void DestroyIcon(MinimapEntityData entity)
        {
            if (hoveredEntity == entity)
                SetHovered(null);

            if (entity.Icon != null)
                Destroy(entity.Icon.gameObject);

            entity.Icon = null;
            entity.Head = null;
        }

        //fires for any member change; the icon is only rebuilt on joining or leaving the party
        private void RefreshPartyIcon(int entityId)
        {
            if (!entities.TryGetValue(entityId, out var entity) || entity.Icon == null || entity.Type != CharacterDisplayType.Player)
                return;

            var isMember = PlayerState.Instance.Party.TryGetMember(entityId, out var member);
            if (isMember && entity.Head != null)
                ShowPartyHead(entity, member);
            else if (isMember || entity.Head != null)
                CreateIcon(entityId, entity);
        }

        private static void ShowPartyHead(MinimapEntityData entity, PartyMemberInfo member)
        {
            ShowHead(entity.Head, member.Appearance);
            entity.Label = string.IsNullOrEmpty(member.PlayerName) ? "Party Member" : member.PlayerName;
        }

        private void ApplyIconScale(Component icon) =>
            icon.transform.localScale = Vector3.one * IconScale;

        public void OnPointerMove(PointerEventData eventData)
        {
            pointerInside = true;
            pointerPosition = eventData.position;
            pointerCamera = eventData.enterEventCamera;

            UpdateHover();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            SetHovered(null);
        }

        //no pointer exit arrives when the minimap is switched off mid-hover
        private void OnDisable() => OnPointerExit(null);

        private void UpdateHover()
        {
            var ui = UiManager.Instance;

            if (!pointerInside || !art.IsLoaded || ui == null || ui.IsDraggingItem)
            {
                SetHovered(null);
                return;
            }

            SetHovered(EntityUnderPointer());
        }

        private MinimapEntityData EntityUnderPointer()
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(ContentContainer, pointerPosition, pointerCamera, out var local))
                return null;

            MinimapEntityData nearest = null;
            var nearestDistance = float.MaxValue;

            var iconScale = IconScale;

            foreach (var pair in entities)
            {
                var entity = pair.Value;

                if (entity.Label == null || entity.Icon == null)
                    continue;

                var half = entity.Icon.rect.size * (0.5f * iconScale);
                var delta = local - (Vector2)MapPosition(entity.Position);

                if (Mathf.Abs(delta.x) > half.x || Mathf.Abs(delta.y) > half.y)
                    continue;

                var distance = delta.sqrMagnitude;
                if (distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                nearest = entity;
            }

            return nearest;
        }

        private void SetHovered(MinimapEntityData entity)
        {
            if (hoveredEntity == entity)
                return;

            var ui = UiManager.Instance;

            if (hoveredEntity != null && hoveredEntity.Icon != null && ui != null)
                ui.HideTooltip(hoveredEntity.Icon.gameObject);

            hoveredEntity = entity;

            if (hoveredEntity != null && hoveredEntity.Icon != null && ui != null)
                ui.ShowTooltip(hoveredEntity.Icon.gameObject, hoveredEntity.Label);
        }

        public void SetEntityPosition(int entityId, CharacterDisplayType type, Vector2Int pos)
        {
            if (!entities.TryGetValue(entityId, out var entity))
            {
                entity = new MinimapEntityData { Type = type };
                entities.Add(entityId, entity);
            }
            else if (entity.Icon != null && pos == entity.Position)
                return;

            //tracked while loading: static entities are only sent on map entry
            entity.Position = pos;

            if (!art.IsLoaded)
                return;

            if (entity.Icon == null)
                CreateIcon(entityId, entity);
            else
                entity.Icon.localPosition = MapPosition(pos);
        }

        //relative to the content's top-left pivot
        private Vector3 MapPosition(Vector2Int pos)
        {
            var p = pos.ToMapPosition();
            return new Vector3(p.x, p.y - art.TileHeight, 0f);
        }

        private void CreateIcon(int entityId, MinimapEntityData entity)
        {
            DestroyIcon(entity);

            if (entity.Type == CharacterDisplayType.Player && PlayerState.Instance.Party.TryGetMember(entityId, out var member))
            {
                entity.Head = Instantiate(PlayerIconPrefab, EntityIconContainer, false);
                entity.Icon = (RectTransform)entity.Head.transform;
                ShowPartyHead(entity, member);
            }
            else
            {
                (Image Prefab, string Label) kind = entity.Type switch
                {
                    CharacterDisplayType.Player => (PlayerPrefab, "Player"),
                    CharacterDisplayType.Monster => (MonsterPrefab, "Monster"),
                    CharacterDisplayType.Boss => (BossPrefab, "Boss"),
                    CharacterDisplayType.Mvp => (MvpPrefab, "Mvp"),
                    CharacterDisplayType.WarpNpc => (WarpNpcPrefab, "Warp"),
                    CharacterDisplayType.Kafra => (KafraPrefab, "Kafra"),
                    CharacterDisplayType.TalkNpc => (TalkNpcPrefab, "NPC"),

                    //a null label makes the icon unhoverable
                    CharacterDisplayType.Portal => (PortalPrefab, null),
                    _ => (PlayerPrefab, null)
                };

                entity.Icon = Instantiate(kind.Prefab, EntityIconContainer, false).rectTransform;
                entity.Label = kind.Label;
            }

            entity.Icon.localPosition = MapPosition(entity.Position);
            ApplyIconScale(entity.Icon);
        }

        public void SetPlayerPosition(Vector2Int pos)
        {
            if (!gameObject.activeInHierarchy || !art.IsLoaded)
                return;

            if (playerIcon == null)
            {
                playerIcon = Instantiate(PlayerIconPrefab, IconLayer.transform, false);
                ShowPlayerHead();
            }

            if (pos == lastIconPosition)
                return;

            lastIconPosition = pos;
            UpdatePlayerMarker();
        }

        private void ShowPlayerHead()
        {
            ShowHead(playerIcon, PlayerState.Instance.Appearance);
        }

        private static void ShowHead(UiPlayerSprite sprite, PlayerAppearance appearance) =>
            sprite.DisplaySprite(ClientDataLoader.Instance.GetPlayerHeadSpriteName(appearance.HairStyleId, appearance.HairColorId, appearance.IsMale));

        private void OnAppearanceChanged()
        {
            if (playerIcon != null)
                ShowPlayerHead();
        }

        private void UpdatePlayerMarker()
        {
            var mapPosition = MapPosition(lastIconPosition);

            playerIcon.transform.localPosition = mapPosition;
            ApplyIconScale(playerIcon);

            ScrollViewportTo(mapPosition);

            //the map moved under the pointer
            UpdateHover();
        }

        //stops at the edges of the drawn content
        private void ScrollViewportTo(Vector2 mapPosition)
        {
            var viewSize = Viewport.rect.size;

            var content = art.Content;
            var scrollX = ScrollAlong(mapPosition.x, content.xMin, content.xMax, viewSize.x);
            var scrollY = ScrollAlong(-mapPosition.y, art.TileHeight - content.yMax, art.TileHeight - content.yMin, viewSize.y);

            ContentContainer.anchoredPosition = new Vector2(-scrollX, scrollY);
        }

        //content smaller than the view stays centred in it
        private float ScrollAlong(float target, float contentStart, float contentEnd, float viewLength)
        {
            var lowest = contentStart * zoom;
            var highest = contentEnd * zoom - viewLength;
            if (highest < lowest)
                return (contentStart + contentEnd) / 2f * zoom - viewLength / 2f;

            return Mathf.Clamp(target * zoom - viewLength / 2f, lowest, highest);
        }

        public void LoadMinimap(string mapName, MapType type)
        {
            if(loadCoroutine != null)
                StopCoroutine(loadCoroutine);

            //a coroutine can't start on an inactive object
            gameObject.SetActive(true);

            ContentContainer.gameObject.SetActive(false);
            mapType = type;
            ResetView();

            loadCoroutine = StartCoroutine(LoadMinimapCoroutine(mapName));
        }

        public void HideMinimap()
        {
            UnloadMap();
            gameObject.SetActive(false);
        }

        private void SetZoom(float value)
        {
            zoom = Mathf.Clamp(value, minZoom, maxZoom);
            ContentContainer.localScale = new Vector3(zoom, zoom, zoom);

            foreach (var pair in entities)
                if (pair.Value.Icon != null)
                    ApplyIconScale(pair.Value.Icon);

            if (playerIcon != null)
                UpdatePlayerMarker();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (!art.IsLoaded || Mathf.Approximately(eventData.scrollDelta.y, 0f))
                return;

            var zoomMultiplier = Mathf.Pow(1f + MouseWheelZoomStep, eventData.scrollDelta.y);
            SetZoom(zoom * zoomMultiplier);
        }

        private IEnumerator LoadMinimapCoroutine(string mapName)
        {
            yield return new WaitForEndOfFrame();

            yield return art.Load(mapName);
            if (!art.IsLoaded)
            {
                Debug.LogWarning("Could not load minimap.");
                yield break;
            }

            var w = art.TileWidth;
            var h = art.TileHeight;
            var content = art.Content;

            //towns show their art without darkening unwalkable tiles
            ContentContainer.sizeDelta = new Vector2(w, h);
            ContentContainer.localPosition = Vector3.zero;
            art.ShowOn(MapImage, MapMaterial, mapType != MapType.Town);

            minZoom = ResizeToMap(content.width, content.height);
            maxZoom = Mathf.Max(minZoom, Mathf.Clamp(minZoom * 6f, 2f, 10f));

            SetZoom(minZoom);
            ScrollViewportTo(new Vector2(content.center.x, content.center.y - h));

            ContentContainer.gameObject.SetActive(true);

            ApplyVisibility();
        }

        void Awake()
        {
            instance = this;
            selfRect = (RectTransform)transform;
            layoutSize = selfRect.rect.size;
            MapMaterial = new Material(MapMaterial);
            MapMaterial.SetFloat(UseMapFog, 1f);
            PlayerState.Instance.Party.MembershipChanged += RefreshPartyIcon;
            PlayerState.Instance.AppearanceChanged += OnAppearanceChanged;
            ApplyVisibility();
        }

        private void OnDestroy()
        {
            PlayerState.Instance.Party.MembershipChanged -= RefreshPartyIcon;
            PlayerState.Instance.AppearanceChanged -= OnAppearanceChanged;
            art.Release();
            Destroy(MapMaterial);
        }

        //takes the map's shape at about the scene frame's area, so every map shows at about the same size
        private float ResizeToMap(int mapWidth, int mapHeight)
        {
            var fit = Mathf.Sqrt(layoutSize.x * layoutSize.y / (mapWidth * mapHeight));
            fit = Mathf.Min(fit, MaxFrameSide / mapWidth, MaxFrameSide / mapHeight);
            selfRect.sizeDelta = new Vector2(mapWidth * fit, mapHeight * fit);
            //the buffs below the minimap move with its height
            LayoutRebuilder.MarkLayoutForRebuild(selfRect);
            return fit;
        }

        public void ApplyVisibility()
        {
            GameConfig.InitializeIfNecessary();

            if (!art.IsLoaded)
                return;

            var config = GameConfig.Data;

            if (!config.EnableMinimap || (config.MinimapOpacity <= 0f && config.MinimapIconOpacity <= 0f))
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            foreach (var pair in entities)
                if (pair.Value.Icon == null)
                    CreateIcon(pair.Key, pair.Value);

            SetAlpha(MapImage, config.MinimapOpacity);
            SetAlpha(Border, config.MinimapOpacity);
            SetAlpha(Background, config.MinimapOpacity);

            IconLayer.alpha = config.MinimapIconOpacity;
        }

        private static void SetAlpha(Image image, float alpha)
        {
            var color = image.color;
            color.a = alpha;
            image.color = color;
        }
    }
}
