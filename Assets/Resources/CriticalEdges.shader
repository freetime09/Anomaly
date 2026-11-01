Shader "UI/AnomalyCriticalEdges"
{
    Properties { [PerRendererData] _MainTex("Mask",2D)="white"{} _PulseTime("Time",Float)=0 }
    SubShader {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct A {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct V {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            float _PulseTime;
            V vert(A a){V o;o.vertex=UnityObjectToClipPos(a.vertex);o.uv=a.uv;o.color=a.color;return o;}
            fixed4 frag(V i):SV_Target{
                float2 uv=i.uv;
                float edge=min(min(uv.x,1-uv.x),min(uv.y,1-uv.y));
                float wave=sin(uv.x*13+_PulseTime*1.9)*sin(uv.y*9-_PulseTime*1.4);
                float width=.105+.018*wave+.012*sin(_PulseTime*3.1);
                float mask=1-smoothstep(0,width,edge);
                float corner=.45+.55*pow(saturate(abs(uv.x-.5)*abs(uv.y-.5)*4),.65);
                return fixed4(.72,.012,.025,mask*mask*corner*.7*i.color.a);
            }
            ENDCG
        }
    }
}
