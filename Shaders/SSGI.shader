Shader "Hidden/FRP/URP/SSGI"
{
    Properties
    {
        [NoScaleoffset] _NoiseTex("Noise Tex", any) = "" {}
        _StencilValue("Stencil Value", Int) = 0
        _StencilCompareFunction("Stencil Compare Function", Int) = 8
    }

    Subshader
    {

        ZWrite Off ZTest Always Cull Off
        Tags
        {
            "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "DisableBatching"="True" "ForceNoShadowCasting"="True"
        }

        HLSLINCLUDE
        #pragma target 3.0
        //#pragma prefer_hlslcc gles
        //#pragma exclude_renderers d3d11_9x

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"

        #undef SAMPLE_TEXTURE2D_X
        #define SAMPLE_TEXTURE2D_X(tex,sampler,uv) SAMPLE_TEXTURE2D_X_LOD(tex,sampler,uv,0)

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
        #include "SSGICommon.hlsl"
        ENDHLSL

        Pass
        {
            // 0
            Name "Copy"
            HLSLPROGRAM
            #pragma vertex VertSSGI
            #pragma fragment FragCopy
            #include "SSGIBlends.hlsl"
            ENDHLSL
        }

        Pass
        {
            // 1
            Name "Raycast"
            HLSLPROGRAM
            #pragma vertex VertRaycast
            #pragma fragment FragRaycast
            #pragma multi_compile_fragment __ _NORMALSRECONSTRUCT
            #pragma multi_compile_local _ _SSGI_USES_BINARY_SEARCH
            #pragma multi_compile_local _ _SSGI_FALLBACK_PROBE
            #pragma multi_compile_local _ _REUSERAYTRACING
            #pragma multi_compile_local _ _SSGI_ORTHO_SUPPORT
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "SSGIRaycast.hlsl"
            ENDHLSL
        }

        Pass
        {
            // 2
            Name "horizontal blur"
            HLSLPROGRAM
            #pragma vertex VertBlur
            #pragma fragment FragBlur
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #define SSGI_BLUR_H
            #include "SSGIBilateralFilter.hlsl"
            ENDHLSL
        }

        Pass
        {
            // 3
            Name "vertical blur"
            HLSLPROGRAM
            #pragma vertex VertBlur
            #pragma fragment FragBlur
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "SSGIBilateralFilter.hlsl"
            ENDHLSL
        }

        Pass
        {
            // 4
            Name "Upscale"
            HLSLPROGRAM
            #pragma vertex VertSSGI
            #pragma fragment FragUpscale
            #pragma multi_compile_local _ _SSGI_ORTHO_SUPPORT
            #include "SSGIUpscale.hlsl"
            ENDHLSL
        }

        Pass
        {
            // 5
            Name "Temporal Denoise"
            HLSLPROGRAM
            #pragma vertex VertSSGI
            #pragma fragment FragTemporalDenoise
            #include "SSGIDenoise.hlsl"
            ENDHLSL
        }

        Pass
        {
            // 6
            Name "Compose"
            Stencil
            {
                Ref [_StencilValue]
                Comp [_StencilCompareFunction]
            }
            HLSLPROGRAM
            #pragma vertex VertSSGI
            #pragma fragment FragCompose
            #pragma multi_compile_local _ _SSGI_ORTHO_SUPPORT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "SSGIUpscale.hlsl"
            ENDHLSL
        }

        Pass
        {
            // 7
            Name "Copy Depth"
            HLSLPROGRAM
            #pragma vertex VertSSGI
            #pragma fragment FragCopyDepth
            #pragma multi_compile_local _ _SSGI_ORTHO_SUPPORT
            #include "SSGIBlends.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}