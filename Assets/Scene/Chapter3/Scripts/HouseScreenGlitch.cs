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
        untilNext = 0f;
        EndBurst();
    }

    void OnDisable() { StopEffect(); }
    void OnDestroy() { StopEffect(); if (material != null) Destroy(material); }
}
