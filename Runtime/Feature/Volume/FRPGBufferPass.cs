using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using Unity.Collections;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

namespace UnityEngine.Rendering.FRP
{
    // Render all tiled-based deferred lights.
    [FRPVolume("FRP GBuffer", FRPVolumeInjectionPoint.BeforeGBuffer)]
    internal class FRPGBufferPass : FRPVolumeRenderer
    {
        static readonly int s_CameraNormalsTextureID = Shader.PropertyToID("_CameraNormalsTexture");
        static ShaderTagId s_ShaderTagLit = new ShaderTagId("Lit");
        static ShaderTagId s_ShaderTagSimpleLit = new ShaderTagId("SimpleLit");
        static ShaderTagId s_ShaderTagUnlit = new ShaderTagId("Unlit");
        static ShaderTagId s_ShaderTagComplexLit = new ShaderTagId("ComplexLit");
        static ShaderTagId s_ShaderTagUniversalGBuffer = new ShaderTagId("UniversalGBuffer");
        static ShaderTagId s_ShaderTagUniversalMaterialType = new ShaderTagId("UniversalMaterialType");
        
        static readonly string k_ClearStencilPartial = "Clear Stencil Partial";

        ProfilingSampler m_ProfilingSampler = new ProfilingSampler("Render GBuffer");
        ProfilingSampler m_ProfilingSamplerClearStencilPartialPass = new ProfilingSampler(k_ClearStencilPartial);

        static ShaderTagId[] s_ShaderTagValues;
        static RenderStateBlock[] s_RenderStateBlocks;

        FilteringSettings m_FilteringSettings;
        RenderStateBlock m_RenderStateBlock;
        private PassData m_PassData;
        private FRPVolumeData volumeData;

        private Material m_CopyDepthMaterial;
        
        // For rendering stencil point lights.
        Mesh m_SphereMesh;
        // For rendering stencil spot lights.
        Mesh m_HemisphereMesh;
        // For rendering directional lights.
        Mesh m_FullscreenMesh;

        internal bool HasNormalPrepass { get; set; }
        internal bool HasDepthPrepass { get; set; }
        internal bool AccurateGbufferNormals { get; set; }
        internal bool IsOverlay { get; set; }

        internal int GBufferAlbedoIndex => 0;
        internal int GBufferNormalSmoothnessIndex => 1; 
        internal int GbufferDepthIndex => 2;
        internal int GBufferSliceCount => 2;

        internal GraphicsFormat[] GbufferFormats { get; set; }

        //internal TextureHandle[] GbufferTextureHandles { get; set; }
        //internal RTHandle[] GbufferAttachments { get; set; }
        
        internal RTHandle[] DeferredInputAttachments { get; set; }
        
        RenderTargetIdentifier[] m_ColorAttachmentIds;
        RenderTargetIdentifier m_DepthAttachmentId;
        static public RTHandle k_CameraTarget = RTHandles.Alloc(BuiltinRenderTextureType.CameraTarget);

        static Mesh CreateFullscreenMesh()
        {
            // TODO reorder for pre&post-transform cache optimisation.
            // Simple full-screen triangle.
            Vector3[] positions =
            {
                new Vector3(-1.0f,  1.0f, 0.0f),
                new Vector3(-1.0f, -3.0f, 0.0f),
                new Vector3(3.0f,  1.0f, 0.0f)
            };

            int[] indices = { 0, 1, 2 };

            Mesh mesh = new Mesh();
            mesh.indexFormat = IndexFormat.UInt16;
            mesh.vertices = positions;
            mesh.triangles = indices;

            return mesh;
        }
        
        internal static readonly string[] k_GBufferNames = new string[]
        {
            "_GBuffer0",
            "_GBuffer1",
            "_GBuffer2",
        };

