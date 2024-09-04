#ifndef SSGI_URP_RAYCAST
#define SSGI_URP_RAYCAST

	#define BINARY_SEARCH_ITERATIONS 4
	TEXTURE2D_X(_PrevResolve);

	struct VaryingsRaycast {
		float4 positionCS : SV_POSITION;
		float4 uv  : TEXCOORD0;
		UNITY_VERTEX_INPUT_INSTANCE_ID
		UNITY_VERTEX_OUTPUT_STEREO
	};

	VaryingsRaycast VertRaycast(AttributesFS input) {
		VaryingsRaycast output;
		UNITY_SETUP_INSTANCE_ID(input);
		UNITY_TRANSFER_INSTANCE_ID(input, output);
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
		output.positionCS = float4(input.positionHCS.xyz, 1.0);

		#if UNITY_UV_STARTS_AT_TOP
			output.positionCS.y *= -1;
		#endif

		output.uv.xy = input.uv;
		float4 projPos = output.positionCS * 0.5;
		projPos.xy = projPos.xy + projPos.w;
		output.uv.zw = projPos.xy;
		return output;
	}


	float4 sposStart;
	float k0, q0;

	void PrepareRay(float2 uv, float3 rayStart) {
		float4 sposStart = mul(unity_CameraProjection, float4(rayStart, 1.0));
		#if _ORTHO_SUPPORT
			float4 orthoSposStart = float4(uv.xy * 2 - 1, rayStart.z, 1.0);
			sposStart = lerp(sposStart, orthoSposStart, unity_OrthoParams.w);
		#endif
		k0 = rcp(sposStart.w);
		q0 = rayStart.z * k0;
	}

	half4 Raycast(float2 uv, float3 rayStart, float3 rayDir, float jitterFactor) {

		float  rayLength = _RayData.y;
		rayDir *= jitterFactor;
		if (rayStart.z + rayDir.z * rayLength < _ProjectionParams.y) {
			rayLength = abs( (rayStart.z - _ProjectionParams.y) / rayDir.z );
		}
		float3 rayEnd = rayStart + rayDir * rayLength;

		float4 sposEnd = mul(unity_CameraProjection, float4(rayEnd, 1.0));
		#if _ORTHO_SUPPORT
			float4 orthoSposEnd = float4(rayEnd.xy * 2 - 1, rayEnd.z, 1.0);
			sposEnd = lerp(sposEnd, orthoSposEnd, unity_OrthoParams.w);
		#endif

		float k1 = rcp(sposEnd.w);
		float q1 = rayEnd.z * k1;
		float4 samplePosition = float4(uv, q0, k0);

		// length in pixels
		float2 uv1 = (sposEnd.xy * rcp(rayEnd.z) + 1.0) * 0.5;

		float2 duv = uv1 - uv;
		float2 duvPixel = abs(duv * _DownscaledDepthRT_TexelSize.zw);
		float pixelDistance = max(duvPixel.x, duvPixel.y);
		pixelDistance = max(1, pixelDistance);

		int sampleCount = (int)min(pixelDistance, _RayData.z);
		float4 sampleIncrement = float4(duv, q1-q0, k1-k0) * rcp(sampleCount);

		bool isHit = false;
		float depthAlongRay = 0;
		float sceneDepth;

		for (int k = 0; k < sampleCount; k++) {
			samplePosition += sampleIncrement;
			if (any(floor(samplePosition.xy)!=0)) return 0;
			sceneDepth = GetLinearEyeDownscaledDepth(samplePosition.xy);
			depthAlongRay = samplePosition.z / samplePosition.w;
			float depthDiff = depthAlongRay - sceneDepth;
			if (depthDiff > 0.02 && depthDiff < _RayData.w) {
				isHit = true;
				break;
			}
		}

		UNITY_BRANCH
		if (isHit) {
			float4 hitp = samplePosition;
			#if _SSGI_USES_BINARY_SEARCH
				if (samplePosition.z > 0) sampleIncrement = -sampleIncrement;
				float4 stepPincr = sampleIncrement;
				float reduction = 1.0;
				UNITY_UNROLL
				for (int j = 0; j < BINARY_SEARCH_ITERATIONS; j++) {
					reduction *= 0.5;
					samplePosition += stepPincr * reduction;
					sceneDepth = GetLinearEyeDownscaledDepth(samplePosition.xy);
					depthAlongRay = samplePosition.z / samplePosition.w;
					float depthDiff = depthAlongRay - sceneDepth;
					stepPincr = sign(depthDiff) * sampleIncrement;
					if (depthDiff > 0.02 && depthDiff < _RayData.w) {
						hitp = samplePosition;
					}
				}
			#endif

			half zdist = rayLength * (hitp.z / hitp.w - rayStart.z) / (0.0001 + rayEnd.z - rayStart.z);
			half3 indirect = SAMPLE_TEXTURE2D(_MainTex, sampler_PointClamp, hitp.xy).rgb;
			indirect = clamp(indirect, 0, 32);
			half distAtten = rcp(1.0 + zdist * zdist);
			half invDistSqrWeight = lerp(1.0, distAtten, _IndirectData.z);
			indirect *= invDistSqrWeight;

			return half4(indirect, 1.0);
		}

		return 0; // miss
	}


	float3 GetTangent(float3 v) {
		return abs(v.x) > abs(v.z) ? float3(-v.y, v.x, 0.0) : float3(0.0, -v.z, v.y);
	}

	// Cosine-Weighted Hemisphere Sampling
	float3 CosineWeightedForNormal(float2 noise, float3 norm, float3 tangent, float3 bitangent) {
		float phi = 2.0f * PI * noise.y;
		float cphi, sphi;
		sincos(phi, sphi, cphi);
		float sqrN = sqrt(noise.x);
		float x = cphi * sqrN;
		float y = sphi * sqrN;
		float z = sqrt(1.0f - noise.x);
		float3 v = tangent * x + bitangent * y + norm * z;
		return normalize(v);
	}

	float3 GetJitteredNormal(float2 uv, float3 noises, float3 norm, float goldenRatio) {
		// Use the golden ratio to get random noise
		// https://www.graphics.rwth-aachen.de/media/papers/jgt.pdf
		noises.x = frac(noises.x + goldenRatio);
		float3 tangent = normalize(GetTangent(norm));
		float3 bitangent = cross(tangent, norm);
		float3 jitteredNormal = CosineWeightedForNormal(noises.xz, norm, tangent, bitangent);
		return jitteredNormal;
	}

	half4 FragRaycast (VaryingsRaycast input) : SV_Target {
		UNITY_SETUP_INSTANCE_ID(input);
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		float2 uv = UnityStereoTransformScreenSpaceTex(input.uv.xy);
		
		float rawDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_PointClamp, input.uv.xy).r;

		if (IsOutOfDepth(rawDepth)) return 0;

		float3 rayStart = GetViewSpacePosition(input.uv.zw, rawDepth);
		float2 pos = uv * _SourceSize.xy;
		float3 normalWS = GetWorldNormal((uint2)pos);
		float3 normalVS = mul((float3x3)_WorldToViewDir, normalWS);
		normalVS.z *= -1.0;

		// jitter
		float screenNoise = InterleavedGradientNoise(pos, _SourceSize.w);
		float2 noise = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_PointRepeat, pos * _NoiseTex_TexelSize.xy, 0).xw;
		float3 noises = float3(noise.xy, screenNoise);
		float3 rayDir = GetJitteredNormal(uv, noises, normalVS, _SourceSize.z);
		
		float jitterFactor = noises.y * _ExtraData.x + 1.0;
		PrepareRay(uv, rayStart);
		half4 indirect = Raycast(uv, rayStart, rayDir, jitterFactor);
		indirect *= _IndirectData.x;
		half eyeDepth = RawToLinearEyeDepth(rawDepth);

		#if _REUSERAYTRACING
			//If the ambient light intensity of the current pixel is 0,
			//turn on reuse of the previous frame's result
			UNITY_BRANCH
			if (indirect.w == 0)  {
				float2 velocity = GetVelocity(uv);
                float2 prevUV = saturate(uv - velocity);
					half4 prevResolve = SAMPLE_TEXTURE2D_X_LOD(_PrevResolve, sampler_LinearClamp, prevUV, 0);
					half depthDiff = abs(prevResolve.w - eyeDepth);
					if (depthDiff < _TemporalData.y) {
						indirect.rgb = prevResolve.rgb * _IndirectData.w;
					}
			}
		#endif

		indirect.w = eyeDepth;

		return indirect;
	}


#endif // SSGI_URP_RAYCAST
