using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Assets.Scripts.Effects;
using Assets.Scripts.Sprites;
using Assets.Scripts.Utility;
using Unity.Collections;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public partial class RoSpriteAndGroundItemBatcher : MonoBehaviour
{
    public static RoSpriteAndGroundItemBatcher Instance;

    public bool EnableBatching = true;
    public int AtlasInitialSliceCount = SpriteAtlasArray.DefaultSliceCount;
    [ReadOnlyField] public Texture2DArray AtlasArrayView;

    internal const int VertsPerQuad = 4;
    internal const int IndicesPerQuad = 6;
    private const int InitialQuadCapacity = 1024;
    private const int MaxQuadCapacity = 16384;
    private const int InitialEntryCapacity = 256;

    internal const MeshUpdateFlags FastUpdate =
        MeshUpdateFlags.DontRecalculateBounds |
        MeshUpdateFlags.DontValidateIndices |
        MeshUpdateFlags.DontResetBoneBounds |
        MeshUpdateFlags.DontNotifyMeshUsers;

    internal static readonly VertexAttributeDescriptor[] VertexAttributes =
    {
        new(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
        new(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
        new(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
        new(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
        new(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 1),
        new(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4),
        new(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 3),
        new(VertexAttribute.TexCoord4, VertexAttributeFormat.Float32, 4),
        new(VertexAttribute.TexCoord5, VertexAttributeFormat.Float32, 4),
        new(VertexAttribute.TexCoord6, VertexAttributeFormat.Float32, 4),
        new(VertexAttribute.TexCoord7, VertexAttributeFormat.Float32, 2),
    };

    private static readonly int AtlasArrayId = Shader.PropertyToID("_AtlasArray");

    private static readonly ProfilerMarker _markerWriteSprite = new("RoSpriteBatcher.WriteSprite");
    private static readonly ProfilerMarker _markerLateUpdate = new("RoSpriteBatcher.LateUpdate");
    private static readonly ProfilerMarker _markerShadowRaycasts = new("RoSpriteBatcher.ShadowRaycasts");
    private static readonly ProfilerMarker _markerSyncTransforms = new("RoSpriteBatcher.SyncBatchedTransforms");
    private static readonly ProfilerMarker _markerCullCollect = new("RoSpriteBatcher.CullAndCollect");
    private static readonly ProfilerMarker _markerSort = new("RoSpriteBatcher.SortEntries");
    private static readonly ProfilerMarker _markerBuildIndices = new("RoSpriteBatcher.BuildIndices");
    private static readonly ProfilerMarker _markerUploadMesh = new("RoSpriteBatcher.UploadMesh");

    private Camera _camera;
    private GameObject _batchRendererGo;
    private MeshRenderer _batchRenderer;

    private static readonly Plane[] _frustumPlanes = new Plane[6];

    // Single 3-pass material (Depth + Color + XRay). The XRay pass is drawn by RoSpriteXRayFeature.
    private Material _baseMaterial;

    private bool _platformSupported = true;
    public int Generation { get; private set; }
    public bool BatchingAvailable => EnableBatching && _platformSupported && isActiveAndEnabled;

    private SpriteAtlasArray _atlasArray;
    private Mesh _mesh;
    private NativeArray<SpriteVertex> _verts;
    private NativeArray<ushort> _indices;
    private int _quadCapacity;
    private int _quadAllocated;
    private readonly Stack<int> _freeQuads = new();
    private int _dirtyMin = int.MaxValue;
    private int _dirtyMax = int.MinValue;

    private struct BatchEntry
    {
        public bool InUse;
        public bool Written;
        public bool Hidden;
        public Texture2D Atlas;
        public SpriteAtlasRegion Region;
        public int[] Slots;
        public int SlotCount;
        public Vector3 RootPos;
        public int RootKey;
        public int RootOrder;
        public int MemberOrder;
        public Vector3 CullCenter;
        public float SortDepth;
        public float Radius;
        public Transform Xform;
        public Transform RootXform;
        public bool CameraFacing;
        public Quaternion CornerRotation;
    }

    private BatchEntry[] _entries = new BatchEntry[InitialEntryCapacity];
    private int[] _entrySeq = new int[InitialEntryCapacity];
    private readonly Stack<int> _freeEntries = new();
    private int _entryHighWater;

    private int[] _sortValues = new int[InitialEntryCapacity];
    private EntrySortComparer _entrySortComparer;

    private sealed class EntrySortComparer : IComparer<int>
    {
        private readonly RoSpriteAndGroundItemBatcher _owner;

        public EntrySortComparer(RoSpriteAndGroundItemBatcher owner)
        {
            _owner = owner;
        }

        public int Compare(int x, int y)
        {
            ref var a = ref _owner._entries[x];
            ref var b = ref _owner._entries[y];

            int result = a.RootOrder.CompareTo(b.RootOrder);
            if (result != 0) return result;

            result = b.SortDepth.CompareTo(a.SortDepth);
            if (result != 0) return result;

            result = a.RootKey.CompareTo(b.RootKey);
            if (result != 0) return result;

            result = a.MemberOrder.CompareTo(b.MemberOrder);
            return result != 0 ? result : x.CompareTo(y);
        }
    }

    private void OnEnable()
    {
        Instance = this;
        _entrySortComparer ??= new EntrySortComparer(this);

        _camera = GetComponent<Camera>();

        const CopyTextureSupport requiredCopySupport = CopyTextureSupport.Basic | CopyTextureSupport.DifferentTypes;
        _platformSupported = SystemInfo.supports2DArrayTextures
            && (SystemInfo.copyTextureSupport & requiredCopySupport) == requiredCopySupport;

        Generation++;

        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;

        EnsureMaterials();
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;

        if (_batchRendererGo != null)
        {
            Destroy(_batchRendererGo);
            _batchRendererGo = null;
            _batchRenderer = null;
        }

        if (_verts.IsCreated) _verts.Dispose();
        if (_indices.IsCreated) _indices.Dispose();
        if (_mesh != null) { Destroy(_mesh); _mesh = null; }
        _quadCapacity = 0;
        _quadAllocated = 0;
        _freeQuads.Clear();
        _dirtyMin = int.MaxValue;
        _dirtyMax = int.MinValue;

        for (var i = 0; i < _entryHighWater; i++)
            _entries[i] = default;
        _entryHighWater = 0;
        _freeEntries.Clear();

        _atlasArray?.Dispose();
        _atlasArray = null;
        AtlasArrayView = null;

        if (_baseMaterial != null) { Destroy(_baseMaterial); _baseMaterial = null; }

        DisposeShadowRaycasts();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InvalidateShadowLightCache();
    }

    private void OnSceneUnloaded(Scene scene)
    {
        InvalidateShadowLightCache();
    }

    private void EnsureMaterials()
    {
        var cache = ShaderCache.Instance;
        if (!cache) return;

        if (!_baseMaterial && cache.SpriteShaderWithXRay)
        {
            _baseMaterial = new Material(cache.SpriteShaderWithXRay);
            _baseMaterial.EnableKeyword("DYNBATCH_ON");
#if UNITY_EDITOR
            WarmUpMaterialVariants(_baseMaterial);
#endif
        }

        SyncArrayTexture();
    }

    private void SyncArrayTexture()
    {
        var tex = _atlasArray?.Texture;
        if (tex == null) return;
        AtlasArrayView = tex;
        if (_baseMaterial != null && _baseMaterial.GetTexture(AtlasArrayId) != tex)
            _baseMaterial.SetTexture(AtlasArrayId, tex);
        if (_batchRenderer != null && _batchRenderer.sharedMaterial != _baseMaterial)
            _batchRenderer.sharedMaterial = _baseMaterial;
    }

#if UNITY_EDITOR
    private static void WarmUpMaterialVariants(Material mat)
    {
        if (!mat) return;
        int passes = mat.passCount;
        for (int i = 0; i < passes; i++)
            UnityEditor.ShaderUtil.CompilePass(mat, i, true);
    }
#endif

    private void EnsureBuffers()
    {
        if (_mesh != null) return;

        _quadCapacity = InitialQuadCapacity;
        _quadAllocated = 0;
        _freeQuads.Clear();

        _verts = new NativeArray<SpriteVertex>(_quadCapacity * VertsPerQuad, Allocator.Persistent);
        _indices = new NativeArray<ushort>(_quadCapacity * IndicesPerQuad, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

        _mesh = new Mesh { name = "RoSpriteBatch" };
        _mesh.MarkDynamic();
        _mesh.indexFormat = IndexFormat.UInt16;
        _mesh.SetVertexBufferParams(_quadCapacity * VertsPerQuad, VertexAttributes);
        _mesh.SetIndexBufferParams(_quadCapacity * IndicesPerQuad, IndexFormat.UInt16);
        _mesh.subMeshCount = 1;
        _mesh.SetSubMesh(0, new SubMeshDescriptor(0, 0), FastUpdate);
        _mesh.bounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 100000f));

        EnsureBatchRenderer();
    }

    private void EnsureBatchRenderer()
    {
        if (_batchRenderer != null || _mesh == null) return;

        //world space mesh, must stay unparented at identity and survive map changes
        _batchRendererGo = new GameObject("RoSpriteBatchRenderer");
        DontDestroyOnLoad(_batchRendererGo);
        _batchRendererGo.layer = LayerMask.NameToLayer("Characters");
        var filter = _batchRendererGo.AddComponent<MeshFilter>();
        filter.sharedMesh = _mesh;
        _batchRenderer = _batchRendererGo.AddComponent<MeshRenderer>();
        _batchRenderer.sharedMaterial = _baseMaterial;
        _batchRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _batchRenderer.receiveShadows = false;
        _batchRenderer.lightProbeUsage = LightProbeUsage.Off;
        _batchRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        _batchRenderer.enabled = false;
    }

    public bool TryRegister(Texture2D atlas, int layerCount, out SpriteBatchHandle handle)
    {
        handle = default;
        if (!BatchingAvailable || atlas == null || layerCount <= 0) return false;

        EnsureBuffers();

        _atlasArray ??= new SpriteAtlasArray(AtlasInitialSliceCount);
        if (!_atlasArray.TryAdd(atlas, out var region)) return false;
        SyncArrayTexture();

        var slots = new int[layerCount];
        for (var i = 0; i < layerCount; i++)
        {
            slots[i] = AllocQuad();
            if (slots[i] < 0)
            {
                for (var j = 0; j < i; j++)
                    _freeQuads.Push(slots[j]);
                _atlasArray.Release(atlas);
                return false;
            }
        }

        int entryId;
        if (_freeEntries.Count > 0)
            entryId = _freeEntries.Pop();
        else
        {
            if (_entryHighWater >= _entries.Length)
            {
                Array.Resize(ref _entries, _entries.Length * 2);
                Array.Resize(ref _entrySeq, _entries.Length);
                Array.Resize(ref _sortValues, _entries.Length);
            }
            entryId = _entryHighWater++;
        }

        _entries[entryId] = new BatchEntry
        {
            InUse = true,
            Atlas = atlas,
            Region = region,
            Slots = slots,
            SlotCount = layerCount,
        };

        handle = new SpriteBatchHandle { EntryId = entryId, Generation = Generation, Seq = _entrySeq[entryId] };
        return true;
    }

    public void Unregister(ref SpriteBatchHandle handle)
    {
        if (IsValidHandle(handle))
        {
            ref var entry = ref _entries[handle.EntryId];
            var slots = entry.Slots;
            for (var i = 0; i < entry.SlotCount; i++)
                _freeQuads.Push(slots[i]);
            if (_atlasArray != null && entry.Atlas != null)
                _atlasArray.Release(entry.Atlas);
            _entries[handle.EntryId] = default;
            _entrySeq[handle.EntryId]++;
            _freeEntries.Push(handle.EntryId);
        }
        handle = default;
    }

    public bool IsValidHandle(in SpriteBatchHandle handle)
    {
        return handle.Generation == Generation
            && handle.EntryId >= 0
            && handle.EntryId < _entryHighWater
            && _entries[handle.EntryId].InUse
            && _entrySeq[handle.EntryId] == handle.Seq;
    }

    private static Camera _billboardCamera;

    internal static Quaternion BillboardCameraRotation()
    {
        if (_billboardCamera == null)
            _billboardCamera = Camera.main;
        return _billboardCamera != null ? _billboardCamera.transform.rotation : Quaternion.identity;
    }

    internal static Matrix4x4 BillboardBakeMatrix(Transform t)
    {
        var ltw = t.localToWorldMatrix;
        if (_billboardCamera == null)
            _billboardCamera = Camera.main;
        if (_billboardCamera == null)
            return ltw;

        var delta = _billboardCamera.transform.rotation * Quaternion.Inverse(t.rotation);
        if (delta == Quaternion.identity)
            return ltw;

        var corrected = Matrix4x4.Rotate(delta) * ltw;
        corrected.SetColumn(3, ltw.GetColumn(3));
        return corrected;
    }

    public bool WriteSprite(ref SpriteBatchHandle handle, Matrix4x4 localToWorld,
        Transform xform, Transform rootXform,
        Vector3[] verts, Vector2[] uvs, Color[] vcolors,
        in SpriteRenderParams p, bool cameraFacing = false)
    {
        if (!IsValidHandle(handle))
            return false;

        if (verts == null
            || verts.Length == 0
            || verts.Length % VertsPerQuad != 0
            || uvs == null
            || uvs.Length < verts.Length
            || vcolors == null
            || vcolors.Length < verts.Length)
        {
            Unregister(ref handle);
            return false;
        }

        using (_markerWriteSprite.Auto())
        {
            int newLayerCount = verts.Length / VertsPerQuad;
            if (!EnsureSlotCount(ref handle, newLayerCount))
            {
                Unregister(ref handle);
                return false;
            }

            ref var entry = ref _entries[handle.EntryId];
            entry.Written = true;
            entry.Hidden = p.hidden;
            entry.RootPos = p.rootPos;
            entry.RootKey = p.rootKey;
            entry.RootOrder = p.rootOrder;
            entry.MemberOrder = p.sortOrder;
            entry.CullCenter = new Vector3(localToWorld.m03, localToWorld.m13, localToWorld.m23);
            entry.Xform = xform;
            entry.RootXform = rootXform;
            entry.CameraFacing = cameraFacing;
            entry.CornerRotation = cameraFacing ? BillboardCameraRotation() : Quaternion.identity;

            var region = entry.Region;
            var slots = entry.Slots;
            float maxCornerSq = 0f;
            for (int layer = 0; layer < newLayerCount; layer++)
            {
                int vb = layer * VertsPerQuad;
                float cornerSq = WriteSlot(slots[layer], localToWorld, region,
                    verts[vb + 0], verts[vb + 1], verts[vb + 2], verts[vb + 3],
                    uvs[vb + 0],   uvs[vb + 1],   uvs[vb + 2],   uvs[vb + 3],
                    vcolors[vb + 0], vcolors[vb + 1], vcolors[vb + 2], vcolors[vb + 3],
                    p);
                if (cornerSq > maxCornerSq) maxCornerSq = cornerSq;
            }

            entry.Radius = Mathf.Sqrt(maxCornerSq) + Mathf.Abs(p.vPos) + 1f;
            return true;
        }
    }

    private bool EnsureSlotCount(ref SpriteBatchHandle handle, int newCount)
    {
        ref var entry = ref _entries[handle.EntryId];
        int oldCount = entry.SlotCount;
        if (oldCount == newCount) return true;

        if (newCount > oldCount)
        {
            if (entry.Slots == null || entry.Slots.Length < newCount)
            {
                var grown = new int[Mathf.Max(newCount, entry.Slots != null ? entry.Slots.Length * 2 : 4)];
                if (entry.Slots != null)
                    Array.Copy(entry.Slots, grown, oldCount);
                entry.Slots = grown;
            }
            for (int i = oldCount; i < newCount; i++)
            {
                int quad = AllocQuad();
                if (quad < 0)
                {
                    for (int j = oldCount; j < i; j++)
                        _freeQuads.Push(entry.Slots[j]);
                    return false;
                }
                entry.Slots[i] = quad;
            }
        }
        else
        {
            for (int i = newCount; i < oldCount; i++)
                _freeQuads.Push(entry.Slots[i]);
        }

        entry.SlotCount = newCount;
        return true;
    }

    private int AllocQuad()
    {
        if (_freeQuads.Count > 0)
            return _freeQuads.Pop();
        if (_quadAllocated >= _quadCapacity)
        {
            if (_quadCapacity >= MaxQuadCapacity)
                return -1;
            Grow();
        }
        return _quadAllocated++;
    }

    private void Grow()
    {
        int newCapacity = Mathf.Min(_quadCapacity * 2, MaxQuadCapacity);

        var newVerts = new NativeArray<SpriteVertex>(newCapacity * VertsPerQuad, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        var newIndices = new NativeArray<ushort>(newCapacity * IndicesPerQuad, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        NativeArray<SpriteVertex>.Copy(_verts, newVerts, _quadCapacity * VertsPerQuad);
        _verts.Dispose();
        _indices.Dispose();
        _verts = newVerts;
        _indices = newIndices;
        _quadCapacity = newCapacity;

        _mesh.SetVertexBufferParams(_quadCapacity * VertsPerQuad, VertexAttributes);
        _mesh.SetIndexBufferParams(_quadCapacity * IndicesPerQuad, IndexFormat.UInt16);

        _dirtyMin = 0;
        _dirtyMax = _quadAllocated - 1;
    }

    private float WriteSlot(int slot, Matrix4x4 localToWorld, in SpriteAtlasRegion region,
        Vector3 c0, Vector3 c1, Vector3 c2, Vector3 c3,
        Vector2 u0, Vector2 u1, Vector2 u2, Vector2 u3,
        Color vc0, Color vc1, Color vc2, Color vc3,
        in SpriteRenderParams p)
    {
        int vb = slot * VertsPerQuad;
        float hiddenF = p.hidden ? 1f : 0f;
        var packed = new Vector4(p.colorDrain, p.offset, p.vPos, p.width);
        var sprColor = p.spriteColor;
        float sliceIdx = region.Slice;

        var anchor = new Vector3(localToWorld.m03, localToWorld.m13, localToWorld.m23);

        var spriteUp = localToWorld.MultiplyVector(Vector3.up);

        var corner0 = localToWorld.MultiplyVector(c0);
        var corner1 = localToWorld.MultiplyVector(c1);
        var corner2 = localToWorld.MultiplyVector(c2);
        var corner3 = localToWorld.MultiplyVector(c3);

        var origin0 = localToWorld.MultiplyVector(new Vector3(c0.x, 0f, 0f));
        var origin1 = localToWorld.MultiplyVector(new Vector3(c1.x, 0f, 0f));
        var origin2 = localToWorld.MultiplyVector(new Vector3(c2.x, 0f, 0f));
        var origin3 = localToWorld.MultiplyVector(new Vector3(c3.x, 0f, 0f));

        var uvScale = region.UvScale;
        var uvOffset = region.UvOffset;
        var uvRect = region.UvClampRect;

        _verts[vb + 0] = new SpriteVertex
        {
            position = origin0, normal = spriteUp, color = vc0,
            uv = new Vector2(u0.x * uvScale.x + uvOffset.x, u0.y * uvScale.y + uvOffset.y),
            slice = sliceIdx, uvRect = uvRect,
            anchorWS = anchor, cornerOS = new Vector4(corner0.x, corner0.y, corner0.z, c0.y),
            spriteColor = sprColor, packed = packed, hiddenX = new Vector2(hiddenF, c0.x),
        };
        _verts[vb + 1] = new SpriteVertex
        {
            position = origin1, normal = spriteUp, color = vc1,
            uv = new Vector2(u1.x * uvScale.x + uvOffset.x, u1.y * uvScale.y + uvOffset.y),
            slice = sliceIdx, uvRect = uvRect,
            anchorWS = anchor, cornerOS = new Vector4(corner1.x, corner1.y, corner1.z, c1.y),
            spriteColor = sprColor, packed = packed, hiddenX = new Vector2(hiddenF, c1.x),
        };
        _verts[vb + 2] = new SpriteVertex
        {
            position = origin2, normal = spriteUp, color = vc2,
            uv = new Vector2(u2.x * uvScale.x + uvOffset.x, u2.y * uvScale.y + uvOffset.y),
            slice = sliceIdx, uvRect = uvRect,
            anchorWS = anchor, cornerOS = new Vector4(corner2.x, corner2.y, corner2.z, c2.y),
            spriteColor = sprColor, packed = packed, hiddenX = new Vector2(hiddenF, c2.x),
        };
        _verts[vb + 3] = new SpriteVertex
        {
            position = origin3, normal = spriteUp, color = vc3,
            uv = new Vector2(u3.x * uvScale.x + uvOffset.x, u3.y * uvScale.y + uvOffset.y),
            slice = sliceIdx, uvRect = uvRect,
            anchorWS = anchor, cornerOS = new Vector4(corner3.x, corner3.y, corner3.z, c3.y),
            spriteColor = sprColor, packed = packed, hiddenX = new Vector2(hiddenF, c3.x),
        };

        if (slot < _dirtyMin) _dirtyMin = slot;
        if (slot > _dirtyMax) _dirtyMax = slot;

        float m0 = corner0.sqrMagnitude, m1 = corner1.sqrMagnitude;
        float m2 = corner2.sqrMagnitude, m3 = corner3.sqrMagnitude;
        float max = m0 > m1 ? m0 : m1;
        if (m2 > max) max = m2;
        if (m3 > max) max = m3;
        return max;
    }

    private void LateUpdate()
    {
        ProcessShadowRaycasts();
        _atlasArray?.SweepExpired();

        if (!BatchingAvailable || _mesh == null)
        {
            if (_batchRenderer != null) _batchRenderer.enabled = false;
            return;
        }

        EnsureMaterials();
        EnsureBatchRenderer();
        if (_baseMaterial == null || _baseMaterial.passCount < 2 || _batchRenderer == null)
        {
            if (_batchRenderer != null) _batchRenderer.enabled = false;
            return;
        }

        using (_markerLateUpdate.Auto())
        {
            var camPos = Vector3.zero;
            var sortAxis = Vector3.forward;
            bool useSortAxis = false;
            bool haveFrustum = _camera != null;
            if (haveFrustum)
            {
                GeometryUtility.CalculateFrustumPlanes(_camera, _frustumPlanes);
                camPos = _camera.transform.position;
                useSortAxis = _camera.transparencySortMode == TransparencySortMode.CustomAxis;
                if (useSortAxis)
                {
                    sortAxis = _camera.transparencySortAxis.normalized;
                    if (sortAxis.sqrMagnitude < 0.0001f)
                        sortAxis = _camera.transform.forward;
                }
            }

            SyncBatchedTransforms();

            int sortCount = 0;
            using (_markerCullCollect.Auto())
            {
                for (var i = 0; i < _entryHighWater; i++)
                {
                    ref var entry = ref _entries[i];
                    if (!entry.InUse || !entry.Written || entry.Hidden) continue;

                    if (haveFrustum && IsCulled(entry.CullCenter, entry.Radius))
                        continue;

                    entry.SortDepth = useSortAxis
                        ? Vector3.Dot(entry.RootPos - camPos, sortAxis)
                        : (entry.RootPos - camPos).magnitude;
                    _sortValues[sortCount] = i;
                    sortCount++;
                }
            }

            if (sortCount == 0)
            {
                _batchRenderer.enabled = false;
                return;
            }

            using (_markerSort.Auto())
                Array.Sort(_sortValues, 0, sortCount, _entrySortComparer);

            int indexCount = 0;
            using (_markerBuildIndices.Auto())
            {
                for (var s = 0; s < sortCount; s++)
                {
                    ref var sorted = ref _entries[_sortValues[s]];
                    var slots = sorted.Slots;
                    for (var q = 0; q < sorted.SlotCount; q++)
                    {
                        int vb = slots[q] * VertsPerQuad;
                        _indices[indexCount + 0] = (ushort)(vb + 0);
                        _indices[indexCount + 1] = (ushort)(vb + 1);
                        _indices[indexCount + 2] = (ushort)(vb + 2);
                        _indices[indexCount + 3] = (ushort)(vb + 1);
                        _indices[indexCount + 4] = (ushort)(vb + 3);
                        _indices[indexCount + 5] = (ushort)(vb + 2);
                        indexCount += IndicesPerQuad;
                    }
                }
            }

            using (_markerUploadMesh.Auto())
            {
                if (_dirtyMin <= _dirtyMax)
                {
                    int vertStart = _dirtyMin * VertsPerQuad;
                    int vertCount = (_dirtyMax - _dirtyMin + 1) * VertsPerQuad;
                    _mesh.SetVertexBufferData(_verts, vertStart, vertStart, vertCount, 0, FastUpdate);
                    _dirtyMin = int.MaxValue;
                    _dirtyMax = int.MinValue;
                }

                _mesh.SetIndexBufferData(_indices, 0, 0, indexCount, FastUpdate);
                _mesh.SetSubMesh(0, new SubMeshDescriptor(0, indexCount), FastUpdate);
            }

            _batchRenderer.enabled = true;
        }
    }

    private void SyncBatchedTransforms()
    {
        using var _ = _markerSyncTransforms.Auto();

        var camRot = BillboardCameraRotation();
        bool haveCam = _billboardCamera != null;

        for (var i = 0; i < _entryHighWater; i++)
        {
            ref var entry = ref _entries[i];
            if (!entry.InUse || !entry.Written || entry.Hidden || entry.Xform == null) continue;

            var anchor = entry.Xform.position;
            bool anchorChanged = anchor != entry.CullCenter; //Vector3 == is epsilon-tolerant; stationary sprites cost one read

            bool rotate = false;
            var delta = Quaternion.identity;
            if (entry.CameraFacing && haveCam)
            {
                delta = camRot * Quaternion.Inverse(entry.CornerRotation);
                if (delta != Quaternion.identity)
                {
                    rotate = true;
                    entry.CornerRotation = camRot;
                }
            }

            if (!anchorChanged && !rotate) continue;

            if (anchorChanged)
            {
                entry.CullCenter = anchor;
                if (entry.RootXform != null)
                    entry.RootPos = entry.RootXform.position;
            }

            var slots = entry.Slots;
            for (var q = 0; q < entry.SlotCount; q++)
            {
                int slot = slots[q];
                int vb = slot * VertsPerQuad;
                for (var v = 0; v < VertsPerQuad; v++)
                {
                    var sv = _verts[vb + v];
                    if (anchorChanged)
                        sv.anchorWS = anchor;
                    if (rotate)
                    {
                        var c = sv.cornerOS;
                        var rc = delta * new Vector3(c.x, c.y, c.z);
                        sv.cornerOS = new Vector4(rc.x, rc.y, rc.z, c.w);
                        sv.normal = delta * sv.normal;
                        sv.position = delta * sv.position;
                    }
                    _verts[vb + v] = sv;
                }
                if (slot < _dirtyMin) _dirtyMin = slot;
                if (slot > _dirtyMax) _dirtyMax = slot;
            }
        }
    }

    private static bool IsCulled(Vector3 center, float radius)
    {
        for (var i = 0; i < 6; i++)
        {
            if (_frustumPlanes[i].GetDistanceToPoint(center) < -radius)
                return true;
        }
        return false;
    }

    public string DumpBatchState()
    {
        int live = 0;
        for (var i = 0; i < _entryHighWater; i++)
            if (_entries[i].InUse) live++;
        var arrayDump = _atlasArray != null ? _atlasArray.DumpOccupancy() : "<no atlas array>";
        return $"Entries: {live} live / {_entryHighWater} high water, quads {_quadAllocated}/{_quadCapacity}\n{arrayDump}";
    }
}

public struct SpriteBatchHandle
{
    public int EntryId;
    public int Generation;
    public int Seq;
}

public struct SpriteRenderParams
{
    public Color spriteColor;
    public float colorDrain;
    public float offset;
    public float vPos;
    public float width;
    public bool hidden;
    public int sortOrder;
    public int rootKey;
    public int rootOrder;
    public Vector3 rootPos;
}

[StructLayout(LayoutKind.Sequential)]
public struct SpriteVertex
{
    public Vector3 position;
    public Vector3 normal;
    public Color color;
    public Vector2 uv;
    public float slice;
    public Vector4 uvRect;
    public Vector3 anchorWS;
    public Vector4 cornerOS;
    public Color spriteColor;
    public Vector4 packed;
    public Vector2 hiddenX;
}
