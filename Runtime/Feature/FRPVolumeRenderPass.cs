using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.FRP
{
    
    /// <summary>
    /// A render pass for executing custom post processing renderers.
    /// </summary>
    public class FRPVolumeRenderPass : ScriptableRenderPass
    {
        private List<ProfilingSampler> m_ProfilingSamplers;
        private string m_PassName;
        private FRPVolumeInjectionPoint injectionPoint;
        private List<FRPVolumeRenderer> m_PostProcessRenderers;
        private List<int> m_ActivePostProcessRenderers;
        RenderTextureDescriptor m_Descriptor;
        
        // URP Uber Volume:
        const int k_MaxPyramidSize = 16;
        GraphicsFormat m_DefaultHDRFormat;
        bool m_UseRGBM;
        private Material bloom_Material;
        private FRPCamreaData frpCamreaData;
        
        public class PostProcessRTHandles
        {
            public RTHandle m_Source;
            public RTHandle m_Depth;
            public RTHandle m_Dest;
        }

        public PostProcessRTHandles m_rtHandles = new PostProcessRTHandles();
        public FRPVolumeData volumeData;

        /// <summary>
        /// Gets whether this render pass has any post process renderers to execute
        /// </summary>
        public bool HasPostProcessRenderers => m_PostProcessRenderers.Count != 0;

        private ScriptableRenderer m_Render = null;

        /// <summary>
        /// Construct the render pass
        /// </summary>
        /// <param name="injectionPoint">The post processing injection point</param>
        /// <param name="classes">The list of classes for the renderers to be executed by this render pass</param>
        internal FRPVolumeRenderPass(FRPVolumeInjectionPoint injectionPoint, ref FRPVolumeData volumeData, List<FRPVolumeRenderer> renderers)
        {
            this.injectionPoint = injectionPoint;
            this.volumeData = volumeData;
            this.m_ProfilingSamplers = new List<ProfilingSampler>(renderers.Count);
            this.m_PostProcessRenderers = renderers;
            foreach (var renderer in renderers)
            {
                // Get renderer name and add it to the names list
                var attribute = FRPVolumeAttribute.GetAttribute(renderer.GetType());
                m_ProfilingSamplers.Add(new ProfilingSampler(attribute?.Name));
            }

            // Pre-allocate a list for active renderers
            this.m_ActivePostProcessRenderers = new List<int>(renderers.Count);
            // Set render pass event and name based on the injection point.
            switch (injectionPoint)
            {
                case FRPVolumeInjectionPoint.BeforeRender:
                    renderPassEvent = RenderPassEvent.BeforeRenderingShadows;
                    m_PassName = "FRP Before Render";
                    break;
                case FRPVolumeInjectionPoint.BeforeGBuffer:
                    renderPassEvent = RenderPassEvent.BeforeRenderingGbuffer;
                    m_PassName = "FRP Before GBuffer";
                    break;
                case FRPVolumeInjectionPoint.BeforeDeferred:
                    renderPassEvent = RenderPassEvent.BeforeRenderingDeferredLights;
                    m_PassName = "FRP Before Deferred";
                    break;
                case FRPVolumeInjectionPoint.BeforeOpaque:
                    renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
                    m_PassName = "FRP Before Opaques";
                    break;
                case FRPVolumeInjectionPoint.AfterOpaqueAndSky:
                    renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
                    m_PassName = "FRP After Opaque & Sky";
                    break;
                case FRPVolumeInjectionPoint.AfterTransparent:
                    renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
                    m_PassName = "FRP After Transparent";
                    break;
                case FRPVolumeInjectionPoint.BeforePostProcess:
                    renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing; // TODO： Should After Motion Vector
                    m_PassName = "FRP Before PostProcess"; 
                    break;
                case FRPVolumeInjectionPoint.AfterPostProcess:
                    renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing + 1; // TODO： Should After PostProcess
                    m_PassName = "FRP After PostProcess"; 
                    break;
            }
        }
        
        /// <summary>
        /// Prepares the renderer for executing on this frame and checks if any of them actually requires rendering
        /// </summary>
        /// <param name="renderingData">Current rendering data</param>
        /// <returns>True if any renderer will be executed for the given camera. False Otherwise.</returns>
        public bool PrepareRenderers(ref RenderingData renderingData, ref FRPCamera frpCamera)
        {
            this.volumeData = volumeData;
            frpCamreaData = FRPCamreaData.GetOrCreate(renderingData.cameraData.camera, renderingData.cameraData);
              
            // See if current camera is a scene view camera to skip renderers with "visibleInSceneView" = false.
            bool isSceneView = renderingData.cameraData.cameraType == CameraType.SceneView;

            // Here, we will collect the inputs needed by all the custom post processing effects
            ScriptableRenderPassInput passInput = ScriptableRenderPassInput.None;

            if (frpCamreaData.uberMaterial == null)
            {
                frpCamreaData.uberMaterial = CoreUtils.CreateEngineMaterial(volumeData.m_frpData.fURPVolumeUberShader);
            }
            
            // Collect the active renderers
            m_ActivePostProcessRenderers.Clear();
            for (int index = 0; index < m_PostProcessRenderers.Count; index++)
            {
                var ppRenderer = m_PostProcessRenderers[index];
                // Skips current renderer if "visibleInSceneView" = false and the current camera is a scene view camera. 
                if (isSceneView && !ppRenderer.visibleInSceneView) continue;
                // Setup the camera for the renderer and if it will render anything, add to active renderers and get its required inputs
                if (ppRenderer.Setup(m_Render, ref renderingData, ref frpCamera, injectionPoint))
                {
                    m_ActivePostProcessRenderers.Add(index);
                    passInput |= ppRenderer.input;
                }
            }
            
            // Configure the pass to tell the renderer what inputs we need
            ConfigureInput(passInput);
            
            return m_ActivePostProcessRenderers.Count != 0;
        }

        /// <summary>
        /// Setup Data
        /// </summary>
        public void Setup(ScriptableRenderer renderer, RTHandle cameraColorTargetHandle, ref FRPVolumeData volumeData)
        {
            m_Render = renderer;
            m_rtHandles.m_Source = cameraColorTargetHandle;
            m_rtHandles.m_Depth = renderer.cameraDepthTargetHandle;
        }

        /// <summary>
        /// cameraColorTargetHandle can only be obtained in SRP render
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="renderingData"></param>
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            base.OnCameraSetup(cmd, ref renderingData);
 
            m_Descriptor = renderingData.cameraData.cameraTargetDescriptor;
            m_Descriptor.useMipMap = false;
            m_Descriptor.autoGenerateMips = false;
        }
        
        bool RequireHDROutput(ref CameraData cameraData)
        {
            // If capturing, don't convert to HDR.
            // If not last in the stack, don't convert to HDR.
            return cameraData.isHDROutputActive && cameraData.captureActions == null;
        }

        /// <summary>
        /// Execute the custom post processing renderers
        /// </summary>
        /// <param name="context">The scriptable render context</param>
        /// <param name="renderingData">Current rendering data</param>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            ref var cameraData = ref renderingData.cameraData;

            CommandBuffer cmd = CommandBufferPool.Get(m_PassName);
            
            
            PostProcessUtils.SetSourceSize(cmd, cameraData.cameraTargetDescriptor);
            
            for (int index = 0; index < m_ActivePostProcessRenderers.Count; ++index)
            {
                var rendererIndex = m_ActivePostProcessRenderers[index];
                var fernPostProcessRenderer = m_PostProcessRenderers[rendererIndex];
                if(!fernPostProcessRenderer.isActive) continue;
                if (!fernPostProcessRenderer.Initialized)
                    fernPostProcessRenderer.InitializeInternal();
                using (new ProfilingScope(cmd, m_ProfilingSamplers[rendererIndex]))
                {
                    Render(cmd, context, fernPostProcessRenderer, ref renderingData);
                }
            }

            // Send command buffer for execution, then release it.
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
        
        void Render(CommandBuffer cmd, ScriptableRenderContext context, FRPVolumeRenderer fernPostRenderer, ref RenderingData renderingData)
        {
            fernPostRenderer.OnCameraSetup(cmd, ref renderingData, this);
            fernPostRenderer.Render(cmd, context, m_rtHandles, ref renderingData, injectionPoint);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            base.OnCameraCleanup(cmd);
            for (int index = 0; index < m_ActivePostProcessRenderers.Count; ++index)
            {
                var rendererIndex = m_ActivePostProcessRenderers[index];
                var fernPostProcessRenderer = m_PostProcessRenderers[rendererIndex];
                fernPostProcessRenderer.OnCameraCleanup(cmd);
            }
        }

        public void Dispose()
        {
            for (int index = 0; index < m_ActivePostProcessRenderers.Count; ++index)
            {
                var rendererIndex = m_ActivePostProcessRenderers[index];
                var fernPostProcessRenderer = m_PostProcessRenderers[rendererIndex];
                fernPostProcessRenderer.Dispose();
            }
            m_rtHandles.m_Source?.Release();
            m_rtHandles.m_Dest?.Release();
            m_rtHandles.m_Depth?.Release();
        }

        public void ReleaseRTHandles()
        {
            for (int index = 0; index < m_ActivePostProcessRenderers.Count; ++index)
            {
                var rendererIndex = m_ActivePostProcessRenderers[index];
                var fernPostProcessRenderer = m_PostProcessRenderers[rendererIndex];
                fernPostProcessRenderer.ReleaseRTHandles();
            }
        }

        public RenderTextureDescriptor GetCompatibleDescriptor()
            => GetCompatibleDescriptor(m_Descriptor.width, m_Descriptor.height, m_Descriptor.graphicsFormat);

        public RenderTextureDescriptor GetCompatibleDescriptor(int width, int height, GraphicsFormat format,
            DepthBits depthBufferBits = DepthBits.None)
            => GetCompatibleDescriptor(m_Descriptor, width, height, format, depthBufferBits);

        internal static RenderTextureDescriptor GetCompatibleDescriptor(RenderTextureDescriptor desc, int width,
            int height, GraphicsFormat format, DepthBits depthBufferBits = DepthBits.None)
        {
            desc.depthBufferBits = (int)depthBufferBits;
            desc.msaaSamples = 1;
            desc.width = width;
            desc.height = height;
            desc.graphicsFormat = format;
            return desc;
        }
    }
}
