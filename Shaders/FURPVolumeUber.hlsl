#ifndef UNIVERSAL_FERNVOLUMEUBER_INCLUDED
#define UNIVERSAL_FERNVOLUMEUBER_INCLUDED

// Includes
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ScreenCoordOverride.hlsl"
#if defined(HDR_ENCODING)
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/HDROutput.hlsl"
#endif
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/DebuggingFullscreen.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"


SAMPLER(sampler_BlitTexture);


#if _EDGEDETECION
	TEXTURE2D(_EdgeDetectionTexture);
	SAMPLER(sampler_EdgeDetectionTexture);
#endif

#if _DUALKAWASEBLUR
	TEXTURE2D(_DualKawaseBlurTex0);
	SAMPLER(sampler_DualKawaseBlurTex0);
	float4 _DualKawaseBlurTex0_TexelSize;
#endif

#if _SSGI
	TEXTURE2D(_SSGITexture);
	SAMPLER(sampler_SSGITexture);
#endif


TEXTURE2D(_FullCoCTexture);
SAMPLER(sampler_FullCoCTexture);

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

float4 _Edge_Threshold;
float3 _Edge_Color;
float4 _SourceSize;
half _BlurOffsetX;
half _BlurOffsetY;


//-----------URP Uber ------------

// Hardcoded dependencies to reduce the number of variants
#if _BLOOM_LQ || _BLOOM_HQ || _BLOOM_LQ_DIRT || _BLOOM_HQ_DIRT
	#define BLOOM
	#if _BLOOM_LQ_DIRT || _BLOOM_HQ_DIRT
		#define BLOOM_DIRT
	#endif
#endif

TEXTURE2D_X(_Bloom_Texture);
TEXTURE2D(_LensDirt_Texture);
TEXTURE2D(_Grain_Texture);
TEXTURE2D(_InternalLut);
TEXTURE2D(_UserLut);
TEXTURE2D(_BlueNoise_Texture);
TEXTURE2D_X(_OverlayUITexture);
TEXTURE2D_X(_DebugTexture);

float4 _Lut_Params;
float4 _UserLut_Params;
float4 _Bloom_Params;
float _Bloom_RGBM;
float4 _LensDirt_Params;
float _LensDirt_Intensity;
float4 _Distortion_Params1;
float4 _Distortion_Params2;
float _Chroma_Params;
half4 _Vignette_Params1;
float4 _Vignette_Params2;
#ifdef USING_STEREO_MATRICES
float4 _Vignette_ParamsXR;
#endif
float2 _Grain_Params;
float4 _Grain_TilingParams;
float4 _Bloom_Texture_TexelSize;
float4 _Dithering_Params;
float4 _HDROutputLuminanceParams;
float3 _BlackFade_Params;
float3 _BlackFade_CenterPos;
float3 _BlackFade_Shape;
float3 _BlackFade_Color;

#define DistCenter              _Distortion_Params1.xy
#define DistAxis                _Distortion_Params1.zw
#define DistTheta               _Distortion_Params2.x
#define DistSigma               _Distortion_Params2.y
#define DistScale               _Distortion_Params2.z
#define DistIntensity           _Distortion_Params2.w

#define ChromaAmount            _Chroma_Params.x

#define BloomIntensity          _Bloom_Params.x
#define BloomTint               _Bloom_Params.yzw
#define BloomRGBM               _Bloom_RGBM.x
#define LensDirtScale           _LensDirt_Params.xy
#define LensDirtOffset          _LensDirt_Params.zw
#define LensDirtIntensity       _LensDirt_Intensity.x

#define VignetteColor           _Vignette_Params1.xyz
#ifdef USING_STEREO_MATRICES
#define VignetteCenterEye0      _Vignette_ParamsXR.xy
#define VignetteCenterEye1      _Vignette_ParamsXR.zw
#else
#define VignetteCenter          _Vignette_Params2.xy
#endif
#define VignetteIntensity       _Vignette_Params2.z
#define VignetteSmoothness      _Vignette_Params2.w
#define VignetteRoundness       _Vignette_Params1.w

#define LutParams               _Lut_Params.xyz
#define PostExposure            _Lut_Params.w
#define UserLutParams           _UserLut_Params.xyz
#define UserLutContribution     _UserLut_Params.w

