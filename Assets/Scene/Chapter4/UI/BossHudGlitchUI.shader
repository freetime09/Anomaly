Shader "UI/AnomalyBossHudGlitch"
{
 Properties {
 [PerRendererData]_MainTex("Sprite",2D)="white"{} _Color("Tint",Color)=(1,1,1,1)
 _Frame("Frame",Vector)=(0,0,1,1) _Glitch("Glitch",Float)=0 _Clock("Clock",Float)=0
 _StencilComp("Stencil Comparison",Float)=8 _Stencil("Stencil ID",Float)=0 _StencilOp("Stencil Operation",Float)=0
 _StencilWriteMask("Stencil Write Mask",Float)=255 _StencilReadMask("Stencil Read Mask",Float)=255 _ColorMask("Color Mask",Float)=15
 [Toggle(UNITY_UI_ALPHACLIP)]_UseUIAlphaClip("Alpha clip",Float)=0
 }
 SubShader {
 Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True"}
 Stencil {Ref[_Stencil] Comp[_StencilComp] Pass[_StencilOp] ReadMask[_StencilReadMask] WriteMask[_StencilWriteMask]}
 Cull Off ZWrite Off ZTest[unity_GUIZTestMode] ColorMask[_ColorMask] Blend SrcAlpha OneMinusSrcAlpha
 Pass {CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
 #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
 #include "UnityCG.cginc"
 #include "UnityUI.cginc"
 #include "BossHudGlitch.cginc"
 sampler2D _MainTex;float4 _Color,_ClipRect;
 struct A{float4 p:POSITION;float2 uv:TEXCOORD0;float4 c:COLOR;};
 struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float4 c:COLOR;float4 local:TEXCOORD1;};
 V vert(A i){V o;o.local=i.p;o.p=UnityObjectToClipPos(i.p);o.uv=i.uv;o.c=i.c*_Color;return o;}
 fixed4 frag(V i):SV_Target{float4 c=hudSample(_MainTex,i.uv);c*=i.c;
 #ifdef UNITY_UI_CLIP_RECT
 c.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
 #endif
 #ifdef UNITY_UI_ALPHACLIP
 clip(c.a-.001);
 #endif
 return c;}
 ENDCG}
 }
}
