Shader "Anomaly/BossNeonSprite"
{
 Properties {
 _MainTex("Sprite",2D)="white"{} _Color("Tint",Color)=(1,1,1,1)
 _NeonEmission("Emission",Float)=3.2 _RectWarning("Warning rim",Float)=0 _WorldSize("World size",Vector)=(1,1,0,0)
 [HideInInspector]_RendererColor("Renderer color",Color)=(1,1,1,1)
 [HideInInspector]_AlphaTex("External alpha",2D)="white"{} [HideInInspector]_EnableExternalAlpha("External alpha enabled",Float)=0
 }
 SubShader {
 Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
 Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha Cull Off ZWrite Off
 Pass {HLSLPROGRAM
 #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 struct Attributes {COMMON_2D_INPUTS half4 color:COLOR; UNITY_SKINNED_VERTEX_INPUTS};
 struct Varyings {COMMON_2D_OUTPUTS half4 color:COLOR;};
 #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
 CBUFFER_START(UnityPerMaterial)
 half4 _Color;float4 _WorldSize;float _NeonEmission,_RectWarning;
 CBUFFER_END
 Varyings Vert(Attributes i){UNITY_SKINNED_VERTEX_COMPUTE(i);SetUpSpriteInstanceProperties();i.positionOS=UnityFlipSprite(i.positionOS,unity_SpriteProps.xy);Varyings o=CommonUnlitVertex(i);o.color=i.color*_Color*unity_SpriteColor;return o;}
 half4 Frag(Varyings i):SV_Target {
 half4 c=CommonUnlitFragment(i,i.color);
 if(_RectWarning>.5){float2 d=min(i.uv,1-i.uv)*_WorldSize.xy;float rim=1-smoothstep(.035,.075,min(d.x,d.y));c.rgb*=lerp(.85,_NeonEmission,rim);c.a=lerp(c.a*.55,saturate(c.a*1.9),rim);}
 else c.rgb*=lerp(1,_NeonEmission,smoothstep(.07,.72,max(c.r,max(c.g,c.b))));
 return c;
 }
 ENDHLSL}
 }
}
