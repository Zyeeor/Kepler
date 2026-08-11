using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class PandaPostRendererFeature : ScriptableRendererFeature
{
    private PandaPostRenderPass renderPass;

    public override void Create()
    {
        renderPass = new PandaPostRenderPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        CameraType cameraType = renderingData.cameraData.cameraType;
        if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
        {
            return;
        }

        if (!PandaPostProcess.TryGetActive(out PandaPostProcess effect))
        {
            return;
        }

        renderPass.SetupMaterial(effect.PostProcessMat);
        renderer.EnqueuePass(renderPass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (renderPass != null && renderPass.HasMaterial)
        {
            renderPass.SetSource(renderer.cameraColorTargetHandle);
        }
    }

    protected override void Dispose(bool disposing)
    {
        renderPass?.Dispose();
        renderPass = null;
    }

    private sealed class PandaPostRenderPass : ScriptableRenderPass
    {
        private static readonly ProfilingSampler ProfilingSampler = new ProfilingSampler("Panda Post Process");

        private RTHandle source;
        private RTHandle temporaryColor;
        private Material material;

        public bool HasMaterial => material != null;

        public PandaPostRenderPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            ConfigureInput(ScriptableRenderPassInput.Color);
        }

        public void SetupMaterial(Material postProcessMaterial)
        {
            material = postProcessMaterial;
        }

        public void SetSource(RTHandle cameraColorTarget)
        {
            source = cameraColorTarget;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            RenderTextureDescriptor descriptor = cameraTextureDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            RenderingUtils.ReAllocateIfNeeded(
                ref temporaryColor,
                descriptor,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "_PandaPostTemporaryColor");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (source == null || temporaryColor == null || material == null)
            {
                return;
            }

            CommandBuffer commandBuffer = CommandBufferPool.Get();
            using (new ProfilingScope(commandBuffer, ProfilingSampler))
            {
                Blitter.BlitCameraTexture(commandBuffer, source, temporaryColor, material, 0);
                Blitter.BlitCameraTexture(commandBuffer, temporaryColor, source);
            }

            context.ExecuteCommandBuffer(commandBuffer);
            commandBuffer.Clear();
            CommandBufferPool.Release(commandBuffer);
        }

        public void Dispose()
        {
            temporaryColor?.Release();
            temporaryColor = null;
            source = null;
            material = null;
        }
    }
}
