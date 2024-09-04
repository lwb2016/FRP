Shader "Hidden/FRP/HBAO_URP"
{
    Properties
    {
        _MainTex("", any) = "" {}
        _HBAOTex("", any) = "" {}
        _TempTex("", any) = "" {}
        _NoiseTex("", 2D) = "" {}
        _DepthTex("", any) = "" {}
        _NormalsTex("", any) = "" {}
    }

    HLSLINCLUDE

    #pragma target 3.0
    //ragma prefer_hlslcc gles
    //#pragma enable_d3d11_debug_symbols

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    TEXTURE2D_X(_MainTex);
    TEXTURE2D_X(_HBAOTex);
    TEXTURE2D_X(_TempTex);
    TEXTURE2D_X(_DepthTex);
    TEXTURE2D_X(_NormalsTex);
    //TEXTURE2D_X(_CameraNormalsTexture);
    TEXTURE2D_X(_MotionVectorTexture);
    TEXTURE2D(_NoiseTex);

    CBUFFER_START(FrequentlyUpdatedUniforms)
    float4 _Input_TexelSize;
    float4 _AO_TexelSize;
    float4 _TargetScale;
    float4 _UVToView[2];
    //float4x4 _WorldToCameraMatrix;
    float _Radius[2];
    float _MaxRadiusPixels;
    float _NegInvRadius2;
    float _AngleBias;
    float _AOmultiplier;
    float _HBAOIntensity;
    float _OffscreenSamplesContrib;
    float _MaxDistance;
    float _DistanceFalloff;
    float _BlurSharpness;
    float _ColorBleedSaturation;
    float _ColorBleedBrightnessMask;
    float2 _ColorBleedBrightnessMaskRange;
    float2 _TemporalParams;
    CBUFFER_END

    CBUFFER_START(PerPassUpdatedUniforms)
    float2 _BlurDeltaUV;
    CBUFFER_END

    CBUFFER_START(PerPassUpdatedDeinterleavingUniforms)
    float2 _Deinterleave_Offset00;
    float2 _Deinterleave_Offset10;
    float2 _Deinterleave_Offset01;
    float2 _Deinterleave_Offset11;
    float2 _AtlasOffset;
    float2 _Jitter;
    CBUFFER_END

    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZWrite Off ZTest Always Blend Off Cull Off

        Pass // 0
        {
            Name "HBAO - AO"

            HLSLPROGRAM
            #pragma multi_compile_local_fragment __ ORTHOGRAPHIC_PROJECTION
            #pragma multi_compile_fragment __ _NORMALSRECONSTRUCT // if _NORMALSRECONSTRUCT then Sample _CameraNormalTexture
            #pragma multi_compile_local_fragment __ INTERLEAVED_GRADIENT_NOISE
            #pragma multi_compile_local_fragment QUALITY_LOWEST QUALITY_LOW QUALITY_MEDIUM QUALITY_HIGH QUALITY_HIGHEST
            #pragma multi_compile_fragment __ _GBUFFER_NORMALS_OCT // support octahedron endoded normals

            #if QUALITY_LOWEST
                #define DIRECTIONS  3
                #define STEPS       2
            #elif QUALITY_LOW
                #define DIRECTIONS  4
                #define STEPS       3
            #elif QUALITY_MEDIUM
                #define DIRECTIONS  6
                #define STEPS       4
            #elif QUALITY_HIGH
                #define DIRECTIONS  8
                #define STEPS       4
            #elif QUALITY_HIGHEST
                #define DIRECTIONS  8
                #define STEPS       6
            #else
                #define DIRECTIONS  1
                #define STEPS       1
            #endif

            #pragma vertex Vert
            #pragma fragment AO_Frag

            #include "HBAO_AO.hlsl"
            ENDHLSL
        }
        
        Pass // 1
        {
            Name "HBAO - AO Mix Opaque"
            
            Blend One SrcAlpha, Zero One
            BlendOp Add, Add

            HLSLPROGRAM
            #pragma multi_compile_local_fragment __ ORTHOGRAPHIC_PROJECTION
            #pragma multi_compile_fragment __ _NORMALSRECONSTRUCT // if _NORMALSRECONSTRUCT then Sample _CameraNormalTexture
            #pragma multi_compile_local_fragment __ INTERLEAVED_GRADIENT_NOISE
            #pragma multi_compile_local_fragment QUALITY_LOWEST QUALITY_LOW QUALITY_MEDIUM QUALITY_HIGH QUALITY_HIGHEST
            #pragma multi_compile_fragment __ _GBUFFER_NORMALS_OCT // support octahedron endoded normals
            #define _MIXOPAQUE 1

            #if QUALITY_LOWEST
                #define DIRECTIONS  3
                #define STEPS       2
            #elif QUALITY_LOW
                #define DIRECTIONS  4
                #define STEPS       3
            #elif QUALITY_MEDIUM
                #define DIRECTIONS  6
                #define STEPS       4
            #elif QUALITY_HIGH
                #define DIRECTIONS  8
                #define STEPS       4
            #elif QUALITY_HIGHEST
                #define DIRECTIONS  8
                #define STEPS       6
            #else
                #define DIRECTIONS  1
                #define STEPS       1
            #endif

            #pragma vertex Vert
            #pragma fragment AO_Frag

            #include "HBAO_AO.hlsl"
            ENDHLSL
        }

        Pass // 2
        {
            Name "HBAO - Blur"

            HLSLPROGRAM
            #pragma multi_compile_local_fragment __ ORTHOGRAPHIC_PROJECTION
            #pragma multi_compile_local_fragment BLUR_RADIUS_2 BLUR_RADIUS_3 BLUR_RADIUS_4 BLUR_RADIUS_5

            #if BLUR_RADIUS_2
                #define KERNEL_RADIUS  2
            #elif BLUR_RADIUS_3
                #define KERNEL_RADIUS  3
            #elif BLUR_RADIUS_4
                #define KERNEL_RADIUS  4
            #elif BLUR_RADIUS_5
                #define KERNEL_RADIUS  5
            #else
                #define KERNEL_RADIUS  0
            #endif

            #pragma vertex Vert
            #pragma fragment Blur_Frag

            #include "HBAO_Blur.hlsl"
            ENDHLSL
        }
        
        Pass // 3
        {
            Name "HBAO - Blur Mix After Opaque"
            
            Blend One SrcAlpha, Zero One
            BlendOp Add, Add

            HLSLPROGRAM

            #pragma multi_compile_local_fragment __ ORTHOGRAPHIC_PROJECTION
            #pragma multi_compile_local_fragment BLUR_RADIUS_2 BLUR_RADIUS_3 BLUR_RADIUS_4 BLUR_RADIUS_5

            #if BLUR_RADIUS_2
                #define KERNEL_RADIUS  2
            #elif BLUR_RADIUS_3
                #define KERNEL_RADIUS  3
            #elif BLUR_RADIUS_4
                #define KERNEL_RADIUS  4
            #elif BLUR_RADIUS_5
                #define KERNEL_RADIUS  5
            #else
                #define KERNEL_RADIUS  0
            #endif

            #pragma vertex Vert
            #pragma fragment AfterOpaqueBlur_Frag


            #include "HBAO_Blur.hlsl"
            ENDHLSL
        }

        Pass // 4
        {
            Name "HBAO - Composite - Normal Mode"

            ColorMask RGB

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Composite_Frag

            #include "HBAO_Composite.hlsl"
            ENDHLSL
        }
        
        
    }

    Fallback Off
}
