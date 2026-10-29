Shader "Anomaly/BossLiquidGauge" {
Properties { _Opening("Opening: red=0 green=1",Float)=0 [PerRendererData] _MainTex("Bar",2D)="white" {} _Fill("Fill",Range(0,1))=1 _Trail("Delayed fill",Range(0,1))=1 _TrailTint("Damage trail",Color)=(1,.6,.2,1) _Flash("Hit",Float)=0 _Clock("Clock",Float)=0 _Glitch("Glitch",Float)=0 }
SubShader {Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True"}
Cull Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha
Pass {
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct A{float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
struct V{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
sampler2D _MainTex;float _Opening;float _Fill,_Trail,_Flash,_Clock,_Glitch;float4 _TrailTint;
V vert(A a){V o;o.vertex=UnityObjectToClipPos(a.vertex);o.uv=a.uv;o.color=a.color;return o;}
fixed4 frag(V i):SV_Target{
float2 uv=i.uv;
float band=floor(uv.y*7);float tear=sin(band*17+floor(_Clock*24)*5)*.006*_Glitch;
uv.x+=tear;
fixed4 original=tex2D(_MainTex,saturate(uv));
float wave=sin(uv.y*7+_Clock*3.4)*.0025+sin(uv.y*15-_Clock*2.1)*.0015;
float edge=_Fill+wave*step(.001,_Fill)*step(_Fill,.999);
float filled=step(uv.x,edge)*step(.0001,_Fill);
float delayed=step(uv.x,_Trail)*step(.0001,_Trail)*(1-filled);
float shine=pow(saturate(sin(uv.x*17-_Clock*1.5+sin(uv.y*5+_Clock)*.9)),12)*.18;
float ripple=sin(uv.x*24+uv.y*4-_Clock*2)*.045;
float3 lit=original.rgb*(1+ripple)+float3(1,1,1)*(shine+.055*(1-abs(uv.y-.5)*2));
lit=lerp(lit,float3(1,1,1),_Flash*.32);
float3 rgb=filled*lit+delayed*_TrailTint.rgb*.85;
float px=lerp(lerp(1,255,i.uv.x),lerp(4,252,i.uv.x),_Opening);
float row=floor(lerp(lerp(1,15,i.uv.y),lerp(16,26,i.uv.y),_Opening));
float inset;
if(_Opening>.5){inset=row<17?6:(row<18?5:(row<23?4:(row<24?5:(row<25?6:7))));}
else{inset=row<2?3:(row<3?2:(row<13?1:(row<14?2:3)));}
float mask=step(inset,px)*step(px,256-inset);
float alpha=mask*(filled+delayed*.85);
return float4(rgb,alpha)*i.color;
}
ENDCG
}
}
}