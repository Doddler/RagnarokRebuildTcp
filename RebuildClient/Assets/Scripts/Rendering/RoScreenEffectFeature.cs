using Assets.Scripts.Effects;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class RoScreenEffectFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader effectShader;

    private static readonly int StrengthId = Shader.PropertyToID("_Strength");

    private HallucinationPass _pass;
    private Material _material;

    public override void Create()
    {
        _pass = new HallucinationPass
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (effectShader == null || ScreenEffectHandler.HallucinationStrength <= 0.005f)
            return;

        if (_material == null || _material.shader != effectShader)
        {
            CoreUtils.Destroy(_material);
            _material = CoreUtils.CreateEngineMaterial(effectShader);
            _material.name = "Hallucination (Render Pass)";
        }

        _material.SetFloat(StrengthId, ScreenEffectHandler.HallucinationStrength);
        _pass.Setup(_material);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_material);
        _material = null;
    }

    private sealed class HallucinationPass : ScriptableRenderPass
    {
        private Material _material;

        public HallucinationPass()
        {
            requiresIntermediateTexture = true;
        }

        public void Setup(Material material) => _material = material;

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.cameraType != CameraType.Game)
                return;
            // RT cameras (screenshots, the damage-number atlas baker) shouldn't be distorted
            if (cameraData.camera.targetTexture != null)
                return;

            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer)
                return;

            var source = resourceData.activeColorTexture;
            var destDesc = renderGraph.GetTextureDesc(source);
            destDesc.name = "_HallucinationTarget";
            destDesc.clearBuffer = false;
            destDesc.depthBufferBits = 0;
            var destination = renderGraph.CreateTexture(destDesc);

            var blit = new RenderGraphUtils.BlitMaterialParameters(source, destination, _material, 0);
            renderGraph.AddBlitPass(blit, "RoHallucination");

            resourceData.cameraColor = destination;
        }
    }
}
