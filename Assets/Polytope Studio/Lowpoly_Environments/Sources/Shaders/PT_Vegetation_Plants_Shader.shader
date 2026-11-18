// Convertido a URP por NostalgIA: el original era un Surface Shader del pipeline Built-in
// (por eso los materiales se veían rosas). Se conservan las propiedades y la lógica de
// Amplify (vertexDataFunc y surf); solo cambian los pases. Funciones de compatibilidad en
// Assets/Polytope Studio/PT_URPCompat.hlsl.
Shader "Polytope Studio/PT_Vegetation_Plants_Shader"
{
    Properties
	{
		[NoScaleOffset]_BaseTexture("Base Texture", 2D) = "white" {}
		[Toggle]_CUSTOMCOLORSTINTING("CUSTOM COLORS  TINTING", Float) = 0
		[HDR]_TopColor("Top Color", Color) = (0,0.2178235,1,1)
		[HDR]_GroundColor("Ground Color", Color) = (1,0,0,1)
		[HDR]_Gradient("Gradient", Range( 0 , 10)) = 1.4
		_GradientPower("Gradient Power", Range( 0 , 10)) = 1
		_LeavesThickness("Leaves Thickness", Range( 0.1 , 0.95)) = 0.5
		_Smoothness("Smoothness", Range( 0 , 1)) = 0
		[Toggle(_TRANSLUCENCYONOFF_ON)] _TRANSLUCENCYONOFF("TRANSLUCENCY ON/OFF", Float) = 1
		[Header(Translucency)]
		_Translucency("Strength", Range( 0 , 50)) = 1
		_TransNormalDistortion("Normal Distortion", Range( 0 , 1)) = 0.1
		_TransScattering("Scaterring Falloff", Range( 1 , 50)) = 2
		_TransDirect("Direct", Range( 0 , 1)) = 1
		_TransAmbient("Ambient", Range( 0 , 1)) = 0.2
		_TransShadow("Shadow", Range( 0 , 1)) = 0.9
		[Toggle(_CUSTOMWIND_ON)] _CUSTOMWIND("CUSTOM WIND", Float) = 1
		[HideInInspector]_MaskClipValue("Mask Clip Value", Range( 0 , 1)) = 0.5
		_WindMovement("Wind Movement", Range( 0 , 10)) = 0.5
		_WindDensity("Wind Density", Range( 0 , 5)) = 3.3
		_WindStrength("Wind Strength", Range( 0 , 1)) = 0.3
		[Toggle(_SNOWONOFF_ON)] _SNOWONOFF("SNOW ON/OFF", Float) = 0
		_SnowGradient("Snow Gradient", Range( 0 , 1)) = 0.83
		_SnowCoverage("Snow Coverage", Range( 0 , 1)) = 0.45
		_SnowAmount("Snow Amount", Range( 0 , 1)) = 1
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout"  "Queue" = "Geometry+0" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/Polytope Studio/PT_URPCompat.hlsl"
        

        // ---- Código original (Amplify) ----

		struct Input
		{
			float2 uv_texcoord;
			float3 worldPos;
			float3 worldNormal;
		};

		struct SurfaceOutputStandardCustom
		{
			half3 Albedo;
			half3 Normal;
			half3 Emission;
			half Metallic;
			half Smoothness;
			half Occlusion;
			half Alpha;
			half3 Translucency;
		};

		uniform float _WindMovement;
		uniform float _WindDensity;
		uniform float _WindStrength;
		uniform float _CUSTOMCOLORSTINTING;
		uniform sampler2D _BaseTexture;
		uniform float4 _GroundColor;
		uniform float4 _TopColor;
		uniform float _Gradient;
		uniform float _GradientPower;
		uniform float _SnowAmount;
		uniform float _SnowGradient;
		uniform float _SnowCoverage;
		uniform float _Smoothness;
		uniform half _Translucency;
		uniform half _TransNormalDistortion;
		uniform half _TransScattering;
		uniform half _TransDirect;
		uniform half _TransAmbient;
		uniform half _TransShadow;
		uniform float _LeavesThickness;
		uniform float _MaskClipValue;


		float3 mod2D289( float3 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }

		float2 mod2D289( float2 x ) { return x - floor( x * ( 1.0 / 289.0 ) ) * 289.0; }

		float3 permute( float3 x ) { return mod2D289( ( ( x * 34.0 ) + 1.0 ) * x ); }

		float snoise( float2 v )
		{
			const float4 C = float4( 0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439 );
			float2 i = floor( v + dot( v, C.yy ) );
			float2 x0 = v - i + dot( i, C.xx );
			float2 i1;
			i1 = ( x0.x > x0.y ) ? float2( 1.0, 0.0 ) : float2( 0.0, 1.0 );
			float4 x12 = x0.xyxy + C.xxzz;
			x12.xy -= i1;
			i = mod2D289( i );
			float3 p = permute( permute( i.y + float3( 0.0, i1.y, 1.0 ) ) + i.x + float3( 0.0, i1.x, 1.0 ) );
			float3 m = max( 0.5 - float3( dot( x0, x0 ), dot( x12.xy, x12.xy ), dot( x12.zw, x12.zw ) ), 0.0 );
			m = m * m;
			m = m * m;
			float3 x = 2.0 * frac( p * C.www ) - 1.0;
			float3 h = abs( x ) - 0.5;
			float3 ox = floor( x + 0.5 );
			float3 a0 = x - ox;
			m *= 1.79284291400159 - 0.85373472095314 * ( a0 * a0 + h * h );
			float3 g;
			g.x = a0.x * x0.x + h.x * x0.y;
			g.yz = a0.yz * x12.xz + h.yz * x12.yw;
			return 130.0 * dot( m, g );
		}


		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			float3 ase_vertex3Pos = v.vertex.xyz;
			float simplePerlin2D308 = snoise( (ase_vertex3Pos*1.0 + ( _Time.y * _WindMovement )).xy*_WindDensity );
			simplePerlin2D308 = simplePerlin2D308*0.5 + 0.5;
			float4 appendResult316 = (float4(( ( ( ( simplePerlin2D308 - 0.5 ) / 10.0 ) * _WindStrength ) + ase_vertex3Pos.x ) , ase_vertex3Pos.y , ase_vertex3Pos.z , 1.0));
			float4 lerpResult317 = lerp( float4( ase_vertex3Pos , 0.0 ) , appendResult316 , ( ase_vertex3Pos.y * 2.0 ));
			float4 transform318 = mul(unity_WorldToObject,float4( _WorldSpaceCameraPos , 0.0 ));
			float4 temp_cast_4 = (transform318.w).xxxx;
			#ifdef _CUSTOMWIND_ON
				float4 staticSwitch320 = ( lerpResult317 - temp_cast_4 );
			#else
				float4 staticSwitch320 = float4( ase_vertex3Pos , 0.0 );
			#endif
			float4 LOCALWIND353 = staticSwitch320;
			v.vertex.xyz = LOCALWIND353.xyz;
			v.vertex.w = 1;
		}

		

		

		void surf( Input i , inout SurfaceOutputStandardCustom o )
		{
			float2 uv_BaseTexture2 = i.uv_texcoord;
			float4 tex2DNode2 = tex2D( _BaseTexture, uv_BaseTexture2 );
			float clampResult738 = clamp( pow( ( i.uv_texcoord.y * _Gradient ) , _GradientPower ) , 0.0 , 1.0 );
			float4 lerpResult557 = lerp( _GroundColor , _TopColor , clampResult738);
			float4 GRADIENT558 = lerpResult557;
			float4 blendOpSrc18 = tex2DNode2;
			float4 blendOpDest18 = GRADIENT558;
			float4 lerpBlendMode18 = lerp(blendOpDest18,( blendOpSrc18 * blendOpDest18 ),0.0);
			float4 COLOR502 = (( _CUSTOMCOLORSTINTING )?( lerpBlendMode18 ):( tex2DNode2 ));
			float3 ase_worldPos = i.worldPos;
			float3 ase_worldViewDir = normalize( UnityWorldSpaceViewDir( ase_worldPos ) );
			float3 ase_worldNormal = i.worldNormal;
			float4 color443 = IsGammaSpace() ? float4(1,1,1,0) : float4(1,1,1,0);
			float fresnelNdotV454 = dot( ase_worldNormal, ase_worldViewDir );
			float fresnelNode454 = ( 0.11 + 1.0 * pow( 1.0 - fresnelNdotV454, color443.r ) );
			float smoothstepResult531 = smoothstep( 0.0 , _SnowGradient , ( ( 1.0 - ( i.uv_texcoord.y * 0.65 ) ) + (-1.0 + (_SnowCoverage - 0.0) * (1.0 - -1.0) / (1.0 - 0.0)) ));
			float SNOW489 = ( ( (0.0 + (_SnowAmount - 0.0) * (10.0 - 0.0) / (1.0 - 0.0)) * fresnelNode454 ) * smoothstepResult531 );
			float4 temp_cast_1 = (( SNOW489 + 0.0 )).xxxx;
			#ifdef _SNOWONOFF_ON
				float4 staticSwitch372 = temp_cast_1;
			#else
				float4 staticSwitch372 = COLOR502;
			#endif
			o.Albedo = staticSwitch372.rgb;
			o.Smoothness = _Smoothness;
			#ifdef _TRANSLUCENCYONOFF_ON
				float4 staticSwitch493 = ( COLOR502 * 1.0 );
			#else
				float4 staticSwitch493 = float4( 0,0,0,0 );
			#endif
			float4 TRANSLUCENCY497 = staticSwitch493;
			o.Translucency = TRANSLUCENCY497.rgb;
			o.Alpha = 1;
			float GENERALALPHA505 = tex2DNode2.a;
			float ALPHACUTOFF496 = ( 1.0 - step( GENERALALPHA505 , ( 1.0 - _LeavesThickness ) ) );
			clip( ALPHACUTOFF496 - _MaskClipValue );
		}

		
        // ---- Pases de URP ----
        struct PTVaryings
        {
            float4 positionCS : SV_POSITION;
            float4 uv01 : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            float3 normalWS : TEXCOORD2;
            float4 tangentWS : TEXCOORD3;
            float4 screenPos : TEXCOORD4;
            half4 color : COLOR;
            float fogFactor : TEXCOORD5;
            DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 6);
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        float3 _LightDirection;
        float3 _LightPosition;

        PTVaryings PTVertCommon(appdata_full v)
        {
            PTVaryings OUT = (PTVaryings)0;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_TRANSFER_INSTANCE_ID(v, OUT);
            Input vin; vertexDataFunc(v, vin);
            VertexPositionInputs p = GetVertexPositionInputs(v.vertex.xyz);
            VertexNormalInputs n = GetVertexNormalInputs(v.normal, v.tangent);
            OUT.positionCS = p.positionCS;
            OUT.positionWS = p.positionWS;
            OUT.normalWS = n.normalWS;
            OUT.tangentWS = float4(n.tangentWS, v.tangent.w * GetOddNegativeScale());
            OUT.uv01 = float4(v.texcoord.xy, v.texcoord1.xy);
            OUT.color = v.color;
            OUT.screenPos = ComputeScreenPos(p.positionCS);
            OUT.fogFactor = ComputeFogFactor(p.positionCS.z);
            OUTPUT_LIGHTMAP_UV(v.texcoord1.xy, unity_LightmapST, OUT.lightmapUV);
            OUTPUT_SH(OUT.normalWS, OUT.vertexSH);
            return OUT;
        }

        PTVaryings PTVert(appdata_full v) { return PTVertCommon(v); }

        PTVaryings PTShadowVert(appdata_full v)
        {
            PTVaryings OUT = PTVertCommon(v);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 lightDir = normalize(_LightPosition - OUT.positionWS);
            #else
            float3 lightDir = _LightDirection;
            #endif
            float4 cs = TransformWorldToHClip(ApplyShadowBias(OUT.positionWS, OUT.normalWS, lightDir));
            #if UNITY_REVERSED_Z
            cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
            #else
            cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
            #endif
            OUT.positionCS = cs;
            return OUT;
        }

        half4 PTDepthFrag(PTVaryings IN) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(IN);
            float3 N = normalize(IN.normalWS);
            float3 T = normalize(IN.tangentWS.xyz);
            float3 B = cross(N, T) * IN.tangentWS.w;
            Input i = (Input)0;
            i.uv_texcoord = IN.uv01.xy;
            i.worldPos = IN.positionWS;
            i.worldNormal = N;
            SurfaceOutputStandardCustom o = (SurfaceOutputStandardCustom)0;
            o.Normal = float3(0, 0, 1);
            surf(i, o);
            return 0;
        }

        half4 PTDepthNormalsFrag(PTVaryings IN, bool isFront : SV_IsFrontFace) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(IN);
            float3 N = normalize(IN.normalWS);
            float3 T = normalize(IN.tangentWS.xyz);
            float3 B = cross(N, T) * IN.tangentWS.w;
            Input i = (Input)0;
            i.uv_texcoord = IN.uv01.xy;
            i.worldPos = IN.positionWS;
            i.worldNormal = N;
            SurfaceOutputStandardCustom o = (SurfaceOutputStandardCustom)0;
            o.Normal = float3(0, 0, 1);
            surf(i, o);
            float3 nrm = normalize(IN.normalWS);
            if (!isFront) nrm = -nrm;
            return half4(nrm, 0);
        }

        half4 PTForwardFrag(PTVaryings IN, bool isFront : SV_IsFrontFace) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(IN);
            float3 N = normalize(IN.normalWS);
            if (!isFront) N = -N;
            float3 T = normalize(IN.tangentWS.xyz);
            float3 B = cross(N, T) * IN.tangentWS.w;
            Input i = (Input)0;
            i.uv_texcoord = IN.uv01.xy;
            i.worldPos = IN.positionWS;
            i.worldNormal = N;

            SurfaceOutputStandardCustom o = (SurfaceOutputStandardCustom)0;
            o.Normal = float3(0, 0, 1);
            o.Occlusion = 1;
            o.Alpha = 1;
            surf(i, o);

            float3 nWS = N;

            InputData d = (InputData)0;
            d.positionWS = IN.positionWS;
            d.positionCS = IN.positionCS;
            d.normalWS = nWS;
            d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
            d.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
            d.fogCoord = IN.fogFactor;
            d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
            d.bakedGI = SAMPLE_GI(IN.lightmapUV, IN.vertexSH, nWS);
            d.shadowMask = SAMPLE_SHADOWMASK(IN.lightmapUV);

            SurfaceData s = (SurfaceData)0;
            s.albedo = o.Albedo;
            s.metallic = o.Metallic;
            s.smoothness = o.Smoothness;
            s.occlusion = o.Occlusion;
            s.emission = o.Emission;
            s.alpha = o.Alpha;
            s.normalTS = float3(0, 0, 1);

            half4 color = UniversalFragmentPBR(d, s);
            #ifdef _TRANSLUCENCYONOFF_ON
            {   // Translucidez de las hojas (la misma fórmula que el LightingStandardCustom original).
                Light mainLight = GetMainLight(d.shadowCoord, d.positionWS, d.shadowMask);
                half3 lightAtten = lerp(mainLight.color, mainLight.color * mainLight.shadowAttenuation, _TransShadow);
                half3 transDir = mainLight.direction + nWS * _TransNormalDistortion;
                half transVdotL = pow(saturate(dot(d.viewDirectionWS, -transDir)), _TransScattering);
                half3 trans = lightAtten * (transVdotL * _TransDirect + d.bakedGI * _TransAmbient) * o.Translucency;
                color.rgb += o.Albedo * trans * _Translucency;
            }
            #endif
            color.rgb = MixFog(color.rgb, IN.fogFactor);
            return half4(color.rgb, 1);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex PTVert
            #pragma fragment PTForwardFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma shader_feature _CUSTOMWIND_ON
            #pragma shader_feature_local _SNOWONOFF_ON
            #pragma shader_feature_local _TRANSLUCENCYONOFF_ON
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex PTShadowVert
            #pragma fragment PTDepthFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature _CUSTOMWIND_ON
            #pragma shader_feature_local _SNOWONOFF_ON
            #pragma shader_feature_local _TRANSLUCENCYONOFF_ON
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex PTVert
            #pragma fragment PTDepthFrag
            #pragma multi_compile_instancing
            #pragma shader_feature _CUSTOMWIND_ON
            #pragma shader_feature_local _SNOWONOFF_ON
            #pragma shader_feature_local _TRANSLUCENCYONOFF_ON
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex PTVert
            #pragma fragment PTDepthNormalsFrag
            #pragma multi_compile_instancing
            #pragma shader_feature _CUSTOMWIND_ON
            #pragma shader_feature_local _SNOWONOFF_ON
            #pragma shader_feature_local _TRANSLUCENCYONOFF_ON
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
