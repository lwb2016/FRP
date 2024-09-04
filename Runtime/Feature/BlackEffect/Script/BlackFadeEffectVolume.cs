using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.LibTessDotNet;

namespace UnityEngine.Rendering.FRP
{

    [ExecuteInEditMode, VolumeComponentMenu("FRPVolume/URP/BlackFadeEffectVolume")]
    public class BlackFadeEffectVolume : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Strength of the Black Fade.")]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f,1,true);
        [Tooltip("Center position of sphere")]
        public Vector3Parameter centerPos = new Vector3Parameter(Vector3.one, true);
        [Tooltip("Shape of sphere")]
        public Vector3Parameter shape = new Vector3Parameter(Vector3.one, true);
        [Tooltip("Radius of sphere")] 
        public FloatParameter radius = new FloatParameter(0, true);
        [Tooltip("Softness of sphere boundary ")]
        public MinFloatParameter soft = new MinFloatParameter(0, 0, true);
        [Tooltip("Mask Color")]
        public ColorParameter color = new ColorParameter(Color.black, true, false, false, true);
        
        public bool IsActive() => intensity.value > 0f;

        /// <inheritdoc/>
        public bool IsTileCompatible() => false;
    }

}