Shader "Anomaly/BossHudGlitchSprite"
{
 Properties {
 _MainTex("Sprite",2D)="white"{} _Color("Tint",Color)=(1,1,1,1) _Frame("Frame",Vector)=(0,0,1,1) _Glitch("Glitch",Float)=0 _Clock("Clock",Float)=0
 [HideInInspector]_RendererColor("Renderer color",Color)=(1,1,1,1) [HideInInspector]_AlphaTex("Alpha",2D)="white"{} [HideInInspector]_EnableExternalAlpha("Use alpha",Float)=0
 }
 SubShader {Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"} Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off
 Pass {HLSLPROGRAM
 #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 struct Attributes{COMMON_2D_INPUTS half4 color:COLOR;UNITY_SKINNED_VERTEX_INPUTS};
 struct Varyings{COMMON_2D_OUTPUTS half4 color:COLOR;};
 #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
 float4 _Frame;float _Glitch,_Clock;half4 _Color;
 float hash(float n){return frac(sin(n*12.9898+floor(_Clock*28)*7.113)*43758.5453);}
 Varyings vert(Attributes i){UNITY_SKINNED_VERTEX_COMPUTE(i);SetUpSpriteInstanceProperties();i.positionOS=UnityFlipSprite(i.positionOS,unity_SpriteProps.xy);Varyings o=CommonUnlitVertex(i);o.color=i.color*_Color*unity_SpriteColor;return o;}
 half4 frag(Varyings i):SV_Target{
 float2 span=max(_Frame.zw-_Frame.xy,.00001);float2 local=(i.uv-_Frame.xy)/span;float band=floor(local.y*12);
 local.x+=(hash(band)-.5)*.065*_Glitch*step(.66,hash(band+3));float2 uv=lerp(_Frame.xy,_Frame.zw,saturate(local));float2 shift=float2(span.x*.012*_Glitch,0);
 half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv);c.r=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,clamp(uv+shift,_Frame.xy,_Frame.zw)).r;c.b=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,clamp(uv-shift,_Frame.xy,_Frame.zw)).b;
 c.a*=step(0,local.x)*step(local.x,1);c.rgb*=1-_Glitch*.12*step(.82,hash(band+11));return c*i.color;
 }
 ENDHLSL}}
}
