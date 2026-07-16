using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// Draws floating damage numbers after transparents. The batcher emits its draws into the
// render-graph raster pass.
public class RoDamageIndicatorFeature : ScriptableRendererFeature
{
    private sealed class DamageIndicatorPass : ScriptableRenderPass
    {
        public DamageIndicatorPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        private class PassData
        {
            public DamageIndicatorBatcher Batcher;
            public Camera Camera;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var batcher = DamageIndicatorBatcher.Instance;
            if (batcher == null || batcher.indicators.Count == 0)
                return;

            var cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.cameraType != CameraType.Game && cameraData.cameraType != CameraType.SceneView)
                return;
            if (!RoRenderUtil.CameraRendersCharacters(cameraData.camera))
                return;

            var resourceData = frameData.Get<UniversalResourceData>();

            using var builder = renderGraph.AddRasterRenderPass<PassData>("RoDamageIndicators", out var passData);
            passData.Batcher = batcher;
            passData.Camera = cameraData.camera;

            builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
            builder.AllowGlobalStateModification(true);

            builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
            {
                data.Batcher.EmitDraws(ctx.cmd, data.Camera);
            });
        }
    }

    private DamageIndicatorPass _pass;

    public override void Create()
    {
        _pass = new DamageIndicatorPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (DamageIndicatorBatcher.Instance == null)
            return;
        renderer.EnqueuePass(_pass);
    }
}
