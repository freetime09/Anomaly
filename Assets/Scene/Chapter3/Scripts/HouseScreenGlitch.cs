using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Native URP distortion; the camera always keeps rendering to its display.</summary>
[DisallowMultipleComponent]
public sealed class HouseScreenGlitch : MonoBehaviour
{
    public Shader effectShader;
    public Camera targetCamera;
    public FullScreenPassRendererFeature renderFeature;
    public GlitchBreakEffect objectGlitch;
    [Range(0f, 2f)] public float objectBlend = 1.35f;
    [Min(0.5f)] public float repeatInterval = 3f;
    [Range(0f, 1f)] public float strength = 0.42f;

    public bool soundEnabled;
    [Range(0f, 1f)] public float staticVolume = .3f;
    AudioSource staticSource;
    AudioClip staticNoise;
    Material material, previousMaterial;
    bool repeating, rendering, ramping;
    float untilNext, age, duration, peak, seed;
    int variant;
    public bool IsRepeating => repeating;
    public bool IsRendering => rendering;
    public int BurstCount { get; private set; }
    public float CurrentAmount { get; private set; }

    public void BeginRepeating()
    {
        StopEffect();
        repeating = true;
        untilNext = repeatInterval;
        BeginBurst(strength);
    }

    public void PulseOnce(float amount) { BeginBurst(amount); }

    public void BeginRamp(float seconds, float amount)
    {
        StopEffect();
        if (!Activate()) return;
        ramping = true;
        age = 0f;
        duration = Mathf.Max(0.1f, seconds);
        peak = Mathf.Clamp01(amount);
        seed = Random.Range(1f, 1000f);
        variant = 0;
        Apply(0f, 0f);
    }

    bool Activate()
    {
        if (!isActiveAndEnabled || renderFeature == null) return false;
        if (effectShader == null) effectShader = Shader.Find("Hidden/AnomalyScreenGlitch");
        if (effectShader == null) return false;
        if (material == null) material = new Material(effectShader);
        if (!rendering) previousMaterial = renderFeature.passMaterial;
        renderFeature.passMaterial = material;
        renderFeature.SetActive(true);
        rendering = true;
        return true;
    }

    void BeginBurst(float amount)
    {
        if (!Activate()) return;
        ramping = false;
        age = 0f;
        duration = Random.Range(0.24f, 0.42f);
        peak = Mathf.Clamp01(amount * Random.Range(0.85f, 1.12f));
        seed = Random.Range(1f, 1000f);
        variant = (variant + Random.Range(1, 3)) % 3;
        BurstCount++;
        PlayStatic();
        Apply(0f, 0f);
    }

    void Update()
    {
        if (PauseMenu.IsPaused)
        {
            if (renderFeature != null) renderFeature.SetActive(false);
            if (objectGlitch != null) objectGlitch.SetIntensity(0f);
            return;
        }
        if (rendering && renderFeature != null) renderFeature.SetActive(true);
        float dt = Time.unscaledDeltaTime;
        if (rendering)
        {
            age += dt;
            if (ramping)
            {
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / duration));
                float flutter = 0.88f + 0.12f * Mathf.Sin(age * 17f + seed);
                Apply(peak * progress * flutter, progress * 0.55f);
            }
            else if (age >= duration) EndBurst();
            else
            {
                float envelope = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 0.035f))
                    * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(duration * 0.55f, duration, age)));
                Apply(peak * envelope, peak * envelope * 0.45f);
            }
        }
        if (!repeating) return;
        untilNext -= dt;
        if (untilNext <= 0f)
        {
            untilNext = Mathf.Max(0.5f, repeatInterval + Random.Range(-0.25f, 0.25f));
            BeginBurst(strength);
        }
    }

    void Apply(float amount, float bloom)
    {
        CurrentAmount = amount;
        if(amount>.03f)GlitchLightPulse.Pulse(variant==1?new Color(1f,.18f,.32f):new Color(.18f,.7f,1f),amount,.1f);
        if (objectGlitch != null) objectGlitch.SetIntensity(Mathf.Clamp01(amount * objectBlend));
        material.SetFloat("_Amount", amount);
        material.SetFloat("_Bloom", bloom);
        material.SetFloat("_NoiseTime", Time.unscaledTime);
        material.SetFloat("_Seed", seed);
        material.SetFloat("_Variant", variant);
    }

    void EndBurst()
    {
        if (renderFeature != null && renderFeature.passMaterial == material)
        {
            renderFeature.SetActive(false);
            renderFeature.passMaterial = previousMaterial;
        }
        rendering = false;
        ramping = false;
        CurrentAmount = 0f;
        if (objectGlitch != null) objectGlitch.SetIntensity(0f);
        previousMaterial = null;
        if (material != null) { material.SetFloat("_Amount", 0f); material.SetFloat("_Bloom", 0f); }
    }

    public void StopEffect()
    {
        repeating = false;
        if (staticSource != null) staticSource.Stop();
        untilNext = 0f;
        EndBurst();
    }

    void PlayStatic()
    {
        if (!soundEnabled) return;
        if (staticSource == null)
        {
            staticSource = gameObject.AddComponent<AudioSource>();
            staticSource.playOnAwake = false;
            staticSource.spatialBlend = 0;
            staticSource.ignoreListenerPause = false;
        }
        if (staticNoise == null)
        {
            const int rate = 44100, length = 12348;
            var samples = new float[length];
            uint state = 3571;
            float low = 0;
            for (int i = 0; i < length; i++)
            {
                state = 1664525u * state + 1013904223u;
                float noise = (state >> 8) / 16777215f * 2 - 1;
                low = Mathf.Lerp(low, noise, .22f);
                float phase = i / (float)(length - 1);
                float envelope = Mathf.Min(1, phase * 24) * Mathf.Pow(1 - phase, .8f);
                float gate = Mathf.Repeat(i / (float)rate, .039f) < .026f ? 1 : .1f;
                samples[i] = (noise - low) * envelope * gate * .65f;
            }
            staticNoise = AudioClip.Create("HouseStaticCrackle", length, 1, rate, false);
            staticNoise.SetData(samples, 0);
        }
        staticSource.pitch = Random.Range(.94f, 1.06f);
        staticSource.PlayOneShot(staticNoise, staticVolume);
    }

    void OnDisable() { StopEffect(); }
    void OnDestroy() { StopEffect(); if (material != null) Destroy(material); if (staticNoise != null) Destroy(staticNoise); }
}
