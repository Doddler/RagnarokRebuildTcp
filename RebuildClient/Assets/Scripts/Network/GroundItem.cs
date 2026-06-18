using System.Collections;
using Assets.Scripts.Effects;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.ConfigWindow;
using Assets.Scripts.Utility;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.Network
{
    public class GroundItem : MonoBehaviour
    {
        public int EntityId;
        public int ItemId;
        public int Count;
        public string ItemName;
        public Sprite Sprite;
        public SpriteRenderer SpriteRenderer;
        public ItemType ItemType;
        public bool IsAnimated;
        private float timer;
        private float velocity;
        private bool isReady = false;

        private Color _color = Color.white;
        private SpriteBatchHandle _batchHandle;
        private bool _registered;
        private int _instanceId;

        private static Material spriteMaterial;
        private static readonly int OffsetPropID = Shader.PropertyToID("_Offset");
        private const float Offset = 0.5f;
        private const float QuadSize = 0.24f; //matched visually with the sprite renderer at scale 3

        private static readonly Vector3[] BatchVerts = new Vector3[4];
        private static readonly Vector2[] BatchUvs = new Vector2[4];
        private static readonly Color[] BatchColors = { Color.white, Color.white, Color.white, Color.white };

        public static GroundItem Create(int entityId, int id, int count, Vector2 position, bool showAnimation)
        {
            if (spriteMaterial == null)
            {
                spriteMaterial = new Material(GameConfig.Data.EnableXRay ? ShaderCache.Instance.SpriteShaderWithXRay : ShaderCache.Instance.SpriteShader);
                spriteMaterial.SetFloat(OffsetPropID, Offset);
            }

            var data = ClientDataLoader.Instance.GetItemById(id);

            var go = new GameObject("GroundItem");

            var item = go.AddComponent<GroundItem>();
            item.ItemId = id;
            item.Count = count;
            item.ItemType = ItemType.RegularItem;
            item.Sprite = ClientDataLoader.Instance.GetIconAtlasSprite(data.Sprite);
            item.ItemName = data.Slots == 0 ? data.Name : $"{data.Name} [{data.Slots}]";
            item.EntityId = entityId;

            var subObject = new GameObject("Sprite");
            subObject.transform.SetParent(go.transform);
            subObject.transform.localScale = new Vector3(3, 3, 3);
            subObject.transform.localPosition = new Vector3(0, showAnimation ? 0.2f : 0f, 0f);
            subObject.layer = LayerMask.NameToLayer("Item");
            // We added the billboard component later when making the shadow.
            //subObject.AddComponent<BillboardObject>();
            if (data.IsUnique)
                subObject.transform.localScale *= 1.4f;

            var sr = subObject.AddComponent<SpriteRenderer>();
            sr.sprite = item.Sprite;
            sr.material = spriteMaterial;
            item.SpriteRenderer = sr;

            var box = subObject.AddComponent<BoxCollider>();
            box.size = new Vector3(0.4f, 0.4f, 0.01f);
            
            var walkProvider = CameraFollower.Instance.WalkProvider;
            if (walkProvider != null)
            {
	            // This was set to 0.02, however things were a bit clippy sometimes so i'm moving it half the size of the box instead.
	            var offset = 0.2f;
                var pos = new Vector3(position.x, CameraFollower.Instance.WalkProvider.GetHeightForPosition(position.x, position.y) + offset, position.y);
                go.transform.position = pos;
                item.isReady = true;
            }
            else
            {
                var pos = new Vector3(position.x, 0f, position.y);
                go.transform.position = pos;
                item.isReady = false;
            }

            // if (data.IsUnique)
            //     go.transform.localScale = new Vector3(2, 2, 2);
            // else
            //     go.transform.localScale = Vector3.one;

            SpriteUtil.AttachShadowToGameObject(go, 0.3f, true);

            if (showAnimation)
            {
                item.IsAnimated = true;
                item.velocity = 20f;
            }

            return item;
        }

        public void Update()
        {
            if (!isReady)
            {
                if (CameraFollower.Instance.WalkProvider != null)
                {
                    transform.localPosition += new Vector3(0, CameraFollower.Instance.WalkProvider.GetHeightForPosition(transform.position), 0);
                    isReady = true;
                }

                return;
            }

            if (IsAnimated)
            {
                var t = SpriteRenderer.transform;
                t.localPosition += Vector3.up * velocity * Time.deltaTime;
                velocity -= 1f * Time.deltaTime * 120f;
                if (t.localPosition.y < 0)
                {
                    IsAnimated = false;
                    t.localPosition = new Vector3(0, 0, 0);
                }
            }

            timer += Time.deltaTime;

            var step1 = 15f;
            var stepSize = 0.08f;

            _color = Color.white;
            
            if (timer > step1 - stepSize && timer < step1)
                _color = Color.Lerp(Color.white, Color.red, (timer - (step1 - stepSize)) * (1 / stepSize));
            if (timer >= step1 && timer < step1 + stepSize)
                _color = Color.Lerp(Color.red, Color.white, (timer - (step1)) * (1 / stepSize));

            if (timer > 16f)
                timer = 1f;
            
            SpriteRenderer.color = _color;

            var shader = GameConfig.Data.EnableXRay ? ShaderCache.Instance.SpriteShaderWithXRay : ShaderCache.Instance.SpriteShader;
            if (spriteMaterial.shader != shader) spriteMaterial.shader = shader;
            
            UpdateBatch();
        }
        
        private void OnEnable()
        {
            StartCoroutine(WaitSpriteThenRegister());
        }

        private IEnumerator WaitSpriteThenRegister()
        {
            while (!Sprite)
            {
                yield return null;
            }

            UpdateBatch();
        }

        private void OnDisable()
        {
            if (_registered && RoSpriteAndGroundItemBatcher.Instance != null)
                RoSpriteAndGroundItemBatcher.Instance.Unregister(ref _batchHandle);
            _registered = false;
        }

        private void UpdateBatch()
        {
            if (!Sprite || SpriteRenderer == null)
                return;

            var batcher = RoSpriteAndGroundItemBatcher.Instance;
            var available = batcher != null && batcher.BatchingAvailable;

            if (_registered && (!available || !batcher.IsValidHandle(_batchHandle)))
            {
                if (batcher != null)
                    batcher.Unregister(ref _batchHandle);
                _registered = false;
            }

            if (!available)
            {
                SpriteRenderer.enabled = true;
                return;
            }

            if (!_registered)
            {
                if (!batcher.TryRegister(Sprite.texture, 1, out _batchHandle))
                {
                    SpriteRenderer.enabled = true;
                    return;
                }
                _registered = true;
            }

            SpriteRenderer.enabled = false;

            var resolution = Sprite.rect.size;
            var pivot = Sprite.pivot;
            var pivotOffset = new Vector3(
                (0.5f - pivot.x / resolution.x) * QuadSize,
                (0.5f - pivot.y / resolution.y) * QuadSize, 0f);
            const float h = QuadSize * 0.5f;
            BatchVerts[0] = pivotOffset + new Vector3(-h, -h, 0f);
            BatchVerts[1] = pivotOffset + new Vector3(h, -h, 0f);
            BatchVerts[2] = pivotOffset + new Vector3(-h, h, 0f);
            BatchVerts[3] = pivotOffset + new Vector3(h, h, 0f);

            var texRect = Sprite.textureRect;
            var tex = Sprite.texture;
            float uMin = texRect.xMin / tex.width, uMax = texRect.xMax / tex.width;
            float vMin = texRect.yMin / tex.height, vMax = texRect.yMax / tex.height;
            BatchUvs[0] = new Vector2(uMin, vMin);
            BatchUvs[1] = new Vector2(uMax, vMin);
            BatchUvs[2] = new Vector2(uMin, vMax);
            BatchUvs[3] = new Vector2(uMax, vMax);

            if (_instanceId == 0)
                _instanceId = GetInstanceID();

            var p = new SpriteRenderParams
            {
                spriteColor = _color,
                offset = Offset,
                rootKey = _instanceId,
                rootPos = transform.position,
            };

            if (!batcher.WriteSprite(ref _batchHandle, SpriteRenderer.transform.localToWorldMatrix,
                SpriteRenderer.transform, transform,
                BatchVerts, BatchUvs, BatchColors, p))
            {
                _registered = false;
                SpriteRenderer.enabled = true;
            }
        }
    }
}
