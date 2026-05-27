using Assets.Scripts.Effects;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Assets.Scripts.UI.ConfigWindow;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public partial class RoSpriteBatcher : MonoBehaviour
{
    public static RoSpriteBatcher Instance;
    
    public bool EnableBatching = true;

    internal const int VertsPerQuad = 4;
    internal const int IndicesPerQuad = 6;
    internal const int InitialQuadCapacity = 256;
    
    internal const int SortLayerStride = 64;
    internal const int SortOrderClamp = 256;

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
        new(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 3),
        new(VertexAttribute.TexCoord4, VertexAttributeFormat.Float32, 4),
        new(VertexAttribute.TexCoord5, VertexAttributeFormat.Float32, 4),
        new(VertexAttribute.TexCoord6, VertexAttributeFormat.Float32, 4),
        new(VertexAttribute.TexCoord7, VertexAttributeFormat.Float32, 1),
    };

    private const CameraEvent DepthEvent = CameraEvent.BeforeForwardAlpha;
    private const CameraEvent ColorEvent = CameraEvent.BeforeForwardAlpha;
    private const CameraEvent XRayEvent = CameraEvent.AfterForwardOpaque;

    private Camera _camera;
    private CommandBuffer _depthCmd;
    private CommandBuffer _colorCmd;
    private CommandBuffer _xrayCmd;

    private readonly HashSet<Camera> _attachedCameras = new();

    private static readonly Plane[] _frustumPlanes = new Plane[6];

    private Material _baseMaterial;
    private Material _xrayMaterial;

    private readonly Dictionary<Texture2D, AtlasBatch> _atlases = new();
    private readonly List<AtlasBatch> _atlasList = new();

    private void OnEnable()
    {
        Instance = this;

        _camera = GetComponent<Camera>();

        _depthCmd = new CommandBuffer { name = "RoSpriteBatcher - Depth" };
        _colorCmd = new CommandBuffer { name = "RoSpriteBatcher - Color" };
        _xrayCmd  = new CommandBuffer { name = "RoSpriteBatcher - XRay" };

        AttachToCamera(_camera);

#if UNITY_EDITOR
        Camera.onPreCull += OnAnyCameraPreCull;
#endif

        EnsureMaterials();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        Camera.onPreCull -= OnAnyCameraPreCull;
#endif

        foreach (var cam in _attachedCameras)
        {
            if (cam != null)
            {
                cam.RemoveCommandBuffer(DepthEvent, _depthCmd);
                cam.RemoveCommandBuffer(ColorEvent, _colorCmd);
                cam.RemoveCommandBuffer(XRayEvent, _xrayCmd);
            }
        }
        _attachedCameras.Clear();

        _depthCmd?.Release(); _depthCmd = null;
        _colorCmd?.Release(); _colorCmd = null;
        _xrayCmd?.Release();  _xrayCmd  = null;

        for (int i = 0; i < _atlasList.Count; i++)
            _atlasList[i].Dispose();
        _atlasList.Clear();
        _atlases.Clear();

        if (_baseMaterial != null) { Destroy(_baseMaterial); _baseMaterial = null; }
        if (_xrayMaterial != null) { Destroy(_xrayMaterial); _xrayMaterial = null; }

        DisposeShadowRaycasts();
    }

    private void AttachToCamera(Camera cam)
    {
        if (cam == null) return;
        if (_attachedCameras.Contains(cam)) return;
        cam.AddCommandBuffer(DepthEvent, _depthCmd);
        cam.AddCommandBuffer(ColorEvent, _colorCmd);
        cam.AddCommandBuffer(XRayEvent, _xrayCmd);
        _attachedCameras.Add(cam);
    }

#if UNITY_EDITOR
    private void OnAnyCameraPreCull(Camera cam)
    {
        if (cam == null) return;
        if (cam.cameraType != CameraType.SceneView) return;
        AttachToCamera(cam);
    }
