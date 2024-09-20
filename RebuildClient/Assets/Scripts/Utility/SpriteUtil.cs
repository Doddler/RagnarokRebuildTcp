using Assets.Scripts.Effects;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Scripts.Utility
{
    public static class SpriteUtil
    {
        public static void AttachShadowToGameObject(GameObject gameObject, float size = 1f, bool addBillboard = false)
        {
            AddressableUtility.LoadSprite(gameObject, "shadow", sprite => PerformShadowAttach(gameObject, sprite, size, addBillboard));
        }

        private static void PerformShadowAttach(GameObject gameObject, Sprite spriteObj, float size, bool addBillboard = false)
        {
            if (gameObject == null)
                return;
            var go = new GameObject("Shadow");
            go.layer = LayerMask.NameToLayer("Characters");
            go.transform.SetParent(gameObject.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one * size;

            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = spriteObj;

            var shader = ShaderCache.Instance.SpriteShaderNoZWrite;
            var mat = new Material(shader);
            mat.SetFloat("_Offset", 1f);
            mat.color = new Color(1f, 1f, 1f, 0.5f);
            mat.renderQueue = 2999;
            sprite.material = mat;

            sprite.sortingOrder = -1;

            var control = gameObject.GetComponent<ServerControllable>();
            if (control != null)
            {
                var spriteAnimator = control.SpriteAnimator;
                spriteAnimator.Shadow = go;
                spriteAnimator.ShadowSortingGroup = go.AddComponent<SortingGroup>();
                spriteAnimator.ShadowSortingGroup.sortingOrder = -20001;
            }

            if (addBillboard)
                gameObject.AddComponent<BillboardObject>();


        }
    }
}