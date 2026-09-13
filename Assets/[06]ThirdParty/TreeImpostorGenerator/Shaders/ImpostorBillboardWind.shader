// ImpostorBillboardWind.shader
// For the simple 2-crossed-plane / billboard-card tree impostor: each vertex's authored
// position is treated as a CARD PIVOT (branch/leaf anchor), and its UV0 is used to
// reconstruct a camera-facing quad around that pivot entirely in the vertex shader - see
// "billboard cloud" technique. Because billboarding happens per-vertex here (not by
// rotating a whole Transform), it works correctly even when a single mesh contains many
// separate quads/cards, unlike BillboardEffect.cs which can only rotate one rigid Transform.
// Do not use this together with BillboardEffect.cs on the same object.
Shader "Roundy/Vegetation/ImpostorBillboardWind"
{
    Properties
    {
        [MainTexture] _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
        [MainColor] _Color ("Color", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        [Enum(Off,0,Front,1,Back,2)] _Cull ("Culling", Float) = 0

        [Header(Billboard)]
        _BillboardSize ("Billboard Size", Float) = 1.0
        _InflateAmount ("Inflate Amount", Float) = 0.0

        [Header(Wind)]
        _WindDirection ("Wind Direction (XY, world XZ)", Vector) = (1, 0, 0, 0)
        _WindSpeed ("Wind Speed", Float) = 1.0
        _WindStrength ("Wind Strength", Float) = 0.15
        _NoiseScale ("Wind Noise Scale", Float) = 0.5
    }

    SubShader
    {
        Tags
        {
            "Queue"="AlphaTest"
            "RenderType"="TransparentCutout"
            "IgnoreProjector"="True"
            "RenderPipeline"="UniversalPipeline"
        }
        Cull [_Cull]
        ZWrite On

        Pass
        {
            Name "Pass"
            Tags { "LightMode" = "UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Cutoff;
                float _BillboardSize;
                float _InflateAmount;
                float4 _WindDirection;
                float _WindSpeed;
                float _WindStrength;
                float _NoiseScale;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float fogCoord : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Matches Shader Graph's "Simple Noise" node: value noise in [0,1], smoothstep-
            // interpolated between random values on a unit lattice.
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float SimpleNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // The authored vertex position is the card's pivot (e.g. a leaf-cluster
                // anchor), not a real quad corner - the quad itself is rebuilt below from UV.
                float3 pivotWS = TransformObjectToWorld(input.positionOS.xyz);

                // ---- Wind sway: displace the pivot in world space (Position(World) -> Simple
                // Noise(UV = worldXZ*scale + windDir*speed*time) -> remap [0,1]->[-1,1] ->
                // * strength -> add to Position(World)).
                float2 windDir = normalize(_WindDirection.xy + 1e-5);
                float2 noiseUV = pivotWS.xz * _NoiseScale + windDir * (_Time.y * _WindSpeed);
                float windNoise = SimpleNoise(noiseUV) * 2.0 - 1.0;
                float3 windOffsetWS = float3(windDir.x, 0, windDir.y) * windNoise * _WindStrength;
                float3 sweptPivotWS = pivotWS + windOffsetWS;

                // ---- Quadmesh-to-billboard: remap UV0 (0..1) to local quad space (-1..1),
                // then rebuild the corner using the camera's world-space right/up axes (rows
                // 0/1 of the View matrix - valid because it's an orthonormal rotation, so its
                // rows equal its inverse's columns, i.e. the camera axes in world space).
                float2 quadOffset = input.uv * 2.0 - 1.0;
                float3 camRightWS = UNITY_MATRIX_V[0].xyz;
                float3 camUpWS = UNITY_MATRIX_V[1].xyz;
                float3 billboardOffsetWS = (camRightWS * quadOffset.x + camUpWS * quadOffset.y) * _BillboardSize;

                // ---- Inflate along the original (pre-billboard) normal for a bit of volume.
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 inflateOffsetWS = normalWS * _InflateAmount;

                float3 finalWorldPos = sweptPivotWS + billboardOffsetWS + inflateOffsetWS;

                output.positionCS = TransformWorldToHClip(finalWorldPos);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                // The card is camera-facing by construction, so light it as if its normal
                // simply points at the camera.
                output.worldNormal = normalize(GetCameraPositionWS() - finalWorldPos);
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                col.rgb *= _Color.rgb;

                half alpha = col.a * _Color.a;
                clip(alpha - _Cutoff);

                half3 worldNormal = normalize(input.worldNormal);
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(worldNormal, mainLight.direction));
                half3 ambient = SampleSH(worldNormal);
                col.rgb *= (ambient + NdotL * mainLight.color);

                col.a = alpha;
                col.rgb = MixFog(col.rgb, input.fogCoord);
                return col;
            }
            ENDHLSL
        }
    }
}
