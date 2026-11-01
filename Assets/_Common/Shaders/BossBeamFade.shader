Shader "Anomaly/BossBeamFade" {
 Properties {_Start("Fade start",Float)=.03 _End("Fade end",Float)=.2 _NeonEmission("Emission",Float)=3.2}
 SubShader {Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"} Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off
 Pass {HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 float _Start,_End,_NeonEmission;
 struct A{float4 p:POSITION;float2 uv:TEXCOORD0;float4 c:COLOR;};struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float4 c:COLOR;};
 V Vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;o.c=i.c;return o;}
 half4 Frag(V i):SV_Target{float fade=smoothstep(_Start,_End,i.uv.x);float pulse=1+.012*sin(_Time.y*1.17)+.006*sin(_Time.y*2.63);return half4(i.c.rgb*_NeonEmission*pulse,i.c.a*fade);}
 ENDHLSL}}
}
