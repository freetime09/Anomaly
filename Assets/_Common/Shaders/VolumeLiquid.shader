Shader "UI/AnomalyVolumeLiquid"{
 Properties{
 [PerRendererData]_MainTex("Sprite",2D)="white"{}
 _Color("Tint",Color)=(1,1,1,1)
 _StencilComp("Stencil Comparison",Float)=8 _Stencil("Stencil ID",Float)=0 _StencilOp("Stencil Operation",Float)=0
 _StencilWriteMask("Stencil Write Mask",Float)=255 _StencilReadMask("Stencil Read Mask",Float)=255 _ColorMask("Color Mask",Float)=15
 [Toggle(UNITY_UI_ALPHACLIP)]_UseUIAlphaClip("Use Alpha Clip",Float)=0
 
 _FlowTime("Flow Time",Float)=0 _RectSize("Size",Vector)=(100,20,0,0) _Pivot("Pivot",Vector)=(.5,.5,0,0)
 }
 SubShader{ Tags{"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True"}
 Stencil{Ref[_Stencil] Comp[_StencilComp] Pass[_StencilOp] ReadMask[_StencilReadMask] WriteMask[_StencilWriteMask]}
 Cull Off Lighting Off ZWrite Off ZTest[unity_GUIZTestMode] ColorMask[_ColorMask]
  Blend SrcAlpha OneMinusSrcAlpha
 Pass{CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
 #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
 #include "UnityCG.cginc"
 #include "UnityUI.cginc"
 struct A{float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
 struct V{float4 vertex:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float4 local:TEXCOORD1;};
 float4 _Color,_ClipRect;
 V vert(A v){V o;o.local=v.vertex;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;o.uv=v.uv;return o;}
 
 float _FlowTime;float2 _RectSize,_Pivot;
 fixed4 frag(V i):SV_Target{
 float2 uv=i.uv;
 float t=_FlowTime;float surface=.82+.047*sin(uv.x*14-t*1.7)+.025*sin(uv.x*27+t*.9);
 float body=smoothstep(.02,.08,uv.y)*(1-smoothstep(surface-.035,surface+.015,uv.y));
 float crest=exp(-abs(uv.y-surface+.035)*45);
 float shine=pow(saturate(.5+.5*sin(uv.x*9-t*1.1)),12)*.16;
 fixed4 c=i.color;c.rgb=lerp(c.rgb*.65,c.rgb*1.16,saturate(uv.y+.15))+crest*.4+shine;
 c.a*=body*smoothstep(0,.018,uv.x)*smoothstep(0,.018,1-uv.x);
 #ifdef UNITY_UI_CLIP_RECT
 c.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
 #endif
 #ifdef UNITY_UI_ALPHACLIP
 clip(c.a-.001);
 #endif
 return c;
 }ENDCG}
 }
}