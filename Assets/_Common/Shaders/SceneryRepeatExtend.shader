Shader "Anomaly/SceneryRepeatExtend" {
 Properties {
 [PerRendererData] _MainTex("Sprite",2D)="white"{}
 _SourceTex("Scenery",2D)="white"{}
 _Rect("Source rect",Vector)=(0,0,1,1)
 _Layout("Tiles, image height, extension, pixel height",Vector)=(3,14,7,224)
 _BaseColor("Source base colour",Color)=(.13,.15,.04,1)
 }
 SubShader {
 Tags{"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
 Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off
 Pass{HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 TEXTURE2D(_SourceTex);SAMPLER(sampler_SourceTex);
 float4 _Rect,_Layout,_BaseColor;
 struct A{float4 p:POSITION;float2 uv:TEXCOORD0;float4 c:COLOR;};
 struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float4 c:COLOR;};
 V vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.uv=a.uv;o.c=a.c;return o;}
 half4 frag(V i):SV_Target{
 float y=(i.uv.y*(_Layout.y+_Layout.z)-_Layout.z)/_Layout.y;
 float x=frac(i.uv.x*_Layout.x);
 half4 src=SAMPLE_TEXTURE2D(_SourceTex,sampler_SourceTex,lerp(_Rect.xy,_Rect.zw,float2(x,saturate(y))));
 float grain=frac(sin(dot(floor(float2(x,y)*float2(384,_Layout.w)),float2(12.9898,78.233)))*43758.5453)-.5;
 half3 baseRGB=_BaseColor.rgb+grain*.004;
 float weight=smoothstep(0,.1,y);
 float alpha=lerp(1,src.a,weight);
 half3 rgb=(baseRGB*(1-weight)+src.rgb*src.a*weight)/max(alpha,.001);
 return half4(rgb,alpha)*i.c;
 }
 ENDHLSL}
 }
}