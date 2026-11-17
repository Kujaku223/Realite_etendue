using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

/// <summary>Copies a projector camera's depth into its floating-point color output.</summary>
public sealed class AnamorphicDepthCaptureFeature : ScriptableRendererFeature
{
    [SerializeField] Shader copyDepthShader;
    CapturePass m_Pass;

    sealed class CapturePass : ScriptableRenderPass
    {
        readonly CopyDepthPass m_Copy;

        public CapturePass(Shader shader)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            ConfigureInput(ScriptableRenderPassInput.Depth);
            m_Copy = new CopyDepthPass(renderPassEvent, shader, copyToDepth: false,
                customPassName: "Anamorphic Occlusion Depth");
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var camera = frameData.Get<UniversalCameraData>().camera;
            if (!camera.TryGetComponent<AnamorphicProjector>(out var projector) || !projector.IsCapturingDepth)
                return;
            var resources = frameData.Get<UniversalResourceData>();
            m_Copy.Render(renderGraph, frameData, resources.activeColorTexture,
                resources.activeDepthTexture, passName: "Anamorphic Occlusion Depth");
            projector.MarkDepthCaptureRecorded();
        }

        public void Dispose() => m_Copy.Dispose();
    }

    public override void Create()
    {
        m_Pass?.Dispose();
        m_Pass = copyDepthShader != null ? new CapturePass(copyDepthShader) : null;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (m_Pass != null && renderingData.cameraData.camera.TryGetComponent<AnamorphicProjector>(out var projector) &&
            projector.IsCapturingDepth)
            renderer.EnqueuePass(m_Pass);
    }

    protected override void Dispose(bool disposing)
    {
        m_Pass?.Dispose();
        m_Pass = null;
    }
}
