// ImpostorCrossBIRP.shader
Shader "Roundy/Vegetation/ImpostorCrossBIRP"
{
    Properties
    {
        _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
        [NoScaleOffset] _BumpMap ("Normal Map (view-space per quad, not a Unity Normal Map)", 2D) = "bump" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
        [Toggle(ALPHA_TO_COVERAGE)] _AlphaToCoverage("Alpha To Coverage", Float) = 0
        _AlphaCoverageStrength ("Alpha Coverage Strength", Range(0.1, 2.0)) = 1.0
        [Enum(Off,0,Front,1,Back,2)] _Cull ("Culling", Float) = 2
    }
    
    SubShader
    {
        Tags
        {
            "Queue"="AlphaTest"
            "RenderType"="TransparentCutout"
            "IgnoreProjector"="True"
        }
        Cull [_Cull]
        ZWrite On
        
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            AlphaToMask [_AlphaToCoverage]
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile _ ALPHA_TO_COVERAGE
            #pragma multi_compile_instancing
            #pragma target 3.0
            
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldTangent : TEXCOORD2;
                float3 worldBitangent : TEXCOORD3;
                UNITY_FOG_COORDS(4)
                float4 screenPos : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            sampler2D _MainTex;
            sampler2D _BumpMap;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed _Cutoff;
            half _AlphaCoverageStrength;
            
            static const half4x4 bayerMatrix = half4x4(
                0.0h, 0.5h, 0.125h, 0.625h,
                0.75h, 0.25h, 0.875h, 0.375h,
                0.1875h, 0.6875h, 0.0625h, 0.5625h,
                0.9375h, 0.4375h, 0.8125h, 0.3125h
            );
            
            UNITY_INSTANCING_BUFFER_START(Props)
                // Add instanced properties here if needed
            UNITY_INSTANCING_BUFFER_END(Props)
            
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
                o.worldBitangent = cross(o.worldNormal, o.worldTangent) * v.tangent.w;
                o.screenPos = ComputeScreenPos(o.pos);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                
                fixed4 col = tex2D(_MainTex, i.uv);
                col.rgb *= _Color.rgb;
                
                // Process alpha differently based on alpha to coverage mode
                #if defined(ALPHA_TO_COVERAGE)
                    half processedAlpha = pow(col.a * _Color.a, _AlphaCoverageStrength);
                    half alpha = (processedAlpha - _Cutoff) / max(fwidth(processedAlpha), 0.0001) + 0.5;
                #else
                    half alpha = col.a * _Color.a;
                    clip(alpha - _Cutoff);
                #endif

                #if defined(LOD_FADE_CROSSFADE)
                    half2 screenPos = i.screenPos.xy / i.screenPos.w * _ScreenParams.xy * 0.5h;
                    uint2 ditherCoord = uint2(fmod(screenPos, 4));
                    half dither = bayerMatrix[ditherCoord.x][ditherCoord.y];
                    half fadeValue = unity_LODFade.x > 0 ?
                        unity_LODFade.x - dither :
                        unity_LODFade.x + dither;
                    
                    #if defined(ALPHA_TO_COVERAGE)
                        alpha *= saturate(fadeValue + 1);
                    #endif
                    
                    if (fadeValue < 0)
                    {
                        discard;
                    }
                #endif

                #if defined(ALPHA_TO_COVERAGE)
                    if (alpha < 0)
                    {
                        discard;
                    }
                #endif

                // _BumpMap stores a raw per-quad "view-space" normal baked by the impostor
                // generator (RGB = normal*0.5+0.5), not a Unity-encoded Normal Map, so it is
                // decoded manually instead of via UnpackNormal/DXT5nm.
                half3 normalTS = tex2D(_BumpMap, i.uv).rgb * 2.0h - 1.0h;
                // Meshes generated before normal-map support was added (or otherwise missing a
                // TANGENT stream) come through with a zero-length tangent; normalizing that would
                // produce NaN and blow the shading out to white, so fall back to the plain surface
                // normal in that case instead of building a degenerate TBN.
                half3 worldNormal;
                if (dot(i.worldTangent, i.worldTangent) > 1e-6h)
                {
                    half3x3 TBN = half3x3(normalize(i.worldTangent), normalize(i.worldBitangent), normalize(i.worldNormal));
                    worldNormal = normalize(mul(normalTS, TBN));
                }
                else
                {
                    worldNormal = normalize(i.worldNormal);
                }

                half3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                half NdotL = saturate(dot(worldNormal, lightDir));
                half3 ambient = ShadeSH9(half4(worldNormal, 1));
                col.rgb *= (ambient + NdotL * _LightColor0.rgb);
                
                col.a = alpha;
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
