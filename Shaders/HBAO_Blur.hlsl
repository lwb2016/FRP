//----------------------------------------------------------------------------------
//
// Copyright (c) 2014, NVIDIA CORPORATION. All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
//  * Redistributions of source code must retain the above copyright
//    notice, this list of conditions and the following disclaimer.
//  * Redistributions in binary form must reproduce the above copyright
//    notice, this list of conditions and the following disclaimer in the
//    documentation and/or other materials provided with the distribution.
//  * Neither the name of NVIDIA CORPORATION nor the names of its
//    contributors may be used to endorse or promote products derived
//    from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS ``AS IS'' AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT OWNER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
// EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
// PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY
// OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//
//----------------------------------------------------------------------------------

#ifndef HBAO_BLUR_INCLUDED
#define HBAO_BLUR_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "HBAO_Common.hlsl"

SAMPLER(sampler_BlitTexture);

# define  BlurSigma KERNEL_RADIUS * 0.5
const float BlurFalloff = 1.0 / (2.0*BlurSigma*BlurSigma);

inline void FetchAoAndDepth(float2 uv, inout half ao, inout float2 depth) {
    float3 aod = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).rgb;
    ao = aod.z;
    depth = aod.xy;
}

inline void ProcessRadius(float2 uv0, float2 deltaUV, float depthCurrent, inout half totalAO, inout float totalW) {
    half ao;
    float z;
    float2 d, uv;
    UNITY_UNROLL
    for (int r = 1; r <= KERNEL_RADIUS; r++) {
        uv = uv0 + r * deltaUV;
        FetchAoAndDepth(uv, ao, d);
        z = DecodeFloatRG(d); 
        float dDiff = (z - depthCurrent) * _ProjectionParams.z * _BlurSharpness * 10;
        //dDiff = 0;
        float w = exp2(-r*r*BlurFalloff - dDiff * dDiff);
        totalW += w;
        totalAO += w * ao;
    }
}

inline float4 ComputeBlur(float2 uv0, float2 deltaUV) {
    half totalAO = 0;
    float2 depth;
    FetchAoAndDepth(uv0, totalAO, depth);

    float depthCurrent = DecodeFloatRG(depth);
    float totalW = 1.0;
    
    ProcessRadius(uv0, -deltaUV, depthCurrent, totalAO, totalW);
    ProcessRadius(uv0, deltaUV, depthCurrent, totalAO, totalW);

    totalAO /= totalW;
    return float4(depth, totalAO, 1.0);
}

half4 Blur_Frag(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);

    return ComputeBlur(uv, _BlurDeltaUV);
}

half4 AfterOpaqueBlur_Frag(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);

    half4 blurFinal = ComputeBlur(uv, _BlurDeltaUV);

    half ao = saturate(pow(abs(blurFinal.b), _HBAOIntensity));

    return float4(0,0,0, ao);
}



#endif