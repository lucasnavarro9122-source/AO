Shader "AO/ParticleVertexAdditive"
{
    Properties { _MainTex ("Sprite Texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent"
               "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha One
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            half4 _AOColor0, _AOColor1, _AOColor2, _AOColor3;
            float4 _AOBounds; // sprite local minX, minY, width, height

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                float2 t = saturate((input.positionOS.xy - _AOBounds.xy) /
                                    max(_AOBounds.zw, float2(0.0001, 0.0001)));
                half4 lower = lerp(_AOColor0, _AOColor2, t.x);
                half4 upper = lerp(_AOColor1, _AOColor3, t.x);
                output.color = input.color * lerp(lower, upper, t.y);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
            }
            ENDHLSL
        }
    }
}
