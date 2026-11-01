using UnityEngine;

public static class RuntimeFramePacing
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Configure()
    {
        // Synchronize desktop rendering instead of exhausting the GPU on uncapped frames.
        if(!Application.isMobilePlatform){
            if(QualitySettings.vSyncCount==0)QualitySettings.vSyncCount=1;
            Application.targetFrameRate=Mathf.Clamp(Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value),60,240);
        }
    }
}
