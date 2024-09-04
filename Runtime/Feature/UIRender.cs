using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class UIRender : ScriptableRendererFeature
{
    class CustomRenderPass : ScriptableRenderPass
    {
        private List<ShaderTagId> m_ShaderTagIDList = new List<ShaderTagId>();
        
        private FilteringSettings m_FilteringSettings;
        private RenderUISettings m_settings;
        RenderQueueRange m_queueOpaque = new RenderQueueRange();
        public CustomRenderPass()
        {
            m_ShaderTagIDList.Clear();
            {
                m_ShaderTagIDList.Add(new ShaderTagId("UniversalForwardOnly"));
                m_ShaderTagIDList.Add(new ShaderTagId("UniversalForward"));
                m_ShaderTagIDList.Add(new ShaderTagId("SRPDefaultUnlit"));
            }
        }

        public void Setup(RenderUISettings settings)
        {
            m_settings = settings;
            m_queueOpaque.lowerBound = 2451;
            m_queueOpaque.upperBound = 5000;
            renderPassEvent = settings.Event;
            m_FilteringSettings = new FilteringSettings(m_queueOpaque, m_settings.reflectLayer);
        }

        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in a performant manner.
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, new ProfilingSampler("Render UI")))
            {
                cmd.SetViewProjectionMatrices(renderingData.cameraData.GetViewMatrix(), renderingData.cameraData.GetProjectionMatrixNoJitter());
                var sortFlags = SortingCriteria.CommonOpaque;
                var drawSetting = CreateDrawingSettings(m_ShaderTagIDList, ref renderingData, sortFlags);
                var rendererListParams = new RendererListParams(renderingData.cullResults, drawSetting, m_FilteringSettings);
                cmd.DrawRendererList(context.CreateRendererList(ref rendererListParams));
                cmd.SetViewProjectionMatrices(renderingData.cameraData.GetViewMatrix(), renderingData.cameraData.GetProjectionMatrix());
            }
           
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }
    }
    [System.Serializable]

    public class RenderUISettings
    {
        public RenderPassEvent Event = RenderPassEvent.AfterRenderingOpaques;
        public LayerMask reflectLayer = new LayerMask();
    }
    
    public RenderUISettings settings = new RenderUISettings();

    CustomRenderPass m_ScriptablePass;
    /// <inheritdoc/>
    public override void Create()
    {
        m_ScriptablePass = new CustomRenderPass();

        // Configures where the render pass should be injected.
        m_ScriptablePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        m_ScriptablePass.Setup(settings);
        renderer.EnqueuePass(m_ScriptablePass);
    }
}


