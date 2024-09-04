#ifndef URP_SSGI_UPSCALE
#define URP_SSGI_UPSCALE

    TEXTURE2D_X(_InputRTGI);
    TEXTURE2D_X(_BaseColorTexture);
    TEXTURE2D_X(_NFO_RT);

    #define TEST_DEPTH(lowestDiff, nearestColor, depthDiff, color) if (depthDiff < lowestDiff) { lowestDiff = depthDiff; nearestColor = color; }

    half4 GetIndirect(float2 uv, float depth) {        
        half4 nearestColor = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv);

        half depthM = nearestColor.w;
        half diff = abs(depth - depthM);

        UNITY_BRANCH
        if (diff > 0.00001) {
            float m = 0.5;

            float2 uvN = uv + float2(0, _MainTex_TexelSize.y * m );
            float2 uvS = uv - float2(0, _MainTex_TexelSize.y * m);
            float2 uvE = uv + float2(_MainTex_TexelSize.x * m, 0);
            float2 uvW = uv - float2(_MainTex_TexelSize.x * m, 0);

            half4 colorN = SAMPLE_TEXTURE2D_X_LOD(_MainTex, sampler_LinearClamp, uvN, 0);
            half4 colorS = SAMPLE_TEXTURE2D_X_LOD(_MainTex, sampler_LinearClamp, uvS, 0);
            half4 colorE = SAMPLE_TEXTURE2D_X_LOD(_MainTex, sampler_LinearClamp, uvE, 0);
            half4 colorW = SAMPLE_TEXTURE2D_X_LOD(_MainTex, sampler_LinearClamp, uvW, 0);

            half4 depths = half4(colorN.w, colorS.w, colorE.w, colorW.w);
            half4 dDiff = abs(depths - depth.xxxx);

            half lowestDiff = diff;
            TEST_DEPTH(lowestDiff, nearestColor, dDiff.x, colorN);
            TEST_DEPTH(lowestDiff, nearestColor, dDiff.y, colorS);
            TEST_DEPTH(lowestDiff, nearestColor, dDiff.z, colorE);
            TEST_DEPTH(lowestDiff, nearestColor, dDiff.w, colorW);

        }
        return nearestColor;
    }

	half4 FragUpscale (VaryingsSSGI input): SV_Target {

        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);

        float rawDepth = GetRawDepth(uv);
        if (IsOutOfDepth(rawDepth)) return 0;
        float depth = RawToLinearEyeDepth(rawDepth);

        float4 res = GetIndirect(uv, depth);
        return res;
	}


	half4 FragCompose (VaryingsSSGI i) : SV_Target {
        UNITY_SETUP_INSTANCE_ID(i);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        float2 uv = UnityStereoTransformScreenSpaceTex(i.uv);

        #if defined(DEBUG_GI)
            half4 input = half4(0, 0, 0, 0);
        #else
            half4 input = SAMPLE_TEXTURE2D_X_LOD(_InputRTGI, sampler_PointClamp, uv, 0);
        #endif

        float rawDepth = GetRawDepth(uv);
        if (IsOutOfDepth(rawDepth)) return input;

        float depth = RawToLinearEyeDepth(rawDepth);

        // limit to volume bounds
        float3 wpos = GetWorldPosition(uv, rawDepth);

   		half3 indirect = GetIndirect(uv, depth).rgb;

        half3 norm = GetWorldNormal(uv);

	    half4 pixelGBuffer0 = SAMPLE_TEXTURE2D_X(_BaseColorTexture, sampler_PointClamp, uv);

	    indirect.rgb *= pixelGBuffer0.rgb;

        // reduce fog effect by enhancing normal mapping 
        float3 cameraPosition = GetCameraPositionWS(); 
        half3 toCamera = normalize(cameraPosition - wpos);
        half ndot = max(0, 1 + dot(norm, toCamera) * _ExtraData.z - _ExtraData.z);
        indirect *= ndot;

        // saturate
        indirect = lerp(GetLuma(indirect), indirect, _ExtraData.w);

        // attenuates near to camera
        indirect *= min(1.0, depth);

        input.rgb += indirect.rgb;

        return input;
	}


#endif