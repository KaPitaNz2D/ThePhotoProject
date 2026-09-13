// ImpostorNormalCapture.shader
// Internal utility shader used only by TreeCrossQuadImpostorGenerator to bake the normal atlas.
// Temporarily swapped onto the source tree's renderers, then rendered with the same orthographic
// camera used for the albedo bake. Outputs each vertex's VIEW-SPACE normal (encoded 0-1 in RGB).
// Because the bake cameras are axis-aligned and upright, camera-space for each of the 4 views
// exactly matches the resulting cross-quad's own tangent basis (tangent = view right, bitangent =
// world up, normal = view forward), so the captured pixels can be sampled directly as a per-quad
// normal map by ImpostorCrossBIRP/URP/HDRP without any additional transform baked in.
Shader "Hidden/Roundy/ImpostorNormalCapture"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 viewNormal : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewNormal = mul((float3x3)UNITY_MATRIX_V, worldNormal);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half3 n = normalize(i.viewNormal);
                return half4(n * 0.5h + 0.5h, 1);
            }
            ENDCG
        }
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Pass"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewNormal : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 worldNormal = TransformObjectToWorldNormal(input.normalOS);
                output.viewNormal = TransformWorldToViewDir(worldNormal, true);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 n = normalize(input.viewNormal);
                return half4(n * 0.5h + 0.5h, 1);
            }
            ENDHLSL
        }
    }
}
