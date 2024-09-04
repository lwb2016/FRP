#ifndef FENRRENDER_URP_HBAO_AO_INCLUDED
#define FENRRENDER_URP_HBAO_AO_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Macros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "HBAO_Common.hlsl"

inline float3 FetchLayerViewPos(float2 uv) {
    uv = clamp(uv, 0, 1 - _Input_TexelSize.xy * 0.5); // uv guard
    float depth = LinearizeDepth(SAMPLE_TEXTURE2D_X(_DepthTex, sampler_PointClamp, uv).r);
#if ORTHOGRAPHIC_PROJECTION
    return float3((uv * _UVToView[unity_StereoEyeIndex].xy + _UVToView[unity_StereoEyeIndex].zw), depth);
#else
    return float3((uv * _UVToView[unity_StereoEyeIndex].xy + _UVToView[unity_StereoEyeIndex].zw) * depth, depth);
#endif
}

inline float Falloff(float distanceSquare) {
    // 1 scalar mad instruction
    return distanceSquare * _NegInvRadius2 + 1.0;
}

inline float ComputeAO(float3 P, float3 N, float3 S) {
    float3 V = S - P;
    float VdotV = dot(V, V);
    float NdotV = dot(N, V) * rsqrt(VdotV);

    // Use saturate(x) instead of max(x,0.f) because that is faster on Kepler
    return saturate(NdotV - _AngleBias) * saturate(Falloff(VdotV));
}

inline float2 RotateDirections(float2 dir, float2 rot) {
    return float2(dir.x * rot.x - dir.y * rot.y,
        dir.x * rot.y + dir.y * rot.x);
}

inline float InterleavedGradientNoise(float2 screenPos) {
    // http://www.iryoku.com/downloads/Next-Generation-Post-Processing-in-Call-of-Duty-Advanced-Warfare-v18.pptx (slide 123)
    float3 magic = float3(0.06711056, 0.00583715, 52.9829189);
    return frac(magic.z * frac(dot(screenPos, magic.xy)));
}

inline float2 FetchNoise(float2 screenPos) {
    #if INTERLEAVED_GRADIENT_NOISE
        // Use Jorge Jimenez's IGN noise and GTAO spatial offsets distribution
        // https://blog.selfshadow.com/publications/s2016-shading-course/activision/s2016_pbs_activision_occlusion.pdf (slide 93)
        return float2(InterleavedGradientNoise(screenPos), SAMPLE_TEXTURE2D(_NoiseTex, sampler_PointRepeat, screenPos / 4.0).g);
    #else
        // (cos(alpha), sin(alpha), jitter)
        return SAMPLE_TEXTURE2D(_NoiseTex, sampler_PointRepeat, screenPos / 4.0).rg;
    #endif
}

float4 AO_Frag(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);

    float3 P = FetchViewPos(uv);

    clip(_MaxDistance - P.z);

    #if ORTHOGRAPHIC_PROJECTION
        float stepSize = min(_Radius[unity_StereoEyeIndex], _MaxRadiusPixels) / (STEPS + 1.0);
    #else
        float stepSize = min((_Radius[unity_StereoEyeIndex] / P.z), _MaxRadiusPixels) / (STEPS + 1.0);
    #endif

    float3 N = FetchViewNormals(uv, _AO_TexelSize.xy, P);
    //float2 rand = FetchNoise(positionSS);
    //float2 rand = FetchNoise(input.positionCS.xy);
    float2 rand = FetchNoise(uv * _AO_TexelSize.zw);

    const float alpha = 2.0 * PI / DIRECTIONS;
    float ao = 0;

    UNITY_UNROLL
    for (int d = 0; d < DIRECTIONS; ++d) {
        float angle = alpha * (float(d) + rand.x + _TemporalParams.x);

        // Compute normalized 2D direction
        float cosA, sinA;
        sincos(angle, sinA, cosA);
        float2 direction = float2(cosA, sinA);

        // Jitter starting sample within the first step
        float rayPixels = (frac(rand.y + _TemporalParams.y) * stepSize + 1.0);

        UNITY_UNROLL
        for (int s = 0; s < STEPS; ++s) {

            float2 snappedUV = round(rayPixels * direction) * _Input_TexelSize.xy + uv;
            float3 S = FetchViewPos(snappedUV);

            rayPixels += stepSize;

            float contrib = ComputeAO(P, N, S);
            ao += contrib;
        }
    }

    float aoOutput = ao;


    // apply bias multiplier
    aoOutput *= (_AOmultiplier / (STEPS * DIRECTIONS));

    float fallOffStart = _MaxDistance - _DistanceFalloff;
    float distFactor = saturate((P.z - fallOffStart) / (_MaxDistance - fallOffStart));
    aoOutput = lerp(saturate(1 - aoOutput), 1, distFactor);

    #if _MIXOPAQUE
        aoOutput = saturate(pow(abs(aoOutput), _HBAOIntensity));
        return float4(0,0,0,aoOutput);
    #else
        return float4(EncodeFloatRG(saturate(P.z * (1.0 / _ProjectionParams.z))), aoOutput, 1.0);
    #endif
}

#endif // FENRRENDER_URP_HBAO_AO_INCLUDED
