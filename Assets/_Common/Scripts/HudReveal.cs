using System.Collections;
using UnityEngine;

/// <summary>Fades the skill HUD in once. Tutorial reveal remains driven by its teleporter event.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(500)]
public class HudReveal : MonoBehaviour
{
    public float fadeDuration = 0.6f;
    public bool waitForVortex = true;
    public float extraDelay = 0f;
    [Tooltip("Enable for chapter starts; leave off for the tutorial's first reveal event.")]
    public bool revealOnStart = false;
    [Tooltip("Small left-to-right delay between icons.")]
    public float staggerDelay = 0.065f;

    SpriteRenderer[] rends;
    float[] baseAlpha;
    float[] delays;
    bool revealed;
    float elapsed = -1f;
    Coroutine co;

    void Start()
    {
        Initialize();
        if (revealOnStart) Reveal();
    }

    void Initialize()
    {
        if (rends != null) return;
        rends = GetComponentsInChildren<SpriteRenderer>(true);
        baseAlpha = new float[rends.Length];
        delays = new float[rends.Length];
        for (int i = 0; i < rends.Length; i++)
        {
            baseAlpha[i] = rends[i].color.a;
            Transform slot = rends[i].transform;
            while (slot.parent != null && slot.parent != transform) slot = slot.parent;
            delays[i] = slot.parent == transform ? slot.GetSiblingIndex() * staggerDelay : 0f;
            SetAlpha(i, 0f);
        }
    }

    public void Reveal()
    {
        if (revealed) return;
        Initialize();
        revealed = true;
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(RevealRoutine());
    }

    IEnumerator RevealRoutine()
    {
        if (waitForVortex)
        {
            yield return null;
            while (VortexTransition.Instance != null && VortexTransition.Instance.IsPlaying)
                yield return null;
        }
        if (extraDelay > 0f) yield return new WaitForSecondsRealtime(extraDelay);
        float end = Mathf.Max(0.001f, fadeDuration);
        for (int i = 0; i < delays.Length; i++)
            end = Mathf.Max(end, delays[i] + Mathf.Max(0.001f, fadeDuration));
        float t = 0f;
        while (t < end)
        {
            t += Time.unscaledDeltaTime;
            elapsed = t;
            for (int i = 0; i < rends.Length; i++)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - delays[i]) / Mathf.Max(fadeDuration, 0.001f)));
                SetAlpha(i, baseAlpha[i] * k);
            }
            yield return null;
        }
        for (int i = 0; i < rends.Length; i++) SetAlpha(i, baseAlpha[i]);
        co = null;
    }

    // SkillBar combines this reveal alpha with each icon's availability tint.
    public float GetOpacity(Transform slot)
    {
        if (!revealed || elapsed < 0f) return 0f;
        while (slot.parent != null && slot.parent != transform) slot = slot.parent;
        float delay = slot.parent == transform ? slot.GetSiblingIndex() * staggerDelay : 0f;
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - delay) / Mathf.Max(fadeDuration, 0.001f)));
    }

    void SetAlpha(int i, float a)
    {
        if (rends[i] == null) return;
        var c = rends[i].color;
        c.a = a;
        rends[i].color = c;
    }
}
