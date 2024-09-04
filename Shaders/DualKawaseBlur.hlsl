#ifndef FERNRENDER_URP_DUALKAWASE
#define FERNRENDER_URP_DUALKAWASE

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

TEXTURE2D_X(_ReflectionTex_Depth); SAMPLER(sampler_ReflectionTex_Depth);
TEXTURE2D_X(_SourceTex);
SAMPLER(sampler_linear_clamp);
float4 _SourceTex_TexelSize;
half _BlurOffsetX;
half _BlurOffsetY;
half _AnisoOffset;
half _AnisoPower;

struct MeshData
{
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct V2F
{
	float4 positionCS : SV_POSITION;
	float2 uv : TEXCOORD0;
	UNITY_VERTEX_OUTPUT_STEREO
};

struct V2F_DownSample 
{
	float4 positionCS : SV_POSITION;
	float2 uv0 : TEXCOORD0;
	UNITY_VERTEX_OUTPUT_STEREO
};

struct V2F_UpSample
{
	float4 positionCS : SV_POSITION;
	float2 uv0 : TEXCOORD0;
	UNITY_VERTEX_OUTPUT_STEREO
};

V2F Vert(MeshData input, const uint vertexID : SV_VertexID)
{
	V2F output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
	
	output.positionCS = float4(
		vertexID <= 1 ? -1.0 : 3.0,
		vertexID == 1 ? 3.0 : -1.0,
		0.0, 1.0
	);
	output.uv = float2(
		vertexID <= 1 ? 0.0 : 2.0,
		vertexID == 1 ? 2.0 : 0.0
	);
	#if UNITY_UV_STARTS_AT_TOP
	output.uv.y = 1.0 - output.uv.y;
	#endif
	
	return output;
}

half4 Frag(const V2F input) : SV_TARGET
{
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
	half4 color = SAMPLE_TEXTURE2D_X_LOD(_SourceTex, sampler_linear_clamp, input.uv, 0);
	
	return SAMPLE_TEXTURE2D_X_LOD(_SourceTex, sampler_linear_clamp, input.uv, 0);
}

float DepthValue(float2 uv)
{
	#if _ANISOBLUR
		float depth = SAMPLE_TEXTURE2D_X_LOD(_ReflectionTex_Depth, sampler_ReflectionTex_Depth, uv, 0);
		#if !UNITY_REVERSED_Z
			depth = 1 - depth;
		#endif
		return clamp(1 - saturate(pow(depth+_AnisoOffset, _AnisoPower)), 0, 1);
	#else
		return 1;
	#endif
}

V2F_DownSample Vert_DownSample(MeshData input, const uint vertexID : SV_VertexID)
{
	V2F_DownSample output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
	
	output.positionCS = float4(
		vertexID <= 1 ? -1.0 : 3.0,
		vertexID == 1 ? 3.0 : -1.0,
		0.0, 1.0
	);
	output.uv0 = float2(
		vertexID <= 1 ? 0.0 : 2.0,
		vertexID == 1 ? 2.0 : 0.0
	);
	#if UNITY_UV_STARTS_AT_TOP
	output.uv0.y = 1.0 - output.uv0.y;
	#endif

	const float2 uv = output.uv0;
	
	return output;
}

half4 Frag_DownSample(const V2F_DownSample input) : SV_TARGET
{
	float2 uv = input.uv0;
	float4 uv1, uv2;
	float depth = DepthValue(uv);

	const float2 halfPixel = _SourceTex_TexelSize * 0.5;
	const float2 offset = float2(lerp(0, _BlurOffsetX, depth), lerp(0, _BlurOffsetY, depth));
	
	uv1.xy = uv - halfPixel * offset; 
	uv1.zw = uv + halfPixel * offset;
	
	uv2.xy = uv - float2(halfPixel.x, -halfPixel.y) * offset;
	uv2.zw = uv + float2(halfPixel.x, -halfPixel.y) * offset;
	 
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
	half4 sum = SAMPLE_TEXTURE2D_X_LOD(_SourceTex, sampler_linear_clamp, input.uv0, 0);
	sum.rgba *= 4;
	sum.rgba += SAMPLE_TEXTURE2D_X_LOD(_SourceTex, sampler_linear_clamp, uv1.xy, 0);
	sum.rgba += SAMPLE_TEXTURE2D_X_LOD(_SourceTex, sampler_linear_clamp, uv1.zw, 0);
	sum.rgba += SAMPLE_TEXTURE2D_X_LOD(_SourceTex, sampler_linear_clamp, uv2.xy, 0);
	sum.rgba += SAMPLE_TEXTURE2D_X_LOD(_SourceTex, sampler_linear_clamp, uv2.zw, 0);
	sum.rgba *= 0.125f;
	return sum;
}

V2F_UpSample Vert_UpSample(MeshData input, const uint vertexID : SV_VertexID)
{
	V2F_UpSample output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
	
	output.positionCS = float4(
		vertexID <= 1 ? -1.0 : 3.0,
		vertexID == 1 ? 3.0 : -1.0,
		0.0, 1.0
	);
	output.uv0 = float2(
		vertexID <= 1 ? 0.0 : 2.0,
		vertexID == 1 ? 2.0 : 0.0
	);
	#if UNITY_UV_STARTS_AT_TOP
	output.uv0.y = 1.0 - output.uv0.y;
	#endif


	return output;
}

half4 Frag_UpSample(const V2F_UpSample input) : SV_TARGET
{
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

	const float2 uv = input.uv0;
	float4 uv1, uv2, uv3, uv4;

	float depth = DepthValue(uv);
	
	const float2 halfPixel = _SourceTex_TexelSize * 0.5;
	const float2 offset = float2(lerp(0, _BlurOffsetX, depth), lerp(0, _BlurOffsetY, depth));
	
	uv1.xy = uv + float2(-halfPixel.x * 2.0, 0.0) * offset;
	uv1.zw = uv + float2(-halfPixel.x, halfPixel.y) * offset;
	
	uv2.xy = uv + float2(0.0, halfPixel.y * 2.0) * offset;
	uv2.zw = uv + halfPixel * offset;

	uv3.xy = uv + float2(halfPixel.x * 2.0, 0.0) * offset;
	uv3.zw = uv + float2(halfPixel.x, -halfPixel.y) * offset;
	
	uv4.xy = uv + float2(0.0, -halfPixel.y * 2.0) * offset; 
	uv4.zw = uv - halfPixel * offset;
	
	half4 sum = SAMPLE_TEXTURE2D_X_LOD(_SourceTex, sampler_linear_clamp, uv1.xy, 0);
	sum.rgba += SAMPLE_TEXTURE2D_X_LOD(_SourceTex, sampler_linear_clamp, uv1.zw, 0) * 2.0;
	sum.rgba += SAMPLE_TEXTURE2D_X_LOD(_SourceTex, sampler_linear_clamp, uv2.xy, 0);
	sum.rgba += SAMPLE_TEXTURE2D_X_LOD(_SourceTex, sampler_linear_clamp, uv2.zw, 0) * 2.0;
	sum.rgba += SAMPLE_TEXTURE2D_X_LOD(_SourceTex, sampler_linear_clamp, uv3.xy, 0);
	sum.rgba += SAMPLE_TEXTURE2D_X_LOD(_SourceTex, sampler_linear_clamp, uv3.zw, 0) * 2.0;
	sum.rgba += SAMPLE_TEXTURE2D_X_LOD(_SourceTex, sampler_linear_clamp, uv4.xy, 0);
	sum.rgba += SAMPLE_TEXTURE2D_X_LOD(_SourceTex, sampler_linear_clamp, uv4.zw, 0) * 2.0;
	sum.rgba *= 0.0833;
	//sum.a = 1;
	return sum;
}
#endif