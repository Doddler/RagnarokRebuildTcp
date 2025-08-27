using System.Collections.Generic;
using System.Runtime.InteropServices;
using Assets.Scripts.Effects;
using UnityEngine;
using UnityEngine.Rendering;

public class RoSpriteBatcher : MonoBehaviour
{
	public static RoSpriteBatcher Instance;
	private static readonly int Instances = Shader.PropertyToID("_Instances");
	private static readonly int BaseInstance = Shader.PropertyToID("_BaseInstance");
	private static readonly int MainTex = Shader.PropertyToID("_MainTex");

	public CollectionDictionary<Texture2D, List<RoSpriteDrawCall>, RoSpriteDrawCall> drawCalls = new();

	private Camera _camera;
	private CommandBuffer _depthCmd;
	// We are only batching depth pre-pass as there is no need to worry about sorting.
	private CommandBuffer _colorCmd;
	//private CommandBuffer _xrayCmd;

	private Material _material;

	private InstanceBufferPool<SpriteInstanceData> _pool;
	private readonly SpriteInstanceData[] _instStage = new SpriteInstanceData[1023];
	private readonly Matrix4x4[] _matrices = new Matrix4x4[1023];
	
	private Mesh _quad;
	private MaterialPropertyBlock _propertyBlock;
	
	private const CameraEvent DepthEvent = CameraEvent.BeforeForwardAlpha;
	private const CameraEvent ColorEvent = CameraEvent.BeforeForwardAlpha;

	public bool EnableInstancing;

	private void OnEnable()
	{
		Instance = this;

		_depthCmd = new CommandBuffer();
		_depthCmd.name = "RoSpriteBatcher - Depth";
			
		_colorCmd = new CommandBuffer();
		_colorCmd.name = "RoSpriteBatcher - Color";
		
		//_xrayCmd = new CommandBuffer();
		//_xrayCmd.name = "RoSpriteBatcher - X-Ray";
		
		_camera = GetComponent<Camera>();
		
		_camera.AddCommandBuffer(DepthEvent, _depthCmd);
		_camera.AddCommandBuffer(ColorEvent, _colorCmd);
		//_camera.AddCommandBuffer(CameraEvent.AfterForwardOpaque + 1, _xrayCmd);

		_material = new Material(ShaderCache.Instance.SpriteShader);
		_material.enableInstancing = true;
		
		_pool = new InstanceBufferPool<SpriteInstanceData>(1023);
		
		_propertyBlock = new MaterialPropertyBlock();

		_quad = BuildUnitQuadMesh();
	}

	private void OnDisable()
	{
		if (_camera && _depthCmd != null)
		{
			_camera.RemoveCommandBuffer(DepthEvent, _depthCmd);
		}
		_depthCmd?.Release();
		_depthCmd = null;

		if (_camera && _colorCmd != null)
		{
			_camera.RemoveCommandBuffer(ColorEvent, _colorCmd);
		}
		_colorCmd?.Release();
		_colorCmd = null;
		
		/*if (_camera && _xrayCmd != null)
		{
			_camera.RemoveCommandBuffer(CameraEvent.AfterForwardOpaque, _xrayCmd);
		}
		_xrayCmd?.Release();
		_xrayCmd = null;*/
		
		if (_material)
		{
			Destroy(_material);
			_material = null;
		}
	}
	
