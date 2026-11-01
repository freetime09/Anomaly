using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class BossSoundDesign : MonoBehaviour
{
    BossFight owner;
    AudioSource warning, alternate, entrance44, entrance55, texture, heartbeat, error;
    AudioClip crackle;
    CanvasGroup danger;
    Image dangerImage;
    Texture2D mask;
    Sprite maskSprite;
    Material dangerMaterial;
    Coroutine warningRoutine, alternateRoutine, errorFade;
    float lastWarning = -10, lastAlternate = -10, nextStatic;
    bool errorEnding;
    public int WarningEvents { get; private set; }
    public int AlternateEvents { get; private set; }
    public int StaticEvents { get; private set; }
    public int EntranceEvents { get; private set; }

    void Awake()
    {
        owner = GetComponent<BossFight>();
        warning = Make("AttackWarning", "BossWarning", .7f);
        alternate = Make("AttackWarningAlternate", "BossWarningAlt", .8f);
        entrance44 = Make("Entrance44", "BossEntrance44", .8f);
        entrance55 = Make("Entrance55", "BossEntrance55", .85f);
        texture = Make("BossStaticTexture", null, .12f);
        heartbeat = Make("QuietHeartbeat", "LowHealthHeartbeat", .14f); heartbeat.loop = true;
        error = Make("ErrorAttackSound", "ErrorAttack", .7f);
        var data = new float[7938]; uint seed = 9457; float low = 0;
        for (int i = 0; i < data.Length; i++)
        {
            seed = 1664525u * seed + 1013904223u;
            float n = (seed >> 8) / 16777215f * 2 - 1;
            low = Mathf.Lerp(low, n, .16f);
            float envelope = Mathf.Sin(Mathf.PI * i / (data.Length - 1));
            data[i] = (n - low) * envelope * (i % 1800 < 1350 ? .32f : .04f);
        }
        crackle = AudioClip.Create("BossPhase3Crackle", data.Length, 1, 44100, false);
        crackle.SetData(data, 0); texture.clip = crackle;
        BuildDanger();
    }
    AudioSource Make(string name, string resource, float volume)
    {
        var go = new GameObject(name); go.transform.SetParent(transform, false);
        var source = go.AddComponent<AudioSource>(); source.playOnAwake = false;
        source.spatialBlend = 0; source.volume = volume;
        if (resource != null) source.clip = Resources.Load<AudioClip>(resource);
        GameAudioAcoustics.Apply(source); return source;
    }
    public void Warn(bool alt, float seconds)
    {
        float now = Time.unscaledTime;
        if (alt ? now - lastAlternate < .25f : now - lastWarning < .25f) return;
        if (alt)
        {
            lastAlternate = now; AlternateEvents++;
            if (alternateRoutine != null) StopCoroutine(alternateRoutine);
            alternateRoutine = StartCoroutine(Warning(alternate, seconds, .8f));
        }
        else
        {
            lastWarning = now; WarningEvents++;
            if (warningRoutine != null) StopCoroutine(warningRoutine);
            warningRoutine = StartCoroutine(Warning(warning, seconds, .7f));
        }
    }
    IEnumerator Warning(AudioSource source, float seconds, float volume)
    {
        if (source.clip == null) yield break;
        source.Stop(); source.volume = 0; source.Play();
        seconds = Mathf.Clamp(seconds, .35f, 3.5f);
        for (float t = 0; t < seconds; t += PauseMenu.IsPaused ? 0 : Time.unscaledDeltaTime)
        {
            source.volume = volume * Mathf.Min(Mathf.Clamp01(t / .07f), Mathf.Clamp01((seconds - t) / .2f));
            yield return null;
        }
        source.Stop();
    }
    public void Entrance() { StartCoroutine(EntranceSequence()); }
    IEnumerator EntranceSequence()
    {
        EntranceEvents++; if (entrance44.clip != null) entrance44.Play();
        yield return new WaitForSeconds(.55f);
        EntranceEvents++; if (entrance55.clip != null) entrance55.Play();
    }
    public void StaticPulse()
    {
        if (Time.unscaledTime < nextStatic) return;
        nextStatic = Time.unscaledTime + Random.Range(1.7f, 2.6f);
        texture.pitch = Random.Range(.85f, 1.1f); texture.Play(); StaticEvents++;
    }
    public void BeginError()
    {
        if (errorFade != null) StopCoroutine(errorFade);
        errorEnding = false; error.volume = .7f;
        if (error.clip != null) { error.Stop(); error.Play(); }
    }
    public void EndError(float duration = 1.35f)
    {
        if (errorEnding || !error.isPlaying) return;
        errorEnding = true;
        if (errorFade != null) StopCoroutine(errorFade);
        errorFade = StartCoroutine(FadeError(duration));
    }
    IEnumerator FadeError(float seconds)
    {
        float volume = error.volume;
        for (float t = 0; t < seconds; t += PauseMenu.IsPaused ? 0 : Time.unscaledDeltaTime)
        { error.volume = volume * (1 - Mathf.SmoothStep(0, 1, t / seconds)); yield return null; }
        error.Stop(); errorFade = null;
    }
    void Update()
    {
        bool critical = owner != null && owner.IsCriticalHealth;
        if (!PauseMenu.IsPaused)
        {
            float target = critical ? .78f + .12f * Mathf.Sin(Time.unscaledTime * 3.1f) : 0;
            danger.alpha = Mathf.MoveTowards(danger.alpha, target, Time.unscaledDeltaTime * 2.5f);
            if(dangerMaterial!=null)dangerMaterial.SetFloat("_PulseTime",Time.unscaledTime);
        }
        if (critical && !heartbeat.isPlaying && !PauseMenu.IsPaused) heartbeat.Play();
        if (!critical && heartbeat.isPlaying) heartbeat.Stop();
        if (owner != null && !owner.CombatActive)
        { warning.Stop(); alternate.Stop(); EndError(.3f); }
    }
    void BuildDanger()
    {
        var go = new GameObject("CriticalHealthEdges", typeof(Canvas), typeof(CanvasGroup));
        go.transform.SetParent(transform, false);
        go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        go.GetComponent<Canvas>().sortingOrder = 35;
        danger = go.GetComponent<CanvasGroup>(); danger.blocksRaycasts = false; danger.alpha = 0;
        var image = new GameObject("SoftRedEdgeMask", typeof(RectTransform), typeof(Image));
        image.transform.SetParent(go.transform, false); dangerImage = image.GetComponent<Image>();
        dangerImage.raycastTarget = false;
        var r = dangerImage.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        const int w = 256, h = 144;
        mask = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            float u = (x + .5f) / w, v = (y + .5f) / h;
            float edge = Mathf.Min(u, 1 - u, v, 1 - v);
            float a = (1 - Mathf.SmoothStep(0, 1, edge/.14f)) * .56f;
            pixels[y * w + x] = new Color(.65f, .015f, .035f, a);
        }
        mask.SetPixels(pixels); mask.Apply();
        maskSprite = Sprite.Create(mask, new Rect(0, 0, w, h), Vector2.one * .5f,100,0,SpriteMeshType.FullRect);
        dangerImage.sprite = maskSprite;
        var shader=Resources.Load<Shader>("CriticalEdges");
        if(shader!=null){dangerMaterial=new Material(shader);dangerImage.material=dangerMaterial;}
    }
    void OnDisable()
    {
        StopAllCoroutines();
        foreach (var a in new[] { warning, alternate, entrance44, entrance55, texture, heartbeat, error }) if (a != null) a.Stop();
        if (danger != null) danger.alpha = 0;
    }
    void OnDestroy() { if(dangerMaterial!=null)Destroy(dangerMaterial);if (crackle != null) Destroy(crackle); if (maskSprite != null) Destroy(maskSprite); if (mask != null) Destroy(mask); }
}
