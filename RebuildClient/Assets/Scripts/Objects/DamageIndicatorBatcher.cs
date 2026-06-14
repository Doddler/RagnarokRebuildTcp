using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Assets.Scripts.UI.ConfigWindow;
using TMPro;
using UnityEngine;
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

	private CommandBuffer _renderCmd;
	private Camera _ownCamera;

	private const CameraEvent Event = CameraEvent.AfterForwardAlpha;

	private Material _material;
	private MaterialPropertyBlock _propertyBlock;

	private BakeDamageIndicatorAtlas _baker;

	private enum InstancingMode { None, StructuredBuffer, ConstantBuffer }
	private InstancingMode _mode = InstancingMode.None;

	private const string KwStructured = "DI_STRUCTURED_BUFFER";
	private const string KwCBuffer = "DI_CBUFFER_INSTANCING";
    
	private const int CBufferBatchSize = 250;
	private static readonly int DIColor = Shader.PropertyToID("_DIColor");
	private static readonly int DIParams0 = Shader.PropertyToID("_DIParams0");
	private static readonly int DIParams1 = Shader.PropertyToID("_DIParams1");
	private readonly Vector4[] _diColors = new Vector4[CBufferBatchSize];
	private readonly Vector4[] _diParams0 = new Vector4[CBufferBatchSize];
	private readonly Vector4[] _diParams1 = new Vector4[CBufferBatchSize];

	private InstanceBufferPool<DamageIndicatorData> _pool;
	private readonly DamageIndicatorData[] _instStage = new DamageIndicatorData[1023];
	private readonly Matrix4x4[] _matrices = new Matrix4x4[1023];

	private readonly Dictionary<Camera, CommandBuffer> _perCamCmd = new();
	private readonly List<Camera> _scratchDeadCameras = new();

	private void OnEnable()
	{
		Instance = this;

		_ownCamera = GetComponent<Camera>();

		Camera.onPreRender += OnAnyCameraPreRender;

		var bakerGo = Instantiate(atlasBakerPrefab, transform);
		bakerGo.transform.position = new Vector3(-5000, -5000, -5000);
		_baker = bakerGo.GetComponent<BakeDamageIndicatorAtlas>();
		_baker.BakeAtlas();

		_material = new Material(shader);
		_material.SetTexture(MainTex, _baker.damageIndicatorTexture);

		_mode = SelectInstancingMode();
		_material.enableInstancing = _mode != InstancingMode.None;
		_material.DisableKeyword(KwStructured);
		_material.DisableKeyword(KwCBuffer);

		if (_mode == InstancingMode.StructuredBuffer)
		{
			_material.EnableKeyword(KwStructured);
			_pool = new InstanceBufferPool<DamageIndicatorData>(1023 * 3);
		}
		else if (_mode == InstancingMode.ConstantBuffer)
		{
			_material.EnableKeyword(KwCBuffer);
		}

		_propertyBlock = new MaterialPropertyBlock();
	}
    
	private InstancingMode SelectInstancingMode()
	{
		if (!EnableInstancing || !SystemInfo.supportsInstancing)
			return InstancingMode.None;
		if (SystemInfo.supportsComputeShaders)
			return InstancingMode.StructuredBuffer;
		return InstancingMode.ConstantBuffer;
	}

	private void OnDisable()
	{
		Camera.onPreRender -= OnAnyCameraPreRender;

		if (_baker != null)
			Destroy(_baker.gameObject);

		foreach (var kv in _perCamCmd)
		{
			if (kv.Key != null)
				kv.Key.RemoveCommandBuffer(Event, kv.Value);
			kv.Value?.Release();
		}
		_perCamCmd.Clear();

		_pool?.Dispose();
		_pool = null;
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
        foreach (var kv in _perCamCmd)
        {
            if (!kv.Key) _scratchDeadCameras.Add(kv.Key);
        }

		foreach (var c in _scratchDeadCameras)
		{
			if (_perCamCmd.TryGetValue(c, out var cb))
			{
				cb?.Release();
				_perCamCmd.Remove(c);
			}
		}
	}

	private void OnAnyCameraPreRender(Camera cam)
	{
		if (!cam) return;
		if (cam.cameraType != CameraType.SceneView && cam != _ownCamera) return;
		if (cam.pixelWidth <= 0 || cam.pixelHeight <= 0) return;

		var cmd = GetIndicatorCmd(cam);
		cmd.Clear();

		if (indicators.Count == 0) return;
        
		_renderCmd = cmd;

		var rotation = cam.transform.rotation * Quaternion.Euler(0, 180, 0);

        if (_mode != InstancingMode.None)
        {
            BuildInstancedDraws(rotation);
        }
        else
        {
            BuildIndividualDraws(rotation);
        }
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
			_renderCmd.DrawMesh(mesh, trs, _material, 0, 0, _propertyBlock);
		}
	}

	private void BuildInstancedDraws(Quaternion rotation)
	{
		if (_mode == InstancingMode.StructuredBuffer)
			_pool.BeginFrame();

		var maxBatch = _mode == InstancingMode.ConstantBuffer ? CBufferBatchSize : 1023;

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

			if (_mode == InstancingMode.StructuredBuffer)
			{
				_instStage[batchCount] = new DamageIndicatorData
				{
					alpha = di.alpha,
					value = di.value,
					color = di.color,
					lifeTime = di.lifeTime,
					critJitter = di.critJitter,
					flags = flags,
				};
			}
			else
			{
				_diColors[batchCount] = di.color;
				_diParams0[batchCount] = new Vector4(di.alpha, di.value, di.lifeTime, di.critJitter);
				_diParams1[batchCount] = new Vector4(flags, 0f, 0f, 0f);
			}

			_matrices[batchCount] = Matrix4x4.TRS(di.pos, rotation, new Vector3(di.size, di.size, 1f));
			batchCount++;

			if (batchCount == maxBatch)
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
		_propertyBlock.Clear();

		if (_mode == InstancingMode.StructuredBuffer)
		{
			var baseInstance = _pool.AppendInstances(_instStage, 0, count);
			_propertyBlock.SetBuffer(Instances, _pool.Instances);
			_propertyBlock.SetInt(BaseInstance, baseInstance);
		}
		else
		{
			_propertyBlock.SetVectorArray(DIColor, _diColors);
			_propertyBlock.SetVectorArray(DIParams0, _diParams0);
			_propertyBlock.SetVectorArray(DIParams1, _diParams1);
		}

		_renderCmd.DrawMeshInstanced(mesh, 0, _material, 0, _matrices, count, _propertyBlock);
	}

	private CommandBuffer GetIndicatorCmd(Camera cam)
	{
		if (_perCamCmd.TryGetValue(cam, out var cmd) && cmd != null) return cmd;

		cmd = new CommandBuffer { name = $"DI Indicators ({cam.name})" };
		cam.AddCommandBuffer(Event, cmd);
		_perCamCmd[cam] = cmd;
		return cmd;
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
