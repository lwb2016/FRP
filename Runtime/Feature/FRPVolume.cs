using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;
using UnityEngine.Serialization;

namespace UnityEngine.Rendering.FRP
{
    public enum NormalQuality
    {
        Low,
        Medium,
        //High,
        Camera
    }
    
    [Serializable]
    public class FRPVolumeData 
    {
        [FormerlySerializedAs("AreaLight")]
        [Header("General")]
        [SerializeField] internal bool areaLight = false;
        [SerializeField] internal bool gBuffer = false;
        [FormerlySerializedAs("PlanarReflectionLightModeTag")] [SerializeField] internal string[] planarReflectionLightModeTag;
        
        [FormerlySerializedAs("mFrpData")] [FormerlySerializedAs("m_furpData")] [Header("Post-Processing")]
        public FRPData m_frpData = null;
        internal FRPCamreaData frpCameraDatas;
    }
    
    [Serializable]
    public class FRPVolume : ScriptableRendererFeature
    {
        [FormerlySerializedAs("furpVolumeData")] [SerializeField]
        internal FRPVolumeData frpVolumeData = new FRPVolumeData();

        internal FRPVolumeRenderPass m_BeforeRenderPass, m_BeforeDeferredPass, m_BeforeGPass, m_BeforeOpaquePass, m_AfterOpaqueAndSkyPass, m_AfterTransparentPass, m_BeforePostProcessPass;
        //internal List<FRPVolumeRenderer> beforeRenders = new List<FRPVolumeRenderer>();
        internal List<FRPVolumeRenderer> beforeGBuffers = new List<FRPVolumeRenderer>();
        internal List<FRPVolumeRenderer> beforeOpaqueRenderers = new List<FRPVolumeRenderer>();
        internal List<FRPVolumeRenderer> beforeDeferredPassRenderers = new List<FRPVolumeRenderer>();
        internal List<FRPVolumeRenderer> afterOpaqueAndSkyRenderers = new List<FRPVolumeRenderer>();
        internal List<FRPVolumeRenderer> afterTransparentRenderers = new List<FRPVolumeRenderer>();
        internal List<FRPVolumeRenderer> beforePostProcessRenderers = new List<FRPVolumeRenderer>();
        internal List<FRPVolumeRenderer> afterPostProcessRenderers = new List<FRPVolumeRenderer>();

        internal FRPGBufferPass furpGbufferPass;
        internal VolumeBeforeDeferred beforeDeferred;
        internal HBAORenderer hbaoRender;
        internal SSGIReneder ssgiReneder;
        internal AreaLightsRender areaLightsRender;
        internal PlanarReflectionRender planarReflectionRender;
        internal NormalReconstructRender normalReconstructRender;
       	internal PCSSRender pcssRender;
       	internal BlackFadeEffectRender m_blackFadeEffectRender;

        private SSGIVolume m_ssgiVolume;