        public FRPGBufferPass(RenderQueueRange renderQueueRange, LayerMask layerMask, StencilState stencilState, FRPVolumeData volumeData, int stencilReference)
        {
            this.volumeData = volumeData;
            m_PassData = new PassData();

            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

            m_RenderStateBlock.stencilState = stencilState;
            m_RenderStateBlock.stencilReference = stencilReference;
            m_RenderStateBlock.mask = RenderStateMask.Stencil;

            if (s_ShaderTagValues == null)
            {
                s_ShaderTagValues = new ShaderTagId[5];
                s_ShaderTagValues[0] = s_ShaderTagLit;
                s_ShaderTagValues[1] = s_ShaderTagSimpleLit;
                s_ShaderTagValues[2] = s_ShaderTagUnlit;
                s_ShaderTagValues[3] = s_ShaderTagComplexLit;
                s_ShaderTagValues[4] = new ShaderTagId(); // Special catch all case for materials where UniversalMaterialType is not defined or the tag value doesn't match anything we know.
            }
            
            s_RenderStateBlocks = new RenderStateBlock[5];
        }
        
        internal void CreateGbufferResources()
        {
            int gbufferSliceCount = this.GBufferSliceCount;
            if (m_frpCameraData.gbufferRTHandles == null || m_frpCameraData.gbufferRTHandles.Length != gbufferSliceCount)
            {
                ReleaseGbufferResources();
                
                m_frpCameraData.gbufferRTHandles = new RTHandle[gbufferSliceCount];
                this.GbufferFormats = new GraphicsFormat[gbufferSliceCount];
                for (int i = 0; i < gbufferSliceCount; ++i)
                {
                    m_frpCameraData.gbufferRTHandles[i] = RTHandles.Alloc(k_GBufferNames[i], name: k_GBufferNames[i]);
                    this.GbufferFormats[i] = this.GetGBufferFormat(i);
                }
            }
        }
        
        internal void ReleaseGbufferResources()
        {
            if (m_frpCameraData.gbufferRTHandles != null)
            {
                // Release the old handles before creating the new one
                for (int i = 0; i < m_frpCameraData.gbufferRTHandles.Length; ++i)
                {
                    RTHandles.Release(m_frpCameraData.gbufferRTHandles[i]);
                }
            }
            m_frpCameraData.gBufferDepthTexture?.Release();
        }

        /// <summary>
        /// Configures render targets for this render pass. Call this instead of CommandBuffer.SetRenderTarget.
        /// This method should be called inside Configure.
        /// </summary>
        /// <param name="colorAttachments">Color attachment handle.</param>
        /// <param name="depthAttachment">Depth attachment handle.</param>
        /// <seealso cref="Configure"/>
        public void ConfigureTarget(RTHandle[] colorAttachments, RTHandle depthAttachment)
        {
            uint nonNullColorBuffers = RenderingUtils.GetValidColorBufferCount(colorAttachments);
            if (nonNullColorBuffers > SystemInfo.supportedRenderTargetCount)
                Debug.LogError("Trying to set " + nonNullColorBuffers + " renderTargets, which is more than the maximum supported:" + SystemInfo.supportedRenderTargetCount);

            if (m_ColorAttachmentIds.Length != colorAttachments.Length)
                m_ColorAttachmentIds = new RenderTargetIdentifier[colorAttachments.Length];
            for (var i = 0; i < m_ColorAttachmentIds.Length; ++i)
                m_ColorAttachmentIds[i] = new RenderTargetIdentifier(colorAttachments[i].nameID, 0, CubemapFace.Unknown, -1);
            m_DepthAttachmentId = depthAttachment.nameID;
        }