#define GrainIntensity          _Grain_Params.x
#define GrainResponse           _Grain_Params.y
#define GrainScale              _Grain_TilingParams.xy
#define GrainOffset             _Grain_TilingParams.zw

#define DitheringScale          _Dithering_Params.xy
#define DitheringOffset         _Dithering_Params.zw

#define MinNits                 _HDROutputLuminanceParams.x
#define MaxNits                 _HDROutputLuminanceParams.y
#define PaperWhite              _HDROutputLuminanceParams.z
#define OneOverPaperWhite       _HDROutputLuminanceParams.w

#define BF_Intensity            _BlackFade_Params.x
#define BF_Radius               _BlackFade_Params.y
#define BF_Soft                 _BlackFade_Params.z
//----------- END ------------

half3 ApplyBlackFade(half3 color, float2 uv)
{
	#if UNITY_REVERSED_Z
	real depth = SampleSceneDepth(uv);
	#else
	real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
	#endif
	float3 worldPos = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);

	float dis = distance(_BlackFade_CenterPos,worldPos * _BlackFade_Shape);
	dis = smoothstep(BF_Radius, BF_Radius + BF_Soft, dis);

	return  lerp(color, _BlackFade_Color.rgb, dis * BF_Intensity);
}

half ApplyBlackFade(float2 uv)
{
	#if UNITY_REVERSED_Z
	real depth = SampleSceneDepth(uv);
	#else
	real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
	#endif
	float3 worldPos = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);

	float dis = distance(_BlackFade_CenterPos,worldPos * _BlackFade_Shape);
	dis = smoothstep(BF_Radius, BF_Radius + BF_Soft, dis);

	return lerp(0, 1, dis * BF_Intensity);
}

#if SHADER_API_GLES
struct AttributesUber
{
	float4 positionOS       : POSITION;
	float2 uv               : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};
#else
struct AttributesUber
{
	uint vertexID : SV_VertexID;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};
#endif


struct VaryingsUber
{
	float4 positionCS : SV_POSITION;
	float2 texcoord   : TEXCOORD0;
	#if _DUALKAWASEBLUR
		float4 uv1 : TEXCOORD1;
		float4 uv2 : TEXCOORD2;
		float4 uv3 : TEXCOORD3;
		float4 uv4 : TEXCOORD4;
	#endif
	UNITY_VERTEX_OUTPUT_STEREO
};

VaryingsUber VertUber(AttributesUber input)
{
	VaryingsUber output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

	#if SHADER_API_GLES
		float4 pos = input.positionOS;
		float2 uv  = input.uv;
	#else
		float4 pos = GetFullScreenTriangleVertexPosition(input.vertexID);
		float2 uv  = GetFullScreenTriangleTexCoord(input.vertexID);
	#endif

	output.positionCS = pos;
	output.texcoord   = uv * _BlitScaleBias.xy + _BlitScaleBias.zw;

	#if _DUALKAWASEBLUR
		const float2 halfPixel = _DualKawaseBlurTex0_TexelSize.xy * 0.5;
		const float2 offset = float2(1.0 + _BlurOffsetX, 1.0 + _BlurOffsetY);
		output.uv1.xy = output.texcoord + float2(-halfPixel.x * 2.0, 0.0) * offset;
		output.uv1.zw = output.texcoord + float2(-halfPixel.x, halfPixel.y) * offset;
		output.uv2.xy = output.texcoord + float2(0.0, halfPixel.y * 2.0) * offset;
		output.uv2.zw = output.texcoord + halfPixel * offset;
		output.uv3.xy = output.texcoord + float2(halfPixel.x * 2.0, 0.0) * offset;
		output.uv3.zw = output.texcoord + float2(halfPixel.x, -halfPixel.y) * offset;
		output.uv4.xy = output.texcoord + float2(0.0, -halfPixel.y * 2.0) * offset;
		output.uv4.zw = output.texcoord - halfPixel * offset;
	#endif
	
	return output;
}

inline half4 FetchSceneColor(float2 uv) {
	//return LOAD_TEXTURE2D_X(_MainTex, positionSS); // load not supported on GLES2
	return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);
}

