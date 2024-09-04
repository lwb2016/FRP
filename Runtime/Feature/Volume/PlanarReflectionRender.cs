using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.FRP
{
    [FRPVolume("PlanarReflection", FRPVolumeInjectionPoint.BeforeOpaque)]
    internal class PlanarReflectionRender : FRPVolumeRenderer
    {
        private FRPVolumeData m_volumeSetting;
        private Material m_BlurMaterial;
        private PlanarReflectionVolume m_VolumeComponent;
        Matrix4x4 reflectionMatrix;
        Vector3 positionOnPlane = Vector3.zero;
        private List<ShaderTagId> m_ShaderTagIDList = new List<ShaderTagId>();
        private FilteringSettings m_FilteringSettingsOpaque;
        private FilteringSettings m_FilteringSettingsTransparent;
        RenderQueueRange queueOpaque = new RenderQueueRange();
        RenderQueueRange queueTransparent = new RenderQueueRange();
        private int renderAdvanceInter = 3; //framing strategy -- Mod 0: Opaque 1, 1: Sky And Transparent, 3: ALl
        private RenderStateBlock m_RenderStateBlock;
        private ProfilingSampler m_ProfilingSampler;

        private RTHandle[] blurTempRT = new RTHandle[2];
        private RTHandle[] m_MipDown;
     

        private RenderTextureDescriptor m_Descriptor;
        private RenderTextureDescriptor m_depthDesc;
        
        private static readonly string m_ProfilerTag = "Render PlanarReflection";

        private static readonly int BLUR_OFFSETX_P = Shader.PropertyToID("_BlurOffsetX");
        private static readonly int BLUR_OFFSETY_P = Shader.PropertyToID("_BlurOffsetY");
        private static readonly int SOURCE_TEX_P = Shader.PropertyToID("_SourceTex");
        private static readonly string ReflectionTexDepthID = "_ReflectionTex_Depth";
        private static readonly string ReflectionTexID = "_ReflectionTex";
        private static string ANISOBLUR = "_ANISOBLUR";


        public PlanarReflectionRender(ref FRPVolumeData volumeSetting)
        {
            m_volumeSetting = volumeSetting;
            m_ShaderTagIDList.Clear();
            if (volumeSetting.planarReflectionLightModeTag == null ||
                volumeSetting.planarReflectionLightModeTag.Length == 0)
            {
                m_ShaderTagIDList.Add(new ShaderTagId("UniversalForwardOnly"));
                m_ShaderTagIDList.Add(new ShaderTagId("UniversalForward"));
                m_ShaderTagIDList.Add(new ShaderTagId("SRPDefaultUnlit"));
            }
            else
            {
                foreach (var tag in volumeSetting.planarReflectionLightModeTag)
                {
                    m_ShaderTagIDList.Add(new ShaderTagId(tag));
                }
            }
            
            m_MipDown = new RTHandle[6];
        } 

        public override void Initialize()
        {
            m_BlurMaterial = CoreUtils.CreateEngineMaterial(m_volumeSetting.m_frpData.dualKawaseBlurShader);
            
            queueOpaque.lowerBound = 1001;
            queueOpaque.upperBound = 2450;
            queueTransparent.lowerBound = 2451;
            queueTransparent.upperBound = 5000;
            
            m_FilteringSettingsOpaque = new FilteringSettings(queueOpaque, m_VolumeComponent.reflectLayer.value);
            m_FilteringSettingsTransparent = new FilteringSettings(queueTransparent, m_VolumeComponent.reflectLayer.value);
            
            m_ProfilingSampler = new ProfilingSampler(m_ProfilerTag);
        }

        public override bool Setup(ScriptableRenderer renderer, ref RenderingData renderingData, ref FRPCamera frpCamera, FRPVolumeInjectionPoint injectionPoint)
        {
            base.Setup(renderer, ref renderingData, ref frpCamera, injectionPoint);
            var stack = VolumeManager.instance.stack;
            m_VolumeComponent = stack.GetComponent<PlanarReflectionVolume>();
            if (!m_VolumeComponent.IsActive())
            {
                ReleaseRTHandles();
                isActive = false;
                return false;
            }
            if (renderingData.cameraData.camera.cameraType == CameraType.Reflection) return false;
            //if(renderingData.cameraData.cameraType == CameraType.Game) if (Time.frameCount % (m_VolumeComponent.renderIntervals.value+1) != 0) return false;

            isActive = true;

            Matrix4x4 originWoldToCam = renderingData.cameraData.camera.worldToCameraMatrix;
            Matrix4x4 reflWorldToCam = originWoldToCam * reflectionMatrix;
            //renderingData.cameraData.camera.cullingMatrix = renderingData.cameraData.camera.cullingMatrix * reflectionMatrix;

            if (m_BlurMaterial == null) m_BlurMaterial = CoreUtils.CreateEngineMaterial(m_volumeSetting.m_frpData.dualKawaseBlurShader);
            
            positionOnPlane.y = m_VolumeComponent.planarHeightOffset.value;

            var reflectionPlane = new Vector4(m_VolumeComponent.planeUp.value.x, m_VolumeComponent.planeUp.value.y, m_VolumeComponent.planeUp.value.z, -Vector3.Dot(m_VolumeComponent.planeUp.value, m_VolumeComponent.position.value) - (m_VolumeComponent.planarHeightOffset.value));
            reflectionMatrix = CalculateReflectMatrix(reflectionPlane);

            var cameraTextureDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            
            m_Descriptor = cameraTextureDescriptor;
            m_Descriptor.depthBufferBits = 0;
            m_Descriptor.useMipMap = false;
            m_Descriptor.msaaSamples = 1;
            m_Descriptor.colorFormat = RenderTextureFormat.ARGBHalf;
            m_Descriptor.width = Mathf.RoundToInt(cameraTextureDescriptor.width * m_VolumeComponent.resolution.value);
            m_Descriptor.height = Mathf.RoundToInt(cameraTextureDescriptor.height * m_VolumeComponent.resolution.value);

            RenderingUtils.ReAllocateIfNeeded(ref m_frpCameraData.planarReflectionRTHandle, m_Descriptor, FilterMode.Trilinear,
                    TextureWrapMode.Clamp, name: "_ReflectionTex");

            m_depthDesc = m_Descriptor;
            m_depthDesc.depthBufferBits = (int)m_VolumeComponent.depthBit.value;
            m_depthDesc.colorFormat = RenderTextureFormat.Depth;
            m_depthDesc.useMipMap = false;
            RenderingUtils.ReAllocateIfNeeded(ref m_frpCameraData.planarReflectDepthRTHandle, m_depthDesc, FilterMode.Point,
                TextureWrapMode.Clamp, name: "_ReflectionMipTex_Depth");
            
            if (m_VolumeComponent.isBlur.value)
            {
                RenderTextureDescriptor blurDescriptor = m_Descriptor;
                blurDescriptor.depthBufferBits = 0;
                blurDescriptor.colorFormat = RenderTextureFormat.ARGBHalf;
   
                for (var i = 0; i < 2; ++i)
                {
                    blurDescriptor.width = Mathf.RoundToInt(cameraTextureDescriptor.width * m_VolumeComponent.resolution.value) >> (i);
                    blurDescriptor.height = Mathf.RoundToInt(cameraTextureDescriptor.height * m_VolumeComponent.resolution.value) >> (i);

                    RenderingUtils.ReAllocateIfNeeded(ref blurTempRT[i], blurDescriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_ReflectionBlurTex"+i);      
                }
            }
            else if (m_VolumeComponent.isUseMipMap.value)
            {
                Shader.SetGlobalFloat("_MipCount", m_VolumeComponent.MipMapCount.value);

                RenderTextureDescriptor mipDescriptor = m_Descriptor;
                mipDescriptor.useMipMap = true;
                mipDescriptor.autoGenerateMips = false;
                mipDescriptor.mipCount = m_VolumeComponent.MipMapCount.value;
                mipDescriptor.width = 512;
                mipDescriptor.height = 512;
                mipDescriptor.msaaSamples = 1;

                RenderingUtils.ReAllocateIfNeeded(ref m_frpCameraData.planarReflectionMipRTHandle, mipDescriptor, FilterMode.Trilinear,
                    TextureWrapMode.Clamp, name: "_ReflectionMipTex");

                mipDescriptor.useMipMap = false;
                mipDescriptor.depthBufferBits = 0;
                
                for (var i = 0; i < m_VolumeComponent.MipMapCount.value; ++i)
                {

                    RenderingUtils.ReAllocateIfNeeded(ref m_MipDown[i], mipDescriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_ReflMipDown" + i);

                    mipDescriptor.width = Mathf.Max(1, (int)(mipDescriptor.width >>1));
                    mipDescriptor.height = Mathf.Max(1, (int)(mipDescriptor.height >>1));
                }
            }
            return true;
        }
        
        private RTHandle PlanarReflectionAllocatorFunction(string viewName, int frameIndex, RTHandleSystem rtHandleSystem)
        {
            return rtHandleSystem.Alloc(Vector2.one * m_VolumeComponent.resolution.value, colorFormat: m_Descriptor.graphicsFormat, dimension: TextureDimension.Tex2D,
                enableRandomWrite: false, useMipMap: false, autoGenerateMips: false,
                name: string.Format("{0}_ReflectionTex{1}", viewName, frameIndex));
        }
        
        private RTHandle PlanarReflectionDepthAllocatorFunction(string viewName, int frameIndex, RTHandleSystem rtHandleSystem)
        {
            return rtHandleSystem.Alloc(Vector2.one * m_VolumeComponent.resolution.value, depthBufferBits:DepthBits.Depth16, colorFormat: m_depthDesc.graphicsFormat, dimension: TextureDimension.Tex2D,
                enableRandomWrite: false, useMipMap: false, autoGenerateMips: false,
                name: string.Format("{0}_ReflectionTex{1}", viewName, frameIndex));
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData, FRPVolumeRenderPass renderPass)
        {
            CoreUtils.SetRenderTarget(cmd, m_frpCameraData.planarReflectionRTHandle.nameID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                m_frpCameraData.planarReflectDepthRTHandle.nameID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, ClearFlag.All);

        }

        public DrawingSettings CreateDrawingSettings(List<ShaderTagId> shaderTagIdList,
            ref RenderingData renderingData, SortingCriteria sortingCriteria)
        {
            return RenderingUtils.CreateDrawingSettings(shaderTagIdList, ref renderingData, sortingCriteria);
        }
        
        private static float sgn(float a)
        {
            return a > 0.0f ? 1.0f : a < 0.0f ? -1.0f : 0.0f;
        }
        
        
        public override void Render(CommandBuffer cmd, ScriptableRenderContext context,
            FRPVolumeRenderPass.PostProcessRTHandles rtHandles,
            ref RenderingData renderingData, FRPVolumeInjectionPoint injectionPoint)
        {

            //Matrix4x4 originWoldToCam = renderingData.cameraData.camera.worldToCameraMatrix;
            Matrix4x4 originWoldToCam = renderingData.cameraData.GetViewMatrix();
            Matrix4x4 reflWorldToCam = originWoldToCam * reflectionMatrix;
            var projectionMatrix = renderingData.cameraData.GetProjectionMatrix();
            Matrix4x4 originProject = projectionMatrix;
            Matrix4x4 refProjectionMatrix =  projectionMatrix;
            
            var clipNormal = reflWorldToCam.MultiplyVector(m_VolumeComponent.planeUp.value).normalized;
            var clipPlane = new Vector4(clipNormal.x, clipNormal.y, clipNormal.z, -Vector3.Dot(reflWorldToCam.MultiplyPoint(positionOnPlane), clipNormal));
            var oblique = clipPlane * (2.0F / (Vector4.Dot(clipPlane, refProjectionMatrix.inverse * new Vector4(sgn(clipPlane.x), sgn(clipPlane.y), 1.0f, 1.0f))));
            refProjectionMatrix[2] = oblique.x - refProjectionMatrix[3];
            refProjectionMatrix[6] = oblique.y - refProjectionMatrix[7];
            refProjectionMatrix[10] = oblique.z - refProjectionMatrix[11];
            refProjectionMatrix[14] = oblique.w - refProjectionMatrix[15];
            
            renderingData.cameraData.camera.cullingMatrix *= reflectionMatrix;
            renderingData.cameraData.camera.TryGetCullingParameters(false, out var cullingParams);
            //cullingParams.cullingMatrix = renderingData.cameraData.camera.cullingMatrix * reflectionMatrix;
            var cullResults = context.Cull(ref cullingParams);
            
            cmd.SetViewProjectionMatrices(reflWorldToCam, refProjectionMatrix);
            cmd.SetInvertCulling(true);
            var originRect = renderingData.cameraData.camera.pixelRect;
            if (m_VolumeComponent.usePropeBox.value && PlanarReflectionBounding.Instance != null)
            {
                PlanarReflectionBounding.Instance.CalculateBounds();
                var pixelSize = PlanarReflectionBounding.Instance.GetScreenPercentage(renderingData.cameraData.camera, reflWorldToCam.inverse,  refProjectionMatrix * reflWorldToCam);

                float wSize = 1.0f / (pixelSize.z - pixelSize.x);
                float hSize = 1.0f / (pixelSize.w - pixelSize.y);

                float ycenter = 0;
                if (!SystemInfo.usesReversedZBuffer)
                {
                    ycenter = (pixelSize.w) / hSize;
                    ycenter = 1-ycenter;
                    // Debug.Log("OpenGL");
                }
                else
                {
                    //Debug.Log("DX");
                    ycenter = (pixelSize.y) / hSize;
                }

                // padding 1 px
                var finalW = (m_Descriptor.width) * wSize + 1;
                var finalH = (m_Descriptor.height) * hSize + 1;
                var rectCenter = new Rect(-pixelSize.x * finalW, -pixelSize.y * finalH, finalW, finalH);
                cmd.SetViewport(rectCenter);
                cmd.SetGlobalVector("_PlanarRect", new Vector4(rectCenter.x/ m_Descriptor.width, rectCenter.y/ m_Descriptor.height, finalW / m_Descriptor.width, finalH / m_Descriptor.height));
            }
            else
            {
                cmd.SetGlobalVector("_PlanarRect", new Vector4(0, 0, 1, 1));
            }
            
            var sortFlags = SortingCriteria.CommonOpaque;
            var drawSetting = CreateDrawingSettings(m_ShaderTagIDList, ref renderingData, sortFlags);
            var rendererListParams = new RendererListParams(cullResults, drawSetting, m_FilteringSettingsOpaque);
            cmd.DrawRendererList(context.CreateRendererList(ref rendererListParams));
            
            if (m_VolumeComponent.renderSkyBox.value)
            {
                cmd.EnableKeyword(GlobalKeyword.Create("_PLANARREFLECTION"));
                cmd.DrawRendererList(context.CreateSkyboxRendererList(renderingData.cameraData.camera));
            }

            if (m_VolumeComponent.renderTransparent.value)
            {
                sortFlags = SortingCriteria.CommonTransparent;
                drawSetting = CreateDrawingSettings(m_ShaderTagIDList, ref renderingData, sortFlags);
                rendererListParams = new RendererListParams(cullResults, drawSetting, m_FilteringSettingsTransparent);
                cmd.DrawRendererList(context.CreateRendererList(ref rendererListParams));
            }
            
            if (m_VolumeComponent.isUseMipMap.value)
            {
                cmd.SetGlobalTexture(ReflectionTexID, m_frpCameraData.planarReflectionMipRTHandle.nameID);
            }
            else 
            {
                cmd.SetGlobalTexture(ReflectionTexID, m_frpCameraData.planarReflectionRTHandle.nameID);
                cmd.SetGlobalTexture(ReflectionTexDepthID, m_frpCameraData.planarReflectDepthRTHandle.nameID);
            }

            if (m_VolumeComponent.isBlur.value)
            {
                cmd.SetGlobalFloat(BLUR_OFFSETX_P, m_VolumeComponent.blurRadiusH.value * m_VolumeComponent.blurRadius.value);
                cmd.SetGlobalFloat(BLUR_OFFSETY_P, m_VolumeComponent.blurRadiusV.value * m_VolumeComponent.blurRadius.value);
                DrawFullScreenTriangle(cmd, m_frpCameraData.planarReflectionRTHandle, blurTempRT[0], m_BlurMaterial, 1);
                DrawFullScreenTriangle(cmd, blurTempRT[0], blurTempRT[1], m_BlurMaterial, 1);
                DrawFullScreenTriangle(cmd, blurTempRT[1], blurTempRT[0], m_BlurMaterial, 2);
                DrawFullScreenTriangle(cmd, blurTempRT[0], m_frpCameraData.planarReflectionRTHandle, m_BlurMaterial, 2);
            }
            else if (m_VolumeComponent.isUseMipMap.value)
            {
                cmd.SetGlobalFloat(BLUR_OFFSETX_P, m_VolumeComponent.blurRadiusH.value * m_VolumeComponent.blurRadius.value);
                cmd.SetGlobalFloat(BLUR_OFFSETY_P, m_VolumeComponent.blurRadiusV.value * m_VolumeComponent.blurRadius.value);

                var lastDown = m_frpCameraData.planarReflectionRTHandle;
                for (int i = 0; i < m_VolumeComponent.MipMapCount.value; i++)
                {
                    int shaderPass = i % 2 + 1;
                    DrawFullScreenTriangle(cmd, lastDown, m_MipDown[i], m_BlurMaterial, shaderPass);                  
                    CopyMipMap(cmd, m_MipDown[i], m_frpCameraData.planarReflectionMipRTHandle, i);
                    lastDown = m_MipDown[i];
                }
            }

            cmd.SetInvertCulling(false);
           
            if (m_VolumeComponent.isAnisoBlur.value)
            {
                m_BlurMaterial.EnableKeyword(ANISOBLUR);
            }
            else
            {
                m_BlurMaterial.DisableKeyword(ANISOBLUR);
            }
            
            cmd.SetGlobalFloat("_AnisoOffset", m_VolumeComponent.anisoOffset.value);
            cmd.SetGlobalFloat("_AnisoPower", m_VolumeComponent.anisoPower.value);
            cmd.SetGlobalFloat("_DepthFade", m_VolumeComponent.depthFade.value);
            // TODO: Can't attenuate by depth when rendering skyboxes, looking for a solution
            cmd.SetGlobalFloat("_IsDepthFade", m_VolumeComponent.depthFadeEnable.value ? 1 : 0);

            cmd.SetViewProjectionMatrices(originWoldToCam, originProject);
            cmd.SetViewport(originRect);
            cmd.DisableKeyword(GlobalKeyword.Create("_PLANARREFLECTION"));
            renderingData.cameraData.camera.ResetCullingMatrix();
        }
        
        private void DrawFullScreenTriangle(CommandBuffer cmd, RTHandle source, RTHandle destination, Material blitMaterial, int pass)
        {
            cmd.SetGlobalTexture(SOURCE_TEX_P, source.nameID);
            Blitter.BlitCameraTexture(cmd, source, destination, blitMaterial, pass);
        }

        private void CopyMipMap(CommandBuffer cmd, RTHandle src, RTHandle dst, int mipCount)
        {
            cmd.CopyTexture(src, 0,0, dst, 0,mipCount);           
        }
        
        
        Matrix4x4 CalculateReflectMatrix(Vector4 reflectionPlane)
        {
            var reflectM = new Matrix4x4();
            reflectM.m00 = (1F - 2F * reflectionPlane[0] * reflectionPlane[0]);
            reflectM.m01 = (-2F * reflectionPlane[0] * reflectionPlane[1]);
            reflectM.m02 = (-2F * reflectionPlane[0] * reflectionPlane[2]);
            reflectM.m03 = (-2F * reflectionPlane[3] * reflectionPlane[0]);
            reflectM.m10 = (-2F * reflectionPlane[1] * reflectionPlane[0]);
            reflectM.m11 = (1F - 2F * reflectionPlane[1] * reflectionPlane[1]);
            reflectM.m12 = (-2F * reflectionPlane[1] * reflectionPlane[2]);
            reflectM.m13 = (-2F * reflectionPlane[3] * reflectionPlane[1]);
            reflectM.m20 = (-2F * reflectionPlane[2] * reflectionPlane[0]);
            reflectM.m21 = (-2F * reflectionPlane[2] * reflectionPlane[1]);
            reflectM.m22 = (1F - 2F * reflectionPlane[2] * reflectionPlane[2]);
            reflectM.m23 = (-2F * reflectionPlane[3] * reflectionPlane[2]);
            reflectM.m30 = 0F;
            reflectM.m31 = 0F;
            reflectM.m32 = 0F;
            reflectM.m33 = 1F;
            return reflectM;
        }

        public override void ReleaseRTHandles()
        {
            m_frpCameraData.planarReflectionRTHandle?.Release();
            m_frpCameraData.planarReflectionMipRTHandle?.Release();
            m_frpCameraData.planarReflectDepthRTHandle?.Release();
        }

        public override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (m_VolumeComponent.isUseMipMap.value)
            {
                foreach (var handle in m_MipDown)
                {
                    handle?.Release();
                }
                m_MipDown = null;
            }

            m_frpCameraData.planarReflectionRTHandle?.Release();
            m_frpCameraData.planarReflectionMipRTHandle?.Release();
            m_frpCameraData.planarReflectDepthRTHandle?.Release();

            foreach (var tempRT in blurTempRT)
            {
                tempRT?.Release();
            }
        }

    }
}