        public override bool Setup(ScriptableRenderer renderer, ref RenderingData renderingData, ref FRPCamera frpCamera, FRPVolumeInjectionPoint injectionPoint)
        {
            base.Setup(renderer, ref renderingData, ref frpCamera, injectionPoint);
            if (!volumeData.gBuffer)
            {
                ReleaseRTHandles();
                return false;
            }
            
            if(m_CopyDepthMaterial == null) m_CopyDepthMaterial = CoreUtils.CreateEngineMaterial(volumeData.m_frpData.copyDepthShader);

            return true;
        }
        
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData, FRPVolumeRenderPass renderPass)
        {
            CreateGbufferResources();
            m_ColorAttachmentIds = new RenderTargetIdentifier[] { renderPass.m_rtHandles.m_Source.nameID, 0, 0, 0, 0, 0, 0, 0 };
            m_DepthAttachmentId = renderPass.depthAttachmentHandle.nameID;
            
            var cameraTextureDescriptor = renderingData.cameraData.cameraTargetDescriptor;

            var depthDescriptor = cameraTextureDescriptor;
            depthDescriptor.graphicsFormat = GraphicsFormat.R32_SFloat;
            depthDescriptor.depthStencilFormat = GraphicsFormat.None;
            depthDescriptor.depthBufferBits = 24;
            depthDescriptor.msaaSamples = 1;// Depth-Only pass don't use MSAA
            RenderingUtils.ReAllocateIfNeeded(ref m_frpCameraData.gBufferDepthTexture, depthDescriptor, FilterMode.Point, wrapMode: TextureWrapMode.Clamp, name: "_GBufferDepthTexture");

            if (cmd != null)
            {
                // Create and declare the render targets used in the pass
                for (int i = 0; i < m_frpCameraData.gbufferRTHandles.Length; ++i)
                {
                    // Normal buffer may have already been created if there was a depthNormal prepass before.
                    // DepthNormal prepass is needed for forward-only materials when SSAO is generated between gbuffer and deferred lighting pass.
                    if (i == GBufferNormalSmoothnessIndex && HasNormalPrepass)
                        continue;

                    if (i == GbufferDepthIndex)
                        continue;
                   
                    ReAllocateGBufferIfNeeded(cameraTextureDescriptor, i);
                    
                    cmd.SetGlobalTexture(m_frpCameraData.gbufferRTHandles[i].name, m_frpCameraData.gbufferRTHandles[i].nameID);
                }
            }

            ConfigureTarget(m_frpCameraData.gbufferRTHandles, m_frpCameraData.gBufferDepthTexture);
            
            CoreUtils.SetRenderTarget(cmd, m_ColorAttachmentIds, m_DepthAttachmentId, ClearFlag.All);
        }
        

        public override void Render(CommandBuffer cmd, ScriptableRenderContext context, FRPVolumeRenderPass.PostProcessRTHandles rtHandles,
            ref RenderingData renderingData, FRPVolumeInjectionPoint injectionPoint)
        {
            m_PassData.filteringSettings = m_FilteringSettings;
            // User can stack several scriptable renderers during rendering but deferred renderer should only lit pixels added by this gbuffer pass.
            // If we detect we are in such case (camera is in overlay mode), we clear the highest bits of stencil we have control of and use them to
            // mark what pixel to shade during deferred pass. Gbuffer will always mark pixels using their material types.


            ref CameraData cameraData = ref renderingData.cameraData;
            ShaderTagId lightModeTag = s_ShaderTagUniversalGBuffer;
            m_PassData.drawingSettings = RenderingUtils.CreateDrawingSettings(lightModeTag, ref renderingData, renderingData.cameraData.defaultOpaqueSortFlags);

            ExecutePass(context, cmd, m_PassData, ref renderingData);

            cmd.SetGlobalTexture(s_CameraNormalsTextureID, m_frpCameraData.gbufferRTHandles[GBufferNormalSmoothnessIndex]);
        }

        public override void Dispose(bool disposing)
        {
            if (m_frpCameraData.gbufferRTHandles != null)
            {
                // Release the old handles before creating the new one
                for (int i = 0; i < m_frpCameraData.gbufferRTHandles.Length; ++i)
                {
                    RTHandles.Release(m_frpCameraData.gbufferRTHandles[i]);
                }
            }

            ReleaseGbufferResources();
        }
        
        internal GraphicsFormat GetGBufferFormat(int index)
        {
            if (index == GBufferAlbedoIndex) // sRGB albedo, materialFlags
                return QualitySettings.activeColorSpace == ColorSpace.Linear ? GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm;
            else if (index == GBufferNormalSmoothnessIndex)
                return AccurateGbufferNormals ? GraphicsFormat.R8G8B8A8_UNorm : DepthNormalOnlyPass.GetGraphicsFormat(); // normal normal normal packedSmoothness
            else if (index == GbufferDepthIndex) // Render-pass on mobiles: reading back real depth-buffer is either inefficient (Arm Vulkan) or impossible (Metal).
                return GraphicsFormat.R32_SFloat;
            else
                return GraphicsFormat.None;
        }
        
