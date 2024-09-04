using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.FRP
{
    [FRPVolume("HBAOVolume", FRPVolumeInjectionPoint.BeforeOpaque)]
    internal class HBAORenderer : FRPVolumeRenderer
    {
        private static class ShaderProperties
        {
            public static int mainTex;
            public static int hbaoTex;
            public static int noiseTex;
            public static int[] depthSliceTex;
            public static int[] normalsSliceTex;
            public static int[] aoSliceTex;
            public static int inputTexelSize;
            public static int aoTexelSize;

            public static int uvToView;

            //public static int worldToCameraMatrix;
            public static int targetScale;
            public static int radius;
            public static int maxRadiusPixels;
            public static int negInvRadius2;
            public static int angleBias;
            public static int aoMultiplier;
            public static int intensity;
            public static int offscreenSamplesContrib;
            public static int maxDistance;
            public static int distanceFalloff;
            public static int blurDeltaUV;
            public static int blurSharpness;
            public static int temporalParams;
            public static int historyBufferRTHandleScale;
            public const string hbaoKeyword = "_HBAO";

            static ShaderProperties()
            {
                mainTex = Shader.PropertyToID("_MainTex");
                hbaoTex = Shader.PropertyToID("_HBAOTex");
                noiseTex = Shader.PropertyToID("_NoiseTex");
                depthSliceTex = new int[4 * 4];
                normalsSliceTex = new int[4 * 4];
                aoSliceTex = new int[4 * 4];
                for (int i = 0; i < 4 * 4; i++)
                {
                    depthSliceTex[i] = Shader.PropertyToID("_DepthSliceTex" + i);
                    normalsSliceTex[i] = Shader.PropertyToID("_NormalsSliceTex" + i);
                    aoSliceTex[i] = Shader.PropertyToID("_AOSliceTex" + i);
                }

                inputTexelSize = Shader.PropertyToID("_Input_TexelSize");
                aoTexelSize = Shader.PropertyToID("_AO_TexelSize");
                uvToView = Shader.PropertyToID("_UVToView");
                targetScale = Shader.PropertyToID("_TargetScale");
                radius = Shader.PropertyToID("_Radius");
                maxRadiusPixels = Shader.PropertyToID("_MaxRadiusPixels");
                negInvRadius2 = Shader.PropertyToID("_NegInvRadius2");
                angleBias = Shader.PropertyToID("_AngleBias");
                aoMultiplier = Shader.PropertyToID("_AOmultiplier");
                intensity = Shader.PropertyToID("_HBAOIntensity");
                offscreenSamplesContrib = Shader.PropertyToID("_OffscreenSamplesContrib");
                maxDistance = Shader.PropertyToID("_MaxDistance");
                distanceFalloff = Shader.PropertyToID("_DistanceFalloff");
                blurDeltaUV = Shader.PropertyToID("_BlurDeltaUV");
                blurSharpness = Shader.PropertyToID("_BlurSharpness");
                temporalParams = Shader.PropertyToID("_TemporalParams");
            }

            public static string GetOrthographicProjectionKeyword(bool orthographic)
            {
                return orthographic ? "ORTHOGRAPHIC_PROJECTION" : "__";
            }

            public static string GetQualityKeyword(HBAOVolume.Quality quality)
            {
                switch (quality)
                {
                    case HBAOVolume.Quality.Lowest:
                        return "QUALITY_LOWEST";
                    case HBAOVolume.Quality.Low:
                        return "QUALITY_LOW";
                    case HBAOVolume.Quality.Medium:
                        return "QUALITY_MEDIUM";
                    case HBAOVolume.Quality.High:
                        return "QUALITY_HIGH";
                    case HBAOVolume.Quality.Highest:
                        return "QUALITY_HIGHEST";
                    default:
                        return "QUALITY_MEDIUM";
                }
            }

            public static string GetNoiseKeyword(HBAOVolume.NoiseType noiseType)
            {
                switch (noiseType)
                {
                    case HBAOVolume.NoiseType.InterleavedGradientNoise:
                        return "INTERLEAVED_GRADIENT_NOISE";
                    case HBAOVolume.NoiseType.Dither:
                    case HBAOVolume.NoiseType.SpatialDistribution:
                    default:
                        return "__";
                }
            }

            public static string GetBlurRadiusKeyword(HBAOVolume.BlurType blurType)
            {
                switch (blurType)
                {
                    case HBAOVolume.BlurType.Narrow:
                        return "BLUR_RADIUS_2";
                    case HBAOVolume.BlurType.Medium:
                        return "BLUR_RADIUS_3";
                    case HBAOVolume.BlurType.Wide:
                        return "BLUR_RADIUS_4";
                    case HBAOVolume.BlurType.ExtraWide:
                        return "BLUR_RADIUS_5";
                    case HBAOVolume.BlurType.None:
                    default:
                        return "BLUR_RADIUS_3";
                }
            }

        }

        private static class MersenneTwister
        {
            // Mersenne-Twister random numbers in [0,1).
            public static float[] Numbers = new float[]
            {
                //0.463937f,0.340042f,0.223035f,0.468465f,0.322224f,0.979269f,0.031798f,0.973392f,0.778313f,0.456168f,0.258593f,0.330083f,0.387332f,0.380117f,0.179842f,0.910755f,
                //0.511623f,0.092933f,0.180794f,0.620153f,0.101348f,0.556342f,0.642479f,0.442008f,0.215115f,0.475218f,0.157357f,0.568868f,0.501241f,0.629229f,0.699218f,0.707733f
                0.556725f, 0.005520f, 0.708315f, 0.583199f, 0.236644f, 0.992380f, 0.981091f, 0.119804f, 0.510866f,
                0.560499f, 0.961497f, 0.557862f, 0.539955f, 0.332871f, 0.417807f, 0.920779f,
                0.730747f, 0.076690f, 0.008562f, 0.660104f, 0.428921f, 0.511342f, 0.587871f, 0.906406f, 0.437980f,
                0.620309f, 0.062196f, 0.119485f, 0.235646f, 0.795892f, 0.044437f, 0.617311f
            };
        }

        private enum HistoryBufferType
        {
            AmbientOcclusion,
        }

        private FRPVolumeData volumeSetting;
        private HBAOVolume m_VolumeComponent;

        private static readonly Vector2[] s_jitter = new Vector2[4 * 4];
        private static readonly float[] s_temporalRotations = { 60.0f, 300.0f, 180.0f, 240.0f, 120.0f, 0.0f };
        private static readonly float[] s_temporalOffsets = { 0.0f, 0.5f, 0.25f, 0.75f };

        private Material m_Material;
        private Material m_UberMaterial;
        private CameraData cameraData { get; set; }

        private RTHandle tempHandle;
        private RTHandle inputHandle;
        private RTHandle currentFrameRT;
        private RTHandle cameraColorTarget;

        private RenderTextureDescriptor sourceDesc;
        private RenderTextureDescriptor aoDesc;
        private RenderTextureDescriptor ssaoDesc;

        private bool motionVectorsSupported { get; set; }
        private Texture2D noiseTex { get; set; }

        private static bool isLinearColorSpace
        {
            get { return QualitySettings.activeColorSpace == ColorSpace.Linear; }
        }

        private bool renderingInSceneView
        {
            get { return cameraData.camera.cameraType == CameraType.SceneView; }
        }

        private int? m_PreviousResolution;
        private HBAOVolume.NoiseType? m_PreviousNoiseType;
        private XRGraphics.StereoRenderingMode m_PrevStereoRenderingMode;
        private string[] m_ShaderKeywords;
        private string lastUberKeyword;
        private Vector4[] m_UVToViewPerEye = new Vector4[2];
        private float[] m_RadiusPerEye = new float[2];

        private bool m_SupportsR8RenderTextureFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8);


        public override bool visibleInSceneView => true;

        public HBAORenderer(ref FRPVolumeData volumeSetting)
        {
            this.volumeSetting = volumeSetting;
        }

        public override void Initialize()
        {
            m_Material = CoreUtils.CreateEngineMaterial(volumeSetting.m_frpData.hbaoShader);
        }

        public override bool Setup(ScriptableRenderer renderer, ref RenderingData renderingData, ref FRPCamera frpCamera, FRPVolumeInjectionPoint injectionPoint)
        {
            base.Setup(renderer, ref renderingData, ref frpCamera, injectionPoint);
            var frpCameraData = FRPCamreaData.GetOrCreate(renderingData.cameraData.camera, renderingData.cameraData);
            m_UberMaterial = frpCameraData.uberMaterial;
            var stack = VolumeManager.instance.stack;
            m_VolumeComponent = stack.GetComponent<HBAOVolume>();
            if (!m_VolumeComponent.IsActive())
            {
                Shader.DisableKeyword(ShaderKeywordStrings.ScreenSpaceOcclusion);
                frpCameraData.uberMaterial.DisableKeyword(ShaderProperties.hbaoKeyword);
                isActive = false;
                return false;
            }

            if (renderingData.cameraData.cameraType == CameraType.Preview)
            {
                Shader.DisableKeyword(ShaderKeywordStrings.ScreenSpaceOcclusion);
            }
            
            if (m_VolumeComponent.mode.value == HBAOVolume.Mode.Normal &&
                injectionPoint == FRPVolumeInjectionPoint.AfterOpaqueAndSky)
            {
                return false;
            }


            if (m_VolumeComponent.mode.value == HBAOVolume.Mode.AfterOpaque &&
                injectionPoint == FRPVolumeInjectionPoint.BeforeOpaque)
            {
                return false;
            }

            if (m_VolumeComponent.mode.value == HBAOVolume.Mode.Normal)
            {
                frpCamera.normalQuality = NormalQuality.Camera;
            }
            
            isActive = true;

            input = ScriptableRenderPassInput.None;
            if (!volumeSetting.gBuffer)
            {
                input = ScriptableRenderPassInput.Depth;
                if (frpCamera.normalQuality == NormalQuality.Camera)
                {
                    input |= ScriptableRenderPassInput.Normal;
                }
            }

            if (m_Material == null) m_Material = CoreUtils.CreateEngineMaterial(volumeSetting.m_frpData.hbaoShader);

            cameraData = renderingData.cameraData;

            motionVectorsSupported = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGHalf);
            
            sourceDesc = renderingData.cameraData.cameraTargetDescriptor;
            aoDesc = sourceDesc;
            aoDesc.msaaSamples = 1;
            aoDesc.depthBufferBits = 0;
            aoDesc.depthBufferBits = 0;
            aoDesc.msaaSamples = 1;
            aoDesc.colorFormat = RenderTextureFormat.ARGB32;
            aoDesc.width = sourceDesc.width >>  m_VolumeComponent.downSample.value;
            aoDesc.height = sourceDesc.height >>  m_VolumeComponent.downSample.value;
            
            RenderingUtils.ReAllocateIfNeeded(ref m_frpCameraData.hbaoRTHadnle, aoDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_HBAOTexture");
            
            if (m_VolumeComponent.blurType.value != HBAOVolume.BlurType.None)
            {
                RenderingUtils.ReAllocateIfNeeded(ref tempHandle, aoDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_HBAOBlurTempTexture");
            }
            
            if (m_VolumeComponent.mode.value == HBAOVolume.Mode.Normal)
            {
                ssaoDesc = aoDesc;
                ssaoDesc.colorFormat = m_SupportsR8RenderTextureFormat ? RenderTextureFormat.R8 : RenderTextureFormat.RGB565;
                RenderingUtils.ReAllocateIfNeeded(ref m_frpCameraData.ssaoRTHadnle, ssaoDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_ScreenSpaceOcclusionTexture");
            }
            else
            {
                RenderingUtils.ReAllocateIfNeeded(ref inputHandle, sourceDesc, FilterMode.Point, TextureWrapMode.Clamp, name: "_InputTex");
            }
            
            CheckParameters();
            UpdateMaterialProperties();
            UpdateShaderKeywords();

            return true;
        }
        
        private RTHandle ssaoAllocatorFunction(string viewName, int frameIndex, RTHandleSystem rtHandleSystem)
        {
            Vector2 scaleRT = Vector2.one;
            scaleRT.x *= 1.0f / (m_VolumeComponent.downSample.value + 1);
            scaleRT.y *= 1.0f / (m_VolumeComponent.downSample.value + 1);
            return rtHandleSystem.Alloc(scaleRT, colorFormat: ssaoDesc.graphicsFormat, dimension: TextureDimension.Tex2D,
                enableRandomWrite: false, useMipMap: false, autoGenerateMips: false,
                name: string.Format("{0}_ScreenSpaceOcclusionTexture{1}", viewName, frameIndex));
        }
        
        private RTHandle hbaoAllocatorFunction(string viewName, int frameIndex, RTHandleSystem rtHandleSystem)
        {
            Vector2 scaleRT = Vector2.one;
            scaleRT.x *= 1.0f / (m_VolumeComponent.downSample.value + 1);
            scaleRT.y *= 1.0f / (m_VolumeComponent.downSample.value + 1);
            return rtHandleSystem.Alloc(scaleRT, colorFormat: aoDesc.graphicsFormat, dimension: TextureDimension.Tex2D,
                enableRandomWrite: false, useMipMap: false, autoGenerateMips: false,
                name: string.Format("{0}_HBAOTexture{1}", viewName, frameIndex));
        }

        public override void Render(CommandBuffer cmd, ScriptableRenderContext context,
            FRPVolumeRenderPass.PostProcessRTHandles rtHandles, ref RenderingData renderingData,
            FRPVolumeInjectionPoint injectionPoint)
        {
            if (m_Material == null)
            {
                Debug.LogError("HBAO material has not been correctly initialized...");
                return;
            }
            
            if(renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.cameraType == CameraType.Reflection) return;

            cameraColorTarget = rtHandles.m_Source;
            
            CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.ScreenSpaceOcclusion, m_VolumeComponent.mode.value == HBAOVolume.Mode.Normal);

            // AO
            AO(cmd);
 
            // Blur
            Blur(cmd);

            // Composite
            if (m_VolumeComponent.mode == HBAOVolume.Mode.Normal)
            {
                Composite(rtHandles.m_Source, cmd);
            }
        }

        private void AO(CommandBuffer cmd)
        {
            Vector2 viewportScale = m_frpCameraData.hbaoRTHadnle.useScaling
                ? new Vector2(m_frpCameraData.hbaoRTHadnle.rtHandleProperties.rtHandleScale.x,
                    m_frpCameraData.hbaoRTHadnle.rtHandleProperties.rtHandleScale.y)
                : Vector2.one;
            
            //if No Blur, Mixed directly into opaque
            bool isMixOpaque = m_VolumeComponent.mode == HBAOVolume.Mode.AfterOpaque &&
                               m_VolumeComponent.blurType == HBAOVolume.BlurType.None;
            
            if (isMixOpaque)
            {                
                Blitter.BlitTexture(cmd, m_frpCameraData.hbaoRTHadnle, viewportScale, m_Material, 1);
            }
            else
            {
                CoreUtils.SetRenderTarget(cmd, m_frpCameraData.hbaoRTHadnle, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, ClearFlag.All, Color.blue);
                Blitter.BlitTexture(cmd, m_frpCameraData.hbaoRTHadnle, viewportScale, m_Material, 0);
            }
        }

        private void Blur(CommandBuffer cmd)
        {
            if (m_VolumeComponent.blurType.value != HBAOVolume.BlurType.None)
            {
                cmd.SetGlobalVector(ShaderProperties.blurDeltaUV, new Vector2(1f / sourceDesc.width, 0));
                Blitter.BlitCameraTexture(cmd, m_frpCameraData.hbaoRTHadnle, tempHandle, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, m_Material, 2);
                cmd.SetGlobalVector(ShaderProperties.blurDeltaUV, new Vector2(0, 1f / sourceDesc.height));
                if (m_VolumeComponent.mode == HBAOVolume.Mode.AfterOpaque)
                {
                    Blitter.BlitCameraTexture(cmd, tempHandle, cameraColorTarget, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, m_Material, 3);
                }
                else
                {
                    Blitter.BlitCameraTexture(cmd, tempHandle, m_frpCameraData.hbaoRTHadnle, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, m_Material, 2);
                }
            }
        }

        private void Composite(RTHandle source, CommandBuffer cmd)
        {
            if (m_VolumeComponent.mode.value != HBAOVolume.Mode.Normal) return;
           
            Vector2 viewportScale = source.useScaling
                ? new Vector2(source.rtHandleProperties.rtHandleScale.x,
                    source.rtHandleProperties.rtHandleScale.y)
                : Vector2.one;
            cmd.SetGlobalTexture(ShaderProperties.mainTex, source);
            cmd.SetGlobalTexture(ShaderProperties.hbaoTex, m_frpCameraData.hbaoRTHadnle.nameID);
            Blitter.BlitCameraTexture(cmd, source, m_frpCameraData.ssaoRTHadnle, RenderBufferLoadAction.Load,
                RenderBufferStoreAction.Store, m_Material, 4);
            
            if (m_VolumeComponent.mode.value == HBAOVolume.Mode.Normal)
            {
                cmd.SetGlobalTexture("_ScreenSpaceOcclusionTexture", m_frpCameraData.ssaoRTHadnle);
                // TODO: Important, currently URPs that don't have SSAO Feature mounted will have the SSAO keyword stripped out
                cmd.SetGlobalVector("_AmbientOcclusionParam", new Vector4(0f, 0f, 0f, m_VolumeComponent.directLightingStrength.value));
            }
        }

        private void CheckParameters()
        {
            if (noiseTex == null || m_PreviousNoiseType != m_VolumeComponent.noiseType.value)
            {
                CoreUtils.Destroy(noiseTex);

                CreateNoiseTexture();

                m_PreviousNoiseType = m_VolumeComponent.noiseType.value;
            }
        }

        private void UpdateShaderKeywords()
        {
            if (m_ShaderKeywords == null || m_ShaderKeywords.Length != 4) m_ShaderKeywords = new string[4];

            m_ShaderKeywords[0] = ShaderProperties.GetOrthographicProjectionKeyword(cameraData.camera.orthographic);
            m_ShaderKeywords[1] = ShaderProperties.GetQualityKeyword(m_VolumeComponent.quality.value);
            m_ShaderKeywords[2] = ShaderProperties.GetNoiseKeyword(m_VolumeComponent.noiseType.value);
            m_ShaderKeywords[3] = ShaderProperties.GetBlurRadiusKeyword(m_VolumeComponent.blurType.value);
            m_Material.shaderKeywords = m_ShaderKeywords;

            m_UberMaterial.DisableKeyword(lastUberKeyword);
            m_UberMaterial.EnableKeyword(lastUberKeyword);
        }

        private void UpdateMaterialProperties()
        {
            var sourceWidth = cameraData.cameraTargetDescriptor.width;
            var sourceHeight = cameraData.cameraTargetDescriptor.height;

            int eyeCount = XRGraphics.enabled &&
                           XRGraphics.stereoRenderingMode == XRGraphics.StereoRenderingMode.SinglePassInstanced &&
                           !renderingInSceneView
                ? 2
                : 1;


            for (int viewIndex = 0; viewIndex < eyeCount; viewIndex++)
            {
                var projMatrix = cameraData.GetProjectionMatrix(viewIndex);
                float invTanHalfFOVxAR = projMatrix.m00; // m00 => 1.0f / (tanHalfFOV * aspectRatio)
                float invTanHalfFOV = projMatrix.m11; // m11 => 1.0f / tanHalfFOV
                m_UVToViewPerEye[viewIndex] = new Vector4(2.0f / invTanHalfFOVxAR, -2.0f / invTanHalfFOV,
                    -1.0f / invTanHalfFOVxAR, 1.0f / invTanHalfFOV);
                m_RadiusPerEye[viewIndex] = m_VolumeComponent.radius.value * 0.5f * (sourceHeight / (2.0f / invTanHalfFOV));
            }

            //float tanHalfFovY = Mathf.Tan(0.5f * cameraData.camera.fieldOfView * Mathf.Deg2Rad);
            //float invFocalLenX = 1.0f / (1.0f / tanHalfFovY * (sourceHeight / (float)sourceWidth));
            //float invFocalLenY = 1.0f / (1.0f / tanHalfFovY);
            float maxRadInPixels = Mathf.Max(16,
                m_VolumeComponent.maxRadiusPixels.value * Mathf.Sqrt(sourceWidth * sourceHeight / (1080.0f * 1920.0f)));

            var targetScale = m_VolumeComponent.downSample.value > 0 ? 
                new Vector4((sourceWidth + 0.5f) / sourceWidth, (sourceHeight + 0.5f) / sourceHeight, 1f, 1f) : Vector4.one;

            m_Material.SetTexture(ShaderProperties.noiseTex, noiseTex);
            m_Material.SetVector(ShaderProperties.inputTexelSize,
                new Vector4(1f / sourceWidth, 1f / sourceHeight, sourceWidth, sourceHeight));
            m_Material.SetVector(ShaderProperties.aoTexelSize,
                new Vector4(1f / aoDesc.width, 1f / aoDesc.height, aoDesc.width, aoDesc.height));
            m_Material.SetVector(ShaderProperties.targetScale, targetScale);
            m_UberMaterial.SetVector(ShaderProperties.targetScale, targetScale);
            //m_Material.SetVector(ShaderProperties.uvToView, new Vector4(2.0f * invFocalLenX, -2.0f * invFocalLenY, -1.0f * invFocalLenX, 1.0f * invFocalLenY));
            m_Material.SetVectorArray(ShaderProperties.uvToView, m_UVToViewPerEye);
            //m_Material.SetMatrix(ShaderProperties.worldToCameraMatrix, cameraData.camera.worldToCameraMatrix);
            //m_Material.SetFloat(ShaderProperties.radius, m_VolumeComponent.radius.value * 0.5f * ((sourceHeight / (m_VolumeComponent.deinterleaving.value == HBAOVolume.Deinterleaving.x4 ? 4 : 1)) / (tanHalfFovY * 2.0f)));
            //m_Material.SetFloat(ShaderProperties.radius, m_VolumeComponent.radius.value * 0.5f * ((sourceHeight / (m_VolumeComponent.deinterleaving.value == HBAOVolume.Deinterleaving.x4 ? 4 : 1)) / (invFocalLenY * 2.0f)));
            m_Material.SetFloatArray(ShaderProperties.radius, m_RadiusPerEye);
            m_Material.SetFloat(ShaderProperties.maxRadiusPixels, maxRadInPixels);
            m_Material.SetFloat(ShaderProperties.negInvRadius2,
                -1.0f / (m_VolumeComponent.radius.value * m_VolumeComponent.radius.value));
            m_Material.SetFloat(ShaderProperties.angleBias, m_VolumeComponent.bias.value);
            m_Material.SetFloat(ShaderProperties.aoMultiplier, 2.0f * (1.0f / (1.0f - m_VolumeComponent.bias.value)));
            
            m_Material.SetFloat(ShaderProperties.offscreenSamplesContrib,
                m_VolumeComponent.offscreenSamplesContribution.value);
            m_Material.SetFloat(ShaderProperties.maxDistance, m_VolumeComponent.maxDistance.value);
            m_Material.SetFloat(ShaderProperties.distanceFalloff, m_VolumeComponent.distanceFalloff.value);
            m_Material.SetFloat(ShaderProperties.blurSharpness, m_VolumeComponent.sharpness.value);
            m_Material.SetFloat(ShaderProperties.intensity,
                isLinearColorSpace
                    ? m_VolumeComponent.intensity.value
                    : m_VolumeComponent.intensity.value * 0.454545454545455f);
        }

        private Vector2 AdjustBrightnessMaskToGammaSpace(Vector2 v)
        {
            return isLinearColorSpace ? v : ToGammaSpace(v);
        }

        private float ToGammaSpace(float v)
        {
            return Mathf.Pow(v, 0.454545454545455f);
        }

        private Vector2 ToGammaSpace(Vector2 v)
        {
            return new Vector2(ToGammaSpace(v.x), ToGammaSpace(v.y));
        }

        private void CreateNoiseTexture()
        {
            noiseTex = new Texture2D(4, 4,
                SystemInfo.SupportsTextureFormat(TextureFormat.RGHalf) ? TextureFormat.RGHalf : TextureFormat.RGB24,
                false, true);
            noiseTex.filterMode = FilterMode.Point;
            noiseTex.wrapMode = TextureWrapMode.Repeat;
            int z = 0;
            for (int x = 0; x < 4; ++x)
            {
                for (int y = 0; y < 4; ++y)
                {
                    float r1 = m_VolumeComponent.noiseType.value != HBAOVolume.NoiseType.Dither
                        ? 0.25f * (0.0625f * ((x + y & 3) << 2) + (x & 3))
                        : MersenneTwister.Numbers[z++];
                    float r2 = m_VolumeComponent.noiseType.value != HBAOVolume.NoiseType.Dither
                        ? 0.25f * ((y - x) & 3)
                        : MersenneTwister.Numbers[z++];
                    Color color = new Color(r1, r2, 0);
                    noiseTex.SetPixel(x, y, color);
                }
            }

            noiseTex.Apply();

            for (int i = 0, j = 0; i < s_jitter.Length; ++i)
            {
                float r1 = MersenneTwister.Numbers[j++];
                float r2 = MersenneTwister.Numbers[j++];
                s_jitter[i] = new Vector2(r1, r2);
            }
        }

        public override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            inputHandle?.Release();
            tempHandle?.Release();
        }
    }
}