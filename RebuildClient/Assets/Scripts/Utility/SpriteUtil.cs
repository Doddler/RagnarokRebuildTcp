using Assets.Scripts.Effects;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using UnityEngine;

namespace Assets.Scripts.Utility
{
    public static class SpriteUtil
    {
	    private static readonly int OffsetId = Shader.PropertyToID("_Offset");
	    public static Material shadowMaterial;
	    private static MaterialPropertyBlock shadowBlock;

	    public static void CacheShadowMaterial()
	    {
		    if (shadowMaterial != null) return;

		    var shader = ShaderCache.Instance.SpriteShaderNoZWrite;
		    shadowMaterial = new Material(shader)
		    {
			    hideFlags = HideFlags.DontUnloadUnusedAsset
		    };
		    shadowMaterial.SetFloat("_Offset", 1f);
		    shadowMaterial.color = new Color(1f, 1f, 1f, 0.5f);
		    shadowMaterial.renderQueue = 2999;
		    shadowMaterial.enableInstancing = true;
		    shadowMaterial.EnableKeyword("WATER_OFF");
	    }

	    public static void ApplyShadowMaterial(SpriteRenderer sprite, float offset = 1f)
	    {
		    CacheShadowMaterial();
		    sprite.sharedMaterial = shadowMaterial;

		    if (Mathf.Approximately(offset, 1f))
		    {
			    sprite.SetPropertyBlock(null);
			    return;
		    }

		    shadowBlock ??= new MaterialPropertyBlock();
		    sprite.GetPropertyBlock(shadowBlock);
		    shadowBlock.SetFloat(OffsetId, offset);
		    sprite.SetPropertyBlock(shadowBlock);
	    }

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

            ApplyShadowMaterial(sprite);
            sprite.sortingOrder = -1;

            var control = gameObject.GetComponent<ServerControllable>();
            if (control != null)
                control.SpriteAnimator.Shadow = go;

            if (addBillboard)
                gameObject.AddComponent<BillboardObject>();

        }
    }
}
