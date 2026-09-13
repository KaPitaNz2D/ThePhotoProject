// ImpostorCrossHDRP.shader
Shader "Roundy/Vegetation/ImpostorCrossHDRP"
{
    Properties
    {
        [MainTexture] _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
        [NoScaleOffset] _BumpMap ("Normal Map (view-space per quad, not a Unity Normal Map)", 2D) = "bump" {}
        [MainColor] _Color ("Color", Color) = (1,1,1,1)
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
        [Toggle(ALPHA_TO_COVERAGE)] _AlphaToCoverage("Alpha To Coverage", Float) = 0
        _AlphaCoverageStrength ("Alpha Coverage Strength", Range(0.1, 2.0)) = 1.0
        [Enum(Off,0,Front,1,Back,2)] _Cull ("Culling", Float) = 2
        _AmbientColor ("Ambient Color (flat fallback, no probe/APV sampling)", Color) = (0.15,0.15,0.15,1)
    }

    SubShader
    {
        Tags
        {
            "Queue"="AlphaTest"
            "RenderType"="TransparentCutout"
            "IgnoreProjector"="True"
            "RenderPipeline"="HDRenderPipeline"
        }
        Cull [_Cull]
        ZWrite On

        // Single ForwardOnly pass only, matching the minimal (no shadow/depth-only pass)
        // feature set of ImpostorCrossURP/BIRP. Depth still resolves correctly via ZWrite On.
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            AlphaToMask [_AlphaToCoverage]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile _ ALPHA_TO_COVERAGE
            #pragma target 4.5
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            // NOTE: unverified — no HDRP package is installed in this project, so this could not be
            // compiled/tested here. _DirectionalLightDatas / DirectionalLightData is HDRP's internal
            // sun-light buffer; its include path and field names have shifted across HDRP versions,
            // so re-check this against Runtime/Lighting/LightLoop/LightLoopDef.hlsl (or wherever your
            // HDRP version declares it) once the package is installed and the shader can be compiled.
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/LightLoopDef.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Cutoff;
                half _AlphaCoverageStrength;
                half4 _AmbientColor;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            static const half4x4 bayerMatrix = half4x4(
                0.0h, 0.5h, 0.125h, 0.625h,
                0.75h, 0.25h, 0.875h, 0.375h,
                0.1875h, 0.6875h, 0.0625h, 0.5625h,
                0.9375h, 0.4375h, 0.8125h, 0.3125h
            );

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldTangent : TEXCOORD2;
                float3 worldBitangent : TEXCOORD3;
                float4 screenPos : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.worldNormal = TransformObjectToWorldNormal(input.normalOS);
                output.worldTangent = TransformObjectToWorldDir(input.tangentOS.xyz);
                output.worldBitangent = cross(output.worldNormal, output.worldTangent) * input.tangentOS.w;
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
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
                    half2 screenPos = input.screenPos.xy / input.screenPos.w * _ScreenParams.xy * 0.5h;
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
                        return 0;
                    }
                #endif

                #if defined(ALPHA_TO_COVERAGE)
                    if (alpha < 0)
                    {
                        return 0;
                    }
                #endif

                // _BumpMap stores a raw per-quad "view-space" normal baked by the impostor
                // generator (RGB = normal*0.5+0.5), not a Unity-encoded Normal Map, so it is
                // decoded manually instead of via UnpackNormal/DXT5nm.
                half3 normalTS = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv).rgb * 2.0h - 1.0h;
                // Meshes generated before normal-map support was added (or otherwise missing a
                // TANGENT stream) come through with a zero-length tangent; normalizing that would
                // produce NaN and blow the shading out to white, so fall back to the plain surface
                // normal in that case instead of building a degenerate TBN.
                half3 worldNormal;
                if (dot(input.worldTangent, input.worldTangent) > 1e-6h)
                {
                    half3x3 TBN = half3x3(normalize(input.worldTangent), normalize(input.worldBitangent), normalize(input.worldNormal));
                    worldNormal = normalize(mul(normalTS, TBN));
                }
                else
                {
                    worldNormal = normalize(input.worldNormal);
                }

                // NOTE: same gap as before applies to shadows too - this does not sample HDRP's
                // shadow atlas at all (that needs the HDShadowContext / GetDirectionalShadowAttenuation
                // machinery from HDRP's Lighting.hlsl, which is too deep/version-fragile to guess at
                // without the package installed to compile against). Until that's added, this impostor
                // will stay fully lit under terrain/other-object shadows and at low/behind-horizon
                // light angles - see the equivalent, verified fix in ImpostorCrossURP.shader/
                // ImpostorCrossBIRP.shader (GetMainLight(shadowCoord) / SHADOW_ATTENUATION) for the
                // pattern to port once this can actually be compiled and tested.
                half3 lightColor = 0;
                half NdotL = 0;
                if (_DirectionalLightCount > 0)
                {
                    DirectionalLightData mainLight = _DirectionalLightDatas[0];
                    half3 lightDir = normalize(-mainLight.forward);
                    NdotL = saturate(dot(worldNormal, lightDir));
                    lightColor = mainLight.color;
                }

                col.rgb *= (_AmbientColor.rgb + NdotL * lightColor);

                col.a = alpha;
                return col;
            }
            ENDHLSL
        }
    }
}