float2 DistortUV(float2 uv)
{
	// Note: this variant should never be set with XR
	#if _DISTORTION
	{
		uv = (uv - 0.5) * DistScale + 0.5;
		float2 ruv = DistAxis * (uv - 0.5 - DistCenter);
		float ru = length(float2(ruv));

		UNITY_BRANCH
		if (DistIntensity > 0.0)
		{
			float wu = ru * DistTheta;
			ru = tan(wu) * (rcp(ru * DistSigma));
			uv = uv + ruv * (ru - 1.0); 
		}
		else
		{
			ru = rcp(ru) * DistTheta * atan(ru * DistSigma);
			uv = uv + ruv * (ru - 1.0);
		}
	}
	#endif

	return uv;
}

half4 frag_MixAO(VaryingsUber input) : SV_Target{
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

	float2 uv = SCREEN_COORD_APPLY_SCALEBIAS(UnityStereoTransformScreenSpaceTex(input.texcoord));
	float2 uvDistorted = DistortUV(uv);
	float4 color = (0.0).xxxx;
	color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

	color.a = 1;
	
	return color;
}

half4 frag_beforeTAA(VaryingsUber input) : SV_Target
{
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

	float2 uv = SCREEN_COORD_APPLY_SCALEBIAS(UnityStereoTransformScreenSpaceTex(input.texcoord));
	float2 uvDistorted = DistortUV(uv);
	float4 color = (0.0).xxxx;

	#if _FURPDEBUG
		color = SAMPLE_TEXTURE2D(_DebugTexture, sampler_LinearClamp, uv);
		return color;
	#elif _FURPDEBUG_AO
		color = SAMPLE_TEXTURE2D(_DebugTexture, sampler_LinearClamp, uv).b;
		return color;
	#endif

	#if _SSGI
		color = SAMPLE_TEXTURE2D(_SSGITexture, sampler_SSGITexture, uv);
	#else
		color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv);
	#endif

	#if _EDGEDETECION
		half edgeDetect = SAMPLE_TEXTURE2D(_EdgeDetectionTexture, sampler_EdgeDetectionTexture, uv);
		color.rgb = lerp(color.rgb, _Edge_Color, edgeDetect * _Edge_Threshold.w);
	#endif


	#if defined(DEBUG_DISPLAY)
		half4 debugColor = 0;

		if(CanDebugOverrideOutputColor(half4(color, 1), uv, debugColor))
		{
			return debugColor;
		}
	#endif

	color.a = 1;
	
	return color;
}

half4 frag_BlackFade(VaryingsUber input) : SV_Target
{
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

	float2 uv = SCREEN_COORD_APPLY_SCALEBIAS(UnityStereoTransformScreenSpaceTex(input.texcoord));
	float2 uvDistorted = DistortUV(uv);
	float4 color = (0.0).xxxx;
	float fade = 1;

	#if UNITY_COLORSPACE_GAMMA
		{
		color = GetSRGBToLinear(color);
		}
	#endif

	#if defined(_BLACKFADE)
		//color.rgb = ApplyBlackFade(color.rgb,uvDistorted);
		fade = ApplyBlackFade(uvDistorted);
	#endif
	#if _GAMMA_20 && !UNITY_COLORSPACE_GAMMA
		{
		color = LinearToGamma20(color);
		}
	// Back to sRGB
	#elif UNITY_COLORSPACE_GAMMA || _LINEAR_TO_SRGB_CONVERSION
	{
		color = GetLinearToSRGB(color);
	}
	#endif

	return half4(0, 0, 0, fade);
}