        public override void Create()
        {
            if (frpVolumeData.m_frpData == null)
            {
                #if UNITY_EDITOR
                    frpVolumeData.m_frpData = FRPData.GetDefaultData();
                #endif
            }
            
            if(frpVolumeData.m_frpData == null) return;

            //beforeRenders.Clear();
            beforeGBuffers.Clear();
            beforeOpaqueRenderers.Clear();
            beforeDeferredPassRenderers.Clear();
            afterOpaqueAndSkyRenderers.Clear();
            afterTransparentRenderers.Clear();
            beforePostProcessRenderers.Clear();
            afterPostProcessRenderers.Clear();
            
           // this.fsr3Renderer = new Fsr3Renderer(ref frpVolumeData);
            this.beforeDeferred = new VolumeBeforeDeferred(ref frpVolumeData);
            this.hbaoRender = new HBAORenderer(ref frpVolumeData);
            this.ssgiReneder = new SSGIReneder(ref frpVolumeData); 
            this.areaLightsRender = new AreaLightsRender(ref frpVolumeData);
            this.planarReflectionRender = new PlanarReflectionRender(ref frpVolumeData);
            this.normalReconstructRender = new NormalReconstructRender(ref frpVolumeData);
            this.pcssRender = new PCSSRender(ref frpVolumeData);
            this.m_blackFadeEffectRender = new BlackFadeEffectRender(ref frpVolumeData);
            
            
            //beforeRenders.Add(beforeRender);
            beforeGBuffers.Add(furpGbufferPass);
            beforeOpaqueRenderers.Add(pcssRender);
            
            if (frpVolumeData.areaLight)
            {
                Shader.EnableKeyword("_FURPAREALIGHT"); // TODO:
                beforeOpaqueRenderers.Add(areaLightsRender);
            }
            else
            {
                Shader.DisableKeyword("_FURPAREALIGHT");
            }
            beforeOpaqueRenderers.Add(planarReflectionRender);
            beforeOpaqueRenderers.Add(hbaoRender);
            
            //beforeDeferredPassRenderers.Add(beforeDeferred);
            
            afterOpaqueAndSkyRenderers.Add(normalReconstructRender);
            afterOpaqueAndSkyRenderers.Add(hbaoRender);

            // Only For forward Light
            beforePostProcessRenderers.Add(ssgiReneder);
            
            afterTransparentRenderers.Add(m_blackFadeEffectRender);
            
           // m_BeforeRenderPass = new FRPVolumeRenderPass(FURPVolumeInjectionPoint.BeforeRender, ref frpVolumeData, beforeRenders);
            //m_BeforeGPass = new FRPVolumeRenderPass(FURPVolumeInjectionPoint.BeforeGBuffer, ref frpVolumeData, beforeGBuffers);
            m_BeforeDeferredPass = new FRPVolumeRenderPass(FRPVolumeInjectionPoint.BeforeDeferred, ref frpVolumeData, beforeDeferredPassRenderers);
            m_BeforeOpaquePass = new FRPVolumeRenderPass(FRPVolumeInjectionPoint.BeforeOpaque, ref frpVolumeData, beforeOpaqueRenderers);
            m_AfterOpaqueAndSkyPass = new FRPVolumeRenderPass(FRPVolumeInjectionPoint.AfterOpaqueAndSky, ref frpVolumeData, afterOpaqueAndSkyRenderers);
            m_AfterTransparentPass = new FRPVolumeRenderPass(FRPVolumeInjectionPoint.AfterTransparent, ref frpVolumeData, afterTransparentRenderers);
            m_BeforePostProcessPass = new FRPVolumeRenderPass(FRPVolumeInjectionPoint.BeforePostProcess, ref frpVolumeData, beforePostProcessRenderers);
        }
        
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
#if UNITY_EDITOR
            if (renderingData.cameraData.cameraType == CameraType.Reflection ||
                renderingData.cameraData.cameraType == CameraType.Preview)
            {
                Shader.DisableKeyword(ShaderKeywordStrings.ScreenSpaceOcclusion);
                return;
            }
#endif
            ref CameraData cameraData = ref renderingData.cameraData;
            cameraData.camera.TryGetComponent<FRPCamera>(out var frpCamera);
            if (frpCamera == null)
            {
                frpCamera = renderingData.cameraData.camera.gameObject.AddComponent<FRPCamera>();
                frpCamera.frpEnable = false;
            }
            
            var frpCameraData = FRPCamreaData.GetOrCreate(cameraData.camera, cameraData); // TODO: XR
            
            if (frpCameraData.uberMaterial == null)
            {
                frpCameraData.uberMaterial = CoreUtils.CreateEngineMaterial(frpVolumeData.m_frpData.fURPVolumeUberShader);
            }
            
            if (renderingData.cameraData.cameraType == CameraType.SceneView)
            {
                frpCamera.frpEnable = CoreUtils.ArePostProcessesEnabled(renderingData.cameraData.camera);
            }

            if (!frpCamera.frpEnable)
            {
                Shader.DisableKeyword(ShaderKeywordStrings.ScreenSpaceOcclusion);
                frpCameraData.uberMaterial.enabledKeywords = null;
                ReleaseRTHandles();
                return;
            }
       
