using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.FRP
{
    [FRPVolume("SimpleAreaLights", FRPVolumeInjectionPoint.BeforeOpaque)]
    internal class TrickAreaLightsRender : FRPVolumeRenderer
    {
        float[] m_AdditionalLightColors;
        public static int _AdditionalSimpleAreaLightsColor = Shader.PropertyToID("_AdditionalSimpleAreaLightsColor");

        public override bool Setup(ScriptableRenderer renderer, ref RenderingData renderingData, ref FRPCamera frpCamera, FRPVolumeInjectionPoint injectionPoint)
        {
            base.Setup(renderer, ref renderingData, ref frpCamera, injectionPoint);
            isActive = false;
            return isActive;
        }
        public override void Render(CommandBuffer cmd, ScriptableRenderContext context, FRPVolumeRenderPass.PostProcessRTHandles rtHandles,
            ref RenderingData renderingData, FRPVolumeInjectionPoint injectionPoint)
        {
            int maxLights = UniversalRenderPipeline.maxVisibleAdditionalLights;
            m_AdditionalLightColors = new float[maxLights];
            
            var lights = renderingData.lightData.visibleLights;
            int maxAdditionalLightsCount = UniversalRenderPipeline.maxVisibleAdditionalLights;

            for (int i = 0, lightIter = 0; i < lights.Length && lightIter < maxAdditionalLightsCount; ++i)
            {
                if (lights[i].lightType == LightType.Point)
                {
                    var simpleAreaLight = lights[i].light.GetComponent<SimpleAreaLights>();
                    if (simpleAreaLight != null)
                    {
                        //Debug.Log(lights[i].finalColor.a);
                        m_AdditionalLightColors[lightIter] = lights[i].finalColor.a;
                    }
                }
                cmd.SetGlobalFloatArray(_AdditionalSimpleAreaLightsColor, m_AdditionalLightColors);
            }
        }
    }
}