#endif

    private void EnsureMaterials()
    {
        var cache = ShaderCache.Instance;
        if (!cache) return;

        if (!_baseMaterial && cache.SpriteShader)
        {
            _baseMaterial = new Material(cache.SpriteShader);
            _baseMaterial.EnableKeyword("DYNBATCH_ON");
#if UNITY_EDITOR
            WarmUpMaterialVariants(_baseMaterial);
#endif
        }
        if (!_xrayMaterial && cache.SpriteShaderWithXRay)
        {
            _xrayMaterial = new Material(cache.SpriteShaderWithXRay);
            _xrayMaterial.EnableKeyword("DYNBATCH_ON");
#if UNITY_EDITOR
            WarmUpMaterialVariants(_xrayMaterial);
#endif
        }
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

    private void LateUpdate()
    {
        ProcessShadowRaycasts();

        if (_depthCmd == null) return;

        _depthCmd.Clear();
        _colorCmd.Clear();
        _xrayCmd.Clear();

        if (!EnableBatching) return;

        EnsureMaterials();
        if (_baseMaterial == null || _baseMaterial.passCount < 2) return;

        bool xrayWanted = GameConfig.Data != null && GameConfig.Data.EnableXRay
            && _xrayMaterial != null && _xrayMaterial.passCount >= 3;

        bool haveFrustum = _camera != null;
        if (haveFrustum)
            GeometryUtility.CalculateFrustumPlanes(_camera, _frustumPlanes);

        for (int i = 0; i < _atlasList.Count; i++)
        {
            var batch = _atlasList[i];
            if (batch.Allocated <= 0) continue;

            batch.FlushUploads();

            if (!batch.TryGetActiveBounds(out var bounds))
                continue;

            batch.Mesh.bounds = bounds;

            if (haveFrustum && !GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds))
                continue;

            _depthCmd.DrawMesh(batch.Mesh, Matrix4x4.identity, _baseMaterial, 0, 0, batch.PropertyBlock);
            _colorCmd.DrawMesh(batch.Mesh, Matrix4x4.identity, _baseMaterial, 0, 1, batch.PropertyBlock);

            if (xrayWanted)
                _xrayCmd.DrawMesh(batch.Mesh, Matrix4x4.identity, _xrayMaterial, 0, 2, batch.PropertyBlock);
        }
    }
    
    public SpriteBatchHandle Register(Texture2D atlas, int layerCount)
    {
        var batch = GetOrCreateBatch(atlas);
        var slots = new int[layerCount];
        for (int i = 0; i < layerCount; i++)
            slots[i] = batch.AllocSlot();
        return new SpriteBatchHandle { atlas = atlas, slots = slots };
    }

    public void Unregister(ref SpriteBatchHandle handle)
    {
        if (handle.atlas == null || handle.slots == null) return;
        if (_atlases.TryGetValue(handle.atlas, out var batch))
        {
            var slots = handle.slots;
            for (int i = 0; i < slots.Length; i++)
                batch.FreeSlot(slots[i]);
        }
        handle.atlas = null;
        handle.slots = null;
    }

    public void WriteSprite(ref SpriteBatchHandle handle, Matrix4x4 localToWorld,
        Vector3[] verts, Vector2[] uvs, Color[] vcolors,
        in SpriteRenderParams p)
    {
        if (!handle.atlas || verts == null) return;
        if (!_atlases.TryGetValue(handle.atlas, out var batch)) return;

        int newLayerCount = verts.Length / VertsPerQuad;
        EnsureSlotCount(ref handle, batch, newLayerCount);

        var slots = handle.slots;
        for (int layer = 0; layer < newLayerCount; layer++)
        {
            int vb = layer * VertsPerQuad;
            batch.WriteSlot(slots[layer], localToWorld,
                verts[vb + 0], verts[vb + 1], verts[vb + 2], verts[vb + 3],
                uvs[vb + 0],   uvs[vb + 1],   uvs[vb + 2],   uvs[vb + 3],
                vcolors[vb + 0], vcolors[vb + 1], vcolors[vb + 2], vcolors[vb + 3],
                p, ComputeSortKey(p.sortOrder, layer));
        }
    }

    internal static float ComputeSortKey(int sortOrder, int layer)
    {
        if (sortOrder > SortOrderClamp) sortOrder = SortOrderClamp;
        else if (sortOrder < -SortOrderClamp) sortOrder = -SortOrderClamp;
        return sortOrder * SortLayerStride + layer;
    }

    private void EnsureSlotCount(ref SpriteBatchHandle handle, AtlasBatch batch, int newCount)
    {
        var slots = handle.slots;
        int oldCount = slots != null ? slots.Length : 0;
        if (oldCount == newCount) return;

        if (newCount > oldCount)
        {
            var newSlots = new int[newCount];
            if (slots != null)
                Array.Copy(slots, newSlots, oldCount);
            for (int i = oldCount; i < newCount; i++)
                newSlots[i] = batch.AllocSlot();
            handle.slots = newSlots;
        }
        else
        {
            for (int i = newCount; i < oldCount; i++)
                batch.FreeSlot(slots[i]);
            var newSlots = new int[newCount];
            Array.Copy(slots, newSlots, newCount);
            handle.slots = newSlots;
        }
    }

    private AtlasBatch GetOrCreateBatch(Texture2D atlas)
    {
        if (!_atlases.TryGetValue(atlas, out var batch))
        {
            batch = new AtlasBatch(atlas, InitialQuadCapacity);
            _atlases[atlas] = batch;
            _atlasList.Add(batch);
        }
        return batch;
    }
}

