using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace UnityEngine.Rendering.FRP
{
    [ExecuteInEditMode, VolumeComponentMenu("FRP/Lighting/HBAOVolume")]
    public class HBAOVolume : VolumeComponent, IPostProcessComponent
    {
        public enum Mode
        {
            AfterOpaque,
            Normal
        }

        public enum RenderingPath
        {
            Forward,
            Deferred
        }

        public enum Quality
        {
            Lowest,
            Low,
            Medium,
            High,
            Highest
        }

        public enum Resolution
        {
            Full,
            Half
        }

        public enum NoiseType
        {
            Dither,
            InterleavedGradientNoise,
            SpatialDistribution
        }

        public enum BlurType
        {
            None,
            Narrow,
            Medium,
            Wide,
            ExtraWide
        }

        // public enum PerPixelNormals
        // {
        //     Reconstruct2Samples,
        //     Reconstruct4Samples,
        //     Camera
        // }

        public enum VarianceClipping
        {
            Disabled,
            _4Tap,
            _8Tap
        }

        [Serializable]
        public sealed class ModeParameter : VolumeParameter<Mode>
        {
            public ModeParameter(Mode value, bool overrideState = false)
                : base(value, overrideState)
            {
            }
        }

        [Serializable]
        public sealed class RenderingPathParameter : VolumeParameter<RenderingPath>
        {
            public RenderingPathParameter(RenderingPath value, bool overrideState = false)
                : base(value, overrideState)
            {
            }
        }

        [Serializable]
        public sealed class QualityParameter : VolumeParameter<Quality>
        {
            public QualityParameter(Quality value, bool overrideState = false)
                : base(value, overrideState)
            {
            }
        }

        [Serializable]
        public sealed class ResolutionParameter : VolumeParameter<Resolution>
        {
            public ResolutionParameter(Resolution value, bool overrideState = false)
                : base(value, overrideState)
            {
            }
        }

        [Serializable]
        public sealed class NoiseTypeParameter : VolumeParameter<NoiseType>
        {
            public NoiseTypeParameter(NoiseType value, bool overrideState = false)
                : base(value, overrideState)
            {
            }
        }

        // [Serializable]
        // public sealed class PerPixelNormalsParameter : VolumeParameter<PerPixelNormals>
        // {
        //     public PerPixelNormalsParameter(PerPixelNormals value, bool overrideState = false)
        //         : base(value, overrideState)
        //     {
        //     }
        // }

        [Serializable]
        public sealed class VarianceClippingParameter : VolumeParameter<VarianceClipping>
        {
            public VarianceClippingParameter(VarianceClipping value, bool overrideState = false)
                : base(value, overrideState)
            {
            }
        }

        [Serializable]
        public sealed class BlurTypeParameter : VolumeParameter<BlurType>
        {
            public BlurTypeParameter(BlurType value, bool overrideState = false)
                : base(value, overrideState)
            {
            }
        }

        [Serializable]
        public sealed class MinMaxFloatParameter : VolumeParameter<Vector2>
        {
            public float min;
            public float max;

            public MinMaxFloatParameter(Vector2 value, float min, float max, bool overrideState = false)
                : base(value, overrideState)
            {
                this.min = min;
                this.max = max;
            }
        }

        public class AOSettings : SettingsGroup
        {
        }

        public class TemporalFilterSettings : SettingsGroup
        {
        }

        public class BlurSettings : SettingsGroup
        {
        }

        [Tooltip("The mode of the AO.")] [GeneralSettings]
        public BoolParameter isEnable = new BoolParameter(false);
        [GeneralSettings, Space(6)]
        public ModeParameter mode = new ModeParameter(Mode.Normal);

        [Tooltip(
            "The rendering path used for AO. Temporary settings as for now rendering path is internal to renderer settings.")]
        [GeneralSettings, Space(6)]
        public RenderingPathParameter renderingPath = new RenderingPathParameter(RenderingPath.Forward);

        [Tooltip("The quality of the AO.")] [GeneralSettings, Space(6)]
        public QualityParameter quality = new QualityParameter(Quality.Medium);

        [Tooltip("The resolution at which the AO is calculated.")] [GeneralSettings]
        public ClampedIntParameter downSample = new ClampedIntParameter(0, 0, 2);

        [Tooltip("The type of noise to use.")] [GeneralSettings, Space(10)]
        public NoiseTypeParameter noiseType = new NoiseTypeParameter(NoiseType.Dither);

        [Tooltip("AO radius: this is the distance outside which occluders are ignored.")] [AOSettings, Space(6)]
        public ClampedFloatParameter radius = new ClampedFloatParameter(0.8f, 0.01f, 5f);

        [Tooltip("Maximum radius in pixels: this prevents the radius to grow too much with close-up " +
                 "object and impact on performances.")]
        [AOSettings]
        public ClampedFloatParameter maxRadiusPixels = new ClampedFloatParameter(128f, 16f, 256f);

        [Tooltip("For low-tessellated geometry, occlusion variations tend to appear at creases and " +
                 "ridges, which betray the underlying tessellation. To remove these artifacts, we use " +
                 "an angle bias parameter which restricts the hemisphere.")]
        [AOSettings]
        public ClampedFloatParameter bias = new ClampedFloatParameter(0.05f, 0f, 0.99f);

        [Tooltip("This value allows to scale up the ambient occlusion values.")] [AOSettings]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(1f, 0, 4f);

        [Tooltip("How much AO affect direct lighting.")] [AOSettings]
        public ClampedFloatParameter directLightingStrength = new ClampedFloatParameter(0.25f, 0, 1f);

        [Tooltip("(None) The amount of AO offscreen samples are contributing.")] [AOSettings]
        public ClampedFloatParameter offscreenSamplesContribution = new ClampedFloatParameter(0f, 0f, 1f);

        [Tooltip("The max distance to display AO.")] [AOSettings, Space(10)]
        public FloatParameter maxDistance = new FloatParameter(150f);

        [Tooltip("The distance before max distance at which AO start to decrease.")] [AOSettings]
        public FloatParameter distanceFalloff = new FloatParameter(50f);

        // [Tooltip("The type of per pixel normals to use.")] [AOSettings, Space(10)]
        // public PerPixelNormalsParameter perPixelNormals = new PerPixelNormalsParameter(PerPixelNormals.Camera);

        // [Tooltip("This setting allow you to set the base color if the AO, the alpha channel value is unused.")]
        // [AOSettings, Space(10)]
        // public ColorParameter baseColor = new ColorParameter(Color.black);

        // [TemporalFilterSettings, ParameterDisplayName("Enabled"), Space(6)]
        // public BoolParameter temporalFilterEnabled = new BoolParameter(false);
        //
        // [Tooltip("The type of variance clipping to use.")] 
        // [TemporalFilterSettings, ParameterDisplayName("Enabled"), Space(6)]
        // public VarianceClippingParameter varianceClipping = new VarianceClippingParameter(VarianceClipping._4Tap);

        [Tooltip("The type of blur to use.")] [BlurSettings, ParameterDisplayName("Type"), Space(6)]
        public BlurTypeParameter blurType = new BlurTypeParameter(BlurType.Medium);

        [Tooltip("This parameter controls the depth-dependent weight of the bilateral filter, to " +
                 "avoid bleeding across edges. A zero sharpness is a pure Gaussian blur. Increasing " +
                 "the blur sharpness removes bleeding by using lower weights for samples with large " +
                 "depth delta from the current pixel.")]
        [BlurSettings, Space(10)]
        public ClampedFloatParameter sharpness = new ClampedFloatParameter(8f, 0f, 16f);

        public Mode GetMode()
        {
            return mode.value;
        }

        public void SetMode(Mode mode)
        {
            this.mode.Override(mode);
        }

        public RenderingPath GetRenderingPath()
        {
            return renderingPath.value;
        }

        public void SetRenderingPath(RenderingPath renderingPath)
        {
            this.renderingPath.Override(renderingPath);
        }

        public void SetQuality(Quality quality)
        {
            this.quality.Override(quality);
        }

        public NoiseType GetNoiseType()
        {
            return noiseType.value;
        }

        public void SetNoiseType(NoiseType noiseType)
        {
            this.noiseType.Override(noiseType);
        }

        public float GetAoRadius()
        {
            return radius.value;
        }

        public void SetAoRadius(float radius)
        {
            this.radius.Override(Mathf.Clamp(radius, this.radius.min, this.radius.max));
        }

        public float GetAoMaxRadiusPixels()
        {
            return maxRadiusPixels.value;
        }

        public void SetAoMaxRadiusPixels(float maxRadiusPixels)
        {
            this.maxRadiusPixels.Override(Mathf.Clamp(maxRadiusPixels, this.maxRadiusPixels.min,
                this.maxRadiusPixels.max));
        }

        public float GetAoBias()
        {
            return bias.value;
        }

        public void SetAoBias(float bias)
        {
            this.bias.Override(Mathf.Clamp(bias, this.bias.min, this.bias.max));
        }

        public float GetAoOffscreenSamplesContribution()
        {
            return offscreenSamplesContribution.value;
        }

        public void SetAoOffscreenSamplesContribution(float offscreenSamplesContribution)
        {
            this.offscreenSamplesContribution.Override(Mathf.Clamp(offscreenSamplesContribution,
                this.offscreenSamplesContribution.min, this.offscreenSamplesContribution.max));
        }

        public float GetAoMaxDistance()
        {
            return maxDistance.value;
        }

        public void SetAoMaxDistance(float maxDistance)
        {
            this.maxDistance.Override(maxDistance);
        }

        public float GetAoDistanceFalloff()
        {
            return distanceFalloff.value;
        }

        public void SetAoDistanceFalloff(float distanceFalloff)
        {
            this.distanceFalloff.Override(distanceFalloff);
        }

        // public PerPixelNormals GetAoPerPixelNormals()
        // {
        //     return perPixelNormals.value;
        // }
        //
        // public void SetAoPerPixelNormals(PerPixelNormals perPixelNormals)
        // {
        //     this.perPixelNormals.Override(perPixelNormals);
        // }

        public float GetAoIntensity()
        {
            return intensity.value;
        }

        public void SetAoIntensity(float intensity)
        {
            this.intensity.Override(Mathf.Clamp(intensity, this.intensity.min, this.intensity.max));
        }

        // public bool IsTemporalFilterEnabled()
        // {
        //     return temporalFilterEnabled.value;
        // }
        //
        // public void EnableTemporalFilter(bool enabled = true)
        // {
        //     temporalFilterEnabled.Override(enabled);
        // }
        //
        // public VarianceClipping GetTemporalFilterVarianceClipping()
        // {
        //     return varianceClipping.value;
        // }
        //
        // public void SetTemporalFilterVarianceClipping(VarianceClipping varianceClipping)
        // {
        //     this.varianceClipping.Override(varianceClipping);
        // }

        public BlurType GetBlurType()
        {
            return blurType.value;
        }

        public void SetBlurType(BlurType blurType)
        {
            this.blurType.Override(blurType);
        }

        public float GetBlurSharpness()
        {
            return sharpness.value;
        }

        public void SetBlurSharpness(float sharpness)
        {
            this.sharpness.Override(Mathf.Clamp(sharpness, this.sharpness.min, this.sharpness.max));
        }

        public bool IsActive()
        {
            return isEnable.value;
        }

        public bool IsTileCompatible() => true;
        
        // Functions
        public class MinMaxSliderAttribute : PropertyAttribute
        {
            public readonly float max;
            public readonly float min;

            public MinMaxSliderAttribute(float min, float max)
            {
                this.min = min;
                this.max = max;
            }
        }
    }
   
}