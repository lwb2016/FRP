Shader "FURPVolume/SkyDome"
{
	Properties
	{
		_Tint ("Tint Color", Color) = (.5, .5, .5, .5)
		[Gamma] _Exposure ("Exposure", Range(0, 8)) = 1.0
		_LightIntensity ("Light Intensity", Range(0, 8)) = 1.0
		[NoScaleOffset] _MainTex ("Texture", Cube) = "white" {}
		_Shadow("Shadow",Range(0,1)) = 0.5
	}
	SubShader
	{
		Tags { "Queue"="Background"  "RenderType"="Opaque"   "PreviewType"="Skybox" }
		LOD 100

		Pass
		{
			Tags
            {
                "LightMode" = "SRPDefaultUnlit"
            }
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			
			struct appdata
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float4 uv : TEXCOORD0;
				float3 uv2 : TEXCOORD1;
			};

			struct v2f
			{
				float3 normal : TEXCOORD2;
				float4 vertex : SV_POSITION;
				float4 pos : TEXCOORD3;
				float3 worldPos : TEXCOORD4;

			};

			TEXTURECUBE(_MainTex);
			SAMPLER(sampler_MainTex);
			half4 _MainTex_HDR;
			float _Shadow;
			half4 _Tint;
			half _Exposure;
			half _LightIntensity;

			
			v2f vert (appdata v)
			{
				v2f o;
				o.worldPos = TransformObjectToWorld(v.vertex.xyz);
				o.vertex = o.pos = mul(UNITY_MATRIX_VP, mul(unity_ObjectToWorld, float4(v.uv2, 1.0)));
				o.normal = v.uv.xyz;
				return o;
			}
			
			half4 frag (v2f i) : SV_Target
			{
				//half atten = LIGHT_ATTENUATION(i);
			 	float3 texcoord = i.normal;

				half4 shdowCoord = TransformWorldToShadowCoord(i.worldPos);
				Light light = GetMainLight(shdowCoord);
				half3 diffuse = (dot(light.direction, i.normal) * 0.5 + 0.5) * light.color * _LightIntensity;
				
				half atten = light.shadowAttenuation;

				// tex = SAMPLE_TEXTURECUBE_LOD (_MainTex, texcoord)*(1-(1-1)*_Shadow);
				half4 tex = SAMPLE_TEXTURECUBE_LOD (_MainTex, sampler_MainTex, texcoord, 0)*(1-(1-atten)*_Shadow);
				half3 c = DecodeHDREnvironment (tex, _MainTex_HDR);
				diffuse *= c;
				c = c * _Tint.rgb;
				c *= _Exposure;
				c += diffuse;
				return half4(c, 1) ;
			}
			ENDHLSL
		}
		
//
//		Pass
//		{
//            Name "DEFERRED"
//		Tags { "LightMode" = "Deferred"  }
//		LOD 100
//			CGPROGRAM
//            #pragma target 3.0
//            #pragma exclude_renderers nomrt
//			#pragma vertex vertDeferred
//			#pragma fragment frag
//            #pragma multi_compile_prepassfinal
//            //#pragma multi_compile_fwdbase
//			// make fog work
//			//#pragma multi_compile_fog
//			
//			#include "UnityCG.cginc"
//			#include "AutoLight.cginc"
//
//			samplerCUBE _MainTex;
//			half4 __MainTex_HDR;
//			
//
//	struct VertexInput
//	{
//	    float4 vertex   : POSITION;
//	    half3 normal    : NORMAL;
//	    float4 uv0      : TEXCOORD0;
//	    float3 uv1      : TEXCOORD1;
//
//	#ifdef _TANGENT_TO_WORLD
//	    half4 tangent   : TANGENT;
//	#endif
//	    UNITY_VERTEX_INPUT_INSTANCE_ID
//	};
//
//	struct VertexOutputDeferred
//	{
//	    UNITY_POSITION(pos);
//	    float4 tex                          : TEXCOORD0;
//	    half3 eyeVec                        : TEXCOORD1;
//	    half4 tangentToWorldAndPackedData[3]: TEXCOORD2;    // [3x3:tangentToWorld | 1x3:viewDirForParallax or worldPos]
//	    float4 vertex           : TEXCOORD5;    // SH or Lightmap UVs
//
//	    #if UNITY_REQUIRE_FRAG_WORLDPOS && !UNITY_PACK_WORLDPOS_WITH_TANGENT
//	        float3 posWorld                     : TEXCOORD6;
//	    #endif
//	   // half color           : TEXCOORD7;
//
//	    UNITY_VERTEX_OUTPUT_STEREO
//	};
//	half3 NormalizePerVertexNormal (float3 n) // takes float to avoid overflow
//	{
//	    #if (SHADER_TARGET < 30) || UNITY_STANDARD_SIMPLE
//	        return normalize(n);
//	    #else
//	        return n; // will normalize per-pixel instead
//	    #endif
//	}
//
//
//
//	VertexOutputDeferred vertDeferred (VertexInput v)
//	{
//	    UNITY_SETUP_INSTANCE_ID(v);
//	    VertexOutputDeferred o;
//	    UNITY_INITIALIZE_OUTPUT(VertexOutputDeferred, o);
//	    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
//
//	    //o.color = v.uv0.w;
//	    o.vertex =  v.vertex;
//	    float4 posWorld = mul(unity_ObjectToWorld, v.uv1);
//	    #if UNITY_REQUIRE_FRAG_WORLDPOS
//	        #if UNITY_PACK_WORLDPOS_WITH_TANGENT
//	            o.tangentToWorldAndPackedData[0].w = posWorld.x;
//	            o.tangentToWorldAndPackedData[1].w = posWorld.y;
//	            o.tangentToWorldAndPackedData[2].w = posWorld.z;
//	        #else
//	            o.posWorld = posWorld.xyz;
//	        #endif
//	    #endif
//	    o.pos = UnityObjectToClipPos(v.uv1);
//
//	    o.eyeVec = NormalizePerVertexNormal(posWorld.xyz - _WorldSpaceCameraPos);
//	    float3 normalWorld = UnityObjectToWorldNormal(v.normal);
//	    o.tex = float4(v.uv0.xyz,v.uv0.w);
//	    #ifdef _TANGENT_TO_WORLD
//	        float4 tangentWorld = float4(UnityObjectToWorldDir(v.tangent.xyz), v.tangent.w);
//
//	        float3x3 tangentToWorld = CreateTangentToWorldPerVertex(normalWorld, tangentWorld.xyz, tangentWorld.w);
//	        o.tangentToWorldAndPackedData[0].xyz = tangentToWorld[0];
//	        o.tangentToWorldAndPackedData[1].xyz = tangentToWorld[1];
//	        o.tangentToWorldAndPackedData[2].xyz = tangentToWorld[2];
//	    #else
//	        o.tangentToWorldAndPackedData[0].xyz = 0;
//	        o.tangentToWorldAndPackedData[1].xyz = 0;
//	        o.tangentToWorldAndPackedData[2].xyz = normalWorld;
//	    #endif
//
//
//	    #ifdef _PARALLAXMAP
//	        TANGENT_SPACE_ROTATION;
//	        half3 viewDirForParallax = mul (rotation, ObjSpaceViewDir(v.vertex));
//	        o.tangentToWorldAndPackedData[0].w = viewDirForParallax.x;
//	        o.tangentToWorldAndPackedData[1].w = viewDirForParallax.y;
//	        o.tangentToWorldAndPackedData[2].w = viewDirForParallax.z;
//	    #endif
//
//	    return o;
//	}
//
//
//
//			void frag (
//			    VertexOutputDeferred i,
//			    out half4 outGBuffer0 : SV_Target0,
//			    out half4 outGBuffer1 : SV_Target1,
//			    out half4 outGBuffer2 : SV_Target2,
//			    out half4 outEmission : SV_Target3          // RT3: emission (rgb), --unused-- (a)
//			#if defined(SHADOWS_SHADOWMASK) && (UNITY_ALLOWED_MRT_COUNT > 4)
//			    ,out half4 outShadowMask : SV_Target4       // RT4: shadowmask (rgba)
//			#endif
//			)
//			{
//				// half atten = LIGHT_ATTENUATION(i);
//			 	float3 texcoord = i.tex.xyz;
//
//				half4 tex = texCUBE (_MainTex, texcoord);
//				//float3 normal = PerPixelWorldNormal(i.tex,i.tangentToWorldAndPackedData);
//				float3 normal =normalize(i.tangentToWorldAndPackedData[2].xyz);
//
//			    // RT0: diffuse color (rgb), occlusion (a) - sRGB rendertarget
//			    // RT1: spec color (rgb), smoothness (a) - sRGB rendertarget
//			    // RT2: normal (rgb), --unused, very low precision-- (a)
//				outGBuffer0 = float4(0,0,0,0); // color
//				outGBuffer1 = float4(0,0,0,0); // reflective?
//
//				if(i.tex.w<0.1) normal = 0;
//				//outGBuffer2 = float4(float3(0,1,0)* 0.5f + 0.5f,0);
//				outGBuffer2 = float4(normal* 0.5f + 0.5f,0); // normal
//				//outEmission= 0 ;
//				outEmission = tex;
//				#ifndef UNITY_HDR_ON
//				    outEmission.rgb = exp2(-outEmission.rgb);
//				#endif
//
//			}
//			ENDCG
//		}

	}


    Fallback "VertexLit"
}
