using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Full-screen swirl / vortex transition driven by the URP FullScreenPassRendererFeature
/// (shader: Hidden/VortexSwirl). Call Play() manually to run an in -> peak -> out sweep.
/// All timing and shape values are exposed in the Inspector.
/// </summary>
[DisallowMultipleComponent]
public class VortexTransition : MonoBehaviour
{
    public static VortexTransition Instance { get; private set; }

    [Header("References")]
    [Tooltip("Material using the Hidden/VortexSwirl shader (assigned to the render feature).")]
    public Material material;
    [Tooltip("The FullScreenPassRendererFeature on the URP renderer. Turned on only while playing.")]
    public ScriptableRendererFeature feature;

    [Header("Timing (seconds)")]
    [Min(0f), Tooltip("소용돌이가 한 번 감기는 시간 (아직 어두워지지 않음).")]
    public float swirlDuration = 0.35f;
    [Min(0f), Tooltip("소용돌이를 유지한 채 검게 페이드아웃되는 시간.")]
    public float fadeOutDuration = 0.2f;
    [Min(0f), Tooltip("완전히 검은 상태로 멈춰 있는 시간 (이 순간 순간이동됨).")]
    public float blackHold = 0.1f;
    [Min(0f), Tooltip("검은 화면에서 다시 밝아지는 시간 (소용돌이 없음).")]
    public float fadeInDuration = 0.3f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Swirl shape")]
    [Tooltip("Twist strength at the eye, in radians. Higher = more spins.")]
    public float maxTwist = 14f;
    [Tooltip("Negative to swirl the other way.")]
    public bool clockwise = true;
    [Range(0f, 3f), Tooltip("How hard pixels are sucked toward the center.")]
    public float maxPullIn = 0.8f;
    [Range(0.05f, 1.6f), Tooltip("Effect radius in screen units (0.5 = half height).")]
    public float radius = 0.95f;
    [Tooltip("Center of the vortex in viewport space (0..1).")]
    public Vector2 center = new Vector2(0.5f, 0.5f);

    [Header("Fade")]
    public Color fadeColor = Color.black;
    [Range(0f, 1f), Tooltip("How far the screen fades to fadeColor at the peak.")]
    public float maxFade = 1f;

    [Header("Events")]
    [Tooltip("화면이 완전히 검어진 순간 발생 - 여기서 순간이동 / 화면전환.")]
    public UnityEvent onPeak;
    public UnityEvent onComplete;

    static readonly int ID_Twist    = Shader.PropertyToID("_Twist");
    static readonly int ID_Radius   = Shader.PropertyToID("_Radius");
    static readonly int ID_Center   = Shader.PropertyToID("_Center");
    static readonly int ID_PullIn   = Shader.PropertyToID("_PullIn");
    static readonly int ID_Fade     = Shader.PropertyToID("_Fade");
    static readonly int ID_FadeColor= Shader.PropertyToID("_FadeColor");
    static readonly int ID_Aspect   = Shader.PropertyToID("_Aspect");

    Coroutine running;

    public bool IsPlaying => running != null;

    void Awake()
    {
        Instance = this;
        ApplyFade(0f);
        SetFeatureActive(false);
    }

    void OnDisable()
    {
        ApplyFade(0f);
        SetFeatureActive(false);
    }

    /// <summary>소용돌이(한 번) → 검게 페이드아웃 → onPeakAction(순간이동) → 일반 페이드인.</summary>
    public void Play(System.Action onPeakAction = null)
    {
        if (material == null) { Debug.LogWarning("[VortexTransition] material not assigned."); return; }
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Run(onPeakAction));
    }

    [ContextMenu("Test Play")]
    void TestPlay() => Play();

    IEnumerator Run(System.Action onPeakAction)
    {
        SetFeatureActive(true);

        // 1) 소용돌이 감김 (한 번, 아직 어둡지 않음)
        float el = 0f;
        while (el < swirlDuration)
        {
            el += Time.unscaledDeltaTime;
            ApplyWind(ease.Evaluate(Mathf.Clamp01(el / Mathf.Max(swirlDuration, 1e-4f))), 0f);
            yield return null;
        }
        ApplyWind(1f, 0f);

        // 2) 소용돌이 유지한 채 검게 페이드아웃
        el = 0f;
        while (el < fadeOutDuration)
        {
            el += Time.unscaledDeltaTime;
            ApplyWind(1f, Mathf.Clamp01(el / Mathf.Max(fadeOutDuration, 1e-4f)));
            yield return null;
        }
        ApplyWind(1f, 1f); // 완전히 검은 상태

        // 3) PEAK (완전 검음) — 여기서 순간이동 / 화면전환
        onPeakAction?.Invoke();
        onPeak?.Invoke();

        if (blackHold > 0f) yield return new WaitForSecondsRealtime(blackHold);

        // 4) 검은 화면에서 일반 페이드인 (소용돌이 없이)
        el = 0f;
        while (el < fadeInDuration)
        {
            el += Time.unscaledDeltaTime;
            ApplyFade(1f - Mathf.Clamp01(el / Mathf.Max(fadeInDuration, 1e-4f)));
            yield return null;
        }
        ApplyFade(0f);

        SetFeatureActive(false);
        running = null;
        onComplete?.Invoke();
    }

    // 소용돌이 단계: wind[0,1] = 감긴 정도, fade[0,1] = 어두워진 정도.
    void ApplyWind(float wind, float fade)
    {
        if (material == null) return;
        float dir = clockwise ? 1f : -1f;
        material.SetFloat(ID_Twist, maxTwist * wind * dir);
        material.SetFloat(ID_PullIn, maxPullIn * wind);
        material.SetFloat(ID_Radius, radius);
        material.SetVector(ID_Center, center);
        material.SetFloat(ID_Fade, maxFade * fade);
        material.SetColor(ID_FadeColor, fadeColor);
        material.SetFloat(ID_Aspect, (float)Screen.width / Mathf.Max(1, Screen.height));
    }

    // 페이드 단계: 소용돌이 왜곡 없이 색만 fade[0,1] (0=투명, 1=완전 색).
    void ApplyFade(float fade)
    {
        if (material == null) return;
        material.SetFloat(ID_Twist, 0f);
        material.SetFloat(ID_PullIn, 0f);
        material.SetFloat(ID_Radius, radius);
        material.SetVector(ID_Center, center);
        material.SetFloat(ID_Fade, maxFade * fade);
        material.SetColor(ID_FadeColor, fadeColor);
        material.SetFloat(ID_Aspect, (float)Screen.width / Mathf.Max(1, Screen.height));
    }

    void SetFeatureActive(bool on)
    {
        if (feature != null) feature.SetActive(on);
    }
}
