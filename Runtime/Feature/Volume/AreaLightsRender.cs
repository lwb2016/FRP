using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.FRP
{
    [FRPVolume("AreaLights", FRPVolumeInjectionPoint.BeforeOpaque)]
    internal class AreaLightsRender : FRPVolumeRenderer
    {
        public FRPVolumeData volumeSetting;
        
        public static int AreaLightDataID = Shader.PropertyToID("_AreaLightData"); // 1 is AreaLight
        public static string AdditionalLightsColorID = "_AdditionalLightsColor"; 
        public static string AreaLightVertsID = "_AreaLightVerts";
        public static string AreaLightColorID = "_AreaLightColor"; 
        public static string AreaLightForwardID = "_AreaLightsForward";
        private static readonly int TransformInvDiffuse = Shader.PropertyToID("_TransformInv_Diffuse");
        private static readonly int TransformInvSpecular = Shader.PropertyToID("_TransformInv_Specular");
        private static readonly int AmpDiffAmpSpecFresnel = Shader.PropertyToID("_AmpDiffAmpSpecFresnel");
        static Texture2D s_TransformInvTexture_Specular;
        static Texture2D s_TransformInvTexture_Diffuse;
        static Texture2D s_AmpDiffAmpSpecFresnel;
        
        float[] AreaLightData;
        Vector4[] AdditionalLightsColorArray;
        Vector4[] AreaLightsColorArray;
        Vector4[] AreaLightsForward;
        Matrix4x4[] AreaLightsVertsArray;

        public AreaLightsRender(ref FRPVolumeData volumeSetting)
        {
            this.volumeSetting = volumeSetting;
        }

        public override bool Setup(ScriptableRenderer renderer, ref RenderingData renderingData, ref FRPCamera frpCamera, FRPVolumeInjectionPoint injectionPoint)
        {
            if (renderingData.cameraData.camera.cameraType == CameraType.Reflection)
            {
                return false;
            }
            if (s_TransformInvTexture_Diffuse == null)
                s_TransformInvTexture_Diffuse = AreaLightLUT.LoadLUT(AreaLightLUT.LUTType.TransformInv_DisneyDiffuse);
            if (s_TransformInvTexture_Specular == null)
                s_TransformInvTexture_Specular = AreaLightLUT.LoadLUT(AreaLightLUT.LUTType.TransformInv_GGX);
            if (s_AmpDiffAmpSpecFresnel == null)
                s_AmpDiffAmpSpecFresnel = AreaLightLUT.LoadLUT(AreaLightLUT.LUTType.AmpDiffAmpSpecFresnel);
    
            Shader.SetGlobalTexture(TransformInvDiffuse, s_TransformInvTexture_Diffuse);
            Shader.SetGlobalTexture(TransformInvSpecular, s_TransformInvTexture_Specular);
            Shader.SetGlobalTexture(AmpDiffAmpSpecFresnel, s_AmpDiffAmpSpecFresnel);


            return true;
        }
        
        public override void Render(CommandBuffer cmd, ScriptableRenderContext context,
            FRPVolumeRenderPass.PostProcessRTHandles rtHandles,
            ref RenderingData renderingData, FRPVolumeInjectionPoint injectionPoint)
        {
            int maxLights = UniversalRenderPipeline.maxVisibleAdditionalLights;
            var lights = renderingData.lightData.visibleLights;
            int maxAdditionalLightsCount = UniversalRenderPipeline.maxVisibleAdditionalLights;
            
            // clear All
            if (renderingData.cameraData.camera.cameraType == CameraType.Reflection)
            {
                cmd.SetGlobalFloatArray(AreaLightDataID, AreaLightData);
                cmd.SetGlobalMatrixArray(AreaLightVertsID, AreaLightsVertsArray);
                cmd.SetGlobalVectorArray(AreaLightColorID, AreaLightsColorArray);
                cmd.SetGlobalVectorArray(AreaLightForwardID, AreaLightsForward);
                return;
            }
            
            if (AreaLightData== null || AreaLightData.Length != UniversalRenderPipeline.maxVisibleAdditionalLights)
            {
                AreaLightData = new float[UniversalRenderPipeline.maxVisibleAdditionalLights];
                AdditionalLightsColorArray = new Vector4[UniversalRenderPipeline.maxVisibleAdditionalLights];
                AreaLightsColorArray = new Vector4[UniversalRenderPipeline.maxVisibleAdditionalLights];
                AreaLightsForward = new Vector4[UniversalRenderPipeline.maxVisibleAdditionalLights];
                AreaLightsVertsArray = new Matrix4x4[UniversalRenderPipeline.maxVisibleAdditionalLights];
                cmd.SetGlobalFloatArray(AreaLightDataID, AreaLightData);
                cmd.SetGlobalMatrixArray(AreaLightVertsID, AreaLightsVertsArray);
                cmd.SetGlobalVectorArray(AreaLightColorID, AreaLightsColorArray);
                cmd.SetGlobalVectorArray(AreaLightForwardID, AreaLightsForward);
            }
           
            for (int i = 0, lightIter = 0; i < lights.Length && i < UniversalRenderPipeline.maxVisibleAdditionalLights && lightIter < maxAdditionalLightsCount; ++i)
            {
                if (renderingData.lightData.mainLightIndex != i)
                {
                    AdditionalLightsColorArray[i] = lights[i].finalColor;
                    if (lights[i].lightType == LightType.Spot)
                    {
                        var simpleAreaLight = lights[i].light.GetComponent<AreaLight>();
                        if (simpleAreaLight != null)
                        {
                            AreaLightData[lightIter] = 1;
                            AdditionalLightsColorArray[lightIter] = Color.black;
                            AreaLightsColorArray[lightIter] = simpleAreaLight.GetColor();
                            AreaLightsVertsArray[lightIter] = simpleAreaLight.lightVerts;
                            AreaLightsForward[lightIter] = simpleAreaLight.transform.forward;
                            //Debug.Log(m_AreaLightAlpha[lightIter] + " " + lights[lightIter].light.name);
                            lightIter++;
                        }
                    }
                }
            }
            
            cmd.SetGlobalFloatArray(AreaLightDataID, AreaLightData);
            cmd.SetGlobalMatrixArray(AreaLightVertsID, AreaLightsVertsArray);
            cmd.SetGlobalVectorArray(AreaLightColorID, AreaLightsColorArray);
            cmd.SetGlobalVectorArray(AreaLightForwardID, AreaLightsForward);
            cmd.SetGlobalVectorArray(AdditionalLightsColorID, AdditionalLightsColorArray);
        }
    }
}
