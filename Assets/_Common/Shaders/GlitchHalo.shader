Shader "Anomaly/GlitchHalo" {
Properties{[PerRendererData] _MainTex("Texture",2D)="white" {} _Tint("Tint",Color)=(1,1,1,1) _Core("Core",Vector)=(.1,.1,.9,.9)}
SubShader{Tags{"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"} Blend SrcAlpha One Cull Off ZWrite Off ZTest Always
Pass{HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
struct A{float4 p:POSITION;float2 uv:TEXCOORD0;float4 c:COLOR;};struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float4 c:COLOR;};
float4 _Tint,_Core;
V vert(A v){V o;o.p=TransformObjectToHClip(v.p.xyz);o.uv=v.uv;o.c=v.c;return o;}
half4 frag(V i):SV_Target{
float2 outside=max(max(_Core.xy-i.uv,i.uv-_Core.zw),0);
float2 margin=max(_Core.xy,float2(.001,.001));
float d=length(outside/margin);
float inside=step(_Core.x,i.uv.x)*step(i.uv.x,_Core.z)*step(_Core.y,i.uv.y)*step(i.uv.y,_Core.w);
float glow=exp(-d*d*3.2)*(1-inside*.88);
return half4(_Tint.rgb*i.c.rgb,glow*_Tint.a*i.c.a*.65);
}
ENDHLSL}}}