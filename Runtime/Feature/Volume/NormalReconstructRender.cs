using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

namespace UnityEngine.Rendering.FRP
{
    [FRPVolume("NormalReconstructRender", FRPVolumeInjectionPoint.BeforePostProcess)]
    internal class NormalReconstructRender : FRPVolumeRenderer
    {
        public FRPVolumeData volumeSetting;
        private Material m_Material;
        private CameraData cameraData { get; set; }
        private RTHandle normalRT;
        private Vector4[] m_UVToViewPerEye = new Vector4[2];
        private static readonly int SOURCE_TEX_P = Shader.PropertyToID("_SourceTex");
        private static readonly int cameraNormalTexture = Shader.PropertyToID("_CameraNormalsTexture");
        private int needNormalCount = 0; // only bigger 1 need this pass
        public NormalReconstructRender(ref FRPVolumeData volumeSetting)
        {
            this.volumeSetting = volumeSetting;
        }

        public override void Initialize()
        {
            m_Material = CoreUtils.CreateEngineMaterial(volumeSetting.m_frpData.normalReconstructShader);
        }
        
        private static readonly int s_ProjectionParams2ID = Shader.PropertyToID("_ProjectionParams2");
        private static readonly int s_CameraViewTopLeftCornerID = Shader.PropertyToID("_CameraViewTopLeftCorner");
        private static readonly int s_CameraViewXExtentID = Shader.PropertyToID("_CameraViewXExtent");
        private static readonly int s_CameraViewYExtentID = Shader.PropertyToID("_CameraViewYExtent");
        private static readonly int s_CameraViewZExtentID = Shader.PropertyToID("_CameraViewZExtent");
        private static readonly int s_CameraViewProjectionsID = Shader.PropertyToID("_CameraViewProjections");
        public static readonly int _SourceSize = Shader.PropertyToID("_SourceSize");
        internal const string k_SourceDepthLowKeyword = "_SOURCE_DEPTH_LOW";
        internal const string k_SourceDepthMediumKeyword = "_SOURCE_DEPTH_MEDIUM";
        internal const string k_SourceDepthHighKeyword = "_SOURCE_DEPTH_HIGH";

