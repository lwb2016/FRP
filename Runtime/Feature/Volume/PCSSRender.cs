using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.FRP
{
    [FRPVolume("PCSS", FRPVolumeInjectionPoint.BeforeOpaque)] // After ShadowCaster
    public class PCSSRender : FRPVolumeRenderer
    {
        private PCSSVolume pcssVolume;
        private FRPVolumeData volumeData;
        
        public PCSSRender(ref FRPVolumeData volumeData)
        {
            this.volumeData = volumeData;
        }

        public override bool Setup(ScriptableRenderer renderer, ref RenderingData renderingData, ref FRPCamera frpCamera,
            FRPVolumeInjectionPoint injectionPoint)
        {
            base.Setup(renderer, ref renderingData, ref frpCamera, injectionPoint);
            var stack = VolumeManager.instance.stack;
            pcssVolume = stack.GetComponent<PCSSVolume>();
            if (!pcssVolume.IsActive()) return false;

            return true;
        }

        public override void Render(CommandBuffer cmd, ScriptableRenderContext context, FRPVolumeRenderPass.PostProcessRTHandles rtHandles,
            ref RenderingData renderingData, FRPVolumeInjectionPoint injectionPoint)
        {
            cmd.SetGlobalTexture(FRPShaderProperty.BLUENOISE, volumeData.m_frpData.blueNoiseTexture);
        }
    }
}