using Assets.Scripts.UI.ConfigWindow;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// Draws the sprite "SpriteXRay" pass after opaques so batched + non-batched character
// sprites show through walls (ZTest Greater against opaque depth).
public class RoSpriteXRayFeature : ScriptableRendererFeature
{
    private sealed class XRayPass : ScriptableRenderPass
    {
        private static readonly ShaderTagId XRayTag = new ShaderTagId("SpriteXRay");

        public XRayPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
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

            if (!RoRenderUtil.CameraRendersCharacters(cameraData.camera))
                return;

            var renderingData = frameData.Get<UniversalRenderingData>();
            var lightData = frameData.Get<UniversalLightData>();
            var resourceData = frameData.Get<UniversalResourceData>();

            var drawSettings = RenderingUtils.CreateDrawingSettings(XRayTag, renderingData, cameraData, lightData,
                SortingCriteria.CommonTransparent);
            var filterSettings = new FilteringSettings(RenderQueueRange.transparent);

            using var builder = renderGraph.AddRasterRenderPass<PassData>("RoSpriteXRay", out var passData);

            var param = new RendererListParams(renderingData.cullResults, drawSettings, filterSettings);
            passData.RendererList = renderGraph.CreateRendererList(param);

            builder.UseRendererList(passData.RendererList);
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
            builder.AllowGlobalStateModification(true);

            builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
            {
                ctx.cmd.DrawRendererList(data.RendererList);
            });
        }
    }

    private XRayPass _xRayPass;

    public override void Create()
    {
        _xRayPass = new XRayPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (GameConfig.Data != null && GameConfig.Data.EnableXRay)
            renderer.EnqueuePass(_xRayPass);
    }
}
