Shader "Anomaly/SettingsDitherMask" {
 Properties {
 [PerRendererData] _MainTex("Sprite",2D)="white"{}
 _Dissolve("Dissolve",Range(0,1))=0
 _StencilComp("Stencil Comparison",Float)=8
 _Stencil("Stencil ID",Float)=0
 _StencilOp("Stencil Operation",Float)=0
 _StencilWriteMask("Stencil Write Mask",Float)=255
 _StencilReadMask("Stencil Read Mask",Float)=255
 _ColorMask("Color Mask",Float)=15
 [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip",Float)=0
 }
 SubShader{
 Tags{"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True"}
 Stencil {Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]}
 Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
 Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]
 Pass{HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;};
 float _Dissolve;
 V vert(A a){V o;o.p=UnityObjectToClipPos(a.p);return o;}
 half4 frag(V i):SV_Target{
 int2 p=(int2)floor(i.p.xy/3);
 int b[16]={0,8,2,10,12,4,14,6,3,11,1,9,15,7,13,5};
 float threshold=(b[(p.y%4)*4+p.x%4]+.5)/16.;
 clip(threshold-_Dissolve);
 return half4(1,1,1,1);
 }
 ENDHLSL}
 }
}