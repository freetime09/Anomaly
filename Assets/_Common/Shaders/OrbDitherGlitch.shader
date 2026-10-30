Shader "Anomaly/OrbDitherGlitch"
{
 Properties {
  [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
  _Reveal ("Reveal", Range(0,1)) = 1
  _Glitch ("Glitch", Range(0,1)) = 0
  _Clock ("Clock", Float) = 0
  _UVRect ("Sprite UV", Vector) = (0,0,1,1)
 }
 SubShader {
 Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
 Blend SrcAlpha OneMinusSrcAlpha
 Cull Off ZWrite Off
 Pass {
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
 float4 _MainTex_TexelSize, _UVRect;
 float _Reveal, _Glitch, _Clock;
 struct A {float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR;};
 struct V {float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR;};
 V vert(A v) {V o;o.vertex=TransformObjectToHClip(v.vertex.xyz);o.uv=v.uv;o.color=v.color;return o;}
 float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
 half4 frag(V i):SV_Target {
   float2 pixel=floor(i.uv*_MainTex_TexelSize.zw);
   float threshold=(floor(hash(floor(pixel/2))*16)+.5)/16;
   clip(_Reveal-threshold);
   float2 local=(i.uv-_UVRect.xy)/(_UVRect.zw-_UVRect.xy);
   float band=floor(local.y*14);
   float noise=hash(float2(band,floor(_Clock*32)));
   float shift=(noise-.5)*.24*_Glitch*step(.48,noise);
   float2 uv=i.uv+float2(shift*(_UVRect.z-_UVRect.x),0);
   clip(uv.x-_UVRect.x);clip(_UVRect.z-uv.x);
   half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv)*i.color;
   float stripe=step(.86,noise)*_Glitch;
   c.rgb=lerp(c.rgb,half3(1,.18,.16),stripe*.7);
   return c;
 }
 ENDHLSL
 }
 }
}