	private void OnPreRender()
	{
		_depthCmd.Clear();
		_colorCmd.Clear();
		//_xrayCmd.Clear();
		
		if (!EnableInstancing) return;
		
		_pool.BeginFrame();
		
		foreach (var rawCalls in drawCalls)
		{
			var rawCall = rawCalls.Value;
			var total = rawCall.Count;
			
			_colorCmd.BeginSample(rawCalls.Key.ToString());
			_depthCmd.BeginSample(rawCalls.Key.ToString());
			
			for (int start = 0; start < total; start += 1023)
			{
				var batchCount = Mathf.Min(1023, total - start);

				for (int i = 0; i < batchCount; i++)
				{
					var rc = rawCall[start + i];

					var inst = new SpriteInstanceData
					{
						v0 = (rc.Vertices.Length == 4) ? rc.Vertices[0] : Vector3.zero,
						v1 = (rc.Vertices.Length == 4) ? rc.Vertices[1] : Vector3.zero,
						v2 = (rc.Vertices.Length == 4) ? rc.Vertices[2] : Vector3.zero,
						v3 = (rc.Vertices.Length == 4) ? rc.Vertices[3] : Vector3.zero,

						u0 = (rc.UV.Length == 4) ? rc.UV[0] : Vector2.zero,
						u1 = (rc.UV.Length == 4) ? rc.UV[1] : Vector2.zero,
						u2 = (rc.UV.Length == 4) ? rc.UV[2] : Vector2.zero,
						u3 = (rc.UV.Length == 4) ? rc.UV[3] : Vector2.zero,

						c0 = (rc.VColor.Length == 4) ? rc.VColor[0] : Vector4.one,
						c1 = (rc.VColor.Length == 4) ? rc.VColor[1] : Vector4.one,
						c2 = (rc.VColor.Length == 4) ? rc.VColor[2] : Vector4.one,
						c3 = (rc.VColor.Length == 4) ? rc.VColor[3] : Vector4.one,

						color = rc.Color,
						isHidden = rc.IsHidden ? 1f : 0f,
						offset = rc.Offset,
						colorDrain = rc.ColorDrain,
						vPos = rc.VPos,
						width = rc.Width,
					};

					_instStage[i] = inst;
					_matrices[i] = rc.Transform ? rc.Transform.localToWorldMatrix : Matrix4x4.identity;
				}
				
				var baseInstance = _pool.AppendInstances(_instStage, 0, batchCount);
				
				_propertyBlock.Clear();
				_propertyBlock.SetBuffer(Instances, _pool.Instances);
				_propertyBlock.SetInt(BaseInstance, baseInstance);
				_propertyBlock.SetTexture(MainTex, rawCalls.Key);
				
				_depthCmd.DrawMeshInstanced(_quad, 0, _material, 0, _matrices, batchCount, _propertyBlock);
				_colorCmd.DrawMeshInstanced(_quad, 0, _material, 1, _matrices, batchCount, _propertyBlock);
				//_xrayCmd.DrawMeshInstanced(_quad, 0, _material, 2, _matrices, batchCount, _propertyBlock);
				
				_colorCmd.EndSample(rawCalls.Key.ToString());
				_depthCmd.EndSample(rawCalls.Key.ToString());
			}
		}
	}

	private static Mesh BuildUnitQuadMesh()
	{
		var mesh = new Mesh { name = "Sprite Base Quad" };

		var vertices = new Vector3[4];
		var uvs = new Vector2[4];
		var triangles = new[] { 0, 1, 2, 1, 3, 2 };

		mesh.vertices = vertices;
		mesh.uv = uvs;
		mesh.triangles = triangles;

		return mesh;
	}
}

public class RoSpriteDrawCall
{
	public Transform Transform;
	public Vector3[] Vertices = new Vector3[4];
	public Vector2[] UV = new Vector2[4];
	public Color[] VColor = new Color[4];
	public bool IsHidden;
	public Color Color;
	public float Offset;
	public float ColorDrain;
	public float VPos;
	public float Width;
}

[StructLayout(LayoutKind.Sequential)]
public struct SpriteInstanceData
{
	// Vertex position
	public Vector3 v0;
	public Vector3 v1;
	public Vector3 v2;
	public Vector3 v3;

	// Texture coords
	public Vector2 u0;
	public Vector2 u1;
	public Vector2 u2;
	public Vector2 u3;

	// Vertex color
	public Vector4 c0;
	public Vector4 c1;
	public Vector4 c2;
	public Vector4 c3;

	public Vector4 color;
	public float isHidden;
	public float offset;
	public float colorDrain;
	public float vPos;
	public float width;
}