        private Vector4[] m_CameraTopLeftCorner = new Vector4[2];
        private Matrix4x4[] m_CameraViewProjections = new Matrix4x4[2];
        private Vector4[] m_CameraXExtent = new Vector4[2];
        private Vector4[] m_CameraYExtent = new Vector4[2];
        private Vector4[] m_CameraZExtent = new Vector4[2];
        public override bool Setup(ScriptableRenderer renderer, ref RenderingData renderingData, ref FRPCamera frpCamera, FRPVolumeInjectionPoint injectionPoint)
        {
            base.Setup(renderer, ref renderingData, ref frpCamera, injectionPoint);
            if (base.m_frpCamera.normalQuality == NormalQuality.Camera || volumeSetting.gBuffer)
            {
                Shader.EnableKeyword("_NORMALSRECONSTRUCT");
                isActive = false;
                return false;
            }
            
            var stack = VolumeManager.instance.stack;
            HBAOVolume m_HBAOVolume = stack.GetComponent<HBAOVolume>();
            SSGIVolume m_SSGI = stack.GetComponent<SSGIVolume>();

            needNormalCount = 0;

            if (m_HBAOVolume.IsActive() && m_HBAOVolume.mode == HBAOVolume.Mode.AfterOpaque &&
                injectionPoint == FRPVolumeInjectionPoint.AfterOpaqueAndSky)
            {
                needNormalCount += 1;
            }
            
            if (m_SSGI.IsActive())
            {
                needNormalCount += 1;
            }

            if (needNormalCount <= 1)
            {
                Shader.DisableKeyword("_NORMALSRECONSTRUCT");
                return false;
            }
            
            Shader.EnableKeyword("_NORMALSRECONSTRUCT");
            
            if (m_Material == null) m_Material = CoreUtils.CreateEngineMaterial(volumeSetting.m_frpData.normalReconstructShader);
            
            cameraData = renderingData.cameraData;
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.graphicsFormat = DepthNormalOnlyPass.GetGraphicsFormat();
            desc.useMipMap = false;
            desc.depthBufferBits = 0;

            RenderingUtils.ReAllocateIfNeeded(ref normalRT, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_CameraNormalsTexture");
            
            #if ENABLE_VR && ENABLE_XR_MODULE
                int eyeCount = renderingData.cameraData.xr.enabled && renderingData.cameraData.xr.singlePassEnabled ? 2 : 1;
            #else
                int eyeCount = 1;
            #endif
            for (int eyeIndex = 0; eyeIndex < eyeCount; eyeIndex++)
            {
                Matrix4x4 view = renderingData.cameraData.GetViewMatrix(eyeIndex);
                Matrix4x4 proj = renderingData.cameraData.GetProjectionMatrix(eyeIndex);
                m_CameraViewProjections[eyeIndex] = proj * view;

                // camera view space without translation, used by SSAO.hlsl ReconstructViewPos() to calculate view vector.
                Matrix4x4 cview = view;
                cview.SetColumn(3, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
                Matrix4x4 cviewProj = proj * cview;
                Matrix4x4 cviewProjInv = cviewProj.inverse;

                Vector4 topLeftCorner = cviewProjInv.MultiplyPoint(new Vector4(-1, 1, -1, 1));
                Vector4 topRightCorner = cviewProjInv.MultiplyPoint(new Vector4(1, 1, -1, 1));
                Vector4 bottomLeftCorner = cviewProjInv.MultiplyPoint(new Vector4(-1, -1, -1, 1));
                Vector4 farCentre = cviewProjInv.MultiplyPoint(new Vector4(0, 0, 1, 1));
                m_CameraTopLeftCorner[eyeIndex] = topLeftCorner;
                m_CameraXExtent[eyeIndex] = topRightCorner - topLeftCorner;
                m_CameraYExtent[eyeIndex] = bottomLeftCorner - topLeftCorner;
                m_CameraZExtent[eyeIndex] = farCentre;
            }

            m_Material.SetVector(s_ProjectionParams2ID, new Vector4(1.0f / renderingData.cameraData.camera.nearClipPlane, 0.0f, 0.0f, 0.0f));
            m_Material.SetMatrixArray(s_CameraViewProjectionsID, m_CameraViewProjections);
            m_Material.SetVectorArray(s_CameraViewTopLeftCornerID, m_CameraTopLeftCorner);
            m_Material.SetVectorArray(s_CameraViewXExtentID, m_CameraXExtent);
            m_Material.SetVectorArray(s_CameraViewYExtentID, m_CameraYExtent);
            m_Material.SetVectorArray(s_CameraViewZExtentID, m_CameraZExtent);
            m_Material.SetVector(_SourceSize, new Vector4(desc.width, desc.height, 1.0f / desc.width, 1.0f / desc.height));
            
            switch (base.m_frpCamera.normalQuality)
            {
                case NormalQuality.Low:
                    CoreUtils.SetKeyword(m_Material, k_SourceDepthLowKeyword, true);
                    CoreUtils.SetKeyword(m_Material, k_SourceDepthMediumKeyword, false);
                    CoreUtils.SetKeyword(m_Material, k_SourceDepthHighKeyword, false);
                    break;
                case NormalQuality.Medium:
                    CoreUtils.SetKeyword(m_Material, k_SourceDepthLowKeyword, false);
                    CoreUtils.SetKeyword(m_Material, k_SourceDepthMediumKeyword, true);
                    CoreUtils.SetKeyword(m_Material, k_SourceDepthHighKeyword, false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            return isActive;
        }
        
                
        public override void Render(CommandBuffer cmd, ScriptableRenderContext context, FRPVolumeRenderPass.PostProcessRTHandles rtHandles,
            ref RenderingData renderingData, FRPVolumeInjectionPoint injectionPoint)
        {
            if (m_Material == null)
            {
                Debug.LogError("material has not been correctly initialized...");
                return;
            }
            DrawFullScreenTriangle(cmd, rtHandles.m_Source, normalRT, m_Material, 0);
            cmd.SetGlobalTexture(cameraNormalTexture, normalRT.nameID);
        }
        
        public override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            normalRT?.Release();
        }

        private void DrawFullScreenTriangle(CommandBuffer cmd, RTHandle source, RTHandle destination, Material blitMaterial, int pass)
        {
            cmd.SetGlobalTexture(SOURCE_TEX_P, source.nameID);
            Blitter.BlitCameraTexture(cmd, source, destination, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, blitMaterial, pass);
        }
    }

}

