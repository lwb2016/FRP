Shader "Hidden/FRP/PostProcess/FRPVolumeUber"
{
    HLSLINCLUDE
        #pragma exclude_renderers gles
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    ENDHLSL
    
    Subshader
    {
        ZTest Always ZWrite Off Cull Off

        // Pass 0: Volume Feature (Must Before TAA)
        Pass
        {

            Name "Fern Volume Feature"
            HLSLPROGRAM

            //----------Debug------------
            #pragma shader_feature_local_fragment __ _FURPDEBUG _FURPDEBUG_AO
            
            //----------FRP Uber------------
            #pragma multi_compile _ _SSGI
            #pragma multi_compile _ _DUALKAWASEBLUR
            #pragma multi_compile _ _EDGEDETECION
            #pragma multi_compile _ _DOF
            #pragma vertex VertUber
            #pragma fragment frag_beforeTAA


            #include "FURPVolumeUber.hlsl"

            ENDHLSL

        }
        
        // Pass 1: Black Fade Effect
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha, Zero One
            BlendOp Add, Add

            Name "Black Fade Effect"
            HLSLPROGRAM

            #pragma multi_compile_fragment _ _BLACKFADE
            
            #pragma vertex VertUber
            #pragma fragment frag_BlackFade


            #include "FURPVolumeUber.hlsl"

            ENDHLSL

        }
    }

    Fallback off
}