public struct SpriteBatchHandle
{
    public Texture2D atlas;
    public int[] slots;
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
}

[StructLayout(LayoutKind.Sequential)]
public struct SpriteVertex
{
    public Vector3 position;
    public Vector3 normal;
    public Color color;
    public Vector2 uv;
    public float sortKey;
    public Vector3 anchorWS;
    public Vector4 cornerOS;
    public Color spriteColor;
    public Vector4 packed;
    public float hidden;
}

internal sealed class AtlasBatch : IDisposable
{
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    public Texture2D Atlas;
    public Mesh Mesh;
    public MaterialPropertyBlock PropertyBlock;
    public NativeArray<SpriteVertex> Verts;
    public NativeArray<ushort> Indices;
    public readonly Stack<int> FreeSlots = new();
    private bool[] _slotInUse;
    public int Capacity;
    public int Allocated;

    private int _dirtyMin = int.MaxValue;
    private int _dirtyMax = int.MinValue;
    private bool _submeshDirty;

    private Bounds _cachedBounds;
    private bool _hasCachedBounds;
    private bool _boundsDirty = true;

    public AtlasBatch(Texture2D atlas, int initialCapacity)
    {
        Atlas = atlas;
        Capacity = initialCapacity;
        Allocated = 0;

        Mesh = new Mesh { name = $"RoSpriteBatch - {atlas?.name}" };
        Mesh.MarkDynamic();
        Mesh.indexFormat = IndexFormat.UInt16;

        _slotInUse = new bool[Capacity];
        Verts = new NativeArray<SpriteVertex>(Capacity * RoSpriteBatcher.VertsPerQuad, Allocator.Persistent);
        Indices = new NativeArray<ushort>(Capacity * RoSpriteBatcher.IndicesPerQuad, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

        FillIndices(0, Capacity);

        Mesh.SetVertexBufferParams(Capacity * RoSpriteBatcher.VertsPerQuad, RoSpriteBatcher.VertexAttributes);
        Mesh.SetIndexBufferParams(Capacity * RoSpriteBatcher.IndicesPerQuad, IndexFormat.UInt16);
        Mesh.SetIndexBufferData(Indices, 0, 0, Capacity * RoSpriteBatcher.IndicesPerQuad, RoSpriteBatcher.FastUpdate);
        Mesh.subMeshCount = 1;
        Mesh.SetSubMesh(0, new SubMeshDescriptor(0, 0), RoSpriteBatcher.FastUpdate);

        PropertyBlock = new MaterialPropertyBlock();
        if (atlas)
            PropertyBlock.SetTexture(MainTexId, atlas);
    }

    private void FillIndices(int startQuad, int endQuad)
    {
        for (int q = startQuad; q < endQuad; q++)
        {
            int vb = q * RoSpriteBatcher.VertsPerQuad;
            int ib = q * RoSpriteBatcher.IndicesPerQuad;
            Indices[ib + 0] = (ushort)(vb + 0);
            Indices[ib + 1] = (ushort)(vb + 1);
            Indices[ib + 2] = (ushort)(vb + 2);
            Indices[ib + 3] = (ushort)(vb + 1);
            Indices[ib + 4] = (ushort)(vb + 3);
            Indices[ib + 5] = (ushort)(vb + 2);
        }
    }

    public int AllocSlot()
    {
        int slot;
        if (FreeSlots.Count > 0)
        {
            slot = FreeSlots.Pop();
        }
        else
        {
            if (Allocated >= Capacity)
                Grow();
            slot = Allocated++;
            _submeshDirty = true;
        }
        _slotInUse[slot] = true;
        _boundsDirty = true;
        return slot;
    }

    public void FreeSlot(int slot)
    {
        int vb = slot * RoSpriteBatcher.VertsPerQuad;
        var zero = default(SpriteVertex);
        Verts[vb + 0] = zero;
        Verts[vb + 1] = zero;
        Verts[vb + 2] = zero;
        Verts[vb + 3] = zero;
        MarkDirty(slot);
        _slotInUse[slot] = false;
        _boundsDirty = true;
        FreeSlots.Push(slot);
    }

    public void WriteSlot(int slot, Matrix4x4 localToWorld,
        Vector3 c0, Vector3 c1, Vector3 c2, Vector3 c3,
        Vector2 u0, Vector2 u1, Vector2 u2, Vector2 u3,
        Color vc0, Color vc1, Color vc2, Color vc3,
        in SpriteRenderParams p, float sortKey)
    {
        int vb = slot * RoSpriteBatcher.VertsPerQuad;
        float hiddenF = p.hidden ? 1f : 0f;
        var packed = new Vector4(p.colorDrain, p.offset, p.vPos, p.width);
        var sprColor = p.spriteColor;

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

        Verts[vb + 0] = new SpriteVertex
        {
            position = origin0, normal = spriteUp, color = vc0, uv = u0,
            anchorWS = anchor, cornerOS = new Vector4(corner0.x, corner0.y, corner0.z, c0.y),
            spriteColor = sprColor, packed = packed, hidden = hiddenF, sortKey = sortKey,
        };
        Verts[vb + 1] = new SpriteVertex
        {
            position = origin1, normal = spriteUp, color = vc1, uv = u1,
            anchorWS = anchor, cornerOS = new Vector4(corner1.x, corner1.y, corner1.z, c1.y),
            spriteColor = sprColor, packed = packed, hidden = hiddenF, sortKey = sortKey,
        };
        Verts[vb + 2] = new SpriteVertex
        {
            position = origin2, normal = spriteUp, color = vc2, uv = u2,
            anchorWS = anchor, cornerOS = new Vector4(corner2.x, corner2.y, corner2.z, c2.y),
            spriteColor = sprColor, packed = packed, hidden = hiddenF, sortKey = sortKey,
        };
        Verts[vb + 3] = new SpriteVertex
        {
            position = origin3, normal = spriteUp, color = vc3, uv = u3,
            anchorWS = anchor, cornerOS = new Vector4(corner3.x, corner3.y, corner3.z, c3.y),
            spriteColor = sprColor, packed = packed, hidden = hiddenF, sortKey = sortKey,
        };

        MarkDirty(slot);
        _boundsDirty = true;
    }

    private void MarkDirty(int slot)
    {
        if (slot < _dirtyMin) _dirtyMin = slot;
        if (slot > _dirtyMax) _dirtyMax = slot;
    }

    private void Grow()
    {
        int oldCapacity = Capacity;
        int newCapacity = Capacity * 2;

        var newVerts = new NativeArray<SpriteVertex>(newCapacity * RoSpriteBatcher.VertsPerQuad, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        var newIndices = new NativeArray<ushort>(newCapacity * RoSpriteBatcher.IndicesPerQuad, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        NativeArray<SpriteVertex>.Copy(Verts, newVerts, oldCapacity * RoSpriteBatcher.VertsPerQuad);
        NativeArray<ushort>.Copy(Indices, newIndices, oldCapacity * RoSpriteBatcher.IndicesPerQuad);
        Verts.Dispose();
        Indices.Dispose();
        Verts = newVerts;
        Indices = newIndices;
        Capacity = newCapacity;
        Array.Resize(ref _slotInUse, newCapacity);
        FillIndices(oldCapacity, newCapacity);

        Mesh.SetVertexBufferParams(Capacity * RoSpriteBatcher.VertsPerQuad, RoSpriteBatcher.VertexAttributes);
        Mesh.SetIndexBufferParams(Capacity * RoSpriteBatcher.IndicesPerQuad, IndexFormat.UInt16);
        Mesh.SetIndexBufferData(Indices, 0, 0, Capacity * RoSpriteBatcher.IndicesPerQuad, RoSpriteBatcher.FastUpdate);
        
        _dirtyMin = 0;
        _dirtyMax = Allocated - 1;
        _submeshDirty = true;
    }

    public bool TryGetActiveBounds(out Bounds bounds)
    {
        if (!_boundsDirty)
        {
            bounds = _cachedBounds;
            return _hasCachedBounds;
        }

        var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        bool any = false;
        for (int s = 0; s < Allocated; s++)
        {
            if (!_slotInUse[s]) continue;
            var a = Verts[s * RoSpriteBatcher.VertsPerQuad].anchorWS;
            if (a.x < min.x) min.x = a.x;
            if (a.y < min.y) min.y = a.y;
            if (a.z < min.z) min.z = a.z;
            if (a.x > max.x) max.x = a.x;
            if (a.y > max.y) max.y = a.y;
            if (a.z > max.z) max.z = a.z;
            any = true;
        }

        _boundsDirty = false;

        if (!any)
        {
            _hasCachedBounds = false;
            bounds = default;
            return false;
        }
        
        var padding = new Vector3(2f, 5f, 2f);
        var center = (min + max) * 0.5f;
        var size = max - min + padding * 2f;
        _cachedBounds = new Bounds(center, size);
        _hasCachedBounds = true;
        bounds = _cachedBounds;
        return true;
    }

    public void FlushUploads()
    {
        if (_dirtyMin <= _dirtyMax && Allocated > 0)
        {
            int vertStart = _dirtyMin * RoSpriteBatcher.VertsPerQuad;
            int vertCount = (_dirtyMax - _dirtyMin + 1) * RoSpriteBatcher.VertsPerQuad;
            Mesh.SetVertexBufferData(Verts, vertStart, vertStart, vertCount, 0, RoSpriteBatcher.FastUpdate);
            _dirtyMin = int.MaxValue;
            _dirtyMax = int.MinValue;
        }

        if (_submeshDirty)
        {
            Mesh.SetSubMesh(0, new SubMeshDescriptor(0, Allocated * RoSpriteBatcher.IndicesPerQuad, MeshTopology.Triangles), RoSpriteBatcher.FastUpdate);
            _submeshDirty = false;
        }
    }

    public void Dispose()
    {
        if (Verts.IsCreated) Verts.Dispose();
        if (Indices.IsCreated) Indices.Dispose();
        if (Mesh != null)
        {
            UnityEngine.Object.Destroy(Mesh);
            Mesh = null;
        }
    }
}
