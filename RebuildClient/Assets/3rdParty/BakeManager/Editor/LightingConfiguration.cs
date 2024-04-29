using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
#pragma warning disable CS0618 //it's not my file, I don't really care lol

[Serializable]
[CreateAssetMenu]
public class LightingConfiguration : ScriptableObject
{
	[SerializeField]
	private LightmapEditorSettings.Lightmapper _lightmapperBackend;
	public LightmapEditorSettings.Lightmapper LightmapperBackend { get { return _lightmapperBackend; } }

	[SerializeField]
	private MixedLightingMode _mixedLightingMode;
	public MixedLightingMode MixedLightingMode { get { return _mixedLightingMode; } }

	[Header("Environment Lighting")]

	[SerializeField]
	public bool UpdateAmbientLight;

	[SerializeField]
	private AmbientMode _ambientMode;
	public AmbientMode AmbientMode { get { return _ambientMode; } }

	[SerializeField]
	private float _ambientIntensity;
	public float AmbientIntensity { get { return _ambientIntensity; } }

	[SerializeField]
	private Color _ambientSkyColor;
	public Color AmbientSkyColor { get { return _ambientSkyColor; } }

	[SerializeField]
	private Color _ambientEquatorColor;
	public Color AmbientEquatorColor { get { return _ambientEquatorColor; } }

	[SerializeField]
	private Color _ambientGroundColor;
	public Color AmbientGroundColor { get { return _ambientGroundColor; } }

	[SerializeField]
	private SphericalHarmonicsL2 _ambientProbe;
	public SphericalHarmonicsL2 AmbientProbe { get { return _ambientProbe; } }

	[SerializeField]
	private GIAmbientOverride _GIAmbientOverrideMode;
	public GIAmbientOverride GIAmbientOverrideMode { get { return _GIAmbientOverrideMode; } }

	[Header("Reflections")]
	[SerializeField]
	private DefaultReflectionMode _defaultReflectionMode;
	public DefaultReflectionMode DefaultReflectionMode { get { return _defaultReflectionMode; } }

	[SerializeField]
	private int _defaultReflectionResolution;
	public int DefaultReflectionResolution { get { return _defaultReflectionResolution; } }

	[SerializeField]
	private int _reflectionBounces;
	public int ReflectionBounces { get { return _reflectionBounces; } }

	[SerializeField]
	private float _reflectionIntensity;
	public float ReflectionIntensity { get { return _reflectionIntensity; } }

	[Header("Ambient Occlusion")]
	[SerializeField]
	private bool _enableAmbientOcclusion;
	public bool EnableAmbientOcclusion { get { return _enableAmbientOcclusion; } }

	[SerializeField]
	private float _aoExponentDirect;
	public float AoExponentDirect { get { return _aoExponentDirect; } }

	[SerializeField]
	private float _aoExponentIndirect;
	public float AoExponentIndirect { get { return _aoExponentIndirect; } }        

	[SerializeField]
	private float _aoMaxDistance;
	public float AoMaxDistance { get { return _aoMaxDistance; } }

	[Header("Baked GI Settings")]
	[SerializeField]
	private bool _bakedGI;
	public bool BakedGI { get { return _bakedGI; } }


	[SerializeField]
	private int _directSamples;
	public int DirectSamples { get { return _directSamples; } }

	[SerializeField]
	private int _indirectSamples;
	public int IndirectSamples { get { return _indirectSamples; } }

	[SerializeField]
	private int _environmentSamples;
	public int EnvironmentSamples { get { return _environmentSamples; } }

	[SerializeField]
	private float _bakeResolution;
	public float BakeResolution { get { return _bakeResolution; } }

	[SerializeField]
	private int _padding;
	public int Padding { get { return _padding; } }

	[SerializeField]
	private int _maxAtlasSize;
	public int MaxAtlasSize { get { return _maxAtlasSize; } }

	[Header("Realtime GI Settings")]
	[SerializeField]
	private bool _realtimeGI;
	public bool RealtimeGI { get { return _realtimeGI; } }

	[SerializeField]
	private float _realtimeResolution;
	public float RealtimeResolution { get { return _realtimeResolution; } }

	[Header("Compression Settings")]
	[SerializeField]
	private ReflectionCubemapCompression _reflectionCubemapCompression;
	public ReflectionCubemapCompression ReflectionCubemapCompression { get { return _reflectionCubemapCompression; } }

