// Convertido a URP por NostalgIA: el original era un Surface Shader del pipeline Built-in
// (por eso los materiales se veían rosas). Se conservan las propiedades y la lógica de
// Amplify (vertexDataFunc y surf); solo cambian los pases. Funciones de compatibilidad en
// Assets/Polytope Studio/PT_URPCompat.hlsl.
Shader "Polytope Studio/PT_Rock_Shader"
{
    Properties
	{
		[NoScaleOffset]_BaseTexture("Base Texture", 2D) = "white" {}
		_Smoothness("Smoothness", Range( 0 , 1)) = 0.2
		[Toggle(_GRADIENTONOFF_ON)] _GRADIENTONOFF("GRADIENT  ON/OFF", Float) = 0
		[HDR]_TopColor("Top Color", Color) = (0.4811321,0.4036026,0.2382966,1)
		[HDR]_GroundColor("Ground Color", Color) = (0.08490568,0.05234205,0.04846032,1)
		[HideInInspector]_SnowDirection("Snow Direction", Vector) = (0.1,1,0.1,0)
		_Gradient("Gradient ", Range( 0 , 1)) = 1
		_GradientPower("Gradient Power", Range( 0 , 10)) = 1
		[Toggle]_WorldObjectGradient("World/Object Gradient", Float) = 1
		[Toggle(_DECALSONOFF_ON)] _DECALSONOFF("DECALS ON/OFF", Float) = 0
		[NoScaleOffset]_DecalsTexture("Decals Texture", 2D) = "white" {}
		_DecalsColor("Decals Color", Color) = (0,0,0,0)
		[Toggle]_DECALEMISSIONONOFF("DECAL EMISSION ON/OFF", Float) = 1
		[HDR]_DecakEmissionColor("Decak Emission Color", Color) = (1,0.9248579,0,0)
		_DecalEmissionIntensity("Decal Emission Intensity", Range( 0 , 10)) = 4
		[Toggle]_ANIMATEDECALEMISSIONONOFF("ANIMATE DECAL EMISSION ON/OFF", Float) = 1
		[Toggle(_DETAILTEXTUREONOFF_ON)] _DETAILTEXTUREONOFF("DETAIL TEXTURE  ON/OFF", Float) = 0
		[NoScaleOffset]_DetailTexture("Detail Texture", 2D) = "white" {}
		_DetailTextureTiling("Detail Texture Tiling", Range( 0.1 , 10)) = 0.5
		[Toggle(_SNOWONOFF_ON)] _SNOWONOFF("SNOW ON/OFF", Float) = 0
		_SnowCoverage("Snow Coverage", Range( 0 , 1)) = 0.46
		_SnowAmount("Snow Amount", Range( 0 , 1)) = 1
		_SnowFade("Snow Fade", Range( 0 , 1)) = 0.32
		[Toggle]_SnowNoiseOnOff("Snow Noise On/Off", Float) = 1
		_SnowNoiseScale("Snow Noise Scale", Range( 0 , 100)) = 87.23351
		_SnowNoiseContrast("Snow Noise Contrast", Range( 0 , 1)) = 0.002
		[HideInInspector]_Vector1("Vector 1", Vector) = (0,1,0,0)
		[Toggle(_TOPPROJECTIONONOFF_ON)] _TOPPROJECTIONONOFF("TOP PROJECTION ON/OFF", Float) = 0
		[NoScaleOffset]_TopProjectionTexture("Top Projection Texture", 2D) = "white" {}
		_TopProjectionTextureTiling("Top Projection Texture Tiling", Range( 0.1 , 10)) = 0.5
		_TopProjectionTextureCoverage("Top Projection Texture  Coverage", Range( 0 , 1)) = 1
		[HDR]_OreColor("Ore Color", Color) = (1,0.9248579,0,0)
		[Toggle]_OREEMISSIONONOFF("ORE EMISSION ON/OFF", Float) = 0
		[HDR]_OreEmissionColor("Ore Emission Color", Color) = (1,0.9248579,0,0)
		_OreEmissionIntensity("Ore Emission Intensity", Range( 0 , 10)) = 1
		[Toggle]_ANIMATEOREEMISSIONONOFF("ANIMATE ORE  EMISSION ON/OFF", Float) = 0
		[HideInInspector] _texcoord2( "", 2D ) = "white" {}
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

    SubShader
    {
        Tags { "RenderType" = "Opaque"  "Queue" = "Geometry+0" "IsEmissive" = "true" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/Polytope Studio/PT_URPCompat.hlsl"
        

        // ---- Código original (Amplify) ----

		#ifdef UNITY_PASS_SHADOWCASTER
			#undef INTERNAL_DATA
			#undef WorldReflectionVector
			#undef WorldNormalVector
			#define INTERNAL_DATA half3 internalSurfaceTtoW0; half3 internalSurfaceTtoW1; half3 internalSurfaceTtoW2;
			#define WorldReflectionVector(data,normal) reflect (data.worldRefl, half3(dot(data.internalSurfaceTtoW0,normal), dot(data.internalSurfaceTtoW1,normal), dot(data.internalSurfaceTtoW2,normal)))
			#define WorldNormalVector(data,normal) half3(dot(data.internalSurfaceTtoW0,normal), dot(data.internalSurfaceTtoW1,normal), dot(data.internalSurfaceTtoW2,normal))
		#endif
		struct Input
		{
			float2 uv_texcoord;
			float3 worldPos;
			float3 worldNormal;
			INTERNAL_DATA
			float2 uv2_texcoord2;
			float4 vertexColor : COLOR;
		};

		uniform sampler2D _BaseTexture;
		uniform float4 _GroundColor;
		uniform float4 _TopColor;
		uniform float _WorldObjectGradient;
		uniform float _Gradient;
		uniform float _GradientPower;
		uniform sampler2D _DetailTexture;
		uniform float _DetailTextureTiling;
		uniform float4 _DecalsColor;
		uniform sampler2D _DecalsTexture;
		uniform sampler2D _TopProjectionTexture;
		uniform float _TopProjectionTextureTiling;
		uniform float3 _Vector1;
		uniform float _TopProjectionTextureCoverage;
		uniform float _SnowNoiseOnOff;
		uniform float _SnowAmount;
		uniform float _SnowFade;
		uniform float _SnowCoverage;
		uniform float3 _SnowDirection;
		uniform float _SnowNoiseScale;
		uniform float _SnowNoiseContrast;
		uniform float4 _OreColor;
		uniform float _DECALEMISSIONONOFF;
		uniform float _DecalEmissionIntensity;
		uniform float _ANIMATEDECALEMISSIONONOFF;
		uniform float4 _DecakEmissionColor;
		uniform float _OREEMISSIONONOFF;
		uniform float _OreEmissionIntensity;
		uniform float _ANIMATEOREEMISSIONONOFF;
		uniform float4 _OreEmissionColor;
		uniform float _Smoothness;


		inline float4 TriplanarSampling173( sampler2D topTexMap, sampler2D midTexMap, sampler2D botTexMap, float3 worldPos, float3 worldNormal, float falloff, float2 tiling, float3 normalScale, float3 index )
		{
			float3 projNormal = ( pow( abs( worldNormal ), falloff ) );
			projNormal /= ( projNormal.x + projNormal.y + projNormal.z ) + 0.00001;
			float3 nsign = sign( worldNormal );
			float negProjNormalY = max( 0, projNormal.y * -nsign.y );
			projNormal.y = max( 0, projNormal.y * nsign.y );
			half4 xNorm; half4 yNorm; half4 yNormN; half4 zNorm;
			xNorm  = tex2D( midTexMap, tiling * worldPos.zy * float2(  nsign.x, 1.0 ) );
			yNorm  = tex2D( topTexMap, tiling * worldPos.xz * float2(  nsign.y, 1.0 ) );
			yNormN = tex2D( botTexMap, tiling * worldPos.xz * float2(  nsign.y, 1.0 ) );
			zNorm  = tex2D( midTexMap, tiling * worldPos.xy * float2( -nsign.z, 1.0 ) );
			return xNorm * projNormal.x + yNorm * projNormal.y + yNormN * negProjNormalY + zNorm * projNormal.z;
		}


		inline float4 TriplanarSampling525( sampler2D topTexMap, float3 worldPos, float3 worldNormal, float falloff, float2 tiling, float3 normalScale, float3 index )
		{
			float3 projNormal = ( pow( abs( worldNormal ), falloff ) );
			projNormal /= ( projNormal.x + projNormal.y + projNormal.z ) + 0.00001;
			float3 nsign = sign( worldNormal );
			half4 xNorm; half4 yNorm; half4 zNorm;
			xNorm = tex2D( topTexMap, tiling * worldPos.zy * float2(  nsign.x, 1.0 ) );
			yNorm = tex2D( topTexMap, tiling * worldPos.xz * float2(  nsign.y, 1.0 ) );
			zNorm = tex2D( topTexMap, tiling * worldPos.xy * float2( -nsign.z, 1.0 ) );
			return xNorm * projNormal.x + yNorm * projNormal.y + zNorm * projNormal.z;
		}


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


		void surf( Input i , inout SurfaceOutputStandard o )
		{
			o.Normal = float3(0,0,1);
			float2 uv_BaseTexture490 = i.uv_texcoord;
			float4 BASETEXTURE498 = tex2D( _BaseTexture, uv_BaseTexture490 );
			float3 ase_worldPos = i.worldPos;
			float4 ase_vertex4Pos = mul( unity_WorldToObject, float4( i.worldPos , 1 ) );
			float clampResult627 = clamp( pow( ( ( (( _WorldObjectGradient )?( ase_vertex4Pos.y ):( ase_worldPos.y )) + 1.5 ) * _Gradient ) , _GradientPower ) , -1.0 , 1.0 );
			float4 lerpResult629 = lerp( _GroundColor , _TopColor , clampResult627);
			float4 Gradient630 = lerpResult629;
			float4 color644 = IsGammaSpace() ? float4(0.8962264,0.8962264,0.8962264,0) : float4(0.7799658,0.7799658,0.7799658,0);
			float4 blendOpSrc643 = BASETEXTURE498;
			float4 blendOpDest643 = color644;
			#ifdef _GRADIENTONOFF_ON
				float4 staticSwitch634 = ( Gradient630 * ( saturate(  (( blendOpSrc643 > 0.5 ) ? ( 1.0 - ( 1.0 - 2.0 * ( blendOpSrc643 - 0.5 ) ) * ( 1.0 - blendOpDest643 ) ) : ( 2.0 * blendOpSrc643 * blendOpDest643 ) ) )) );
			#else
				float4 staticSwitch634 = BASETEXTURE498;
			#endif
			float2 temp_cast_0 = (_DetailTextureTiling).xx;
			float3 ase_worldNormal = WorldNormalVector( i, float3( 0, 0, 1 ) );
			float4 triplanar173 = TriplanarSampling173( _DetailTexture, _DetailTexture, _DetailTexture, ase_worldPos, ase_worldNormal, 1.0, temp_cast_0, float3( 1,1,1 ), float3(0,0,0) );
			float4 DETAILTEXTUREvar414 = triplanar173;
			#ifdef _DETAILTEXTUREONOFF_ON
				float4 staticSwitch543 = ( DETAILTEXTUREvar414 * staticSwitch634 );
			#else
				float4 staticSwitch543 = staticSwitch634;
			#endif
			float4 decalscolor730 = _DecalsColor;
			float2 uv1_DecalsTexture495 = i.uv2_texcoord2;
			float DECALSMASK497 = tex2D( _DecalsTexture, uv1_DecalsTexture495 ).a;
			float4 lerpResult596 = lerp( staticSwitch543 , decalscolor730 , DECALSMASK497);
			#ifdef _DECALSONOFF_ON
				float4 staticSwitch600 = lerpResult596;
			#else
				float4 staticSwitch600 = staticSwitch543;
			#endif
			float2 temp_cast_4 = (_TopProjectionTextureTiling).xx;
			float4 triplanar525 = TriplanarSampling525( _TopProjectionTexture, ase_worldPos, ase_worldNormal, 1.0, temp_cast_4, 1.0, 0 );
			float4 TOPPROJECTION527 = triplanar525;
			float dotResult524 = dot( ase_worldNormal , _Vector1 );
			float saferPower582 = abs( ( ( dotResult524 * _TopProjectionTextureCoverage ) * 3.0 ) );
			float clampResult584 = clamp( pow( saferPower582 , 5.0 ) , 0.0 , 1.0 );
			float TOPPROJECTIONMASK528 = clampResult584;
			float4 lerpResult555 = lerp( staticSwitch600 , TOPPROJECTION527 , TOPPROJECTIONMASK528);
			#ifdef _TOPPROJECTIONONOFF_ON
				float4 staticSwitch557 = lerpResult555;
			#else
				float4 staticSwitch557 = staticSwitch600;
			#endif
			float4 color205 = IsGammaSpace() ? float4(1,1,1,0) : float4(1,1,1,0);
			float dotResult211 = dot( ase_worldNormal , _SnowDirection );
			float smoothstepResult552 = smoothstep( 0.0 , _SnowFade , ( (-1.0 + (_SnowCoverage - 0.0) * (1.0 - -1.0) / (1.0 - 0.0)) + dotResult211 ));
			float4 temp_output_363_0 = ( ( (0.0 + (_SnowAmount - 0.0) * (10.0 - 0.0) / (1.0 - 0.0)) * color205 ) * smoothstepResult552 );
			float4 transform200 = mul(unity_WorldToObject,float4( ase_worldPos , 0.0 ));
			float4 appendResult209 = (float4(transform200.x , transform200.z , 0.0 , 0.0));
			float simplePerlin2D213 = snoise( appendResult209.xy*_SnowNoiseScale );
			simplePerlin2D213 = simplePerlin2D213*0.5 + 0.5;
			float saferPower216 = abs( simplePerlin2D213 );
			float4 SNOW220 = (( _SnowNoiseOnOff )?( ( pow( saferPower216 , _SnowNoiseContrast ) * temp_output_363_0 ) ):( temp_output_363_0 ));
			#ifdef _SNOWONOFF_ON
				float4 staticSwitch545 = ( staticSwitch557 + SNOW220 );
			#else
				float4 staticSwitch545 = staticSwitch557;
			#endif
			float4 lerpResult607 = lerp( staticSwitch545 , _OreColor , ( 1.0 - i.vertexColor.a ));
			float4 COLOR539 = lerpResult607;
			o.Albedo = COLOR539.rgb;
			float3 temp_cast_9 = (1.0).xxx;
			float4 color717 = IsGammaSpace() ? float4(1,1,1,0) : float4(1,1,1,0);
			float4 color716 = IsGammaSpace() ? float4(0,0,0,0) : float4(0,0,0,0);
			float4 lerpResult718 = lerp( color717 , color716 , (_SinTime.w*0.3 + 0.5));
			float3 desaturateInitialColor720 = lerpResult718.rgb;
			float desaturateDot720 = dot( desaturateInitialColor720, float3( 0.299, 0.587, 0.114 ));
			float3 desaturateVar720 = lerp( desaturateInitialColor720, desaturateDot720.xxx, 1.0 );
			float4 Decalemission685 = (( _DECALEMISSIONONOFF )?( ( ( float4( ( _DecalEmissionIntensity * (( _ANIMATEDECALEMISSIONONOFF )?( desaturateVar720 ):( temp_cast_9 )) ) , 0.0 ) * _DecakEmissionColor ) * DECALSMASK497 ) ):( float4( 0,0,0,0 ) ));
			float3 temp_cast_12 = (0.1).xxx;
			float4 color701 = IsGammaSpace() ? float4(1,1,1,0) : float4(1,1,1,0);
			float4 color702 = IsGammaSpace() ? float4(0,0,0,0) : float4(0,0,0,0);
			float4 lerpResult703 = lerp( color701 , color702 , (_SinTime.w*0.3 + 0.5));
			float3 desaturateInitialColor704 = lerpResult703.rgb;
			float desaturateDot704 = dot( desaturateInitialColor704, float3( 0.299, 0.587, 0.114 ));
			float3 desaturateVar704 = lerp( desaturateInitialColor704, desaturateDot704.xxx, 1.0 );
			float4 oreemission684 = (( _OREEMISSIONONOFF )?( ( ( float4( ( _OreEmissionIntensity * (( _ANIMATEOREEMISSIONONOFF )?( desaturateVar704 ):( temp_cast_12 )) ) , 0.0 ) * _OreEmissionColor ) * ( 1.0 - i.vertexColor.a ) ) ):( float4( 0,0,0,0 ) ));
			o.Emission = ( Decalemission685 + oreemission684 ).rgb;
			float4 color617 = IsGammaSpace() ? float4(1,1,1,0) : float4(1,1,1,0);
			o.Smoothness = ( _Smoothness * color617 ).r;
			o.Alpha = 1;
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
            return 0;
        }

        half4 PTDepthNormalsFrag(PTVaryings IN, bool isFront : SV_IsFrontFace) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(IN);
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
            i.uv2_texcoord2 = IN.uv01.zw;
            i.worldPos = IN.positionWS;
            i.worldNormal = N;
            i.vertexColor = IN.color;
            i.internalSurfaceTtoW0 = float3(T.x, B.x, N.x);
            i.internalSurfaceTtoW1 = float3(T.y, B.y, N.y);
            i.internalSurfaceTtoW2 = float3(T.z, B.z, N.z);

            SurfaceOutputStandard o = (SurfaceOutputStandard)0;
            o.Normal = float3(0, 0, 1);
            o.Occlusion = 1;
            o.Alpha = 1;
            surf(i, o);

            float3 nWS = normalize(mul(o.Normal, float3x3(T, B, N)));

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
            #pragma shader_feature_local _SNOWONOFF_ON
            #pragma shader_feature_local _TOPPROJECTIONONOFF_ON
            #pragma shader_feature_local _DECALSONOFF_ON
            #pragma shader_feature_local _DETAILTEXTUREONOFF_ON
            #pragma shader_feature_local _GRADIENTONOFF_ON
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
            #pragma shader_feature_local _SNOWONOFF_ON
            #pragma shader_feature_local _TOPPROJECTIONONOFF_ON
            #pragma shader_feature_local _DECALSONOFF_ON
            #pragma shader_feature_local _DETAILTEXTUREONOFF_ON
            #pragma shader_feature_local _GRADIENTONOFF_ON
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
            #pragma shader_feature_local _SNOWONOFF_ON
            #pragma shader_feature_local _TOPPROJECTIONONOFF_ON
            #pragma shader_feature_local _DECALSONOFF_ON
            #pragma shader_feature_local _DETAILTEXTUREONOFF_ON
            #pragma shader_feature_local _GRADIENTONOFF_ON
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
            #pragma shader_feature_local _SNOWONOFF_ON
            #pragma shader_feature_local _TOPPROJECTIONONOFF_ON
            #pragma shader_feature_local _DECALSONOFF_ON
            #pragma shader_feature_local _DETAILTEXTUREONOFF_ON
            #pragma shader_feature_local _GRADIENTONOFF_ON
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
