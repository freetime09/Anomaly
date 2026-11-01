float4 _Frame;
float _Glitch,_Clock;
float hudHash(float n){return frac(sin(n*12.9898+floor(_Clock*28)*7.113)*43758.5453);}
float2 hudUV(float2 local)
{
    return lerp(_Frame.xy,_Frame.zw,saturate(local));
}
float4 hudSample(sampler2D textureSampler,float2 uv)
{
    float2 span=max(_Frame.zw-_Frame.xy,.00001);
    float2 local=(uv-_Frame.xy)/span;
    float band=floor(local.y*12);
    float tear=(hudHash(band)-.5)*.065*_Glitch*step(.66,hudHash(band+3));
    local.x+=tear;
    float4 c=tex2D(textureSampler,hudUV(local));
    float shift=.012*_Glitch;
    c.r=tex2D(textureSampler,hudUV(local+float2(shift,0))).r;
    c.b=tex2D(textureSampler,hudUV(local-float2(shift,0))).b;
    float valid=step(0,local.x)*step(local.x,1)*step(0,local.y)*step(local.y,1);
    c.rgb*=1-_Glitch*.12*step(.82,hudHash(band+11));c.a*=valid;
    return c;
}
