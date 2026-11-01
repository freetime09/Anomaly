Shader "UI/AnomalySoftGlow"{
 Properties{
 [PerRendererData]_MainTex("Sprite",2D)="white"{}
 _Color("Tint",Color)=(1,1,1,1)
 _StencilComp("Stencil Comparison",Float)=8 _Stencil("Stencil ID",Float)=0 _StencilOp("Stencil Operation",Float)=0
 _StencilWriteMask("Stencil Write Mask",Float)=255 _StencilReadMask("Stencil Read Mask",Float)=255 _ColorMask("Color Mask",Float)=15
 [Toggle(UNITY_UI_ALPHACLIP)]_UseUIAlphaClip("Use Alpha Clip",Float)=0
 
 _CoreSize("Core Size",Vector)=(100,10,0,0) _OuterSize("Outer Size",Vector)=(120,30,0,0) _Radius("Glow Radius",Float)=8
 _Strength("Emission Strength",Float)=1
 }
 SubShader{ Tags{"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True"}
 Stencil{Ref[_Stencil] Comp[_StencilComp] Pass[_StencilOp] ReadMask[_StencilReadMask] WriteMask[_StencilWriteMask]}
 Cull Off Lighting Off ZWrite Off ZTest[unity_GUIZTestMode] ColorMask[_ColorMask]
  Blend SrcAlpha One
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
 
 float2 _CoreSize,_OuterSize;float _Radius,_Strength;
 fixed4 frag(V i):SV_Target{
 float2 p=(i.uv-.5)*_OuterSize;float2 q=abs(p)-_CoreSize*.5;float d=length(max(q,0))+min(max(q.x,q.y),0);
 float a=exp(-pow(max(d,0)/max(_Radius,.1),2)*2);
 fixed4 c=i.color;c.rgb*=_Strength;c.a*=a;
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