#ifndef URP_SSGI_DENOISE
#define URP_SSGI_DENOISE

TEXTURE2D_X(_PrevResolve);

half4 FragTemporalDenoise (VaryingsSSGI i) : SV_Target { 
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    // 提前计算常用的值
    float2 uv = UnityStereoTransformScreenSpaceTex(i.uv);
    half2 velocity = GetVelocity(uv);
    float2 prevUV = uv - velocity;
    float delta = unity_DeltaTime.z * _TemporalData.x;

    half4 newData = SAMPLE_TEXTURE2D_X(_MainTex, sampler_PointClamp, uv);
    half4 prevData = SAMPLE_TEXTURE2D_X(_PrevResolve, sampler_PointClamp, prevUV);

    half4 newDataN = SAMPLE_TEXTURE2D_X(_MainTex, sampler_PointClamp, uv + float2(0, 1) * _MainTex_TexelSize.xy);
    half4 newDataS = SAMPLE_TEXTURE2D_X(_MainTex, sampler_PointClamp, uv + float2(0, -1) * _MainTex_TexelSize.xy);
    half4 newDataW = SAMPLE_TEXTURE2D_X(_MainTex, sampler_PointClamp, uv + float2(-1, 0) * _MainTex_TexelSize.xy);
    half4 newDataE = SAMPLE_TEXTURE2D_X(_MainTex, sampler_PointClamp, uv + float2(1, 0) * _MainTex_TexelSize.xy);

    half4 newDataMin = min( newData, min( min(newDataN, newDataS), min(newDataW, newDataE) ));
    half4 newDataMax = max( newData, max( max(newDataN, newDataS), max(newDataW, newDataE) ));

    half4 newDataMinExt = newDataMin * (1 - _TemporalData.z);
    half4 newDataMaxExt = newDataMax * (1 + _TemporalData.z);
    
    // 限制历史数据范围
    prevData = clamp(prevData, min(newDataMinExt, newDataMaxExt), max(newDataMinExt, newDataMaxExt));

    half4 res = lerp(prevData, newData, saturate(delta));
    return res;
}

#endif