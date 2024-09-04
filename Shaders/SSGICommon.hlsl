#ifndef URP_SSGI_COMMON
#define URP_SSGI_COMMON

    TEXTURE2D_X(_MainTex);
    float4 _MainTex_TexelSize;

    TEXTURE2D(_NoiseTex);
    float4 _NoiseTex_TexelSize;

	TEXTURE2D_X(_MotionVectorTexture);
    TEXTURE2D_X(_GBuffer0);
    TEXTURE2D_X(_GBuffer1);

    TEXTURE2D_X(_DownscaledDepthRT);
    float4 _DownscaledDepthRT_TexelSize;

    #define dot2(x) dot(x, x)
    
    float4x4 _WorldToViewDir;

    /**
     * \brief
     * xy: size\n
     * z:golden ratio\n
     * w:frame count\n
     */
    float4 _SourceSize;

    /**
     * \brief
     * x: indirect intensity\n
     * y: blur scale\n
     * z: distance attenuation\n
     * w: reuse intensity\n
     */
    float4 _IndirectData;

    /**
     * \brief
     * x: ray count, (only one for now)\n
     * y: max length\n
     * z: max samples\n
     * w: thickness\n
     */
    float4 _RayData;

    /**
     * \brief
     * x: response speed\n
     * y: max depth difference\n
     * z: chroma threshold\n
     */
    float3 _TemporalData;

    /**
     * \brief
     * x: jitter amount\n
     * y: blur spread\n
     * z: normals influence\n
     * w: color saturation\n
     */
    float4 _ExtraData;

    float4 _BoundsXZ;

	struct AttributesFS {
		float4 positionHCS : POSITION;
		float2 uv          : TEXCOORD0;
		UNITY_VERTEX_INPUT_INSTANCE_ID
	};

 	struct VaryingsSSGI {
    	float4 positionCS : SV_POSITION;
    	float2 uv  : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
	};


	VaryingsSSGI VertSSGI(AttributesFS input) {
	    VaryingsSSGI output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_TRANSFER_INSTANCE_ID(input, output);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = float4(input.positionHCS.xyz, 1.0);
        output.positionCS.y *= _ProjectionParams.x;

        output.uv = input.uv;
    	return output;
	}

    float GetRawDepth(float2 uv) {
        float depth = SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_PointClamp, uv, 0).r;
        return depth;
    }

    float RawToLinearEyeDepth(float rawDepth) {
        float eyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
        #if _SSGI_ORTHO_SUPPORT
            #if UNITY_REVERSED_Z
                rawDepth = 1.0 - rawDepth;
            #endif
            float orthoEyeDepth = lerp(_ProjectionParams.y, _ProjectionParams.z, rawDepth);
            eyeDepth = lerp(eyeDepth, orthoEyeDepth, unity_OrthoParams.w);
        #endif
        return eyeDepth;
    }

    float GetLinearEyeDepth(float2 uv) {
        float rawDepth = GetRawDepth(uv);
        return RawToLinearEyeDepth(rawDepth);
    }

    float GetDownscaledRawDepth(float2 uv) {
	float depth = SAMPLE_TEXTURE2D_X_LOD(_DownscaledDepthRT, sampler_PointClamp, uv, 0).r;
	return depth;
    }

    float GetLinearEyeDownscaledDepth(float2 uv) {
        float rawDepth = GetDownscaledRawDepth(uv);
        return RawToLinearEyeDepth(rawDepth);
    }

    float3 GetViewSpacePosition(float2 uv, float rawDepth) {
        #if UNITY_REVERSED_Z
            float depth = 1.0 - rawDepth;
        #else
            float depth = rawDepth;
        #endif
        depth = 2.0 * depth - 1.0;
        float3 rayStart = ComputeViewSpacePosition(uv, depth, unity_CameraInvProjection);
        return rayStart;
    }

    float3 GetViewSpacePosition(float2 uv) {
        float rawDepth = GetRawDepth(uv);
        return GetViewSpacePosition(uv, rawDepth);
    }

    float3 GetWorldPosition(float2 uv, float rawDepth) {
        
         #if UNITY_REVERSED_Z
              float depth = rawDepth;
         #else
              float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
         #endif

         // Reconstruct the world space positions.
         float3 worldPos = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);

        return worldPos;
    }

    float3 GetWorldPosition(float2 uv) {
        float rawDepth = GetRawDepth(uv);
        return GetWorldPosition(uv, rawDepth);
    }

    half3 GetWorldNormal(uint2 uv) {
	    half3 norm = LoadSceneNormals(uv);
	    return norm;
	}

    half3 GetWorldNormal(float2 uv) {
	    half3 norm = SampleSceneNormals(uv);
	    return norm;
	}

    half2 GetVelocity(float2 uv) {
		half2 mv = SAMPLE_TEXTURE2D_X_LOD(_MotionVectorTexture, sampler_PointClamp, uv, 0).xy;
        return mv;
    }

    half GetLuma(half3 rgb) {
        const half3 lum = half3(0.299, 0.587, 0.114);
        return dot(rgb, lum);
    }

    void GetAlbedoAndSpecularColors(float2 uv, out half3 albedo, out half3 specular) {
        half4 pixelGBuffer0 = SAMPLE_TEXTURE2D_X(_GBuffer0, sampler_PointClamp, uv);
        albedo = pixelGBuffer0.rgb;

        half3 pixelSpecular = SAMPLE_TEXTURE2D_X(_GBuffer1, sampler_PointClamp, uv).rgb;

        uint materialFlags = UnpackMaterialFlags(pixelGBuffer0.a);
        if ((materialFlags & kMaterialFlagSpecularSetup) != 0) {
            specular = pixelSpecular;
        } else {
            specular = pixelSpecular.rrr;
        }
    }
        
    half3 GetSpecularColor(float2 uv) {
        half3 albedo, specular;
        GetAlbedoAndSpecularColors(uv, albedo, specular);
        return specular;
    }

    /**
     * \brief Determine content that is out of range, such as skybox
     * \param rawDepth 
     * \return 
     */
    bool IsOutOfDepth(float rawDepth) {
        #if UNITY_REVERSED_Z
            return rawDepth <= 0;
		#else
            return rawDepth >= 1.0;
		#endif
    }

#endif // URP_SSGI_COMMON