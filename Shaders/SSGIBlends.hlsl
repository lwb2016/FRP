#ifndef URP_SSGI_BLENDS
#define URP_SSGI_BLENDS

    TEXTURE2D_X(_CompareTexGI);
    float4 _CompareParams;
    float _DebugDepthMultiplier;

	half4 FragCopy (VaryingsSSGI i) : SV_Target {
        UNITY_SETUP_INSTANCE_ID(i);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        i.uv     = UnityStereoTransformScreenSpaceTex(i.uv);
   		half4 pixel = SAMPLE_TEXTURE2D_X(_MainTex, sampler_PointClamp, i.uv);

        return pixel;
	}

	half4 FragAlbedo (VaryingsSSGI input) : SV_Target {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);
        half3 albedo = SAMPLE_TEXTURE2D_X(_GBuffer0, sampler_LinearClamp, uv).rgb;
        return half4(albedo, 1.0);
	}

	half4 FragNormals (VaryingsSSGI input) : SV_Target {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);
        half3 normals = GetWorldNormal(uv);
        return half4(normals, 1.0);
	}

	half4 FragSpecular (VaryingsSSGI input) : SV_Target {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);
        half3 specular = GetSpecularColor(uv);
        return half4(specular, 1.0);
	}

	half4 FragDepth (VaryingsSSGI input) : SV_Target {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);
        half depth = GetLinearEyeDownscaledDepth(uv);
        return half4(depth.xxx * _DebugDepthMultiplier / _ProjectionParams.z, 1.0);
	}

	half4 FragCopyDepth (VaryingsSSGI input) : SV_Target {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);
        half depth = GetRawDepth(uv);
        return half4(depth.xxx, 1.0);
	}

	half4 FragMotion (VaryingsSSGI input) : SV_Target {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);
        half2 velocity = GetVelocity(uv);
        return half4(velocity, 0, 1.0);
	}

    half4 FragCompare (VaryingsSSGI i) : SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        i.uv     = UnityStereoTransformScreenSpaceTex(i.uv);

        // separator line + antialias
        float2 dd     = i.uv - 0.5.xx;
       float  co     = dot(_CompareParams.xy, dd);
       float  dist   = distance( _CompareParams.xy * co, dd );
       float4 aa     = saturate( (_CompareParams.w - dist) / abs(_MainTex_TexelSize.y) );

       float  sameSide = (_CompareParams.z > -5);
       float2 pixelUV = lerp(i.uv, float2(i.uv.x + _CompareParams.z, i.uv.y), sameSide);
       float2 pixelNiceUV = lerp(i.uv, float2(i.uv.x - 0.5 + _CompareParams.z, i.uv.y), sameSide);
       float4 pixel  = SAMPLE_TEXTURE2D_X(_MainTex, sampler_PointClamp, pixelUV);
       float4 pixelNice = SAMPLE_TEXTURE2D_X(_CompareTexGI, sampler_PointClamp, pixelNiceUV);
        
       // are we on the beautified side?
       float2 cp     = float2(_CompareParams.y, -_CompareParams.x);
       float t       = dot(dd, cp) > 0;
       pixel         = lerp(pixel, pixelNice, t);
       return pixel + aa;
    }


#endif // URP_SSGI_BLENDS