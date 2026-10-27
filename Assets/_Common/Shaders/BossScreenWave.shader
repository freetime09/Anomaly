Shader "Anomaly/BossScreenWave" {
SubShader { Tags {"RenderPipeline"="UniversalPipeline"} ZWrite Off ZTest Always Cull Off
Pass {
HLSLPROGRAM
#pragma vertex Vert
#pragma fragment Frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
float4 _BossWaveParams;
float4 _BossWaveCenter;
half4 Frag(Varyings i):SV_Target {
float2 uv=i.texcoord;
float2 d=(uv-_BossWaveCenter.xy)*float2(_BossWaveParams.z,1);
float r=length(d);float t=_BossWaveParams.x;
float front=t*1.9;
float envelope=exp(-pow((r-front)/.17,2))*pow(saturate(1-t),1.5);
float displacement=sin((r-front)*58)*_BossWaveParams.y*envelope;
uv+=d/max(r,.001)*float2(1/_BossWaveParams.z,1)*displacement;
return SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,saturate(uv));
}
ENDHLSL
}
}
}