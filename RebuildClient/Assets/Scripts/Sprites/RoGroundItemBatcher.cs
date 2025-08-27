using System.Collections.Generic;
using System.Runtime.InteropServices;
using Assets.Scripts.Effects;
using Assets.Scripts.UI.ConfigWindow;
using UnityEngine;
using UnityEngine.Rendering;

public class RoGroundItemBatcher : MonoBehaviour
{
	public static RoGroundItemBatcher Instance;
	private static readonly int Instances = Shader.PropertyToID("_Instances");
	private static readonly int BaseInstance = Shader.PropertyToID("_BaseInstance");
	private static readonly int MainTex = Shader.PropertyToID("_MainTex");

	// Using the dictionary here as well in case we ever have more than 1 atlas.
	public CollectionDictionary<Texture2D, List<RoGroundItemDrawCall>, RoGroundItemDrawCall> drawCalls = new();

	private Camera _camera;
	private CommandBuffer _depthCmd;
	private CommandBuffer _colorCmd;
	private CommandBuffer _xRayCmd;
	private Material _material;
	private Mesh _quad;
	private MaterialPropertyBlock _propertyBlock;

	private InstanceBufferPool<GroundItemInstanceData> _pool;
	private readonly GroundItemInstanceData[] _instStage = new GroundItemInstanceData[1023];
	private readonly Matrix4x4[] _matrices = new Matrix4x4[1023];

	public bool EnableInstancing;

	private const CameraEvent DepthEvent = CameraEvent.BeforeForwardAlpha;
	private const CameraEvent ColorEvent = CameraEvent.BeforeForwardAlpha;
	private const CameraEvent XRayEvent = CameraEvent.AfterForwardOpaque;
	
	// I've matched this visually with the sprite scale / 3f
	const float QuadSize = 0.24f;
	
	private void OnEnable()
	{
		Instance = this;

		_depthCmd = new CommandBuffer();
		_depthCmd.name = "RoGroundItemBatcher - Depth";
			
		_colorCmd = new CommandBuffer();
		_colorCmd.name = "RoGroundItemBatcher - Color";
		
		_xRayCmd = new CommandBuffer();
		_xRayCmd.name = "RoGroundItemBatcher - XRay";

		_camera = GetComponent<Camera>();
		_camera.AddCommandBuffer(DepthEvent, _depthCmd);
		_camera.AddCommandBuffer(ColorEvent, _colorCmd);
		_camera.AddCommandBuffer(XRayEvent, _xRayCmd);
		
		_material = new Material(ShaderCache.Instance.SpriteShaderWithXRay);
		_material.enableInstancing = true;

		_pool = new InstanceBufferPool<GroundItemInstanceData>(1023);
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
		
		if (_camera && _colorCmd != null)
		{
			_camera.RemoveCommandBuffer(XRayEvent, _xRayCmd);
		}
		_xRayCmd?.Release();
		_xRayCmd = null;
		
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
		_xRayCmd.Clear();

		if (!EnableInstancing)
			return;

		_pool.BeginFrame();

		foreach (var kvp in drawCalls)
		{
			var texture = kvp.Key;
			var calls = kvp.Value;
			var total = calls.Count;

			for (int start = 0; start < total; start += 1023)
			{
				int batchCount = Mathf.Min(1023, total - start);

				for (int i = 0; i < batchCount; i++)
				{
					var rc = calls[start + i];
					
					var rect = rc.UVRect;
					_instStage[i] = new GroundItemInstanceData
					{
						color = rc.Color,
						uvRect = new Vector4(rect.x, rect.y, rect.width, rect.height),
						offset = rc.Offset,
					};

					var p = rc.Pivot / rc.SpriteResolution;
					var localPivotOffset = new Vector3((0.5f - p.x) * QuadSize, (0.5f - p.y) * QuadSize, 0);
					
					_matrices[i] = (rc.Transform ? rc.Transform.localToWorldMatrix : Matrix4x4.identity) * Matrix4x4.Translate(localPivotOffset);
				}

				var baseInstance = _pool.AppendInstances(_instStage, 0, batchCount);

				_propertyBlock.Clear();
				_propertyBlock.SetBuffer(Instances, _pool.Instances);
				_propertyBlock.SetInt(BaseInstance, baseInstance);
				_propertyBlock.SetTexture(MainTex, texture);
				
				_material.EnableKeyword("GROUND_ITEM");
				
				_depthCmd.DrawMeshInstanced(_quad, 0, _material, 0, _matrices, batchCount, _propertyBlock);
				_colorCmd.DrawMeshInstanced(_quad, 0, _material, 1, _matrices, batchCount, _propertyBlock);

				if (GameConfig.Data.EnableXRay)
				{
					_xRayCmd.DrawMeshInstanced(_quad, 0, _material, 2, _matrices, batchCount, _propertyBlock);
				}
			}
		}
	}
	
	private static Mesh BuildUnitQuadMesh()
	{
		var mesh = new Mesh { name = "Ground Item Quad" };
		const float h = QuadSize * 0.5f;
		
		var vertices = new Vector3[]
		{
			new(-h, -h, 0f),
			new( h, -h, 0f),
			new(-h,  h, 0f),
			new( h,  h, 0f)
		};

		var uvs = new Vector2[]
		{
			new(0f, 0f),
			new(1f, 0f),
			new(0f, 1f),
			new(1f, 1f)
		};

		var triangles = new[] { 0, 1, 2, 1, 3, 2 };

		mesh.vertices = vertices;
		mesh.uv = uvs;
		mesh.triangles = triangles;
		mesh.RecalculateBounds();
		mesh.RecalculateNormals();

		return mesh;
	}
}

public class RoGroundItemDrawCall
{
	public Transform Transform;
	public Rect UVRect;
	public Vector2 Pivot;
	public Vector2 SpriteResolution;
	public Color Color;
	public float Offset;
}

[StructLayout(LayoutKind.Sequential)]
public struct GroundItemInstanceData
{
	public Vector4 color;
	public Vector4 uvRect;
	public float offset;
}
