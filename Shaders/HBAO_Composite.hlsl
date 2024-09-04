#ifndef HBAO_COMPOSITE_INCLUDED
#define HBAO_COMPOSITE_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "HBAO_Common.hlsl"

inline half4 FetchOcclusion(float2 uv) {
    return SAMPLE_TEXTURE2D_X(_HBAOTex, sampler_LinearClamp, uv * _TargetScale.zw);
}

inline half4 FetchSceneColor(float2 uv) {
    //return LOAD_TEXTURE2D_X(_MainTex, positionSS); // load not supported on GLES2
    return SAMPLE_TEXTURE2D_X(_MainTex, sampler_PointClamp, uv);
}

float4 Composite_Frag(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    //uint2 positionSS = input.uv * _ScreenSize.xy;
    float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);

    half4 ao = FetchOcclusion(uv);
    ao.b = saturate(pow(abs(ao.b), _HBAOIntensity));

    half3 aoColor = ao.bbb;
    half4 col = ao.bbbb;
    
    return col;
}

float4 CompositeAfterOpaque_Frag(Varyings input) : SV_Target
{
    half4 ao = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord * _TargetScale.zw);
    ao.b = saturate(pow(abs(ao.b), _HBAOIntensity));
    return half4(0,0,0, ao.b);
}

#endif // HBAO_COMPOSITE_INCLUDED
