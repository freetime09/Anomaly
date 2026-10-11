Shader "Hidden/VortexSwirl"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        Pass
        {
            Name "VortexSwirl"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            TEXTURE2D(_BlitTexture);
            SAMPLER(sampler_LinearClamp);

            float  _Twist;
            float  _Radius;
            float2 _Center;
            float  _PullIn;
            float  _Fade;
            float4 _FadeColor;
            float  _Aspect;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord   : TEXCOORD0;
            };

            Varyings Vert (Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.texcoord   = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            half4 Frag (Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                float2 d = uv - _Center;
                d.x *= _Aspect;                        // keep the swirl circular
                float dist = length(d);

                float t = saturate(1.0 - dist / max(_Radius, 1e-4));
                float falloff = t * t;                 // strongest at the eye, fades to edge

                float angle = _Twist * falloff;        // rotate more toward the center
                float pull  = 1.0 + _PullIn * falloff; // > 1 sucks pixels inward

                float s = sin(angle);
                float c = cos(angle);
                float2 rd = float2(d.x * c - d.y * s, d.x * s + d.y * c);
                rd /= pull;
                rd.x /= _Aspect;

                float2 suv = _Center + rd;

                half4 col = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, suv);
                col.rgb = lerp(col.rgb, _FadeColor.rgb, saturate(_Fade));
                return col;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
