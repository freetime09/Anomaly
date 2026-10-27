using UnityEngine;
using UnityEngine.Rendering.Universal;
public sealed class BossScreenWaveFeature : FullScreenPassRendererFeature
{
    public static Camera Target;
    public static float Strength;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState(){Target=null;Strength=0;Shader.SetGlobalVector("_BossWaveParams",Vector4.zero);}
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if(!Application.isPlaying || Strength<=0 || Target==null || renderingData.cameraData.camera!=Target)return;
        base.AddRenderPasses(renderer,ref renderingData);
    }
}
