Shader "Anomaly/WakeDither" { Properties{_Reveal("Reveal",Range(0,1))=0 _MainTex("Texture",2D)="white"{}}
SubShader{Tags{"Queue"="Overlay" "RenderType"="Transparent"} Blend SrcAlpha OneMinusSrcAlpha ZTest Always ZWrite Off Cull Off Pass{HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
float _Reveal; struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;};
V vert(A a){V o;o.p=UnityObjectToClipPos(a.p);return o;}
half4 frag(V i):SV_Target{int2 p=(int2)floor(i.p.xy/4);int x=p.x%4,y=p.y%4;int b[16]={0,8,2,10,12,4,14,6,3,11,1,9,15,7,13,5};float threshold=(b[y*4+x]+.5)/16.;return half4(0,0,0,step(_Reveal,threshold));}
ENDHLSL}}}