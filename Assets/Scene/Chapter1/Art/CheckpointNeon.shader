Shader "Anomaly/CheckpointNeon"
{
    Properties
    {
        _GlowTex("Activated monument", 2D) = "white" {}
        _Frame("Sprite frame", Vector) = (0,0,1,1)
        _Margin("Glow margin", Vector) = (.2,.2,0,0)
        [HDR] _Tint("Mint light", Color) = (.15,1.2,.84,1)
        _Mode("Glyph / ground / spark", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_GlowTex); SAMPLER(sampler_GlowTex);
            float4 _Frame, _Margin, _Tint;
            float _Mode;
            struct Attributes { float4 position:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.position = TransformObjectToHClip(input.position.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            float mintMask(float2 uv)
            {
                float inside = step(0,uv.x)*step(0,uv.y)*step(uv.x,1)*step(uv.y,1);
                float4 color = SAMPLE_TEXTURE2D(_GlowTex,sampler_GlowTex,lerp(_Frame.xy,_Frame.zw,saturate(uv)));
                return color.a * smoothstep(.08,.45,min(color.g,color.b)-color.r) * smoothstep(.3,.6,color.g) * inside;
            }
            half4 frag(Varyings input):SV_Target
            {
                float alpha;
                float3 color = _Tint.rgb * input.color.rgb;
                if (_Mode < .5)
                {
                    float2 uv = input.uv*(1+2*_Margin.xy)-_Margin.xy;
                    float glow=0, weight=0;
                    [unroll] for(int y=-2;y<=2;y++) [unroll] for(int x=-2;x<=2;x++)
                    {
                        float w=exp(-(x*x+y*y)*.55);
                        glow+=mintMask(uv+float2(x,y)*_Margin.xy*.5)*w;
                        weight+=w;
                    }
                    float core=mintMask(uv);
                    alpha=saturate(glow/weight*1.6+core*.2)*.78;
                    color=lerp(color,float3(.58,1.45,1.12),core*.45);
                }
                else
                {
                    float2 p=(input.uv-.5)*2;
                    if (_Mode < 1.5) alpha=exp(-dot(p,p)*3.5)*(1-smoothstep(.65,1,length(p)));
                    else
                    {
                        alpha=exp(-dot(p,p)*18)+.38*exp(-abs(p.x)*32-abs(p.y)*4)+.38*exp(-abs(p.y)*32-abs(p.x)*4);
                        color=lerp(color,float3(.8,1.4,1.15),exp(-dot(p,p)*28)*.6);
                    }
                }
                return half4(color,saturate(alpha*_Tint.a*input.color.a));
            }
            ENDHLSL
        }
    }
}
