Shader "Hidden/FRP/NormalReconstruct"
{
	HLSLINCLUDE
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
	#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
	ENDHLSL
	
	SubShader
	{
		Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
		LOD 100
		ZTest Always
		ZWrite Off
		Cull Off
		Fog {Mode Off}
		
		Pass
		{ // 0
			Name "Copy"
			
			HLSLPROGRAM

			#pragma multi_compile_local_fragment _SOURCE_DEPTH_LOW _SOURCE_DEPTH_MEDIUM _SOURCE_DEPTH_HIGH
			
			#pragma vertex Vert
			#pragma fragment Frag

			#define NORMALS_RECONSTRUCT4 1

			float4 _SourceSize;
			float4 _BlitTexture_TexelSize;
			float4 _ProjectionParams2;
			float4 _CameraViewTopLeftCorner[2];
			float4x4 _CameraViewProjections[2]; // This is different from UNITY_MATRIX_VP (platform-agnostic projection matrix is used). Handle both non-XR and XR modes.
			float4 _CameraViewXExtent[2];
			float4 _CameraViewYExtent[2];
			float4 _CameraViewZExtent[2];
			
			#if defined(USING_STEREO_MATRICES)
			    #define unity_eyeIndex unity_StereoEyeIndex
			#else
			    #define unity_eyeIndex 0
			#endif

			inline float FetchRawDepth(float2 uv) {
			    return SampleSceneDepth(uv);
			}

			inline float3 MinDiff(float3 P, float3 Pr, float3 Pl) {
			    float3 V1 = Pr - P;
			    float3 V2 = P - Pl;
			    return (dot(V1, V1) < dot(V2, V2)) ? V1 : V2;
			}

	// This returns a vector in world unit (not a position), from camera to the given point described by uv screen coordinate and depth (in absolute world unit).
	half3 ReconstructViewPos(float2 uv, float linearDepth)
	{
	    #if defined(_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
	        uv = RemapFoveatedRenderingDistort(uv);
	    #endif

	    // Screen is y-inverted.
	    uv.y = 1.0 - uv.y;

	    // view pos in world space
	    #if defined(_ORTHOGRAPHIC)
	        float zScale = linearDepth * _ProjectionParams.w; // divide by far plane
	        float3 viewPos = _CameraViewTopLeftCorner[unity_eyeIndex].xyz
	                            + _CameraViewXExtent[unity_eyeIndex].xyz * uv.x
	                            + _CameraViewYExtent[unity_eyeIndex].xyz * uv.y
	                            + _CameraViewZExtent[unity_eyeIndex].xyz * zScale;
	    #else
	        float zScale = linearDepth * _ProjectionParams2.x; // divide by near plane
	        float3 viewPos = _CameraViewTopLeftCorner[unity_eyeIndex].xyz
	                            + _CameraViewXExtent[unity_eyeIndex].xyz * uv.x
	                            + _CameraViewYExtent[unity_eyeIndex].xyz * uv.y;
	        viewPos *= zScale;
	    #endif

	    return half3(viewPos);
	}
			
			
	float GetLinearEyeDepth(float rawDepth)
	{
		#if defined(_ORTHOGRAPHIC)
		    return LinearDepthToEyeDepth(rawDepth);
		#else
		    return LinearEyeDepth(rawDepth, _ZBufferParams);
		#endif
	}
			
	float SampleAndGetLinearEyeDepth(float2 uv)
	{
	    const float rawDepth = SampleSceneDepth(uv);
	    return GetLinearEyeDepth(rawDepth);
	}

	// Try reconstructing normal accurately from depth buffer.
	// Low:    DDX/DDY on the current pixel
	// Medium: 3 taps on each direction | x | * | y |
	// High:   5 taps on each direction: | z | x | * | y | w |
	// https://atyuwen.github.io/posts/normal-reconstruction/
	// https://wickedengine.net/2019/09/22/improved-normal-reconstruction-from-depth/
	half3 ReconstructNormal(float2 uv, float linearDepth, float3 vpos, float2 pixelDensity)
	{
	    #if defined(_SOURCE_DEPTH_LOW)
	        return half3(normalize(cross(ddy(vpos), ddx(vpos))));
	    #else
	        float2 delta = float2(_SourceSize.zw * 2.0);

	        pixelDensity = rcp(pixelDensity);

	        // Sample the neighbour fragments
	        float2 lUV = float2(-delta.x, 0.0) * pixelDensity;
	        float2 rUV = float2(delta.x, 0.0) * pixelDensity;
	        float2 uUV = float2(0.0, delta.y) * pixelDensity;
	        float2 dUV = float2(0.0, -delta.y) * pixelDensity;

	        float3 l1 = float3(uv + lUV, 0.0); l1.z = SampleAndGetLinearEyeDepth(l1.xy); // Left1
	        float3 r1 = float3(uv + rUV, 0.0); r1.z = SampleAndGetLinearEyeDepth(r1.xy); // Right1
	        float3 u1 = float3(uv + uUV, 0.0); u1.z = SampleAndGetLinearEyeDepth(u1.xy); // Up1
	        float3 d1 = float3(uv + dUV, 0.0); d1.z = SampleAndGetLinearEyeDepth(d1.xy); // Down1

	        // Determine the closest horizontal and vertical pixels...
	        // horizontal: left = 0.0 right = 1.0
	        // vertical  : down = 0.0    up = 1.0
	        #if defined(_SOURCE_DEPTH_MEDIUM)
	             uint closest_horizontal = l1.z > r1.z ? 0 : 1;
	             uint closest_vertical   = d1.z > u1.z ? 0 : 1;
	        #else
	            float3 l2 = float3(uv + lUV * 2.0, 0.0); l2.z = SampleAndGetLinearEyeDepth(l2.xy); // Left2
	            float3 r2 = float3(uv + rUV * 2.0, 0.0); r2.z = SampleAndGetLinearEyeDepth(r2.xy); // Right2
	            float3 u2 = float3(uv + uUV * 2.0, 0.0); u2.z = SampleAndGetLinearEyeDepth(u2.xy); // Up2
	            float3 d2 = float3(uv + dUV * 2.0, 0.0); d2.z = SampleAndGetLinearEyeDepth(d2.xy); // Down2

	            const uint closest_horizontal = abs( (2.0 * l1.z - l2.z) - linearDepth) < abs( (2.0 * r1.z - r2.z) - linearDepth) ? 0 : 1;
	            const uint closest_vertical   = abs( (2.0 * d1.z - d2.z) - linearDepth) < abs( (2.0 * u1.z - u2.z) - linearDepth) ? 0 : 1;
	        #endif

	        // Calculate the triangle, in a counter-clockwize order, to
	        // use based on the closest horizontal and vertical depths.
	        // h == 0.0 && v == 0.0: p1 = left,  p2 = down
	        // h == 1.0 && v == 0.0: p1 = down,  p2 = right
	        // h == 1.0 && v == 1.0: p1 = right, p2 = up
	        // h == 0.0 && v == 1.0: p1 = up,    p2 = left
	        // Calculate the view space positions for the three points...
	        half3 P1;
	        half3 P2;
	        if (closest_vertical == 0)
	        {
	            P1 = half3(closest_horizontal == 0 ? l1 : d1);
	            P2 = half3(closest_horizontal == 0 ? d1 : r1);
	        }
	        else
	        {
	            P1 = half3(closest_horizontal == 0 ? u1 : r1);
	            P2 = half3(closest_horizontal == 0 ? l1 : u1);
	        }

	        // Use the cross product to calculate the normal...
	        return half3(normalize(cross(ReconstructViewPos(P2.xy, P2.z) - vpos, ReconstructViewPos(P1.xy, P1.z) - vpos)));
		    #endif
		}

		half3 SampleNormal(float2 uv, float linearDepth, float2 pixelDensity)
		{
	        float3 vpos = ReconstructViewPos(uv, linearDepth);
	        return ReconstructNormal(uv, linearDepth, vpos, pixelDensity);
		}

			float4 Frag(Varyings input) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
				#if defined(_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
			        float2 pixelDensity = RemapFoveatedRenderingDensity(RemapFoveatedRenderingDistort(uv));
			    #else
			        float2 pixelDensity = float2(1.0f, 1.0f);
			    #endif

				float rawDepth_o = SampleSceneDepth(uv);
				float linearDepth_o = GetLinearEyeDepth(rawDepth_o);
				float3 N = SampleNormal(uv, linearDepth_o, pixelDensity);
				return half4(N, 1);
			}
			
			ENDHLSL
		}

	}
}
