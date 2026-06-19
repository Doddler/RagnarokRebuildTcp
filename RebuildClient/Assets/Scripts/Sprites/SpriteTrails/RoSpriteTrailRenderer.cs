using Assets.Scripts.MapEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Scripts.Sprites
{
    public class RoSpriteTrailRenderer : MonoBehaviour
    {
        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;
        public RoSpriteTrail Parent;
        public SortingGroup SortingGroup;
        public float SpriteOffset;
        public float VerticalOffset;
        public RoSpriteData SpriteData;
        private MaterialPropertyBlock propertyBlock;
        //private bool skipFrame = true;
        
        private static readonly int VPos = Shader.PropertyToID("_VPos");
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int ColorProp = Shader.PropertyToID("_Color");
        private static readonly int EnvColor = Shader.PropertyToID("_EnvColor");
        private static readonly int Width = Shader.PropertyToID("_Width");
        private static readonly int Offset = Shader.PropertyToID("_Offset");

        private Vector3[] _verts;
        private Vector2[] _uvs;
        private Color[] _colors;
        private Texture2D _atlas;
        private int _layerCount;
        private int _rootKey;
        private int _rootOrder;
        private int _memberOrder;

        private SpriteBatchHandle _batchHandle;
        private bool _registered;

        public void Init()
        {
            if (MeshRenderer == null)
            {
                MeshRenderer = gameObject.AddComponent<MeshRenderer>();
                MeshRenderer.receiveShadows = false;
                MeshRenderer.lightProbeUsage = LightProbeUsage.Off;
                MeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            if (MeshFilter == null)
                MeshFilter = gameObject.AddComponent<MeshFilter>();
            if (SortingGroup == null)
                SortingGroup = gameObject.AddComponent<SortingGroup>();
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();

            _registered = false;
            _batchHandle = default;

            gameObject.SetActive(true);
        }

        public void SetBatchSource(Vector3[] verts, Vector2[] uvs, Color[] colors, Texture2D atlas, int rootKey, int rootOrder, int memberOrder)
        {
            _verts = verts;
            _uvs = uvs;
            _colors = colors;
            _atlas = atlas;
            _layerCount = verts != null ? verts.Length / 4 : 0;
            _rootKey = rootKey;
            _rootOrder = rootOrder;
            _memberOrder = memberOrder;
        }

        private float EffectiveOffset()
        {
            if (Mathf.Approximately(0, SpriteOffset))
                return Mathf.Max(SpriteData.Size / 125f, 1f);
            return SpriteOffset;
        }

        public void SetPropertyBlock()
        {
            var envColor = RoMapRenderSettings.GetBakedLightContribution(new Vector2(transform.position.x, transform.position.z));

            propertyBlock.SetTexture(MainTex, SpriteData.Atlas);
            propertyBlock.SetColor(ColorProp, Parent.Color);
            propertyBlock.SetColor(EnvColor, envColor);
            propertyBlock.SetFloat(Width, SpriteData.AverageWidth / 25f);
            propertyBlock.SetFloat(Offset, EffectiveOffset());
            propertyBlock.SetFloat(VPos, VerticalOffset);
            MeshRenderer.SetPropertyBlock(propertyBlock);
        }

        private bool TryWriteToBatcher()
        {
            var batcher = RoSpriteAndGroundItemBatcher.Instance;
            if (batcher == null || !batcher.BatchingAvailable)
                return false;
            if (_verts == null || _atlas == null || _layerCount <= 0)
                return false;

            if (_registered && !batcher.IsValidHandle(_batchHandle))
                _registered = false;

            if (!_registered)
            {
                if (!batcher.TryRegister(_atlas, _layerCount, out _batchHandle))
                    return false;
                _registered = true;
            }

            if (MeshRenderer != null && MeshRenderer.enabled)
                MeshRenderer.enabled = false;

            var p = new SpriteRenderParams
            {
                spriteColor = Parent.Color,
                colorDrain = 0f,
                offset = EffectiveOffset(),
                vPos = VerticalOffset,
                width = SpriteData.AverageWidth / 25f,
                hidden = false,
                sortOrder = _memberOrder,
                rootKey = _rootKey,
                rootOrder = _rootOrder,
                rootPos = transform.position,
            };

            if (!batcher.WriteSprite(ref _batchHandle, transform.localToWorldMatrix, transform, transform,
                    _verts, _uvs, _colors, p))
            {
                _registered = false;
                return false;
            }

            return true;
        }

        private void UpdateFallback()
        {
            if (MeshRenderer == null) return;
            if (!MeshRenderer.enabled) MeshRenderer.enabled = true;
            SetPropertyBlock();
        }

        private void Update()
        {
            if (!TryWriteToBatcher())
                UpdateFallback();
        }

        private void OnDisable()
        {
            if (_registered)
            {
                var batcher = RoSpriteAndGroundItemBatcher.Instance;
                if (batcher != null)
                    batcher.Unregister(ref _batchHandle);
                _registered = false;
            }
            _batchHandle = default;
        }
    }
}