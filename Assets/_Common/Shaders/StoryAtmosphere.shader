Shader "Anomaly/StoryAtmosphere" {
SubShader { Tags { "Queue"="Background" "RenderType"="Opaque" } Cull Off ZWrite Off
Pass { HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct A { float4 vertex:POSITION; float2 uv:TEXCOORD0; }; struct V { float4 vertex:SV_POSITION;float2 uv:TEXCOORD0; };
V vert(A a){V o;o.vertex=UnityObjectToClipPos(a.vertex);o.uv=a.uv;return o;}
float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
half4 frag(V i):SV_Target {float t=_Time.y;float2 q=i.uv*float2(180,110)+float2(t*.12,t*.04);float2 cell=floor(q),f=frac(q);float seed=hash(cell);float mote=step(.9975,seed)*(1-smoothstep(.03,.17,length((f-.5)*float2(1,2))))*(.55+.45*sin(t*.7+seed*90));float3 c=float3(.001,.0015,.002)+mote*float3(.025,.033,.04);return half4(c,1);}
ENDHLSL }
} }
