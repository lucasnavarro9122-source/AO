Shader "AO/WeatherMultiply"
{
    // Tinte del clima (nube, 26/09): multiplica lo que ya está dibujado. El negro sigue negro (el vacío del mapa no
    // se aclara) y los colores se enfrían u oscurecen. Color del vértice: rgb = tinte, a = fuerza.
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent"
               "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend DstColor Zero

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
            half4 _Color;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half strength = saturate(tex.a * input.color.a);
                return half4(lerp(half3(1, 1, 1), tex.rgb * input.color.rgb, strength), 1);
            }
            ENDHLSL
        }
    }
}
