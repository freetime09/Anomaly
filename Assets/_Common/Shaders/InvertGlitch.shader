// 화면 스냅샷을 받아 "색상 반전 + 글리치(RGB 분리·가로 블록 찢김·스캔라인)"로 그린다.
// 타이틀에서 O를 그렸을 때 "버그/이상현상 난 것처럼" 연출하는 용도.
// UGUI RawImage 위에서 동작(Overlay 캔버스). _Glitch/_Invert 를 스크립트에서 0→1 로 올리며 연출.
Shader "UI/InvertGlitch"
{
    Properties
    {
        [PerRendererData] _MainTex ("Snapshot", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Glitch ("Glitch", Range(0,1)) = 0
        _Invert ("Invert", Range(0,1)) = 0
        _Split ("RGB Split", Float) = 0.02

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };

            sampler2D _MainTex;
            fixed4 _Color;
            float _Glitch, _Invert, _Split;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            float rand(float2 c){ return frac(sin(dot(c, float2(12.9898, 78.233))) * 43758.5453); }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;
                float t = _Time.y;

                // 가로 줄 단위 흔들림 (작은 떨림)
                float row = floor(uv.y * 40.0);
                float jitter = (rand(float2(row, floor(t * 18.0))) - 0.5) * _Glitch * 0.12;
                // 가끔 큰 블록이 크게 찢김
                float bigSel = step(0.82, rand(float2(row * 0.13, floor(t * 11.0))));
                float big = bigSel * (rand(float2(row, floor(t * 7.0))) - 0.5) * _Glitch * 0.5;
                uv.x += jitter + big;

                // RGB 채널 분리
                float off = _Split * _Glitch;
                float r = tex2D(_MainTex, uv + float2(off, 0)).r;
                float g = tex2D(_MainTex, uv).g;
                float b = tex2D(_MainTex, uv - float2(off, 0)).b;
                float3 col = float3(r, g, b);

                // 색상 반전
                col = lerp(col, 1.0 - col, _Invert);

                // 스캔라인 살짝 어둡게
                float scan = 1.0 - _Glitch * 0.22 * step(0.5, frac(uv.y * 220.0));
                col *= scan;

                // 스냅샷은 화면 전체를 덮는 불투명 이미지 → 알파 1 고정 (RT 알파가 0이어도 보이게)
                return fixed4(col, 1.0) * i.color;
            }
            ENDCG
        }
    }
}