half4 frag(VaryingsUber input) : SV_Target
{
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

	float2 uv = SCREEN_COORD_APPLY_SCALEBIAS(UnityStereoTransformScreenSpaceTex(input.texcoord));
	float2 uvDistorted = DistortUV(uv);
	float4 color = (0.0).xxxx;

	#if _FURPDEBUG
		color = SAMPLE_TEXTURE2D(_DebugTexture, sampler_LinearClamp, uv);
		return color;
	#elif _FURPDEBUG_AO
		color = SAMPLE_TEXTURE2D(_DebugTexture, sampler_LinearClamp, uv).b;
		return color;
	#endif
	
	#if _CHROMATIC_ABERRATION
	{
		// Very fast version of chromatic aberration from HDRP using 3 samples and hardcoded
		// spectral lut. Performs significantly better on lower end GPUs.
		float2 coords = 2.0 * uv - 1.0;
		float2 end = uv - coords * dot(coords, coords) * ChromaAmount;
		float2 delta = (end - uv) / 3.0;
		#if _SSGI
			half r = SAMPLE_TEXTURE2D_X(_SSGITexture, sampler_LinearClamp, SCREEN_COORD_REMOVE_SCALEBIAS(uvDistorted)                ).x;
			half g = SAMPLE_TEXTURE2D_X(_SSGITexture, sampler_LinearClamp, SCREEN_COORD_REMOVE_SCALEBIAS(DistortUV(delta + uv)      )).y;
			half b = SAMPLE_TEXTURE2D_X(_SSGITexture, sampler_LinearClamp, SCREEN_COORD_REMOVE_SCALEBIAS(DistortUV(delta * 2.0 + uv))).z;
		#else
			half r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, SCREEN_COORD_REMOVE_SCALEBIAS(uvDistorted)                ).x;
			half g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, SCREEN_COORD_REMOVE_SCALEBIAS(DistortUV(delta + uv)      )).y;
			half b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, SCREEN_COORD_REMOVE_SCALEBIAS(DistortUV(delta * 2.0 + uv))).z;
		#endif
		color.rgb = half3(r, g, b);
	}
	#else
		#if _SSGI
		{
			color = SAMPLE_TEXTURE2D(_SSGITexture, sampler_SSGITexture, uv);
			#if _SSGI_DEBUG
			return color;
			#endif
		}
		#else
			color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv);
		#endif
	#endif

	#if UNITY_COLORSPACE_GAMMA
		{
		color = GetSRGBToLinear(color);
		}
	#endif

	#if defined(BLOOM)
	{
		float2 uvBloom = uvDistorted;
		#if defined(_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
		uvBloom = RemapFoveatedRenderingDistort(uvBloom);
		#endif

		#if _BLOOM_HQ && !defined(SHADER_API_GLES)
			half4 bloom = SampleTexture2DBicubic(TEXTURE2D_X_ARGS(_Bloom_Texture, sampler_LinearClamp), SCREEN_COORD_REMOVE_SCALEBIAS(uvBloom), _Bloom_Texture_TexelSize.zwxy, (1.0).xx, unity_StereoEyeIndex);
		#else
			half4 bloom = SAMPLE_TEXTURE2D_X(_Bloom_Texture, sampler_LinearClamp, SCREEN_COORD_REMOVE_SCALEBIAS(uvBloom));
		#endif

		#if UNITY_COLORSPACE_GAMMA
			bloom.xyz *= bloom.xyz; // γ to linear
		#endif

		UNITY_BRANCH
		if (BloomRGBM > 0)
		{
			bloom.xyz = DecodeRGBM(bloom);
		}

		bloom.xyz *= BloomIntensity;
		color.rgb += bloom.xyz * BloomTint;

		#if defined(BLOOM_DIRT)
			{
			// UVs for the dirt texture should be DistortUV(uv * DirtScale + DirtOffset) but
			// considering we use a cover-style scale on the dirt texture the difference
			// isn't massive so we chose to save a few ALUs here instead in case lens
			// distortion is active.
			half3 dirt = SAMPLE_TEXTURE2D(_LensDirt_Texture, sampler_LinearClamp, uvDistorted * LensDirtScale + LensDirtOffset).xyz;
			dirt *= LensDirtIntensity;
			color.xyz += dirt * bloom.xyz;
			}
		#endif
	}
	#endif
	
	// To save on variants we'll use an uniform branch for vignette. Lower end platforms
	// don't like these but if we're running Uber it means we're running more expensive
	// effects anyway. Lower-end devices would limit themselves to on-tile compatible effect
	// and thus this shouldn't too much of a problem (famous last words).
	UNITY_BRANCH
	if (VignetteIntensity > 0)
	{
		#ifdef USING_STEREO_MATRICES
			// With XR, the views can use asymmetric FOV which will have the center of each
			// view be at a different location.
			const float2 VignetteCenter = unity_StereoEyeIndex == 0 ? VignetteCenterEye0 : VignetteCenterEye1;
		#endif

		color.rgb = ApplyVignette(color, uvDistorted, VignetteCenter, VignetteIntensity, VignetteRoundness, VignetteSmoothness, VignetteColor);
	}

	// Color grading is always enabled when post-processing/uber is active
	{
		color.rgb = ApplyColorGrading(color, PostExposure, TEXTURE2D_ARGS(_InternalLut, sampler_LinearClamp), LutParams, TEXTURE2D_ARGS(_UserLut, sampler_LinearClamp), UserLutParams, UserLutContribution);
	}

	// #if _FILM_GRAIN
	// {
	// color = ApplyGrain(color, uv, TEXTURE2D_ARGS(_Grain_Texture, sampler_LinearRepeat), GrainIntensity, GrainResponse, GrainScale, GrainOffset, OneOverPaperWhite);
	// }
	// #endif
	// When Unity is configured to use gamma color encoding, we ignore the request to convert to gamma 2.0 and instead fall back to sRGB encoding
	#if _GAMMA_20 && !UNITY_COLORSPACE_GAMMA
		{
		color = LinearToGamma20(color);
		}
	// Back to sRGB
	#elif UNITY_COLORSPACE_GAMMA || _LINEAR_TO_SRGB_CONVERSION
	{
		color = GetLinearToSRGB(color);
	}
	#endif

	// #if _DITHERING
	// {
	// 	color = ApplyDithering(color, uv, TEXTURE2D_ARGS(_BlueNoise_Texture, sampler_PointRepeat), DitheringScale, DitheringOffset, PaperWhite, OneOverPaperWhite);
	// 	// Assume color > 0 and prevent 0 - ditherNoise.
	// 	// Negative colors can cause problems if fed back to the postprocess via render to FP16 texture.
	// 	color = max(color, 0);
	// }
	// #endif

	#ifdef HDR_ENCODING
	{
		float4 uiSample = SAMPLE_TEXTURE2D_X(_OverlayUITexture, sampler_PointClamp, input.texcoord);
		color.rgb = SceneUIComposition(uiSample, color.rgb, PaperWhite, MaxNits);
		color.rgb = OETF(color.rgb, MaxNits);
	}
	#endif

	#if defined(DEBUG_DISPLAY)
		half4 debugColor = 0;

		if(CanDebugOverrideOutputColor(half4(color, 1), uv, debugColor))
		{
			return debugColor;
		}
	#endif

	
	
	#if _DUALKAWASEBLUR
		float4 dualBlurSum = SAMPLE_TEXTURE2D(_DualKawaseBlurTex0, sampler_DualKawaseBlurTex0, input.uv1.xy);
		dualBlurSum += SAMPLE_TEXTURE2D(_DualKawaseBlurTex0, sampler_DualKawaseBlurTex0, input.uv1.zw) * 2.0;
		dualBlurSum += SAMPLE_TEXTURE2D(_DualKawaseBlurTex0, sampler_DualKawaseBlurTex0, input.uv2.xy);
		dualBlurSum += SAMPLE_TEXTURE2D(_DualKawaseBlurTex0, sampler_DualKawaseBlurTex0, input.uv2.zw) * 2.0;
		dualBlurSum += SAMPLE_TEXTURE2D(_DualKawaseBlurTex0, sampler_DualKawaseBlurTex0, input.uv3.xy);
		dualBlurSum += SAMPLE_TEXTURE2D(_DualKawaseBlurTex0, sampler_DualKawaseBlurTex0, input.uv3.zw) * 2.0;
		dualBlurSum += SAMPLE_TEXTURE2D(_DualKawaseBlurTex0, sampler_DualKawaseBlurTex0, input.uv4.xy);
		dualBlurSum += SAMPLE_TEXTURE2D(_DualKawaseBlurTex0, sampler_DualKawaseBlurTex0, input.uv4.zw) * 2.0;
		dualBlurSum *= 0.0833;
		color.rgb = dualBlurSum.rgb;
		return color;
	#endif

	#if _EDGEDETECION
		half edgeDetect = SAMPLE_TEXTURE2D(_EdgeDetectionTexture, sampler_EdgeDetectionTexture, uv);
		color.rgb = lerp(color.rgb, _Edge_Color, edgeDetect * _Edge_Threshold.w);
	#endif

	color.a = 1;

	return color;
}

#endif
