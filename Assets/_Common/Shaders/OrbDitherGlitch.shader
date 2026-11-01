Shader "Anomaly/OrbDitherGlitch"
{
 Properties {
  [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
  _Reveal ("Reveal", Range(0,1)) = 1
  _Glitch ("Glitch", Range(0,1)) = 0
  _Clock ("Clock", Float) = 0
  _UVRect ("Sprite UV", Vector) = (0,0,1,1)
  _NeonEmission ("Emission", Float) = 1
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
 float _Reveal, _Glitch, _Clock, _NeonEmission;
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
   float shift=(noise-.5)*.4*_Glitch*step(.48,noise);
   float2 uv=i.uv+float2(shift*(_UVRect.z-_UVRect.x),0);
   clip(uv.x-_UVRect.x);clip(_UVRect.z-uv.x);
   half4 src=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv);
   float2 d=float2(_MainTex_TexelSize.x*(2.6+sin(_Clock*21)*1.2)*_Glitch,0);
   float ar=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,clamp(uv+d,_UVRect.xy,_UVRect.zw)).a;
   float ag=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).a;
   float ab=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,clamp(uv-d,_UVRect.xy,_UVRect.zw)).a;
   float alpha=max(ar,max(ag,ab));
   half3 rgb=lerp(src.rgb,half3(ar,ag,ab)/max(alpha,.001),_Glitch*.9);
   half4 c=half4(rgb*(1+_Glitch*.3),lerp(src.a,alpha,_Glitch))*i.color;
   c.rgb=saturate(c.rgb);c.rgb*=lerp(1,_NeonEmission,smoothstep(.07,.72,max(c.r,max(c.g,c.b))));return c;
 }
 ENDHLSL
 }
 }
}
