using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.FRP
{
    [FRPVolume("BlackFadeEffect", FRPVolumeInjectionPoint.BeforePostProcess)]
    public class BlackFadeEffectRender : FRPVolumeRenderer
    {
        public static readonly int _BlackFade_Params = Shader.PropertyToID("_BlackFade_Params");
        public static readonly int _BlackFade_CenterPos = Shader.PropertyToID("_BlackFade_CenterPos");
        public static readonly int _BlackFade_Shape = Shader.PropertyToID("_BlackFade_Shape");
        public static readonly int _BlackFade_Color = Shader.PropertyToID("_BlackFade_Color");
        public const string BlackFade = "_BLACKFADE";

        private Material m_Material;
        private FRPVolumeData m_VolumeData;
        private BlackFadeEffectVolume m_VolumeComponent;

        public BlackFadeEffectRender(ref FRPVolumeData volumeData)
        {
            m_VolumeData = volumeData;
        }
        
        public override bool Setup(ScriptableRenderer renderer, ref RenderingData renderingData,
            ref FRPCamera frpCamera, FRPVolumeInjectionPoint injectionPoint)
        {
            base.Setup(renderer, ref renderingData, ref frpCamera, injectionPoint);
            var stack = VolumeManager.instance.stack;
            m_VolumeComponent = stack.GetComponent<BlackFadeEffectVolume>();
            if (!m_VolumeComponent.IsActive()) return false;
            
            var frpCameraData = FRPCamreaData.GetOrCreate(renderingData.cameraData.camera, renderingData.cameraData);

            m_Material = frpCameraData.uberMaterial;
            
            input = ScriptableRenderPassInput.Depth;

            return true;
        }

        public override void Render(CommandBuffer cmd, ScriptableRenderContext context, FRPVolumeRenderPass.PostProcessRTHandles rtHandles,
            ref RenderingData renderingData, FRPVolumeInjectionPoint injectionPoint)
        {
            var color = m_VolumeComponent.color.value;
            var center = m_VolumeComponent.centerPos.value;
            var shape = m_VolumeComponent.shape.value;
            var intensity = m_VolumeComponent.intensity.value;
            var radius = m_VolumeComponent.radius.value;
            var soft = m_VolumeComponent.soft.value;
            
            Vector4 blackFade_Param = new Vector4(intensity, radius, soft, 0);
            CoreUtils.SetKeyword(m_Material, BlackFade, intensity > 0);
            m_Material.SetColor(_BlackFade_Color,color);
            m_Material.SetVector(_BlackFade_CenterPos,center);
            m_Material.SetVector(_BlackFade_Shape,shape);
            m_Material.SetVector(_BlackFade_Params,blackFade_Param);
            
            Vector2 viewportScale = rtHandles.m_Source.useScaling
                ? new Vector2(rtHandles.m_Source.rtHandleProperties.rtHandleScale.x,
                    rtHandles.m_Source.rtHandleProperties.rtHandleScale.y)
                : Vector2.one;
            
            Blitter.BlitTexture(cmd, rtHandles.m_Source, viewportScale, m_Material, 1);
        }

    }
}