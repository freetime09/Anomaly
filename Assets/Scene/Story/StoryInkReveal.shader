Shader "UI/AnomalyStoryInkReveal"
{
 Properties {
  [PerRendererData]_MainTex("Picture",2D)="white"{} _Color("Tint",Color)=(1,1,1,1)
  _Reveal("Reveal",Range(0,1))=1 _Smudge("Wet ink",Range(0,1))=0 _Seed("Seed",Float)=0
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
   sampler2D _MainTex;float4 _MainTex_TexelSize,_Color,_ClipRect;
   float _Reveal,_Smudge,_Seed;
   struct A {float4 p:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;float4 local:TEXCOORD1;};
   V vert(A i){V o;o.local=i.p;o.p=UnityObjectToClipPos(i.p);o.uv=i.uv;o.color=i.color*_Color;return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7))+_Seed)*43758.5453);}
   float noise(float2 p){float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(cell),hash(cell+float2(1,0)),f.x),lerp(hash(cell+float2(0,1)),hash(cell+1),f.x),f.y);}
   fixed4 frag(V i):SV_Target {
    float2 uv=i.uv+float2(sin(i.uv.y*35+_Seed),sin(i.uv.x*27-_Seed))*.003*_Smudge;
    fixed4 c=tex2D(_MainTex,saturate(uv));
    if(_Smudge>.001){
     float2 radius=_MainTex_TexelSize.xy*(2+_Smudge*13);
     c*=2;
     c+=tex2D(_MainTex,saturate(uv+radius*float2(1,0)))+tex2D(_MainTex,saturate(uv-radius*float2(1,0)));
     c+=tex2D(_MainTex,saturate(uv+radius*float2(0,1)))+tex2D(_MainTex,saturate(uv-radius*float2(0,1)));
     c+=tex2D(_MainTex,saturate(uv+radius*.7))+tex2D(_MainTex,saturate(uv-radius*.7));
     c+=tex2D(_MainTex,saturate(uv+radius*float2(.7,-.7)))+tex2D(_MainTex,saturate(uv+radius*float2(-.7,.7)));
     c*=.1;
    }
    float grain=noise(i.uv*float2(12,8))*.7+noise(i.uv*float2(38,25))*.3;
    c.a*=smoothstep(grain-.2,grain+.2,lerp(-.3,1.3,_Reveal));c*=i.color;
    #ifdef UNITY_UI_CLIP_RECT
     c.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
    #endif
    #ifdef UNITY_UI_ALPHACLIP
     clip(c.a-.001);
    #endif
    return c;
   }
  ENDCG}
 }
}
