using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace UnityEngine.Rendering.FRP
{
    [ExecuteInEditMode, VolumeComponentMenu("FRP/Lighting/SSGI")]
    public class SSGIVolume : VolumeComponent, IPostProcessComponent
    {
        [GeneralSettings, Space(6)]
        public BoolParameter isEnable = new BoolParameter(false);
        [GeneralSettings]
        public ClampedFloatParameter indirectIntensity = new ClampedFloatParameter(1, 0,8);
        [GeneralSettings]
        public ClampedFloatParameter indirectDistanceAttenuation = new ClampedFloatParameter(0, 0, 1);
        [GeneralSettings]
        public ClampedFloatParameter normalInfluence = new ClampedFloatParameter(1f, 0, 1);
        [GeneralSettings]
        public ClampedFloatParameter saturation = new ClampedFloatParameter(1, 0, 2);
        [GeneralSettings]
        public DebugModeParameter debugMode = new DebugModeParameter(DebugMode.Disabled); 

        [RaySetting, Space(6)]
        public BoolParameter rayBounce = new BoolParameter(false);
        [RaySetting]
        public ClampedIntParameter rayCount = new ClampedIntParameter(1, 1, 4);
        [RaySetting]
        public FloatParameter rayMaxLength = new FloatParameter(8);
        [RaySetting]
        public IntParameter rayMaxSamples = new IntParameter(32);
        [RaySetting]
        public FloatParameter rayJitter = new FloatParameter(0);
        [RaySetting]
        public FloatParameter thickness = new FloatParameter(1f);
        [RaySetting]
        public BoolParameter rayBinarySearch = new BoolParameter(true);
        [RaySetting]
        public BoolParameter reuseRays = new BoolParameter(false);
        [RaySetting]
        public ClampedFloatParameter rayReuseIntensity = new ClampedFloatParameter(0, 0, 1);

        [PerformanceSetting, Space(6)]
        public ClampedFloatParameter rayTracingResolutionScale = new ClampedFloatParameter(1, 0.1f, 1);
        [PerformanceSetting]
        public ClampedFloatParameter baseColorResolutionScale = new ClampedFloatParameter(1, 0.1f, 1);
        [PerformanceSetting]
        public ClampedFloatParameter depthResolutionScale = new ClampedFloatParameter(1, 0.1f, 1);
        [PerformanceSetting]
        public ClampedIntParameter blurIteration  = new ClampedIntParameter(4, 1, 5);
        [PerformanceSetting]
        public ClampedFloatParameter blurWide  = new ClampedFloatParameter(2, 0, 2);
       // public BlurModeParameter blurMode = new BlurModeParameter(BlurMode.Low);
        
        [DenoiseSetting, Space(6)]
        public BoolParameter temporalDenoise = new BoolParameter(true);
        [DenoiseSetting]
        public FloatParameter temporalResponseSpeed = new FloatParameter(12);
        [FormerlySerializedAs("temporalDepthRejection")] [DenoiseSetting]
        public FloatParameter temporalDepthThresold = new FloatParameter(1f);
        [DenoiseSetting]
        public ClampedFloatParameter temporalBlendWeight = new ClampedFloatParameter(0.2f, 0, 1f);

        public enum DebugMode
        {
            Disabled,
            BaseColorOnly,
            RayCasterOnly,
            SSGIDIFFUSEONLY,
            SSGIRESULT,
        }
        public enum BlurMode
        {
            Low,
            High
        }

        public bool IsActive()
        {
            return isEnable.value && (indirectIntensity.value > 0);
        }

        public bool IsTileCompatible() => true;

        void OnValidate() {
            indirectIntensity.value = Mathf.Max(0, indirectIntensity.value);
            temporalResponseSpeed.value = Mathf.Max(0, temporalResponseSpeed.value);
            temporalDepthThresold.value = Mathf.Max(0, temporalDepthThresold.value);
            rayMaxLength.value = Mathf.Max(0.1f, rayMaxLength.value);
            rayMaxSamples.value = Mathf.Max(2, rayMaxSamples.value);
            rayJitter.value = Mathf.Max(0, rayJitter.value);
            thickness.value = Mathf.Max(0.1f, thickness.value);
        }
        
        [Serializable]
        public sealed class DebugModeParameter : VolumeParameter<DebugMode>
        {
            public DebugModeParameter(DebugMode value, bool overrideState = false)
                : base(value, overrideState)
            {
            }
        } 
        
        [Serializable]
        public sealed class BlurModeParameter : VolumeParameter<BlurMode>
        {
            public BlurModeParameter(BlurMode value, bool overrideState = false)
                : base(value, overrideState)
            {
            }
        }
        
        public class RaySetting : SettingsGroup
        {
            
        }
        
        public class PerformanceSetting : SettingsGroup
        {
            
        }
        
        public class DenoiseSetting : SettingsGroup
        {
            
        }
    }
}

