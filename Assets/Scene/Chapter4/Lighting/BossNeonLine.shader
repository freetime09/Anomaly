Shader "Anomaly/BossNeonLine" {
 Properties {_NeonEmission("Emission",Float)=3.8}
 SubShader {Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"} Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off
 Pass {HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float _NeonEmission;struct A{float4 p:POSITION;float4 c:COLOR;};struct V{float4 p:SV_POSITION;float4 c:COLOR;};
 V Vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.c=i.c;return o;}
 half4 Frag(V i):SV_Target{return half4(i.c.rgb*_NeonEmission,i.c.a);}
 ENDHLSL}}
}
