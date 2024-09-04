using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.FRP
{
    [FRPVolume("SSGIVolume", FRPVolumeInjectionPoint.BeforePostProcess)]
    internal class SSGIReneder : FRPVolumeRenderer
    {
        const float GOLDENRATIO = 1.618033989f;

        static class ShaderParams
        {
            // targets
            public static int MainTex = Shader.PropertyToID("_MainTex");
            public static int SourceSize = Shader.PropertyToID("_SourceSize");
            public static int NoiseTex = Shader.PropertyToID("_NoiseTex");
            public static int InputRT = Shader.PropertyToID("_InputRTGI");
            public static int PrevResolve = Shader.PropertyToID("_PrevResolve");
            public static int DownBaseColorRT = Shader.PropertyToID("_BaseColorTexture");
            public static int SSGITexture = Shader.PropertyToID("_SSGITexture");
            public static int DownscaledDepthRT = Shader.PropertyToID("_DownscaledDepthRT");

            // uniforms
            public static int IndirectData = Shader.PropertyToID("_IndirectData");
            public static int RayData = Shader.PropertyToID("_RayData");
            public static int TemporalData = Shader.PropertyToID("_TemporalData");
            public static int WorldToViewDir = Shader.PropertyToID("_WorldToViewDir");
            public static int ExtraData = Shader.PropertyToID("_ExtraData");

            // keywords
            public const string SSGI_USES_BINARY_SEARCH = "_SSGI_USES_BINARY_SEARCH";
            public const string SSGI_REUSERAYTRACING = "_REUSERAYTRACING";
            public const string SSGI_FALLBACK_PROBE = "_SSGI_FALLBACK_PROBE";
            public const string SSGI_ORTHO_SUPPORT = "_SSGI_ORTHO_SUPPORT";
            public const string SSGI_DEBUG = "_SSGI_DEBUG";
        }

        enum Pass
        {
            Copy,
            Raycast,
            BlurHorizontal,
            BlurVertical,
            Upscale,
            TemporalDenoise,
            Compose,
            CopyDepth,
        }

        static Mesh _fullScreenMesh;

        static Mesh fullscreenMesh
        {
            get
            {
                if (_fullScreenMesh != null)
                {
                    return _fullScreenMesh;
                }

                float num = 1f;
                float num2 = 0f;
                Mesh val = new Mesh();
                _fullScreenMesh = val;
                _fullScreenMesh.SetVertices(new List<Vector3>
                {
                    new Vector3(-1f, -1f, 0f),
                    new Vector3(-1f, 1f, 0f),
                    new Vector3(1f, -1f, 0f),
                    new Vector3(1f, 1f, 0f)
                });
                _fullScreenMesh.SetUVs(0, new List<Vector2>
                {
                    new Vector2(0f, num2),
                    new Vector2(0f, num),
                    new Vector2(1f, num2),
                    new Vector2(1f, num)
                });
                _fullScreenMesh.SetIndices(new int[6] { 0, 1, 2, 2, 1, 3 }, (MeshTopology)0, 0, false);
                _fullScreenMesh.UploadMeshData(true);
                return _fullScreenMesh;
            }
        }

        private FRPVolumeData volumeData;
        private SSGIVolume m_VolumeComponent;
        private Material ssgiMaterial;

        public Material SSGIMaterial
        {
            get
            {
                if (ssgiMaterial == null)
                {
                    ssgiMaterial = CoreUtils.CreateEngineMaterial(volumeData.m_frpData.ssgiShader);
                }

                return ssgiMaterial;
            }
        }
        

        class PerCameraData
        {
            public Vector3 lastCameraPosition;
            public RTHandle rtDenoise;
            public int rtAcumCreationFrame;
            public RTHandle rtBounce;
            public int rtBounceCreationFrame;
        }

        ScriptableRenderer renderer;
        readonly Dictionary<Camera, PerCameraData> prevs = new Dictionary<Camera, PerCameraData>();

        float goldenRatioAcum;
        bool usesDenoise, usesCompareMode;
        Vector3 camPos;
        Volume[] volumes;
        Vector4[] emittersBoxMin, emittersBoxMax, emittersColors, emittersPositions;

        private RTHandle downScaleDepthHadnle;
        private RTHandle[] blurHHandles = new RTHandle[5];
        private RTHandle[] blurVHandles = new RTHandle[5];
        
        private RTHandle TempAcumHadnle;

        private RenderTextureDescriptor sourceDesc;
        private RenderTextureDescriptor downDesc;
        private RenderTextureDescriptor raytracingDesc;

        private Texture2D noiseTexture;

        public Texture2D NoiseTexture
        {
            get
            {
                if (noiseTexture == null)
                {
                    noiseTexture = Resources.Load<Texture2D>("FernVolumeRes/blueNoiseGI128RA");
                }

                return noiseTexture;
            }
        }

        public SSGIReneder(ref FRPVolumeData volumeData)
        {
            this.volumeData = volumeData;
        }
        
        public override bool Setup(ScriptableRenderer renderer, ref RenderingData renderingData, ref FRPCamera frpCamera, FRPVolumeInjectionPoint injectionPoint)
        {
            base.Setup(renderer, ref renderingData, ref frpCamera, injectionPoint);

            var stack = VolumeManager.instance.stack;
            m_VolumeComponent = stack.GetComponent<SSGIVolume>();
            m_frpCameraData.uberMaterial = m_frpCameraData.uberMaterial;
            var cam = renderingData.cameraData.camera;
            if (!m_VolumeComponent.IsActive())
            {
                CoreUtils.SetKeyword(m_frpCameraData.uberMaterial, FRPShaderProperty.SSGI, false);
                return false;
            }

            isActive = true;
            
            input = ScriptableRenderPassInput.None;
            if (!volumeData.gBuffer)
            {
                input |= ScriptableRenderPassInput.Depth; 
                if (frpCamera.normalQuality == NormalQuality.Camera)
                {
                    input |= ScriptableRenderPassInput.Normal; 
                }
            }
            
            m_frpCameraData.uberMaterial.EnableKeyword(FRPShaderProperty.SSGI);

            // Material
            SSGIMaterial.SetTexture(ShaderParams.NoiseTex, NoiseTexture);
            
            float rayTracingResolutionScale = m_VolumeComponent.rayTracingResolutionScale.value;
            sourceDesc = renderingData.cameraData.cameraTargetDescriptor;
            sourceDesc.colorFormat = RenderTextureFormat.ARGBHalf;
            sourceDesc.useMipMap = false;
            sourceDesc.depthBufferBits = 0;
            sourceDesc.msaaSamples = 1;
            
            //depth scale rthandle
            if (m_VolumeComponent.depthResolutionScale.value < 1.0f)
            {
                var rtDownDepth = sourceDesc;
                rtDownDepth.width = Mathf.CeilToInt((float)sourceDesc.width * m_VolumeComponent.depthResolutionScale.value);
                rtDownDepth.height = Mathf.CeilToInt((float)sourceDesc.height * m_VolumeComponent.depthResolutionScale.value);
                rtDownDepth.colorFormat = RenderTextureFormat.RHalf;
                rtDownDepth.sRGB = false;
                rtDownDepth.useMipMap = false;
                RenderingUtils.ReAllocateIfNeeded(ref downScaleDepthHadnle, rtDownDepth, FilterMode.Point,
                    TextureWrapMode.Clamp, name: "_DownscaledDepthRT");
            }
            
            downDesc = sourceDesc;
            downDesc.width = (int)(sourceDesc.width * rayTracingResolutionScale);
            downDesc.height = (int)(sourceDesc.height * rayTracingResolutionScale);
            raytracingDesc = downDesc;
            
            RenderingUtils.ReAllocateIfNeeded(ref m_frpCameraData.ssgiRTHadnle, raytracingDesc, FilterMode.Bilinear,
                TextureWrapMode.Clamp, name: "_RayTracingTexture");

            // m_frpCameraData.ssgiRTHadnle = m_frpCameraData.GetCurrentFrameRT((int)FRPCameraFrameHistoryType.SSGI)
            //                                ?? m_frpCameraData.AllocHistoryFrameRT((int)FRPCameraFrameHistoryType.SSGI,
            //                                    SSGIAllocatorFunction, 1);
            
            RenderingUtils.ReAllocateIfNeeded(ref TempAcumHadnle, raytracingDesc, FilterMode.Bilinear,
                name: "_TempDenoiseTexture");
            
            var blurDesc = raytracingDesc;
            
            for (var i = 0; i < m_VolumeComponent.blurIteration.value; ++i)
            {
                blurDesc.width = Mathf.Max(1, downDesc.width >> (i+1));
                blurDesc.height = Mathf.Max(1, downDesc.height >> (i+1));
                RenderingUtils.ReAllocateIfNeeded(ref blurHHandles[i], blurDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_SSGIBlurHTempTex"+i);
                RenderingUtils.ReAllocateIfNeeded(ref blurVHandles[i], blurDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_SSGIBlurVTempTex"+i);
            }
            
            // blurDesc.width = (int)(downDesc.width / 2);
            // blurDesc.height = (int)(downDesc.height / 2);
            // RenderingUtils.ReAllocateIfNeeded(ref blurDx2Handle, blurDesc, FilterMode.Bilinear,
            //     TextureWrapMode.Clamp, name: "_Downscaled2RT");
            // RenderingUtils.ReAllocateIfNeeded(ref blurDx2HandleA, blurDesc, FilterMode.Bilinear,
            //     TextureWrapMode.Clamp, name: "_Downscaled2RTA");
            // blurDesc.width = (int)(downDesc.width / 4);
            // blurDesc.height = (int)(downDesc.height / 4);
            // RenderingUtils.ReAllocateIfNeeded(ref blurDx4Handle, blurDesc, FilterMode.Bilinear,
            //     TextureWrapMode.Clamp, name: "_Downscaled4RT");
            // RenderingUtils.ReAllocateIfNeeded(ref blurDx4HandleA, blurDesc, FilterMode.Bilinear,
            //     TextureWrapMode.Clamp, name: "_Downscaled4RTA");
            // blurDesc.width = (int)(downDesc.width / 8);
            // blurDesc.height = (int)(downDesc.height / 8);
            // RenderingUtils.ReAllocateIfNeeded(ref blurDx8Handle, blurDesc, FilterMode.Bilinear,
            //     TextureWrapMode.Clamp, name: "_Downscaled8RTA");
            // RenderingUtils.ReAllocateIfNeeded(ref blurDx8HandleA, blurDesc, FilterMode.Bilinear,
            //     TextureWrapMode.Clamp, name: "_Downscaled8RTB");
            // blurDesc.width = (int)(downDesc.width / 16);
            // blurDesc.height = (int)(downDesc.height / 16);
            // RenderingUtils.ReAllocateIfNeeded(ref blurDx16Handle, blurDesc, FilterMode.Bilinear,
            //     TextureWrapMode.Clamp, name: "_Downscaled16RTA");
            // RenderingUtils.ReAllocateIfNeeded(ref blurDx16HandleA, blurDesc, FilterMode.Bilinear,
            //     TextureWrapMode.Clamp, name: "_Downscaled16RTB");
            
            return true;
        }
        
        private RTHandle SSGIAllocatorFunction(string viewName, int frameIndex, RTHandleSystem rtHandleSystem)
        {   
            return rtHandleSystem.Alloc(Vector2.one * m_VolumeComponent.rayTracingResolutionScale.value, colorFormat: raytracingDesc.graphicsFormat, dimension: TextureDimension.Tex2D,
                enableRandomWrite: false, useMipMap: false, autoGenerateMips: false,
                name: string.Format("{0}_RayTracingTexture{1}", viewName, frameIndex));
        }

        public override void Render(CommandBuffer cmd, ScriptableRenderContext context,
            FRPVolumeRenderPass.PostProcessRTHandles rtHandles,
            ref RenderingData renderingData, FRPVolumeInjectionPoint injectionPoint)
        {
            var cam = renderingData.cameraData.camera;
            var cameraData = renderingData.cameraData;
            if (renderingData.cameraData.renderType != CameraRenderType.Base) return;
            
            camPos = cam.transform.position;

            int frameCount = Application.isPlaying ? Time.frameCount : 0;
            usesDenoise = m_VolumeComponent.temporalDenoise.value && Application.isPlaying && cameraData.cameraType == CameraType.Game;
           
            SSGIMaterial.SetVector(ShaderParams.IndirectData,
                new Vector4(m_VolumeComponent.indirectIntensity.value,
                    m_VolumeComponent.blurWide.value,
                    m_VolumeComponent.indirectDistanceAttenuation.value, m_VolumeComponent.rayReuseIntensity.value));
            SSGIMaterial.SetVector(ShaderParams.RayData,
                new Vector4(m_VolumeComponent.rayCount.value, m_VolumeComponent.rayMaxLength.value,
                    m_VolumeComponent.rayMaxSamples.value, m_VolumeComponent.thickness.value));

            if (m_VolumeComponent.rayBinarySearch.value)
            {
                SSGIMaterial.EnableKeyword(ShaderParams.SSGI_USES_BINARY_SEARCH);
            }
            else
            {
                SSGIMaterial.DisableKeyword(ShaderParams.SSGI_USES_BINARY_SEARCH);
            }

            if (cam.orthographic)
            {
                SSGIMaterial.EnableKeyword(ShaderParams.SSGI_ORTHO_SUPPORT);
            }
            else
            {
                SSGIMaterial.DisableKeyword(ShaderParams.SSGI_ORTHO_SUPPORT);
            }

            if (usesDenoise)
            {
                goldenRatioAcum += GOLDENRATIO * m_VolumeComponent.rayCount.value;
                goldenRatioAcum %= 5000.0f;
            }

            cmd.SetGlobalVector(ShaderParams.SourceSize,
                new Vector4(this.sourceDesc.width, this.sourceDesc.height, goldenRatioAcum, frameCount));
            cmd.SetGlobalVector(ShaderParams.ExtraData, new Vector4(m_VolumeComponent.rayJitter.value, 2, // TODO: Params
                m_VolumeComponent.normalInfluence.value, m_VolumeComponent.saturation.value));

            // pass UNITY_MATRIX_V
            cmd.SetGlobalMatrix(ShaderParams.WorldToViewDir, cam.worldToCameraMatrix);

            int currentFrame = Time.frameCount;
            // are we reusing rays?
            if (!prevs.TryGetValue(cam, out PerCameraData frameAcumData))
            {
                prevs[cam] = frameAcumData = new PerCameraData();
            }

            var bounceRT = frameAcumData.rtBounce;
            var raycastInput = rtHandles.m_Source;
            
            if (m_VolumeComponent.rayBounce.value)
            {
                if(bounceRT ==null || bounceRT.rt.width != sourceDesc.width || bounceRT.rt.height != sourceDesc.height)
                {
                    RenderingUtils.ReAllocateIfNeeded(ref bounceRT, sourceDesc, FilterMode.Bilinear,
                        name: "_bounceRT");
                    frameAcumData.rtBounce = bounceRT;
                    frameAcumData.rtBounceCreationFrame = currentFrame;
                }
                
                if (currentFrame - frameAcumData.rtBounceCreationFrame > 2)
                {
                    raycastInput = bounceRT; // only uses bounce rt a few frames after it's created
                }
            }

            // set the fallback mode
            SSGIMaterial.DisableKeyword(ShaderParams.SSGI_REUSERAYTRACING);
            SSGIMaterial.DisableKeyword(ShaderParams.SSGI_FALLBACK_PROBE);

            if (m_VolumeComponent.reuseRays.value && currentFrame - frameAcumData.rtAcumCreationFrame > 2 &&
                m_VolumeComponent.rayReuseIntensity.value > 0 && frameAcumData.rtDenoise != null)
            {
                cmd.SetGlobalTexture(ShaderParams.PrevResolve, frameAcumData.rtDenoise.nameID);
                SSGIMaterial.EnableKeyword(ShaderParams.SSGI_REUSERAYTRACING);
            }
        
            // Draw Base Color
            var downDesc = raytracingDesc;
            downDesc.depthBufferBits = 16;
            downDesc.colorFormat = RenderTextureFormat.RGB111110Float;
            downDesc.useMipMap = false;
            downDesc.width = Mathf.Min(renderingData.cameraData.cameraTargetDescriptor.width, downDesc.width / 2);
            downDesc.height = Mathf.Min(renderingData.cameraData.cameraTargetDescriptor.height, downDesc.height / 2);
            //cmd.GetTemporaryRT(ShaderParams.DownBaseColorRT, downDesc, FilterMode.Point);
            
            //CoreUtils.SetRenderTarget(cmd, ShaderParams.DownBaseColorRT, ClearFlag.All);

            // var rendererListParams = new RendererListParams(renderingData.cullResults, baseColorDrawSetting,
            //     baseColorFilteringSetting);
            // cmd.DrawRendererList(context.CreateRendererList(ref rendererListParams));

            // Draw Down Depth 
            if (!volumeData.gBuffer)
            {
                FullScreenBlit(cmd, downScaleDepthHadnle.nameID, Pass.CopyDepth);
                cmd.SetGlobalTexture(ShaderParams.DownscaledDepthRT, downScaleDepthHadnle.nameID);
            }
            else
            {
                cmd.SetGlobalTexture(ShaderParams.DownscaledDepthRT, m_frpCameraData.gBufferDepthTexture.nameID);
            }
             
            // raytracing
            FullScreenBlit(cmd, raycastInput.nameID, m_frpCameraData.ssgiRTHadnle.nameID, Pass.Raycast);
            
            if (m_VolumeComponent.debugMode.value != SSGIVolume.DebugMode.RayCasterOnly)
            {
                FullScreenBlit(cmd, m_frpCameraData.ssgiRTHadnle.nameID, blurHHandles[0].nameID, Pass.BlurHorizontal);
                FullScreenBlit(cmd, blurHHandles[0].nameID, blurVHandles[0].nameID, Pass.BlurVertical);
                for (var i = 0; i < m_VolumeComponent.blurIteration.value-1; ++i)
                {
                    FullScreenBlit(cmd, blurVHandles[i].nameID, blurHHandles[i+1].nameID, Pass.BlurHorizontal);
                    FullScreenBlit(cmd, blurHHandles[i+1].nameID, blurVHandles[i+1].nameID, Pass.BlurVertical);
                }
                
                for (var i = m_VolumeComponent.blurIteration.value-1; i > 0; --i)
                {
                    FullScreenBlit(cmd, blurVHandles[i].nameID, blurVHandles[i-1].nameID, Pass.Upscale);
                }
                FullScreenBlit(cmd, blurVHandles[0].nameID, m_frpCameraData.ssgiRTHadnle.nameID, Pass.Upscale);
            }
            
            var computedGIRT = m_frpCameraData.ssgiRTHadnle.nameID;
            var prev = frameAcumData?.rtDenoise;
            
            if (usesDenoise)
            {
                float responseSpeed = m_VolumeComponent.temporalResponseSpeed.value;
                Pass acumPass = Pass.TemporalDenoise;

                if (prev == null || (prev.rt.width != raytracingDesc.width || prev.rt.height != raytracingDesc.height))
                {
                    RenderingUtils.ReAllocateIfNeeded(ref prev, raytracingDesc, FilterMode.Bilinear, name: "_PrevResolve");
                    frameAcumData.rtDenoise = prev;
                    frameAcumData.lastCameraPosition = camPos;
                    frameAcumData.rtAcumCreationFrame = currentFrame;
                    acumPass = Pass.Copy;
                }
                
                float camTranslationDelta = Vector3.Distance(camPos, frameAcumData.lastCameraPosition);
                frameAcumData.lastCameraPosition = camPos;
                responseSpeed += camTranslationDelta * 200;
                SSGIMaterial.SetVector(ShaderParams.TemporalData,
                    new Vector4(responseSpeed, m_VolumeComponent.temporalDepthThresold.value,
                        m_VolumeComponent.temporalBlendWeight.value, 0));

                //RenderTargetIdentifier prevRT = new RenderTargetIdentifier(prev, 0, CubemapFace.Unknown, -1);
                cmd.SetGlobalTexture(ShaderParams.PrevResolve, prev.nameID);
                FullScreenBlit(cmd, computedGIRT, TempAcumHadnle.nameID, acumPass);
                FullScreenBlit(cmd, TempAcumHadnle.nameID, prev.nameID, Pass.Copy);
                computedGIRT = TempAcumHadnle.nameID;
            }
            
            //cmd.SetGlobalTexture(ShaderParams.DownBaseColorRT, ShaderParams.DownBaseColorRT);
            cmd.SetGlobalTexture(ShaderParams.InputRT, rtHandles.m_Source.nameID);
            if (m_VolumeComponent.rayBounce.value) {
                FullScreenBlit(cmd, computedGIRT, bounceRT.nameID, Pass.Compose);
                cmd.SetGlobalTexture(ShaderParams.MainTex, bounceRT.nameID);
                cmd.SetGlobalTexture(ShaderParams.SSGITexture, bounceRT.nameID);
                //FullScreenBlit(cmd, bounceRT.nameID, rtHandles.m_Source.nameID, Pass.CopyExact);
            } else {
                cmd.SetGlobalTexture(ShaderParams.MainTex, computedGIRT);
                FullScreenBlit(cmd, computedGIRT, rtHandles.m_Source.nameID, Pass.Compose);
            }

#if UNITY_EDITOR
            switch (m_VolumeComponent.debugMode.value)
            {
                case SSGIVolume.DebugMode.SSGIDIFFUSEONLY:
                    m_frpCameraData.uberMaterial.EnableKeyword(ShaderParams.SSGI_DEBUG);
                    cmd.SetGlobalTexture(ShaderParams.SSGITexture, Application.isPlaying ? prev.nameID : m_frpCameraData.ssgiRTHadnle.nameID);
                    break;
                case SSGIVolume.DebugMode.SSGIRESULT:
                    cmd.SetGlobalTexture(ShaderParams.SSGITexture, bounceRT.nameID);
                    m_frpCameraData.uberMaterial.EnableKeyword(ShaderParams.SSGI_DEBUG);
                    break; 
                case SSGIVolume.DebugMode.RayCasterOnly:
                    cmd.SetGlobalTexture(ShaderParams.SSGITexture, m_frpCameraData.ssgiRTHadnle.nameID);
                    m_frpCameraData.uberMaterial.EnableKeyword(ShaderParams.SSGI_DEBUG);
                    break;
                default:
                    m_frpCameraData.uberMaterial.DisableKeyword(ShaderParams.SSGI_DEBUG);
                    break;
            }        
#endif
           
            
            //cmd.ReleaseTemporaryRT(ShaderParams.DownBaseColorRT);
        }

        public override void ReleaseRTHandles()
        {
            
        }

        static readonly Vector4 unlimitedBounds = new Vector4(-1e8f, -1e8f, 1e8f, 1e8f);

        void FullScreenBlit(CommandBuffer cmd, RenderTargetIdentifier destination, Pass pass)
        {
            cmd.SetRenderTarget(destination, 0, CubemapFace.Unknown, -1);
            cmd.DrawMesh(fullscreenMesh, Matrix4x4.identity, SSGIMaterial, 0, (int)pass);
        }

        void FullScreenBlit(CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier destination,
            Pass pass)
        {
            cmd.SetRenderTarget(destination, 0, CubemapFace.Unknown, -1);
            cmd.SetGlobalTexture(ShaderParams.MainTex, source);
            cmd.DrawMesh(fullscreenMesh, Matrix4x4.identity, SSGIMaterial, 0, (int)pass);
        }

        public override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            downScaleDepthHadnle?.Release();
            TempAcumHadnle?.Release();
            foreach (PerCameraData fad in prevs.Values) {
                fad.rtDenoise?.Release();
                fad.rtBounce?.Release();
            }

            for(var i=0; i<blurVHandles.Length;++i)
            {
                blurHHandles[i]?.Release();
                blurVHandles[i]?.Release();
            }
        }
    }
}