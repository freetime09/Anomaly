Shader "Anomaly/Chapter2/EdgeEmission"
{
    Properties
    {
        _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        [HDR] _EmissionColor("White neon", Color) = (1,1.015,1.035,1)
        _Intensity("Emission", Float) = 2.6
        [HideInInspector] _RendererColor("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("External Alpha Enabled", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            struct Attributes { COMMON_2D_INPUTS half4 color:COLOR; UNITY_SKINNED_VERTEX_INPUTS };
            struct Varyings { COMMON_2D_OUTPUTS half4 color:COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _EmissionColor;
                float _Intensity;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half4 color = CommonUnlitFragment(input, input.color);
                float steady = 1 + .012*sin(_Time.y*1.17) + .006*sin(_Time.y*2.63);
                color.rgb *= _EmissionColor.rgb * _Intensity * steady;
                return color;
            }
            ENDHLSL
        }
    }
}
