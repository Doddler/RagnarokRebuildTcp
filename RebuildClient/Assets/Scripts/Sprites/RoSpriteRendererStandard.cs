using Assets.Scripts.Effects;
using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.MapEditor;
using Assets.Scripts.UI.ConfigWindow;
using RebuildSharedData.Enum;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Scripts.Sprites
{
    internal class RoSpriteRendererStandard : MonoBehaviour, IRoSpriteRenderer
    {
        private int _currentAngleIndex;

        public int CurrentAngleIndex
        {
            get => _currentAngleIndex;
            set { _currentAngleIndex = value; }
        }

        public bool UpdateAngleWithCamera;
        public bool SecondPassForWater;
        public int SortingOrder;
        public Vector2Int LastPosition;
        public bool HasWater;

        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;
        public MeshCollider MeshCollider;
        public SortingGroup SortingGroup;

        public Material OverrideMaterial;
        public float VerticalOffset;
        public float ZOffset;
        public float ShaderYOffset;

        private static readonly int Drain = Shader.PropertyToID("_ColorDrain");
        private static readonly int Offset = Shader.PropertyToID("_Offset");
        private static readonly int Width = Shader.PropertyToID("_Width");
        private static readonly int EnvColor = Shader.PropertyToID("_EnvColor");
        private static readonly int Color1 = Shader.PropertyToID("_Color");
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int LightingSamplePosition = Shader.PropertyToID("_LightingSamplePosition");
        private static readonly int IsMeshRenderer = Shader.PropertyToID("_IsMeshRenderer");
        private static readonly int VPos = Shader.PropertyToID("_VPos");
        private MaterialPropertyBlock propertyBlock;

        private Shader shader;

        private Dictionary<int, Mesh> meshCache;
        private Dictionary<int, Mesh> colliderCache;

        private bool isInitialized;
        private bool is8Direction;

        public int ActionId;
        public int CurrentFrame;
        public Color Color;
        public float ColorDrain;
        public float Angle;
        public float SpriteOffset;
        public int PaletteId;
        public RoSpriteData SpriteData;
        public bool IsHidden;

        internal RoSpriteAnimator OwnerAnimator;

        private SpriteBatchHandle _batchHandle;
        private bool _registered;
        private bool _useFallback;
        private MeshArrays _meshArrays;
        private RoSpriteRendererStandard _rootRenderer;
        private Texture2D _registeredAtlas;
        private bool _batchRejected;
        private int _rejectedGeneration;
        private int _instanceId;

        private bool _dirty = true;
        private Vector3 _lastWritePos;
        private Quaternion _lastWriteRot;
        private Color _lastWriteColor;
        private float _lastWriteDrain;
        private float _lastWriteOffset;
        private bool _lastWriteHidden;
        private int _lastWriteRootOrder;
        private bool _hasBeenPositioned;
        
        private bool _hasRebuiltOnce;
        private int _lastRebuiltAction = -1;
        private int _lastRebuiltAngle = -1;
        private int _lastRebuiltFrame = -1;
        private int _lastRebuiltPalette = -1;
        private int _lastRebuiltSortingOrder;
        private Material _lastRebuiltOverride;
        private float _lastRebuiltVerticalOffset;

        private struct MeshArrays
        {
            public Vector2[] Uv;
            public Vector3[] Vertices;
            public Color[] Colors;
        }

        private static readonly Dictionary<Mesh, MeshArrays> meshArrayCache = new();

        public Direction Direction
        {
            get => RoAnimationHelper.GetFacingForAngle(Angle);
            set => Angle = RoAnimationHelper.FacingDirectionToRotation(value);
        }
        public Direction LastDirection;

        private Mesh _mesh;

        public void SetAction(int action, bool is8Direction)
        {
            ActionId = action;
            this.is8Direction = is8Direction;
        }

        public void SetColor(Color color) => Color = color;
        public void SetColorDrain(float drainStrength) => ColorDrain = drainStrength;
        public void SetDirection(Direction direction) => Direction = direction;
        public void SetAngle(float angle) => Angle = angle;

        public void SetFrame(int frame) => CurrentFrame = frame;
        public void SetSprite(RoSpriteData sprite) => SpriteData = sprite;
        public void SetOffset(float offset) => SpriteOffset = offset;

        public RoFrame GetActiveRendererFrame()
        {
            var actions = SpriteData.Actions[ActionId + CurrentAngleIndex];
            var frame = CurrentFrame;

            if (frame >= actions.Frames.Length)
                frame = actions.Frames.Length - 1;

            return actions.Frames[frame];
        }

        public void Initialize(bool makeCollider = false)
        {
            if (isInitialized)
                return;
            if (gameObject == null)
                return;
            if (SpriteData == null)
                return;

            MeshFilter = gameObject.AddComponent<MeshFilter>();
            MeshRenderer = gameObject.AddComponent<MeshRenderer>();
            MeshRenderer.enabled = false;

            MeshRenderer.sortingOrder = SortingOrder;
            if (makeCollider)
                MeshCollider = gameObject.AddComponent<MeshCollider>();

            SortingGroup = gameObject.GetOrAddComponent<SortingGroup>();
            SortingGroup.sortingOrder = SortingOrder;

            MeshRenderer.receiveShadows = false;
            MeshRenderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            MeshRenderer.shadowCastingMode = ShadowCastingMode.Off;

            propertyBlock = new MaterialPropertyBlock();

            if (shader == null)
                shader = GameConfig.Data.EnableXRay ? ShaderCache.Instance?.SpriteShaderWithXRay : ShaderCache.Instance?.SpriteShader;

            meshCache = SpriteMeshCache.GetMeshCacheForSprite(SpriteData.Name);
            colliderCache = SpriteMeshCache.GetColliderCacheForSprite(SpriteData.Name);

            Color = Color.white;

            CurrentAngleIndex = (int)Direction;
            if (UpdateAngleWithCamera)
                CurrentAngleIndex = RoAnimationHelper.GetSpriteIndexForAngle(Direction, 360 - CameraFollower.Instance.Rotation);

            isInitialized = true;

            Rebuild();
        }

        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }

        public void SetOverrideMaterial(Material mat)
        {
            OverrideMaterial = mat;
            if (MeshRenderer != null)
                MeshRenderer.sharedMaterial = OverrideMaterial;
        }

        public void SetLightProbeAnchor(GameObject anchor)
        {
            if (MeshRenderer != null)
                MeshRenderer.probeAnchor = anchor.transform;
        }

        public void Rebuild()
        {
            if (!isInitialized)
                return;

            if (!UpdateAngleWithCamera)
                CurrentAngleIndex = (int)Direction;

            if (_hasRebuiltOnce
                && ActionId == _lastRebuiltAction
                && CurrentAngleIndex == _lastRebuiltAngle
                && CurrentFrame == _lastRebuiltFrame
                && PaletteId == _lastRebuiltPalette
                && SortingOrder == _lastRebuiltSortingOrder
                && ReferenceEquals(OverrideMaterial, _lastRebuiltOverride)
                && VerticalOffset == _lastRebuiltVerticalOffset)
            {
                return;
            }

            if (OverrideMaterial != null)
            {
                MeshRenderer.sharedMaterial = OverrideMaterial;
            }

            var reverseNorth = SpriteData.ReverseSortingWhenFacingNorth && CurrentAngleIndex >= 2 && CurrentAngleIndex <= 5;
            SortingGroup.sortingOrder = EffectiveSortingOrder();
            if (ZOffset != 0)
                transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, reverseNorth ? -ZOffset : ZOffset);

            _mesh = GetMeshForFrame();
            var cMesh = GetColliderForFrame();

            if (MeshFilter.sharedMesh != _mesh)
            {
                MeshFilter.sharedMesh = null;
                MeshFilter.sharedMesh = _mesh;
            }
            if (MeshCollider != null && MeshCollider.sharedMesh != cMesh)
            {
                MeshCollider.sharedMesh = null;
                MeshCollider.sharedMesh = cMesh;
            }

            if (propertyBlock != null && MeshRenderer != null)
            {
                MeshRenderer.GetPropertyBlock(propertyBlock, 0);
                SetPropertyBlock();
                MeshRenderer.SetPropertyBlock(propertyBlock, 0);
            }

            StampMeshArrays();
            _dirty = true;

            _hasRebuiltOnce = true;
            _lastRebuiltAction = ActionId;
            _lastRebuiltAngle = CurrentAngleIndex;
            _lastRebuiltFrame = CurrentFrame;
            _lastRebuiltPalette = PaletteId;
            _lastRebuiltSortingOrder = SortingOrder;
            _lastRebuiltOverride = OverrideMaterial;
            _lastRebuiltVerticalOffset = VerticalOffset;
        }
        
        internal int EffectiveSortingOrder()
        {
            if (SpriteData && SpriteData.ReverseSortingWhenFacingNorth && CurrentAngleIndex >= 2 && CurrentAngleIndex <= 5)
                return SortingOrder < 0 ? -SortingOrder : SortingOrder - 10;
            return SortingOrder;
        }

        private void StampMeshArrays()
        {
            if (_mesh == null) return;
            if (!meshArrayCache.TryGetValue(_mesh, out _meshArrays))
            {
                _meshArrays = new MeshArrays
                {
                    Uv = _mesh.uv,
                    Vertices = _mesh.vertices,
                    Colors = _mesh.colors,
                };
                meshArrayCache[_mesh] = _meshArrays;
            }
        }

        private void SetPropertyBlock()
        {
            var envColor = RoMapRenderSettings.GetBakedLightContribution(new Vector2(transform.position.x, transform.position.z));

            propertyBlock.SetTexture(MainTex, SpriteData.Atlas);
            propertyBlock.SetColor(Color1, Color);
            propertyBlock.SetColor(EnvColor, envColor);
            propertyBlock.SetFloat(Width, SpriteData.AverageWidth / 25f);

            var sampleAnchor = transform.parent != null ? transform.parent.position : transform.position;
            propertyBlock.SetVector(LightingSamplePosition, sampleAnchor);

            propertyBlock.SetFloat(IsMeshRenderer, 1f);

            if (Mathf.Approximately(0, SpriteOffset))
                propertyBlock.SetFloat(Offset, Mathf.Max(SpriteData.Size / 125f, 1f));
            else
                propertyBlock.SetFloat(Offset, SpriteOffset);

            propertyBlock.SetFloat(Drain, ColorDrain);
            propertyBlock.SetFloat(VPos, ShaderYOffset);

            MeshRenderer.SetPropertyBlock(propertyBlock);
        }

        public Mesh GetMeshForFrame()
        {
            var id = ((ActionId + CurrentAngleIndex) << 16) + CurrentFrame;

            if (meshCache == null)
                throw new Exception("Meshcache is not initialized! But how? isInitialized status is " + isInitialized);

            if (meshCache.TryGetValue(id, out var mesh))
                return mesh;

            var newMesh = SpriteMeshBuilder.BuildSpriteMesh(SpriteData, ActionId, CurrentAngleIndex, CurrentFrame, 1f, VerticalOffset, PaletteId);

            meshCache.Add(id, newMesh);

            return newMesh;
        }

        private Mesh GetColliderForFrame()
        {
            var id = ((ActionId + CurrentAngleIndex) << 16) + CurrentFrame;

            if (colliderCache.TryGetValue(id, out var mesh))
                return mesh;

            var newMesh = SpriteMeshBuilder.BuildColliderMesh(SpriteData, ActionId, CurrentAngleIndex, CurrentFrame);

            colliderCache.Add(id, newMesh);

            return newMesh;
        }

        public bool UpdateRenderer()
        {
            if (!isInitialized)
                return false;

            bool rebuilt = false;

            if (UpdateAngleWithCamera)
            {
                var rotation = 360 - (int)CameraFollower.Instance.Rotation;
                if (is8Direction)
                    rotation += 22;
                rotation %= 360;

                var angleIndex = RoAnimationHelper.GetSpriteIndexForAngle(Angle, rotation);
                if (!is8Direction && angleIndex % 2 == 0)
                    angleIndex++;

                if (angleIndex != CurrentAngleIndex)
                {
                    CurrentAngleIndex = angleIndex;

                    Rebuild();
                    rebuilt = true;
                }
            }

            var batcher = RoSpriteBatcher.Instance;
            if (_batchRejected && batcher != null && _rejectedGeneration != batcher.Generation)
                _batchRejected = false;
            bool wantFallback = OverrideMaterial != null
                || batcher == null
                || !batcher.BatchingAvailable
                || _batchRejected;
            if (wantFallback != _useFallback)
            {
                _useFallback = wantFallback;
                if (_useFallback && _registered)
                {
                    if (RoSpriteBatcher.Instance != null)
                        RoSpriteBatcher.Instance.Unregister(ref _batchHandle);
                    _registered = false;
                }
            }

            if (_useFallback)
            {
                MeshRenderer.enabled = !IsHidden;
                EnsureFallbackMaterial();
                MeshRenderer.GetPropertyBlock(propertyBlock, 0);
                SetPropertyBlock();
                MeshRenderer.SetPropertyBlock(propertyBlock, 0);
            }
            else
            {
                if (MeshRenderer != null && MeshRenderer.enabled)
                    MeshRenderer.enabled = false;

                if (SpriteData != null && _meshArrays.Vertices != null)
                    WriteToBatcher();
            }

            return rebuilt;
        }

        private void EnsureFallbackMaterial()
        {
            if (MeshRenderer == null) return;

            if (OverrideMaterial != null)
            {
                if (MeshRenderer.sharedMaterial != OverrideMaterial)
                    MeshRenderer.sharedMaterial = OverrideMaterial;
                return;
            }

            var cache = ShaderCache.Instance;
            if (cache == null) return;
            var wantShader = GameConfig.Data != null && GameConfig.Data.EnableXRay
                ? cache.SpriteShaderWithXRay
                : cache.SpriteShader;
            if (wantShader == null) return;

            if (MeshRenderer.sharedMaterial == null)
                MeshRenderer.material = new Material(wantShader);
            else if (MeshRenderer.material.shader != wantShader)
                MeshRenderer.material.shader = wantShader;
        }

        internal int CachedInstanceId => _instanceId != 0 ? _instanceId : _instanceId = GetInstanceID();

        private RoSpriteRendererStandard ResolveRootRenderer()
        {
            if (_rootRenderer != null) return _rootRenderer;
            if (OwnerAnimator == null || OwnerAnimator.Parent == null)
            {
                _rootRenderer = this;
                return _rootRenderer;
            }
            if (OwnerAnimator.Parent.SpriteRenderer is RoSpriteRendererStandard parentRenderer)
            {
                _rootRenderer = parentRenderer;
                return _rootRenderer;
            }
            return this;
        }

        private void WriteToBatcher()
        {
            var batcher = RoSpriteBatcher.Instance;
            if (batcher == null) return;
            if (_meshArrays.Vertices == null) return;

            if (_registered && !batcher.IsValidHandle(_batchHandle))
            {
                _registered = false;
                _batchHandle = default;
                _registeredAtlas = null;
                _dirty = true;
            }

            if (_registered && _registeredAtlas != SpriteData.Atlas)
            {
                batcher.Unregister(ref _batchHandle);
                _registered = false;
                _registeredAtlas = null;
                _dirty = true;
            }

            if (!_registered)
            {
                int layerCount = _meshArrays.Vertices.Length / 4;
                if (layerCount <= 0) return;
                if (!batcher.TryRegister(SpriteData.Atlas, layerCount, out _batchHandle))
                {
                    _batchRejected = true;
                    _rejectedGeneration = batcher.Generation;
                    return;
                }
                _registeredAtlas = SpriteData.Atlas;
                _registered = true;
                _dirty = true;
            }

            var pos = transform.position;
            var rot = transform.rotation;

            if (!_hasBeenPositioned)
            {
                if (pos.sqrMagnitude < 0.0001f)
                    return;
                _hasBeenPositioned = true;
                _dirty = true;
            }

            var root = ResolveRootRenderer();
            int rootOrder = root.EffectiveSortingOrder();

            if (!_dirty
                && pos == _lastWritePos
                && rot == _lastWriteRot
                && Color == _lastWriteColor
                && ColorDrain == _lastWriteDrain
                && SpriteOffset == _lastWriteOffset
                && IsHidden == _lastWriteHidden
                && rootOrder == _lastWriteRootOrder)
            {
                return;
            }

            var p = new SpriteRenderParams
            {
                spriteColor = Color,
                colorDrain = ColorDrain,
                offset = Mathf.Approximately(0, SpriteOffset) ? Mathf.Max(SpriteData.Size / 125f, 1f) : SpriteOffset,
                vPos = ShaderYOffset,
                width = SpriteData.AverageWidth / 25f,
                hidden = IsHidden,
                sortOrder = EffectiveSortingOrder(),
                rootKey = root.CachedInstanceId,
                rootOrder = rootOrder,
                rootPos = root.transform.position,
            };

            if (!batcher.WriteSprite(ref _batchHandle, transform.localToWorldMatrix,
                _meshArrays.Vertices, _meshArrays.Uv, _meshArrays.Colors, p))
            {
                _registered = false;
                _batchHandle = default;
                _registeredAtlas = null;
                _batchRejected = true;
                _rejectedGeneration = batcher.Generation;
                return;
            }

            _lastWritePos = pos;
            _lastWriteRot = rot;
            _lastWriteColor = Color;
            _lastWriteDrain = ColorDrain;
            _lastWriteOffset = SpriteOffset;
            _lastWriteHidden = IsHidden;
            _lastWriteRootOrder = rootOrder;
            _dirty = false;
        }

        private void OnEnable()
        {
            StartCoroutine(WaitSpriteDataThenRegister());
        }

        private IEnumerator WaitSpriteDataThenRegister()
        {
            while (!SpriteData || !isInitialized || _mesh == null)
                yield return null;

            StampMeshArrays();

            if (OverrideMaterial != null)
            {
                _useFallback = true;
                if (MeshRenderer != null) MeshRenderer.enabled = true;
            }
            else
            {
                _useFallback = false;
                if (MeshRenderer != null) MeshRenderer.enabled = false;
            }
        }

        private void OnDisable()
        {
            if (_registered)
            {
                if (RoSpriteBatcher.Instance != null)
                    RoSpriteBatcher.Instance.Unregister(ref _batchHandle);
                _registered = false;
            }
        }
    }
}
