using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Keeps the outgoing camera music alive while a scene loads.
public sealed class SceneMusicBridge : MonoBehaviour
{
    static SceneMusicBridge active;
    AudioSource outgoing, incoming;
    float incomingVolume;
    bool fading;
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
        if (source != null && source.clip != null && source.isPlaying)
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
        SceneManager.sceneLoaded += bridge.Loaded;
    }
    void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || fading) return;
        fading = true;
        var camera = Camera.main;
        incoming = camera != null ? camera.GetComponent<AudioSource>() : null;
        if (incoming != null)
        {
            incomingVolume = incoming.volume;
            incoming.volume = 0f;
        }
        StartCoroutine(Crossfade());
    }
    IEnumerator Crossfade()
    {
        yield return null; // allow scene initialization and SoundLevels to finish
        if (incoming != null && outgoing != null && incoming.clip != null &&
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
            float from = outgoing != null ? outgoing.volume : 0f;
            for (float t = 0f; t < 1.25f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 1.25f);
                if (outgoing != null) outgoing.volume = from * Mathf.Cos(k * Mathf.PI * .5f);
                if (incoming != null) incoming.volume = incomingVolume * Mathf.Sin(k * Mathf.PI * .5f);
                yield return null;
            }
            if (incoming != null) incoming.volume = incomingVolume;
        }
        Destroy(gameObject);
    }
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= Loaded;
        if (active == this) active = null;
    }
}