	[SerializeField]
	private bool _textureCompression;
	public bool TextureCompression { get { return _textureCompression; } }
	
	[Header("General Lighting Settings")]
	[SerializeField]
	private float _bounceBoost;
	public float BounceBoost { get { return _bounceBoost; } }

	[SerializeField]
	private float _indirectOutputScale;
	public float IndirectOutputScale { get { return _indirectOutputScale; } }

	[SerializeField]
	private Lightmapping.GIWorkflowMode _giWorkflowMode;
	public Lightmapping.GIWorkflowMode GiWorkflowMode { get { return _giWorkflowMode; } }

	[Header("Directional Lightmap Mode")]
	[SerializeField]
	private LightmapsMode _lightmapsMode;
	public LightmapsMode LightmapsMode { get { return _lightmapsMode; } }

	[Header("Realtime Shadow Color (Subtractive only)")]
	[SerializeField]
	private Color _subtractiveShadowColor;
	public Color SubtractiveShadowColor { get { return _subtractiveShadowColor; } }

	private const string FLAT_GI_AMBIENT_COLOR_PROPERTY = "_flatGIAmbientColor";
	public enum GIAmbientOverride
	{
		None = 0,
		RealtimeFlatColor = 1,
	}

	public void Load()
	{
		if (UpdateAmbientLight)
		{
			RenderSettings.ambientEquatorColor = _ambientEquatorColor;
			RenderSettings.ambientGroundColor = _ambientGroundColor;
			RenderSettings.ambientIntensity = _ambientIntensity;
			RenderSettings.ambientMode = _ambientMode;
			RenderSettings.ambientSkyColor = _ambientSkyColor;
			RenderSettings.ambientProbe = _ambientProbe;
		}

		RenderSettings.subtractiveShadowColor = _subtractiveShadowColor;
		RenderSettings.defaultReflectionMode = _defaultReflectionMode;
		RenderSettings.defaultReflectionResolution = _defaultReflectionResolution;
		RenderSettings.reflectionBounces = _reflectionBounces;
		RenderSettings.reflectionIntensity = _reflectionIntensity;
		
		if (!Application.isPlaying)
		{
			LightmapEditorSettings.aoExponentDirect = _aoExponentDirect;
			LightmapEditorSettings.aoExponentIndirect = _aoExponentIndirect;
			LightmapEditorSettings.aoMaxDistance = _aoMaxDistance;
			LightmapEditorSettings.bakeResolution = _bakeResolution;
			LightmapEditorSettings.enableAmbientOcclusion = _enableAmbientOcclusion;
			LightmapEditorSettings.maxAtlasSize = _maxAtlasSize;
			LightmapEditorSettings.padding = _padding;
			LightmapEditorSettings.realtimeResolution = _realtimeResolution;
			LightmapEditorSettings.reflectionCubemapCompression = _reflectionCubemapCompression;
			LightmapEditorSettings.textureCompression = _textureCompression;
    		LightmapEditorSettings.lightmapper = _lightmapperBackend;
            LightmapEditorSettings.directSampleCount = _directSamples;
            LightmapEditorSettings.indirectSampleCount = _indirectSamples;
            LightmapEditorSettings.environmentSampleCount = _environmentSamples;
            LightmapEditorSettings.mixedBakeMode = _mixedLightingMode;
			Lightmapping.bakedGI = _bakedGI;
			Lightmapping.bounceBoost = _bounceBoost;
			Lightmapping.giWorkflowMode = _giWorkflowMode;
			Lightmapping.indirectOutputScale = _indirectOutputScale;
			Lightmapping.realtimeGI = _realtimeGI;
		}
		LightmapSettings.lightmapsMode = _lightmapsMode;
	}

