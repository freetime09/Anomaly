using System.Collections.Generic;
using UnityEngine;

/// <summary>One audible code explosion per clip at a time, shared by 404 blocks and breakable code blocks.</summary>
public static class CodeExplosionAudio
{
    static readonly Dictionary<AudioClip, double> busyUntil = new Dictionary<AudioClip, double>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { busyUntil.Clear(); }

    public static void PlayBreak(AudioClip clip, float volume)
    {
        if (clip == null || !TryClaim(clip)) return;
        var go = new GameObject("CodeBlockBreakSfx");
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.Play();
        Object.Destroy(go, clip.length + .1f);
    }

    public static bool TryClaim(AudioClip clip)
    {
        if (clip == null) return false;
        // Other object effects keep their existing behavior.
        if (clip.name != "Explosion") return true;
        double now = AudioSettings.dspTime;
        double until;
        if (busyUntil.TryGetValue(clip, out until) && now < until) return false;
        busyUntil[clip] = now + Mathf.Max(0.10f, clip.length);
        return true;
    }
}
