using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace UnityEngine.Rendering.FRP
{
    [Serializable]
    public class FRPData : ScriptableObject
    {
        [Reload("Shaders/Utils/CopyDepth.shader")]
        public Shader copyDepthShader;
        [FormerlySerializedAs("dualKawaseBlur")] 
        [Reload("Shaders/DualKawaseBlur.shader")]
        public Shader dualKawaseBlurShader; 
        [FormerlySerializedAs("fURPVolumeUber")] 
        [Reload("Shaders/FURPVolumeUber.shader")]
        public Shader fURPVolumeUberShader; 
        [Reload("Shaders/HBAO_URP.shader")]
        public Shader hbaoShader; 
        [Reload("Shaders/NormalReconstruct.shader")]
        public Shader normalReconstructShader; 
        [Reload("Shaders/SSGI.shader")]
        public Shader ssgiShader; 
        [Reload("Shaders/AreaLightSource.shader")]
        public Shader areaLightSourceShader;
        [Reload("Shaders/GaussinBlur.shader")]
        public Shader GaussinBlurShader;
        
        [Reload("Shaders/Utils/BlitHDROverlay.shader"), SerializeField]
        internal Shader blitHDROverlay;
        
        // Texture:
        public Texture2D blueNoiseTexture;

#if UNITY_EDITOR
        internal static FRPData GetDefaultData()
        {
            var path = System.IO.Path.Combine("Packages/com.tateam.frp", "Runtime/Data/FRPData.asset");
            return AssetDatabase.LoadAssetAtPath<FRPData>(path);
        }
#endif
    }

}

