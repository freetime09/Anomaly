using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Keeps outgoing music alive while a scene loads.
public sealed class SceneMusicBridge : MonoBehaviour
{
    static SceneMusicBridge active;
    AudioSource outgoing, incoming;
    StoryMusicLoop outgoingStory;
    readonly System.Collections.Generic.List<AudioSource> ambient=new System.Collections.Generic.List<AudioSource>();
    readonly System.Collections.Generic.List<float> ambientVolume=new System.Collections.Generic.List<float>();
    float incomingVolume, incomingStoryFade;
    bool fading, incomingManaged;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() { active = null; }

    public static void Begin()
    {
        if (active != null) return;
        var camera = Camera.main;
        var source = camera != null ? camera.GetComponent<AudioSource>() : null;
        var bridge = new GameObject("SceneMusicBridge").AddComponent<SceneMusicBridge>();
        active = bridge;
        DontDestroyOnLoad(bridge.gameObject);
        bridge.outgoingStory = Object.FindFirstObjectByType<StoryMusicLoop>();
        if (bridge.outgoingStory != null)
        {
            // Preserve both scheduled voices and their overlap, including during the loading cover.
            bridge.outgoingStory.transform.SetParent(bridge.transform, true);
        }
        else if (source != null && source.clip != null && source.isPlaying)
        {
            bridge.outgoing = bridge.gameObject.AddComponent<AudioSource>();
            var copy = bridge.outgoing;
            copy.playOnAwake = false;
            copy.clip = source.clip;
            copy.loop = source.loop;
            copy.volume = source.volume;
            copy.pitch = source.pitch;
            copy.outputAudioMixerGroup = source.outputAudioMixerGroup;
            copy.spatialBlend = 0f;
            copy.timeSamples = source.timeSamples;
            copy.Play();
            source.Stop();
        }
        foreach(var marker in Object.FindObjectsByType<SceneAmbientLoop>(FindObjectsSortMode.None)){var original=marker.GetComponent<AudioSource>();if(original==null||!original.isPlaying||original.clip==null)continue;var copy=bridge.gameObject.AddComponent<AudioSource>();copy.playOnAwake=false;copy.clip=original.clip;copy.loop=original.loop;copy.volume=original.volume;copy.timeSamples=original.timeSamples;copy.outputAudioMixerGroup=original.outputAudioMixerGroup;copy.Play();bridge.ambient.Add(copy);bridge.ambientVolume.Add(original.volume);original.Stop();}
        SceneManager.sceneLoaded += bridge.Loaded;
    }
    void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || fading) return;
        fading = true;
        var camera = Camera.main;
        incoming = camera != null ? camera.GetComponent<AudioSource>() : null;
        if (outgoingStory == null)
        {
            var story = Object.FindFirstObjectByType<StoryMusicLoop>();
            if (story != null) incomingStoryFade = story.fadeInDuration;
        }
        if (incoming != null)
        {
            incomingManaged=incoming.GetComponent<DelayedMusicFadeIn>()!=null;
            incomingVolume = incoming.volume;
            if(!incomingManaged)incoming.volume = 0f;
        }
        StartCoroutine(Crossfade());
    }
    IEnumerator Crossfade()
    {
        yield return null; // allow scene initialization and SoundLevels to finish
        if (!incomingManaged && incoming != null && outgoing != null && incoming.clip != null &&
            incoming.clip.name == outgoing.clip.name &&
            incoming.clip.samples == outgoing.clip.samples)
        {
            incoming.timeSamples = Mathf.Min(outgoing.timeSamples, incoming.clip.samples - 1);
            if (!incoming.isPlaying) incoming.Play();
            incoming.volume = incomingVolume;
            outgoing.Stop();
        }
        else
        {
            float duration=outgoingStory!=null?2.4f:incomingStoryFade>0?incomingStoryFade:UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="LastScene"?2.8f:1.25f;
            float from = outgoing != null ? outgoing.volume : 0f;
            float storyFrom = outgoingStory != null ? outgoingStory.TransitionVolume : 0f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                if (outgoing != null) outgoing.volume = from * Mathf.Cos(k * Mathf.PI * .5f);
                if (outgoingStory != null) outgoingStory.TransitionVolume = storyFrom * Mathf.Cos(k * Mathf.PI * .5f);
                for(int j=0;j<ambient.Count;j++)if(ambient[j]!=null)ambient[j].volume=ambientVolume[j]*Mathf.Cos(k*Mathf.PI*.5f);
                if (incoming != null && !incomingManaged) incoming.volume = incomingVolume * Mathf.Sin(k * Mathf.PI * .5f);
                yield return null;
            }
            if (incoming != null && !incomingManaged) incoming.volume = incomingVolume;
        }
        Destroy(gameObject);
    }
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= Loaded;
        if (active == this) active = null;
    }
}
