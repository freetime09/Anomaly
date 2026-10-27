Shader "Anomaly/BossLocalDistortion" {
Properties { _HitTint ("Hit tint",Color)=(1,1,1,1) [PerRendererData] _MainTex ("Sprite",2D)="white" {} _MaskTex ("Eye mask",2D)="white" {} _UseMask ("Use mask",Float)=0 _MaskUVOffset ("Mask offset",Vector)=(0,0,0,0) _SpriteRect ("Frame UV bounds",Vector)=(0,0,1,1) _Amount ("Distortion",Range(0,1))=0 _Phase ("Noise phase",Float)=0 }
SubShader {
Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
Cull Off Lighting Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
Pass {
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
struct A {float4 p:POSITION;float2 uv:TEXCOORD0;float4 c:COLOR;};
struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;float4 c:COLOR;};
TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
TEXTURE2D(_MaskTex);SAMPLER(sampler_MaskTex);
CBUFFER_START(UnityPerMaterial)
float4 _HitTint;float _UseMask;float4 _MaskUVOffset;float4 _SpriteRect;float _Amount;float _Phase;
CBUFFER_END
float hash(float2 p){return frac(sin(dot(p,float2(12.9898,78.233)))*43758.5453);}
V vert(A v){V o;o.p=TransformObjectToHClip(v.p.xyz);o.uv=v.uv;o.c=v.c;return o;}
half4 frag(V i):SV_Target {
float2 span=max(_SpriteRect.zw-_SpriteRect.xy,float2(.00001,.00001));
float2 local=(i.uv-_SpriteRect.xy)/span;
float band=floor(local.y*40);
float key=hash(float2(band,_Phase));
float stripe=step(.68,key)*step(.001,_Amount);
float shift=(hash(float2(band+7,_Phase))-.5)*.19*_Amount*stripe;
float2 uv=i.uv+float2(shift*span.x,0);
float inside=step(_SpriteRect.x,uv.x)*step(uv.x,_SpriteRect.z);
half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv)*i.c;
c.a*=inside;
if(stripe>0 && key>.88){
float pick=floor(hash(float2(band+31,_Phase))*3);
half3 rgb=pick<1?half3(1,0,0):(pick<2?half3(0,1,0):half3(0,0,1));
c.rgb=rgb;
}
c.a*=lerp(1,SAMPLE_TEXTURE2D(_MaskTex,sampler_MaskTex,i.uv+_MaskUVOffset.xy).a,_UseMask);
c.rgb*=_HitTint.rgb;return c;
}
ENDHLSL
}
}
}