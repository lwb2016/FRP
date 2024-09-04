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
    [ExecuteInEditMode, VolumeComponentMenu("FRP/Lighting/Planar Reflection Volume")]
    public class PlanarReflectionVolume : VolumeComponent, IPostProcessComponent
    {
        
        public enum DepthBit
        {
            _16 = 16,
            _24 = 24,
        }
        
        [Serializable]
        public sealed class DepthBitParameter : VolumeParameter<DepthBit>
        {
            public DepthBitParameter(DepthBit value, bool overrideState = false)
                : base(value, overrideState)
            {
            }
        }
        
        [GeneralSettings, Space(6)]
        public BoolParameter isEnable = new BoolParameter(false);
        [GeneralSettings]
        public LayerMaskParameter reflectLayer = new LayerMaskParameter(1);
        [GeneralSettings]
        public BoolParameter renderSkyBox = new BoolParameter(true);
        [GeneralSettings]
        public BoolParameter renderTransparent = new BoolParameter(true);
        [GeneralSettings] 
        public BoolParameter isBlur = new BoolParameter(true);
        //[GeneralSettings]
        internal BoolParameter isUseMipMap = new BoolParameter(false);
        //[GeneralSettings]
        internal ClampedIntParameter MipMapCount = new ClampedIntParameter(6,2,6);
        [GeneralSettings]
        public ClampedFloatParameter blurRadius = new ClampedFloatParameter(2.0f, 0.01f, 16.0f);
        [GeneralSettings]
        public FloatParameter blurRadiusH = new FloatParameter(0.5f);
        [GeneralSettings]
        public FloatParameter blurRadiusV = new FloatParameter(0.5f);
        [AnisoSettings]
        public BoolParameter isAnisoBlur = new BoolParameter(false);
        [AnisoSettings]
        public ClampedFloatParameter anisoOffset = new ClampedFloatParameter(0.01f, 0, 0.99f);
        [AnisoSettings]
        public ClampedFloatParameter anisoPower = new ClampedFloatParameter(4f, 2, 12);
        [DepthFadeSettings]
        [Tooltip("Can't attenuate by depth when rendering skyboxes, looking for a solution")]
        public BoolParameter depthFadeEnable = new BoolParameter(false);
        [DepthFadeSettings]
        public ClampedFloatParameter depthFade = new ClampedFloatParameter(4f, 1, 8);
        [FormerlySerializedAs("planarHeight")] [PlaneSetting, Space(6)]
        public FloatParameter planarHeightOffset = new FloatParameter(0);
        [PlaneSetting]
        public Vector3Parameter position = new Vector3Parameter(Vector3.zero);
        [PlaneSetting]
        public Vector3Parameter planeUp = new Vector3Parameter(Vector3.up);
        [PerformanceSetting, Space(6)]
        public DepthBitParameter depthBit = new DepthBitParameter(DepthBit._16);
        [PerformanceSetting]
        public ClampedFloatParameter resolution = new ClampedFloatParameter(1, 0.1f, 1.0f);
        [PerformanceSetting]
        public BoolParameter usePropeBox = new BoolParameter(false);
        // [PerformanceSetting]
        // public ClampedIntParameter renderIntervals = new ClampedIntParameter(0, 0, 30);
        
        
        public bool IsActive()
        {
            return isEnable.value;
        } 

        public void SetBlur(float roughness, float anisotropy)
        {
            if ((roughness - 0.001f)<0)
            {
                isBlur.Override(false);
                return;
            }
            isBlur.Override(true);
            roughness *= roughness;
            float h = roughness + Mathf.Pow((1 + anisotropy), 4) * Mathf.Abs(anisotropy);
            float v = roughness + Mathf.Pow((1 - anisotropy),4) * Mathf.Abs(anisotropy);
            blurRadiusH.Override(h);
            blurRadiusV.Override(v);
        }

        public bool IsTileCompatible() => true;
        
        
        
        public class AnisoSettings : SettingsGroup
        {
        }
        public class DepthFadeSettings : SettingsGroup
        {
        }
        
        public class PlaneSetting : SettingsGroup
        {
            
        }
    }
}