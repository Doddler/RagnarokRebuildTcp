using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// Builds the screen-space water lookup texture sampled by sprite shaders.
// The normal water material still renders through URP's transparent pass.
public class RoWaterFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader waterDepthShader;
    [SerializeField, Min(1)] private int downsample = 4;

    private WaterDepthPass _pass;

    public override void Create()
    {
        _pass = new WaterDepthPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        _pass.Setup(waterDepthShader, downsample);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.Dispose();
    }

    private sealed class WaterDepthPass : ScriptableRenderPass
    {
        private static readonly int WaterDepthId = Shader.PropertyToID("_WaterDepth");
        private static readonly ShaderTagId ForwardTag = new ShaderTagId("UniversalForward");

        private Material _overrideMaterial;
        private Shader _shader;
        private int _downsample = 4;
        private int _waterLayer = -1;

        public WaterDepthPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        }

        public void Setup(Shader shader, int downsample)
        {
            _shader = shader;
            _downsample = Mathf.Max(1, downsample);
        }

        private class PassData
        {
            public RendererListHandle RendererList;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.cameraType != CameraType.Game && cameraData.cameraType != CameraType.SceneView)
                return;

            if (_waterLayer < 0)
                _waterLayer = LayerMask.NameToLayer("Water");
            if (_waterLayer < 0)
                return;

            var waterMask = 1 << _waterLayer;
            if ((cameraData.camera.cullingMask & waterMask) == 0)
                return;

            var material = GetOverrideMaterial();
            if (material == null)
                return;

            var renderingData = frameData.Get<UniversalRenderingData>();
            var lightData = frameData.Get<UniversalLightData>();

            var width = Mathf.Max(1, cameraData.cameraTargetDescriptor.width / _downsample);
            var height = Mathf.Max(1, cameraData.cameraTargetDescriptor.height / _downsample);
            var waterDesc = new TextureDesc(width, height)
            {
                name = "_WaterDepth",
                colorFormat = GraphicsFormat.R16G16B16A16_SFloat,
                clearBuffer = true,
                clearColor = new Color(0f, 0f, -1000f, 0f),
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var waterDepth = renderGraph.CreateTexture(waterDesc);

            var drawSettings = RenderingUtils.CreateDrawingSettings(ForwardTag, renderingData, cameraData, lightData,
                SortingCriteria.CommonTransparent);
            drawSettings.overrideMaterial = material;
            drawSettings.overrideMaterialPassIndex = 0;

            var filterSettings = new FilteringSettings(RenderQueueRange.all, waterMask);

            using var builder = renderGraph.AddRasterRenderPass<PassData>("RoWaterDepth", out var passData);

            var param = new RendererListParams(renderingData.cullResults, drawSettings, filterSettings);
            passData.RendererList = renderGraph.CreateRendererList(param);

            builder.UseRendererList(passData.RendererList);
            builder.SetRenderAttachment(waterDepth, 0, AccessFlags.Write);
            builder.SetGlobalTextureAfterPass(waterDepth, WaterDepthId);
            builder.AllowGlobalStateModification(true);

            builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
            {
                ctx.cmd.DrawRendererList(data.RendererList);
            });
        }

        public void Dispose()
        {
            if (_overrideMaterial != null)
                CoreUtils.Destroy(_overrideMaterial);
        }

        private Material GetOverrideMaterial()
        {
            if (_shader == null)
                return null;

            if (_overrideMaterial != null && _overrideMaterial.shader == _shader)
                return _overrideMaterial;

            if (_overrideMaterial != null)
                CoreUtils.Destroy(_overrideMaterial);

            _overrideMaterial = CoreUtils.CreateEngineMaterial(_shader);
            _overrideMaterial.name = "WaterDepthMaterial (Render Pass)";
            return _overrideMaterial;
        }
    }
}
