using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Assets.Scripts.UI.ConfigWindow;
using TMPro;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class DamageIndicatorBatcher : MonoBehaviour
{
	public static DamageIndicatorBatcher Instance;
	private static readonly int Instances = Shader.PropertyToID("_Instances");
	private static readonly int BaseInstance = Shader.PropertyToID("_BaseInstance");
	private static readonly int MainTex = Shader.PropertyToID("_MainTex");
	private static readonly int Spacing = Shader.PropertyToID("_Spacing");
	private static readonly int Alpha = Shader.PropertyToID("_Alpha");
	private static readonly int Value = Shader.PropertyToID("_Value");
	private static readonly int ColorProp = Shader.PropertyToID("_Color");
	private static readonly int LifeTime = Shader.PropertyToID("_LifeTime");
	private static readonly int CritJitter = Shader.PropertyToID("_CritJitter");
	private static readonly int IsCrit = Shader.PropertyToID("_IsCrit");
	private static readonly int IsMiss = Shader.PropertyToID("_IsMiss");
	private static readonly int IsAgi = Shader.PropertyToID("_IsAgi");
	private static readonly int IsSlow = Shader.PropertyToID("_IsSlow");
	private static readonly int IsExp = Shader.PropertyToID("_IsExp");

	public List<DamageIndicator> indicators = new();
	public Mesh mesh;
	public Shader shader;
	public GameObject atlasBakerPrefab;
	public bool EnableInstancing;

	private CommandBuffer _cmd;
	private Camera _ownCamera;

	private const CameraEvent Event = CameraEvent.AfterForwardAlpha;

	private Material _material;
	private MaterialPropertyBlock _propertyBlock;
	private Mesh _fst;

	private BakeDamageIndicatorAtlas _baker;

	private InstanceBufferPool<DamageIndicatorData> _pool;
	private readonly DamageIndicatorData[] _instStage = new DamageIndicatorData[1023];
	private readonly Matrix4x4[] _matrices = new Matrix4x4[1023];

	private readonly Dictionary<Camera, RenderTexture> _perCamRT = new();
	private readonly Dictionary<Camera, CommandBuffer> _perCamBlit = new();
	private readonly List<Camera> _scratchDeadCameras = new();

	private void OnEnable()
	{
		Instance = this;

		_cmd = new CommandBuffer { name = "DamageIndicatorBatcher - Render" };
		_ownCamera = GetComponent<Camera>();

		Camera.onPreRender += OnAnyCameraPreRender;

		var bakerGo = Instantiate(atlasBakerPrefab, transform);
		bakerGo.transform.position = new Vector3(-5000, -5000, -5000);
		_baker = bakerGo.GetComponent<BakeDamageIndicatorAtlas>();
		_baker.BakeAtlas();

		_material = new Material(shader);
		_material.SetTexture(MainTex, _baker.damageIndicatorTexture);
		_material.enableInstancing = true;

		_pool = new InstanceBufferPool<DamageIndicatorData>(1023 * 3);
		_propertyBlock = new MaterialPropertyBlock();

		GenerateFullScreenTriangle();

		if (!SystemInfo.supportsInstancing)
		{
			EnableInstancing = false;
			Debug.Log("System doesn't support Instancing, disabling...");
		}
	}

	private void OnDisable()
	{
		Camera.onPreRender -= OnAnyCameraPreRender;

		if (_baker != null)
			Destroy(_baker.gameObject);

		foreach (var kv in _perCamBlit)
		{
			if (kv.Key != null)
				kv.Key.RemoveCommandBuffer(Event, kv.Value);
			kv.Value?.Release();
		}
		_perCamBlit.Clear();

		foreach (var kv in _perCamRT)
			if (kv.Value != null) kv.Value.Release();
		_perCamRT.Clear();

		_cmd?.Release();
		_cmd = null;
	}

	private void LateUpdate()
	{
        if (GameConfig.Data == null) return;

		_material.SetFloat(Spacing, GameConfig.Data.DamageSpacingSize);

		for (int i = indicators.Count - 1; i >= 0; i--)
		{
			var di = indicators[i];
			di.OnUpdate();
			if (!di.ShouldRender())
			{
				di.OnRemoved();
				indicators.RemoveAt(i);
			}
		}

		PruneDeadCameras();
	}

	private void PruneDeadCameras()
	{
		_scratchDeadCameras.Clear();
        foreach (var kv in _perCamRT)
        {
            if (kv.Key == null) _scratchDeadCameras.Add(kv.Key);
        }
		foreach (var kv in _perCamBlit)
        {
            if (kv.Key == null && !_scratchDeadCameras.Contains(kv.Key)) _scratchDeadCameras.Add(kv.Key);
        }

		foreach (var c in _scratchDeadCameras)
		{
			if (_perCamRT.TryGetValue(c, out var rt))
			{
				if (rt != null) rt.Release();
				_perCamRT.Remove(c);
			}
			if (_perCamBlit.TryGetValue(c, out var cb))
			{
				cb?.Release();
				_perCamBlit.Remove(c);
			}
		}
	}

	private void OnAnyCameraPreRender(Camera cam)
	{
		if (cam == null) return;
		if (cam.cameraType != CameraType.SceneView && cam != _ownCamera) return;
		if (cam.pixelWidth <= 0 || cam.pixelHeight <= 0) return;

		var blitCb = GetBlitCmd(cam);
		blitCb.Clear();

		if (indicators.Count == 0) return;

		var rt = GetRT(cam);

		_cmd.Clear();
		_cmd.SetRenderTarget(rt);
		_cmd.ClearRenderTarget(true, true, Color.clear);
		_cmd.SetViewProjectionMatrices(cam.worldToCameraMatrix, cam.projectionMatrix);

		var rotation = cam.transform.rotation * Quaternion.Euler(0, 180, 0);

        if (EnableInstancing)
        {
            BuildInstancedDraws(rotation);
        }
        else
        {
            BuildIndividualDraws(rotation);
        }

		Graphics.ExecuteCommandBuffer(_cmd);

		_propertyBlock.Clear();
		_propertyBlock.SetTexture(MainTex, rt);
		blitCb.DrawMesh(_fst, Matrix4x4.identity, _material, 0, 1, _propertyBlock);
	}

	private void BuildIndividualDraws(Quaternion rotation)
	{
		for (int i = indicators.Count - 1; i >= 0; i--)
		{
			var di = indicators[i];
			if (di.UseTmpFallback) continue;

			_propertyBlock.Clear();

			_propertyBlock.SetFloat(Alpha, di.alpha);
			_propertyBlock.SetInt(Value, di.value);
			_propertyBlock.SetColor(ColorProp, di.color);
			_propertyBlock.SetFloat(LifeTime, di.lifeTime);

			_propertyBlock.SetFloat(CritJitter, di.critJitter);

			_propertyBlock.SetFloat(IsCrit, di.type == TextIndicatorType.Critical ? 1 : 0);
			_propertyBlock.SetFloat(IsMiss, di.type == TextIndicatorType.Miss ? 1 : 0);
			_propertyBlock.SetFloat(IsAgi, di.type is TextIndicatorType.Effect or TextIndicatorType.Debuff ? 1 : 0);
			_propertyBlock.SetFloat(IsSlow, di.type == TextIndicatorType.Debuff ? 1 : 0);
			_propertyBlock.SetFloat(IsExp, di.type == TextIndicatorType.Experience ? 1 : 0);

			var trs = Matrix4x4.TRS(di.pos, rotation, new Vector3(di.size, di.size, 1f));
			_cmd.DrawMesh(mesh, trs, _material, 0, 0, _propertyBlock);
		}
	}

	private void BuildInstancedDraws(Quaternion rotation)
	{
		_pool.BeginFrame();

		int batchCount = 0;
		for (int i = 0; i < indicators.Count; i++)
		{
			var di = indicators[i];
			if (di.UseTmpFallback) continue;

			uint flags = 0;
			if (di.type == TextIndicatorType.Critical) flags |= 1 << 0;
			if (di.type == TextIndicatorType.Miss) flags |= 1 << 1;
			if (di.type is TextIndicatorType.Effect or TextIndicatorType.Debuff) flags |= 1 << 2;
			if (di.type == TextIndicatorType.Debuff) flags |= 1 << 3;
			if (di.type == TextIndicatorType.Experience) flags |= 1 << 4;

			_instStage[batchCount] = new DamageIndicatorData
			{
				alpha = di.alpha,
				value = di.value,
				color = di.color,
				lifeTime = di.lifeTime,
				critJitter = di.critJitter,
				flags = flags,
			};

			_matrices[batchCount] = Matrix4x4.TRS(di.pos, rotation, new Vector3(di.size, di.size, 1f));
			batchCount++;

			if (batchCount == 1023)
			{
				FlushInstancedBatch(batchCount);
				batchCount = 0;
			}
		}

		if (batchCount > 0)
			FlushInstancedBatch(batchCount);
	}

	private void FlushInstancedBatch(int count)
	{
		var baseInstance = _pool.AppendInstances(_instStage, 0, count);
		_propertyBlock.Clear();
		_propertyBlock.SetBuffer(Instances, _pool.Instances);
		_propertyBlock.SetInt(BaseInstance, baseInstance);

		_cmd.DrawMeshInstanced(mesh, 0, _material, 0, _matrices, count, _propertyBlock);
	}

	private RenderTexture GetRT(Camera cam)
	{
		if (_perCamRT.TryGetValue(cam, out var rt) && rt != null && rt.width == cam.pixelWidth && rt.height == cam.pixelHeight) return rt;

		if (rt != null) rt.Release();
		rt = new RenderTexture(cam.pixelWidth, cam.pixelHeight, 24, DefaultFormat.LDR);
		rt.antiAliasing = 1;
		rt.name = $"DI Target ({cam.name})";
		_perCamRT[cam] = rt;
		return rt;
	}

	private CommandBuffer GetBlitCmd(Camera cam)
	{
		if (_perCamBlit.TryGetValue(cam, out var cmd) && cmd != null) return cmd;

		cmd = new CommandBuffer { name = $"DI Blit ({cam.name})" };
		cam.AddCommandBuffer(Event, cmd);
		_perCamBlit[cam] = cmd;
		return cmd;
	}
    
	private void GenerateFullScreenTriangle()
	{
		_fst = new Mesh();
		_fst.vertices = new[]
		{
			new Vector3(-1f, -1f, 0f),
			new Vector3(-1f,  3f, 0f),
			new Vector3( 3f, -1f, 0f)
		};
		_fst.uv = new[]
		{
			new Vector2(0f, 0f),
			new Vector2(0f, 2f),
			new Vector2(2f, 0f)
		};
		_fst.triangles = new[] { 0, 2, 1 };
	}
}

[StructLayout(LayoutKind.Sequential)]
public struct DamageIndicatorData
{
	public float alpha;
	public int value;
	public Vector4 color;
	public float lifeTime;
	public float critJitter;

	public uint flags;
}
