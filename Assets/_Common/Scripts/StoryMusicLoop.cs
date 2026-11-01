using System.Collections;
using UnityEngine;

// Two scheduled voices overlap the end and beginning without restarting the music at scene load.
public sealed class StoryMusicLoop : MonoBehaviour
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = .32f;
    [Min(.1f)] public float loopCrossfade = 2.4f;
    [Min(.1f)] public float fadeInDuration = 2.5f;

    public float TransitionVolume { get; set; } = 1f;

    readonly AudioSource[] voices = new AudioSource[2];
    double firstStart, nextStart, hop;
    float overlap;
    int current;
    bool started;
    float levelGain = 1f;

    IEnumerator Start()
    {
        if (clip == null) yield break;
        // Apply the same level table as source gain, keeping a long music clip compressed.
        var levels = Resources.Load<SoundLevels>("SoundLevels");
        if (levels != null)
        {
            var entry = levels.entries.Find(item => item.clip == clip);
            levelGain = Mathf.Pow(10f, (levels.masterDb + (entry != null ? entry.GainDb : 0f)) / 20f);
        }
        clip.LoadAudioData();
        while (clip.loadState == AudioDataLoadState.Loading) yield return null;
        if (clip.loadState != AudioDataLoadState.Loaded) yield break;

        double length = clip.samples / (double)clip.frequency;
        overlap = Mathf.Clamp(loopCrossfade, .01f, (float)length * .25f);
        hop = length - overlap;
        for (int i = 0; i < voices.Length; i++)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.clip = clip;
            source.loop = false;
            source.spatialBlend = 0f;
            source.reverbZoneMix = 0f;
            source.dopplerLevel = 0f;
            source.priority = 64;
            source.volume = 0f;
            voices[i] = source;
        }

        firstStart = AudioSettings.dspTime + .15;
        nextStart = firstStart + hop;
        Schedule(voices[0], firstStart);
        Schedule(voices[1], nextStart);
        started = true;
    }

    void Schedule(AudioSource source, double when)
    {
        source.Stop();
        source.volume = 0f;
        source.timeSamples = 0;
        source.PlayScheduled(when);
        source.SetScheduledEndTime(when + clip.samples / (double)clip.frequency);
    }

    void Update()
    {
        if (!started) return;
        double now = AudioSettings.dspTime;
        if (now >= nextStart + overlap)
        {
            int finished = current;
            current = 1 - current;
            nextStart += hop;
            Schedule(voices[finished], nextStart);
        }

        float entry = Mathf.SmoothStep(0f, 1f, (float)(now - firstStart) / Mathf.Max(.01f, fadeInDuration));
        float gain = volume * levelGain * entry * Mathf.Clamp01(TransitionVolume);
        float blend = Mathf.Clamp01((float)(now - nextStart) / overlap);
        voices[current].volume = gain * Mathf.Cos(blend * Mathf.PI * .5f);
        voices[1 - current].volume = gain * Mathf.Sin(blend * Mathf.PI * .5f);
    }

    void OnDisable()
    {
        foreach (var source in voices) if (source != null) source.Stop();
        started = false;
    }
}