            var stack = VolumeManager.instance.stack;
            m_ssgiVolume = stack.GetComponent<SSGIVolume>();

            // EnqueuePass:
            // if (m_BeforeRenderPass.HasPostProcessRenderers &&
            //     m_BeforeRenderPass.PrepareRenderers(ref renderingData, ref frpCamera))
            // { 
            //     renderer.EnqueuePass(m_BeforeRenderPass);
            // }
            // if (m_BeforeGPass.HasPostProcessRenderers &&
            //     m_BeforeGPass.PrepareRenderers(ref renderingData, ref frpCamera))
            // {
            //     renderer.EnqueuePass(m_BeforeGPass);
            // }
            if (m_BeforeDeferredPass.HasPostProcessRenderers &&
                m_BeforeDeferredPass.PrepareRenderers(ref renderingData, ref frpCamera))
            {
                renderer.EnqueuePass(m_BeforeDeferredPass);
            }
            if (m_BeforeOpaquePass.HasPostProcessRenderers &&
                m_BeforeOpaquePass.PrepareRenderers(ref renderingData, ref frpCamera))
            {
                renderer.EnqueuePass(m_BeforeOpaquePass);
            }
            if (m_AfterOpaqueAndSkyPass.HasPostProcessRenderers &&
                m_AfterOpaqueAndSkyPass.PrepareRenderers(ref renderingData, ref frpCamera))
            {
                renderer.EnqueuePass(m_AfterOpaqueAndSkyPass);
            }
            if (m_AfterTransparentPass.HasPostProcessRenderers &&
                m_AfterTransparentPass.PrepareRenderers(ref renderingData, ref frpCamera))
            {
                renderer.EnqueuePass(m_AfterTransparentPass);
            }

            if (m_BeforePostProcessPass.HasPostProcessRenderers &&
                m_BeforePostProcessPass.PrepareRenderers(ref renderingData, ref frpCamera))
            {
                renderer.EnqueuePass(m_BeforePostProcessPass);
            }
        }
        
        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            if(renderingData.cameraData.cameraType == CameraType.Preview) return;

            var hasCameraData = renderingData.cameraData.camera.TryGetComponent<FRPCamera>(out var frpCamera);
            if (hasCameraData == false)
            {
                frpCamera = renderingData.cameraData.camera.gameObject.AddComponent<FRPCamera>();
                frpCamera.frpEnable = false;
            }
            
            CameraData cameraData = renderingData.cameraData;
            
           // m_BeforeRenderPass.Setup(renderer, renderer.cameraColorTargetHandle, ref frpVolumeData);
            //m_BeforeGPass.Setup(renderer, renderer.cameraColorTargetHandle, ref frpVolumeData);
            m_BeforeDeferredPass.Setup(renderer, renderer.cameraColorTargetHandle, ref frpVolumeData);
            m_BeforeOpaquePass.Setup(renderer, renderer.cameraColorTargetHandle, ref frpVolumeData);
            m_AfterOpaqueAndSkyPass.Setup(renderer, renderer.cameraColorTargetHandle, ref frpVolumeData);
            m_AfterTransparentPass.Setup(renderer, renderer.cameraColorTargetHandle, ref frpVolumeData);
            m_BeforePostProcessPass.Setup(renderer, renderer.cameraColorTargetHandle, ref frpVolumeData);
        }

        
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            m_BeforeOpaquePass.Dispose();
            m_AfterOpaqueAndSkyPass.Dispose();
            m_AfterTransparentPass.Dispose();
            m_BeforePostProcessPass.Dispose();
            
            FRPCamreaData.ClearAll();
        }

        protected void ReleaseRTHandles()
        {
            m_BeforeOpaquePass.ReleaseRTHandles();
            m_BeforeDeferredPass.ReleaseRTHandles();
            //m_BeforeGPass.ReleaseRTHandles();
            m_BeforeOpaquePass.ReleaseRTHandles();
            m_AfterOpaqueAndSkyPass.ReleaseRTHandles();
            m_AfterTransparentPass.ReleaseRTHandles();
            m_BeforePostProcessPass.ReleaseRTHandles();
        }
    }

}