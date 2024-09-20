using Assets.Scripts.Effects;
using Assets.Scripts.Sprites;
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

        private static Material spriteMaterial;

        public static GroundItem Create(int entityId, int id, int count, Vector2 position, bool showAnimation)
        {
            if (spriteMaterial == null)
            {
                spriteMaterial = new Material(ShaderCache.Instance.SpriteShader);
                spriteMaterial.SetFloat("_Offset", 0.5f);
            }

            var data = ClientDataLoader.Instance.GetItemById(id);

            var go = new GameObject("GroundItem");

            var item = go.AddComponent<GroundItem>();
            item.ItemId = id;
            item.Count = count;
            item.ItemType = ItemType.RegularItem;
            item.Sprite = ClientDataLoader.Instance.ItemIconAtlas.GetSprite(data.Sprite);
            item.ItemName = data.Name;
            item.EntityId = entityId;

            var subObject = new GameObject("Sprite");
            subObject.transform.SetParent(go.transform);
            subObject.transform.localScale = new Vector3(3, 3, 3);
            subObject.transform.localPosition = new Vector3(0, showAnimation ? 0.2f : 0f, 0f);
            subObject.layer = LayerMask.NameToLayer("Item");
            subObject.AddComponent<BillboardObject>();
            if (data.IsUnique)
                subObject.transform.localScale *= 2f;

            var sr = subObject.AddComponent<SpriteRenderer>();
            sr.sprite = item.Sprite;
            sr.material = spriteMaterial;
            item.SpriteRenderer = sr;

            var box = subObject.AddComponent<BoxCollider>();
            box.size = new Vector3(0.4f, 0.4f, 0.01f);
            
            var walkProvider = CameraFollower.Instance.WalkProvider;
            if (walkProvider != null)
            {
                var pos = new Vector3(position.x, CameraFollower.Instance.WalkProvider.GetHeightForPosition(position.x, position.y) + 0.02f, position.y);
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

            SpriteUtil.AttachShadowToGameObject(go, 0.3f);

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

            var color = Color.white;
            
            if (timer > step1 - stepSize && timer < step1)
                color = Color.Lerp(Color.white, Color.red, (timer - (step1 - stepSize)) * (1 / stepSize));
            if (timer >= step1 && timer < step1 + stepSize)
                color = Color.Lerp(Color.red, Color.white, (timer - (step1)) * (1 / stepSize));

            if (timer > 16f)
                timer = 1f;
            
            SpriteRenderer.color = color;
        }
    }
}