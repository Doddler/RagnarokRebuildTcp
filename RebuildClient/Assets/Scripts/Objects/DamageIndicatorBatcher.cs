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

	public List<DamageIndicator> indicators = new();
	public Mesh mesh;
	public Shader shader;
	public GameObject atlasBakerPrefab;
	public bool EnableInstancing;
	
	private Camera _camera;
	private CommandBuffer _cmd;
	private CommandBuffer _blitCmd;
	private RenderTexture _targetTexture;

	private const CameraEvent Event = CameraEvent.AfterForwardAlpha;
	
	private Material _material;
	private MaterialPropertyBlock _propertyBlock;
	private Mesh _fst;

	private BakeDamageIndicatorAtlas _baker;
	
	private InstanceBufferPool<DamageIndicatorData> _pool;
	private readonly DamageIndicatorData[] _instStage = new DamageIndicatorData[1023];
	private readonly Matrix4x4[] _matrices = new Matrix4x4[1023];
	
	private void OnEnable()
	{
		Instance = this;
		
		UpdateRenderTexture();
		
		_cmd = new CommandBuffer();
		_cmd.name = "DamageIndicatorBatcher - Render";
		_cmd.SetRenderTarget(_targetTexture);
		
		_blitCmd = new CommandBuffer();
		_blitCmd.name = "DamageIndicatorBatcher - Blit";

		_camera = GetComponent<Camera>();
		//_camera.AddCommandBuffer(Event, _cmd);
		_camera.AddCommandBuffer(Event, _blitCmd);

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
		Destroy(_baker.gameObject);
		
		_camera.RemoveCommandBuffer(Event, _blitCmd);
	
		_blitCmd?.Release();
		_blitCmd = null;
		
		_cmd?.Release();
		_cmd = null;
	}

	private void Update()
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
	}

	private void OnPreRender()
	{
		if (EnableInstancing)
		{
			DrawInstanced();
		}
		else
		{
			DrawIndividualMeshes();
		}
	}

	private void DrawIndividualMeshes()
	{
		_blitCmd.Clear();
		
		UpdateRenderTexture();
		_cmd.Clear();
		_cmd.SetRenderTarget(_targetTexture);
		_cmd.ClearRenderTarget(true, true, Color.clear);
		_cmd.SetViewProjectionMatrices(_camera.worldToCameraMatrix, _camera.projectionMatrix);
		
		var rotation = _camera.transform.rotation * Quaternion.Euler(0, 180, 0);

		for (int i = indicators.Count - 1; i >= 0; i--)
		{
			var di = indicators[i];
			if (di.UseTmpFallback) continue;

			_propertyBlock.Clear();

			_propertyBlock.SetFloat("_Alpha", di.alpha);
			_propertyBlock.SetInt("_Value", di.value);
			_propertyBlock.SetColor("_Color", di.color);
			_propertyBlock.SetFloat("_LifeTime", di.lifeTime);

			_propertyBlock.SetFloat("_CritJitter", di.critJitter);
			
			_propertyBlock.SetFloat("_IsCrit", di.type == TextIndicatorType.Critical ? 1 : 0);
			_propertyBlock.SetFloat("_IsMiss", di.type == TextIndicatorType.Miss ? 1 : 0);
			_propertyBlock.SetFloat("_IsAgi", di.type is TextIndicatorType.Effect or TextIndicatorType.Debuff ? 1 : 0);
			_propertyBlock.SetFloat("_IsSlow", di.type == TextIndicatorType.Debuff ? 1 : 0);
			_propertyBlock.SetFloat("_IsExp", di.type == TextIndicatorType.Experience ? 1 : 0);
			
			var trs = Matrix4x4.TRS(di.pos, rotation, new Vector3(di.size, di.size, 1f));
			_cmd.DrawMesh(mesh, trs, _material, 0, 0, _propertyBlock);
		}
		
		Graphics.ExecuteCommandBuffer(_cmd);
		
		_propertyBlock.Clear();
		_propertyBlock.SetTexture("_MainTex", _targetTexture);
		_blitCmd.DrawMesh(_fst, Matrix4x4.identity, _material, 0, 1, _propertyBlock);
	}
	
	private void DrawInstanced()
	{
		_blitCmd.Clear();
		
		UpdateRenderTexture();
		_cmd.Clear();
		_cmd.SetRenderTarget(_targetTexture);
		_cmd.ClearRenderTarget(true, true, Color.clear);
		_cmd.SetViewProjectionMatrices(_camera.worldToCameraMatrix, _camera.projectionMatrix);
		
		_pool.BeginFrame();
		
		var rotation = _camera.transform.rotation * Quaternion.Euler(0, 180, 0);
		
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
		
		Graphics.ExecuteCommandBuffer(_cmd);

		_propertyBlock.Clear();
		_propertyBlock.SetTexture(MainTex, _targetTexture);
		_blitCmd.DrawMesh(_fst, Matrix4x4.identity, _material, 0, 1, _propertyBlock);
	}

	private void FlushInstancedBatch(int count)
	{
		var baseInstance = _pool.AppendInstances(_instStage, 0, count);
		_propertyBlock.Clear();
		_propertyBlock.SetBuffer(Instances, _pool.Instances);
		_propertyBlock.SetInt(BaseInstance, baseInstance);

		_cmd.DrawMeshInstanced(mesh, 0, _material, 0, _matrices, count, _propertyBlock);
	}

	private void UpdateRenderTexture()
	{
		if (!_targetTexture || _targetTexture.width != Screen.width || _targetTexture.height != Screen.height)
		{
			if (_targetTexture) _targetTexture.Release();
			_targetTexture = new RenderTexture(Screen.width, Screen.height, 24, DefaultFormat.LDR);
			_targetTexture.antiAliasing = 1;
			_targetTexture.name = "Damage Indicator Target Texture";
		}
	}

	/*private bool _curIsCritBilinear;
	private TMP_FontAsset _curFont;
	private void RequestRebakeIfNeeded()
	{
		
		
		_baker.BakeAtlas();
		_curIsCritBilinear = false;
		_curFont = 
	}*/
	
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