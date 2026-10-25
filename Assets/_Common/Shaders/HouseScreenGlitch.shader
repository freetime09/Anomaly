Shader "Hidden/AnomalyScreenGlitch"
{
    Properties
    {
        _Amount ("Distortion", Range(0,1)) = 0
        _Bloom ("Light bleed", Range(0,1)) = 0
        _NoiseTime ("Time", Float) = 0
        _Seed ("Seed", Float) = 1
        _Variant ("Variant", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            Name "AnomalyScreenGlitch"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            TEXTURE2D(_BlitTexture);
            SAMPLER(sampler_LinearClamp);
            float4 _BlitTexture_TexelSize;
            float _Amount, _Bloom, _NoiseTime, _Seed, _Variant;
            struct Attributes { uint vertexID:SV_VertexID; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS=GetFullScreenTriangleVertexPosition(v.vertexID);
                o.uv=GetFullScreenTriangleTexCoord(v.vertexID);
                return o;
            }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float3 view(float2 uv) { return SAMPLE_TEXTURE2D(_BlitTexture,sampler_LinearClamp,saturate(uv)).rgb; }
            half4 Frag(Varyings i):SV_Target
            {
                float2 uv=i.uv;
                if (_Amount<0.0001 && _Bloom<0.0001) return half4(view(uv),1);
                float tick=floor(_NoiseTime*24);
                float row=floor(uv.y*(36+_Variant*23));
                float n=hash(float2(row+_Seed,tick));
                float tear=step(0.84,n)*(hash(float2(row,tick+_Seed))-0.5);
                uv.x+=_Amount*(tear*0.09+(n-0.5)*0.004);
                uv.y+=sin(uv.x*11+_NoiseTime*21+_Seed)*_Amount*0.0015;
                float split=_Amount*(0.0016+step(0.5,_Variant)*0.0016);
                float3 c=float3(view(uv+float2(split,0)).r,view(uv).g,view(uv-float2(split,0)).b);
                float scan=sin(i.uv.y/max(abs(_BlitTexture_TexelSize.y),0.0001)*1.5+tick)*0.5+0.5;
                c*=1-_Amount*0.07*scan;
                float snow=hash(floor(i.uv*float2(960,540))+float2(tick,_Seed));
                c+=(snow-0.5)*_Amount*(0.025+_Variant*0.006);
                float2 px=abs(_BlitTexture_TexelSize.xy)*(5+_Bloom*8);
                float3 glow=0;
                glow+=max(view(uv+float2(px.x,0))-0.58,0);
                glow+=max(view(uv-float2(px.x,0))-0.58,0);
                glow+=max(view(uv+float2(0,px.y))-0.58,0);
                glow+=max(view(uv-float2(0,px.y))-0.58,0);
                glow+=max(view(uv+px*0.6)-0.58,0);
                glow+=max(view(uv-px*0.6)-0.58,0);
                c+=glow*_Bloom*0.3;
                return half4(max(c,0),1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}