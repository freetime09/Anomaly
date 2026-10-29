Shader "Anomaly/BossEffectSilhouette" {
Properties { _Silhouette("Silhouette mode",Range(0,1))=1 [PerRendererData] _MainTex("Effect",2D)="white" {} }
SubShader { Tags{"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
Pass{
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
struct A{float4 p:POSITION;float2 uv:TEXCOORD0;float4 c:COLOR;};struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float4 c:COLOR;};
TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
float _Silhouette;
V vert(A v){V o;o.p=TransformObjectToHClip(v.p.xyz);o.uv=v.uv;o.c=v.c;return o;}
half4 frag(V i):SV_Target{half4 tex=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);return half4(lerp(tex.rgb,half3(1,1,1),_Silhouette)*i.c.rgb,tex.a*i.c.a);}
ENDHLSL
}
}
}