        internal void ReAllocateGBufferIfNeeded(RenderTextureDescriptor gbufferSlice, int gbufferIndex)
        {
            if (m_frpCameraData.gbufferRTHandles != null)
            {
                // In case DeferredLight does not own the RTHandle, we can skip realloc.
                if (m_frpCameraData.gbufferRTHandles[gbufferIndex].GetInstanceID() != this.m_frpCameraData.gbufferRTHandles[gbufferIndex].GetInstanceID())
                    return;

                gbufferSlice.depthBufferBits = 0; // make sure no depth surface is actually created
                gbufferSlice.stencilFormat = GraphicsFormat.None;
                gbufferSlice.graphicsFormat = GetGBufferFormat(gbufferIndex);
                RenderingUtils.ReAllocateIfNeeded(ref m_frpCameraData.gbufferRTHandles[gbufferIndex], gbufferSlice, FilterMode.Point, TextureWrapMode.Clamp, name: k_GBufferNames[gbufferIndex]);
                this.m_frpCameraData.gbufferRTHandles[gbufferIndex] = m_frpCameraData.gbufferRTHandles[gbufferIndex];
            }
        }

        void ExecutePass(ScriptableRenderContext context, CommandBuffer cmd, PassData data, ref RenderingData renderingData, bool useRenderGraph = false)
        {
            // TODO: Render Layer
            
            // TODO: OverLay

            NativeArray<ShaderTagId> tagValues = new NativeArray<ShaderTagId>(s_ShaderTagValues, Allocator.Temp);
            NativeArray<RenderStateBlock> stateBlocks = new NativeArray<RenderStateBlock>(s_RenderStateBlocks, Allocator.Temp);

            //context.DrawRenderers(renderingData.cullResults, ref data.drawingSettings, ref data.filteringSettings, s_ShaderTagUniversalMaterialType, false, tagValues, stateBlocks);
            var rendererListParams = new RendererListParams(renderingData.cullResults, data.drawingSettings, data.filteringSettings);
            cmd.DrawRendererList(context.CreateRendererList(ref rendererListParams));
            
            cmd.SetGlobalTexture("_BaseColorTexture", m_frpCameraData.gbufferRTHandles[GBufferAlbedoIndex]);

            tagValues.Dispose();
            stateBlocks.Dispose();

            // Render objects that did not match any shader pass with error shader
            RenderingUtils.RenderObjectsWithError(context, ref renderingData.cullResults, renderingData.cameraData.camera, data.filteringSettings, SortingCriteria.None);

            cmd.SetGlobalTexture(s_CameraNormalsTextureID, m_frpCameraData.gbufferRTHandles[GBufferNormalSmoothnessIndex]);
            
            cmd.SetGlobalTexture("_CameraDepthTexture", m_frpCameraData.gBufferDepthTexture.nameID);

            // if (!UseRenderPass)
            // {
            //     // TODO: Copy Depth For Now
            //     Vector2 viewportScale = DepthAttachment.useScaling ? new Vector2(DepthAttachment.rtHandleProperties.rtHandleScale.x, DepthAttachment.rtHandleProperties.rtHandleScale.y) : Vector2.one;
            //     bool yflip = renderingData.cameraData.IsHandleYFlipped(DepthAttachment) != renderingData.cameraData.IsHandleYFlipped(furpCameraData.DepthCopyTexture);
            //     Vector4 scaleBias = yflip ? new Vector4(viewportScale.x, -viewportScale.y, 0, viewportScale.y) : new Vector4(viewportScale.x, viewportScale.y, 0, 0);
            //     Blitter.BlitCameraTexture(cmd, DepthAttachment, furpCameraData.DepthCopyTexture, scaleBias);
            //     
            //     //cmd.SetGlobalTexture("_CameraDepthAttachment", DepthAttachment);
            //     cmd.SetGlobalTexture("_CameraDepthTexture", furpCameraData.DepthCopyTexture);
            // }
        }

        private class PassData
        {
            internal TextureHandle[] gbuffer;
            internal TextureHandle depth;

            internal RenderingData renderingData;

            internal FilteringSettings filteringSettings;
            internal DrawingSettings drawingSettings;
        }
    }
}
