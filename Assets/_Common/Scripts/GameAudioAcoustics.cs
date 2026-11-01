using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameAudioAcoustics : MonoBehaviour
{
    float nextScan;
    static readonly HashSet<int> configured = new HashSet<int>();
    readonly HashSet<int> live = new HashSet<int>();
    readonly List<GameObject> roots = new List<GameObject>(256);
    readonly List<AudioSource> sources = new List<AudioSource>(32);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState(){configured.Clear();}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        var go = new GameObject("SoftSoundReverb");
        DontDestroyOnLoad(go); go.AddComponent<GameAudioAcoustics>();
    }
    void LateUpdate()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + .5f;
        live.Clear();
        for(int i=0;i<SceneManager.sceneCount;i++)Scan(SceneManager.GetSceneAt(i));
        Scan(gameObject.scene);
        configured.IntersectWith(live);
    }
    void Scan(Scene scene)
    {
        if(!scene.IsValid()||!scene.isLoaded)return;
        if(roots.Capacity<=scene.rootCount)roots.Capacity=scene.rootCount+1;
        scene.GetRootGameObjects(roots);
        foreach(var root in roots){
            root.GetComponentsInChildren(false,sources);
            foreach(var source in sources){live.Add(source.GetInstanceID());Apply(source);}
        }
    }
    public static void Apply(AudioSource source)
    {
        if (source == null || !configured.Add(source.GetInstanceID())) return;
        if (source.GetComponent<AudioReverbFilter>() != null) return;
        if (source.GetComponent<Camera>() != null || source.GetComponent<SceneMusicBridge>() != null ||
            source.GetComponent<SceneAmbientLoop>() != null || source.GetComponent<DelayedMusicFadeIn>() != null) return;
        string name = source.name.ToLowerInvariant();
        if (name.Contains("music") || name.Contains("ambien") || name.Contains("heartbeat") || name.Contains("bird")) return;
        var filter = source.gameObject.AddComponent<AudioReverbFilter>();
        filter.reverbPreset = AudioReverbPreset.User;
        filter.dryLevel = 0; filter.room = -2500; filter.roomHF = -3000;
        filter.decayTime = .58f; filter.decayHFRatio = .45f;
        filter.reflectionsLevel = -3200; filter.reflectionsDelay = .018f;
        filter.reverbLevel = -2400; filter.reverbDelay = .028f;
        filter.diffusion = 75; filter.density = 65;
    }
}