	public void Save()
	{
		if (_ambientEquatorColor != RenderSettings.ambientEquatorColor)
		{
			_ambientEquatorColor = RenderSettings.ambientEquatorColor;
		}
		if (_ambientGroundColor != RenderSettings.ambientGroundColor)
		{
			_ambientGroundColor = RenderSettings.ambientGroundColor;
		}
		if (_ambientIntensity != RenderSettings.ambientIntensity)
		{
			_ambientIntensity = RenderSettings.ambientIntensity;
		}
		if (_subtractiveShadowColor != RenderSettings.subtractiveShadowColor)
		{
			_subtractiveShadowColor = RenderSettings.subtractiveShadowColor;
		}
		if (_ambientProbe != RenderSettings.ambientProbe)
		{
			_ambientProbe = RenderSettings.ambientProbe;
		}
		if (_ambientMode != RenderSettings.ambientMode)
		{
			_ambientMode = RenderSettings.ambientMode;
		}
		if (_ambientSkyColor != RenderSettings.ambientSkyColor)
		{
			_ambientSkyColor = RenderSettings.ambientSkyColor;
		}

		if (_defaultReflectionMode != RenderSettings.defaultReflectionMode)
		{
			_defaultReflectionMode = RenderSettings.defaultReflectionMode;
		}
		if (_defaultReflectionResolution != RenderSettings.defaultReflectionResolution)
		{
			_defaultReflectionResolution = RenderSettings.defaultReflectionResolution;
		}
		if (_reflectionBounces != RenderSettings.reflectionBounces)
		{
			_reflectionBounces = RenderSettings.reflectionBounces;
		}
		if (_reflectionIntensity != RenderSettings.reflectionIntensity)
		{
			_reflectionIntensity = RenderSettings.reflectionIntensity;
		}
		if (_aoExponentDirect != LightmapEditorSettings.aoExponentDirect)
		{
			_aoExponentDirect = LightmapEditorSettings.aoExponentDirect;
		}
		if (_aoExponentIndirect != LightmapEditorSettings.aoExponentIndirect)
		{
			_aoExponentIndirect = LightmapEditorSettings.aoExponentIndirect;
		}
		if (_aoMaxDistance != LightmapEditorSettings.aoMaxDistance)
		{
			_aoMaxDistance = LightmapEditorSettings.aoMaxDistance;
		}
		if (_bakeResolution != LightmapEditorSettings.bakeResolution)
		{
			_bakeResolution = LightmapEditorSettings.bakeResolution;
		}
		if (_enableAmbientOcclusion != LightmapEditorSettings.enableAmbientOcclusion)
		{
			_enableAmbientOcclusion = LightmapEditorSettings.enableAmbientOcclusion;
		}
		if (_maxAtlasSize != LightmapEditorSettings.maxAtlasSize)
		{
			_maxAtlasSize = LightmapEditorSettings.maxAtlasSize;
		}
		if (_padding != LightmapEditorSettings.padding)
		{
			_padding = LightmapEditorSettings.padding;
		}
		if (_realtimeResolution != LightmapEditorSettings.realtimeResolution)
		{
			_realtimeResolution = LightmapEditorSettings.realtimeResolution;
		}
		if (_reflectionCubemapCompression != LightmapEditorSettings.reflectionCubemapCompression)
		{
			_reflectionCubemapCompression = LightmapEditorSettings.reflectionCubemapCompression;
		}
		if (_textureCompression != LightmapEditorSettings.textureCompression)
		{
			_textureCompression = LightmapEditorSettings.textureCompression;
		}
		
		if (_bakedGI != Lightmapping.bakedGI)
		{
			_bakedGI = Lightmapping.bakedGI;
		}
		if (_bounceBoost != Lightmapping.bounceBoost)
		{
			_bounceBoost = Lightmapping.bounceBoost;
		}
		if (_giWorkflowMode != Lightmapping.giWorkflowMode)
		{
			_giWorkflowMode = Lightmapping.giWorkflowMode;
		}
		if (_indirectOutputScale != Lightmapping.indirectOutputScale)
		{
			_indirectOutputScale = Lightmapping.indirectOutputScale;
		}
		if (_realtimeGI != Lightmapping.realtimeGI)
		{
			_realtimeGI = Lightmapping.realtimeGI;
		}
		if (_lightmapsMode != LightmapSettings.lightmapsMode)
		{
			_lightmapsMode = LightmapSettings.lightmapsMode;
		}

		if (_mixedLightingMode != LightmapEditorSettings.mixedBakeMode)
			_mixedLightingMode = LightmapEditorSettings.mixedBakeMode;


		if (_directSamples != LightmapEditorSettings.directSampleCount)
			_directSamples = LightmapEditorSettings.directSampleCount;
		if (_indirectSamples != LightmapEditorSettings.indirectSampleCount)
			_indirectSamples = LightmapEditorSettings.indirectSampleCount;
		if (_environmentSamples != LightmapEditorSettings.environmentSampleCount)
			_environmentSamples = LightmapEditorSettings.environmentSampleCount;
	}
}
