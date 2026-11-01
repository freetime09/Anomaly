Shader "Anomaly/MountainParallax"
{
 Properties{
 [PerRendererData] _MainTex("Sprite",2D)="white"{}
 _SourceTex("Layer",2D)="white"{}
 _Rect("Frame",Vector)=(0,0,1,1)
 _Layout("Phase, span, bottom, height",Vector)=(.5,1,0,1)
 _Extend("Extend lower edge",Float)=0
 _Sky("Sky",Float)=0 _Repeat("Repeat",Float)=1
 }
 SubShader{
 Tags{"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
 Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off
 Pass{HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 TEXTURE2D(_SourceTex);
 float4 _Rect,_Layout;float _Extend,_Sky,_Repeat;
 struct A{float4 p:POSITION;float2 uv:TEXCOORD0;float4 c:COLOR;};
 struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float4 c:COLOR;};
 V vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.uv=a.uv;o.c=a.c;return o;}
 half4 frag(V i):SV_Target{
 float2 screenUV=(i.uv-.5)*1.04+.5;
 float x=_Layout.x+(screenUV.x-.5)*_Layout.y;
 float rawX=x;if(_Repeat>.5)x=1-abs(frac(x*.5)*2-1);else x=saturate(x);
 float y=(screenUV.y-_Layout.z)/_Layout.w;
 half4 c=SAMPLE_TEXTURE2D(_SourceTex,sampler_PointClamp,lerp(_Rect.xy,_Rect.zw,float2(x,saturate(y))))*i.c;
 if(_Repeat<.5)c.a*=step(0,rawX)*step(rawX,1);
 if(_Sky<.5){c.a*=step(y,1);if(_Extend<.5)c.a*=step(0,y);}
 return c;
 }
 ENDHLSL}
 }
}