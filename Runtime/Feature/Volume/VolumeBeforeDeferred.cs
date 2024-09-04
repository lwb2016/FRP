using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

namespace UnityEngine.Rendering.FRP
{
    [FRPVolume("VolumeBeforeDeferred", FRPVolumeInjectionPoint.BeforeDeferred)]
    internal class VolumeBeforeDeferred : FRPVolumeRenderer
    {

        public FRPVolumeData volumeData;
        public VolumeBeforeDeferred(ref FRPVolumeData volumeSetting)
        {
            volumeData = volumeSetting;
        }

        public override bool Setup(ScriptableRenderer renderer, ref RenderingData renderingData, ref FRPCamera frpCamera, FRPVolumeInjectionPoint injectionPoint)
        {
            base.Setup(renderer, ref renderingData, ref frpCamera, injectionPoint);
            return true;
        }
        
        public override void Render(CommandBuffer cmd, ScriptableRenderContext context,
            FRPVolumeRenderPass.PostProcessRTHandles rtHandles,
            ref RenderingData renderingData, FRPVolumeInjectionPoint injectionPoint)
        {
        }
    }
}
