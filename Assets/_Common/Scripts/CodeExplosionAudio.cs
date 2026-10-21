using System.Collections.Generic;
using UnityEngine;

/// <summary>One audible code explosion per clip at a time, shared by 404 blocks and breakable code blocks.</summary>
public static class CodeExplosionAudio
{
    static readonly Dictionary<AudioClip, double> busyUntil = new Dictionary<AudioClip, double>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { busyUntil.Clear(); }

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
