using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.UI.ConfigWindow;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Hud
{
    public class MinimapController : MonoBehaviour, IScrollHandler
    {
        public RectTransform ContentContainer;
        public RectTransform Viewport;
        public TextMeshProUGUI CoordinateText;
        private Vector2Int lastCoordinate = new(-1, -1);

        private static readonly Vector3[] cornerBuffer = new Vector3[4];

        //set when the map is resized or rescaled, so anything derived from its rect is recalculated once
        private bool mapTransformDirty;

        //distance the coordinate label is inset from the corner it's pinned to
        private static readonly Vector3 CoordinateInset = new(-2f, 0f, 0f);

        private RectTransform playerIconRect;
        private Vector2Int lastIconPosition = new(-1, -1);
        private float lastIconAngle = float.NaN;

        //icons stay this much more opaque than the map so they stay readable as it fades
        private const float IconOpacityOffset = 0.2f;
        private static float IconAlpha => Mathf.Min(GameConfig.Data.MinimapOpacity + IconOpacityOffset, 1f);

        public Image MapImage;
        public Material OverworldMaterial;
        public Material DungeonMaterial;
        public Sprite PlayerIcon;
        public Sprite OtherPlayerIcon;
        public Sprite PartyMemberIcon;
        public Sprite BossIcon;
        public Sprite MvpIcon;
        public Sprite PortalIcon;
        public Sprite WarpNpcIcon;
        public Sprite KafraIcon;

        private GameObject playerMapIconObject;
        private Dictionary<int, MinimapEntityData> mapIcons = new();

        public MapType MapType;

        public float ObjectScaleFactor = 1f;
        public float MinimapPixelsPerTile = 5f;
        public float MouseWheelZoomStep = 0.2f;

        public float minScale;
        public float maxScale;

        public float curSize;
        public float lastZoom;

        private float offsetX;
        private float offsetY;

        private static MinimapController instance;

        private Coroutine loadCoroutine;

        private Sprite mapSprite;
        public Sprite walkSprite;

        private class MinimapEntityData
        {
            public GameObject MapIcon;
            public Image Icon;
            public RectTransform Rect;
            public Vector2Int Position;
            public CharacterDisplayType Type;
        }



        public static MinimapController Instance
        {
            get
            {
                if (instance != null)
                    return instance;
                instance = FindObjectOfType<MinimapController>();
                return instance;
            }
        }

        public void RemoveAllEntities()
        {
            if (mapIcons == null) return;

            foreach(var icon in mapIcons)
                Destroy(icon.Value.MapIcon);
            mapIcons.Clear();
        }

        public void RemoveEntity(int entityId)
        {
            if (mapIcons == null || !mapIcons.Remove(entityId, out var mapIcon))
                return;

            Destroy(mapIcon.MapIcon);
        }

        public void RefreshPartyMembers()
        {
            var state = PlayerState.Instance;
            var isInParty = state.IsInParty;

            foreach (var (entityId, mapEntry) in mapIcons)
            {
                if (mapEntry.MapIcon == null || entityId == state.EntityId || mapEntry.Type != CharacterDisplayType.Player)
                    continue;

                var icon = OtherPlayerIcon;
                if (isInParty && state.PartyMemberIdLookup.ContainsKey(entityId))
                    icon = PartyMemberIcon;

                mapEntry.Icon.sprite = icon;
            }
        }

        public void SetEntityPosition(int entityId, CharacterDisplayType type, Vector2Int pos)
        {
            if (!mapIcons.TryGetValue(entityId, out var iconData))
            {
                iconData = new MinimapEntityData() { MapIcon = null, Position = pos, Type = type };
                mapIcons.Add(entityId, iconData);
            }

            if (!gameObject.activeInHierarchy || MapImage == null || mapSprite == null)
                return;

            var scale = 0.3f;
            if (type == CharacterDisplayType.Boss || type == CharacterDisplayType.Mvp)
                scale = 0.4f;
            if (type == CharacterDisplayType.Portal)
                scale = 0.08f;

            GameObject mapIcon = iconData.MapIcon;

            if (mapIcon == null)
            {
                mapIcon = new GameObject("PlayerIcon");
                mapIcon.transform.SetParent(MapImage.transform, false);


                var img = mapIcon.AddComponent<Image>();
                switch (type)
                {
                    case CharacterDisplayType.Player:
                        if (PlayerState.Instance.PartyMemberIdLookup.ContainsKey(entityId))
                            img.sprite = PartyMemberIcon;
                        else
                            img.sprite = OtherPlayerIcon; 
                        break;
                    case CharacterDisplayType.Boss: img.sprite = BossIcon; break;
                    case CharacterDisplayType.Mvp: img.sprite = MvpIcon; break;
                    case CharacterDisplayType.Portal: img.sprite = PortalIcon; break;
                    case CharacterDisplayType.WarpNpc: img.sprite = WarpNpcIcon; break;
                    case CharacterDisplayType.Kafra: img.sprite = KafraIcon; break;
                    default:
                        Debug.Log($"Unknown character display type for minimap icon: {type}");
                        img.sprite = OtherPlayerIcon;
                        break;
                }

                SetAlpha(img, IconAlpha);

                iconData.MapIcon = mapIcon;
                iconData.Icon = img;
                iconData.Rect = mapIcon.GetComponent<RectTransform>();
                iconData.Rect.anchorMin = Vector2.zero;
                iconData.Rect.anchorMax = Vector2.zero;

                //keep the player marker drawn over the icons that were just added
                playerMapIconObject?.transform.SetAsLastSibling();
            }

            var h = mapSprite.texture.height;
            var offset = new Vector3(0.5f, 0.5f, 0);

            //scrolling the map is the player's job - an entity moving must not drag the view with it
            iconData.Rect.localPosition = new Vector3(pos.x * MinimapPixelsPerTile / 2f, pos.y * MinimapPixelsPerTile / 2f - h, 0f) + offset;

            var s = scale * ObjectScaleFactor * (1 / curSize);

            mapIcon.transform.localScale = Vector3.one * s;
        }

        public void SetPlayerPosition(Vector2Int pos, float angle)
        {
            //this runs every frame, so only rebuild the label when it's shown and the tile actually changed
            if (pos != lastCoordinate && CoordinateText.gameObject.activeSelf)
            {
                lastCoordinate = pos;
                CoordinateText.SetText("{0:0}, {1:0}", pos.x, pos.y);
            }

            if (!gameObject.activeInHierarchy || MapImage == null || mapSprite == null)
                return;

            if (playerMapIconObject == null)
            {
                playerMapIconObject = new GameObject("PlayerIcon");
                playerMapIconObject.transform.SetParent(MapImage.transform, false);


                var img = playerMapIconObject.AddComponent<Image>();
                img.sprite = PlayerIcon;
                SetAlpha(img, IconAlpha);

                playerIconRect = playerMapIconObject.GetComponent<RectTransform>();
                playerIconRect.anchorMin = Vector2.zero;
                playerIconRect.anchorMax = Vector2.zero;
            }

            //everything below only moves when the player does or when the map itself changed
            if (pos == lastIconPosition && Mathf.Approximately(angle, lastIconAngle) && !mapTransformDirty)
                return;

            lastIconPosition = pos;
            lastIconAngle = angle;

            var r = playerIconRect;

            var w = mapSprite.texture.width;
            var h = mapSprite.texture.height;
            var offset = new Vector3(0.5f, 0.5f, 0);

            r.localPosition = new Vector3(pos.x * MinimapPixelsPerTile / 2f, pos.y * MinimapPixelsPerTile / 2f - h, 0f) + offset;

            //ScrollRect.horizontalNormalizedPosition = pos.x / (float)w;

            var px = (pos.x * MinimapPixelsPerTile / 2f + offsetX) * curSize;
            var py = ((h - pos.y * MinimapPixelsPerTile / 2f) + offsetY) * curSize;

            var scrollx = px - 125f;
            var scrolly = py - 125f;



            var maxScroll = ((Mathf.Max(w, h) * curSize - 250f));


            scrollx = Mathf.Clamp(-scrollx, -maxScroll, 0);
            scrolly = Mathf.Clamp(scrolly, 0, maxScroll);

            //Debug.Log($"{curSize} {px} {py} {scrollx} {scrolly} {maxScroll}");

            ContentContainer.anchoredPosition = new Vector3(scrollx, scrolly, 0f);

            //playerMapIconObject.transform.localPosition = new Vector3(pos.x * 10f, pos.y * 10f, 0f);
            playerMapIconObject.transform.localRotation = Quaternion.Euler(0f, 0f, -angle);

            var s = 0.3f * ObjectScaleFactor * (1 / curSize);

            playerMapIconObject.transform.localScale = Vector3.one * s;

            if (mapTransformDirty)
            {
                mapTransformDirty = false;
                AnchorCoordinatesToMap();
            }
        }

        public void LoadMinimap(string mapName, MapType type)
        {
            if(loadCoroutine != null)
                StopCoroutine(loadCoroutine);

            gameObject.SetActive(true);
            ContentContainer.gameObject.SetActive(false);
            //if(mapSprite != null)
            //    Destroy(mapSprite);
            mapSprite = null;
            //if(walkSprite != null)
            //    Destroy(walkSprite);
            walkSprite = null;
            MapType = type;

            loadCoroutine = StartCoroutine(LoadMinimapCoroutine(mapName));
        }

        public void SetZoom(float zoom)
        {
            zoom = Mathf.Clamp(zoom, minScale, maxScale);
            //Debug.Log($"Setting minimap size to {zoom} (in a range of {minScale} to {maxScale})");

            curSize = zoom;

            if (mapSprite == null)
                return;

            UpdateMapMaterial();

            var w = mapSprite.texture.width;
            var h = mapSprite.texture.height;

            MapImage.rectTransform.sizeDelta = new Vector2(w, h);

            ContentContainer.sizeDelta = MapImage.rectTransform.sizeDelta;
            ContentContainer.localScale = new Vector3(curSize, curSize, curSize);

            offsetX = 0f;
            offsetY = 0f;

            if (w != h && (w * curSize < 250 || h * curSize < 250))
            {

                if (w > h)
                    offsetY = -(w - h) / 2f;

                else
                    offsetX = (h - w) / 2f;

                MapImage.transform.localPosition = new Vector3(offsetX, offsetY, 0);
            }
            else
                MapImage.transform.localPosition = Vector3.zero;

            lastZoom = curSize;

            if(mapIcons.Count > 0)
                foreach(var icon in mapIcons)
                    SetEntityPosition(icon.Key, icon.Value.Type, icon.Value.Position);

            //the map isn't scrolled to the player yet, so dependent work happens on the next player update
            mapTransformDirty = true;
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (mapSprite == null || Mathf.Approximately(eventData.scrollDelta.y, 0f))
                return;

            var zoomMultiplier = Mathf.Pow(1f + MouseWheelZoomStep, eventData.scrollDelta.y);
            SetZoom(curSize * zoomMultiplier);
        }

        private void UpdateMapMaterial()
        {

            MapImage.sprite = mapSprite;

            if (MapType == MapType.Dungeon)
            {
                MapImage.sprite = walkSprite;
                MapImage.material = DungeonMaterial;
            }
            else
            {
                if (MapType == MapType.Town)
                {
                    //dungeon material but with regular map, so no highlighted walk
                    MapImage.material = DungeonMaterial;
                }
                else
                {
                    MapImage.material = OverworldMaterial;
                    OverworldMaterial.SetTexture("_SecondaryTex", walkSprite.texture);
                }

            }
        }

        public IEnumerator LoadMinimapCoroutine(string mapName)
        {
            yield return new WaitForEndOfFrame();

            var loadMap = Addressables.LoadAssetAsync<Sprite>($"Assets/Maps/minimap/{mapName}.png");
            var loadWalk = Addressables.LoadAssetAsync<Sprite>($"Assets/Maps/minimap/{mapName}_walkmask.png");

            yield return loadMap;
            yield return loadWalk;

            if (!loadWalk.IsDone || !loadWalk.IsValid() || !loadMap.IsDone || !loadMap.IsValid())
            {
                Debug.LogWarning("Could not load minimap.");
                yield break; //give up
            }

            //var map = loadMap.Result;

            mapSprite = loadMap.Result;
            walkSprite = loadWalk.Result;

            UpdateMapMaterial();

            minScale = 250f / mapSprite.texture.width;

            if (250f / mapSprite.texture.height < minScale)
                minScale = 250f / mapSprite.texture.height;

            maxScale = minScale * 6f;

            maxScale = Mathf.Clamp(maxScale, 2f, 10f);

            if (minScale > maxScale)
                maxScale = minScale;



            //var sprite = Sprite.Create(map, new Rect(0, 0, map.width, map.height), new Vector2(0, 1), 1);

            ContentContainer.localPosition = new Vector3(0, 0, 0f);

            SetZoom(minScale);

            ContentContainer.gameObject.SetActive(true);

            if (mapIcons.Count > 0)
            {
                foreach(var icon in mapIcons)
                    SetEntityPosition(icon.Key, icon.Value.Type, icon.Value.Position);
            }
        }

        void Awake()
        {
            instance = this;
            OverworldMaterial = new Material(OverworldMaterial);
            DungeonMaterial = new Material(DungeonMaterial);
            ApplyOpacity();
        }

        public void ApplyOpacity()
        {
            var opacity = GameConfig.Data.MinimapOpacity;
            var visible = opacity > 0f;

            //only the contents are hidden - this object stays active so it can still run its map load coroutine
            Viewport.gameObject.SetActive(visible);
            ApplyCoordinateVisibility();
            if (!visible)
                return;

            SetAlpha(MapImage, opacity);

            //the label reads over the map like the icons do, so it gets their offset too
            var iconAlpha = IconAlpha;
            SetAlpha(CoordinateText, iconAlpha);
            foreach (Transform icon in MapImage.transform)
                SetAlpha(icon.GetComponent<Image>(), iconAlpha);
        }

        //the label sits outside the map so zoom can't scale or clip it, so pin it to the map's bottom
        //right corner, kept inside the viewport for when zoom pushes that corner out of sight
        private void AnchorCoordinatesToMap()
        {
            MapImage.rectTransform.GetWorldCorners(cornerBuffer);
            var mapCorner = cornerBuffer[3];

            Viewport.GetWorldCorners(cornerBuffer);
            var viewCorner = cornerBuffer[3];

            var rect = CoordinateText.rectTransform;
            rect.position = new Vector3(Mathf.Min(mapCorner.x, viewCorner.x), Mathf.Max(mapCorner.y, viewCorner.y), 0f);
            rect.localPosition += CoordinateInset;
        }

        //hidden along with the map itself, so it can't be left floating when the minimap is turned off
        public void ApplyCoordinateVisibility() => CoordinateText.gameObject.SetActive(
            GameConfig.Data.ShowMinimapCoordinates && GameConfig.Data.MinimapOpacity > 0f);

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            var color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            if(!Mathf.Approximately(curSize, lastZoom))
                SetZoom(curSize);
        }
    }
}
