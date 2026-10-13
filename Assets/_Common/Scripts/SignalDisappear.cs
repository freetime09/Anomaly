using UnityEngine;

/// <summary>
/// 신호(채널)를 받으면 파티클 이펙트를 터뜨리며 사라지는 오브젝트(404 블럭 등).
///  - signalId 채널 ON  → 이펙트 재생 + 스프라이트/콜라이더/애니메이터 숨김(사라짐)
///  - signalId 채널 OFF → reappearWhenOff 면 다시 나타남, 아니면 사라진 채 유지
/// 파괴하지 않고 '숨김'이라 신호가 다시 꺼지면 복구 가능하고, 이펙트도 끝까지 재생된다.
/// </summary>
public class SignalDisappear : SignalReceiver, IPuzzleState
{
    [Header("사라짐 동작")]
    [Tooltip("신호 조건이 풀리면 다시 나타난다. 끄면 한 번 사라지면 계속 유지.")]
    public bool reappearWhenOff = false;

    [Header("이펙트")]
    [Tooltip("사라질 때 터지는 파티클 색")]
    public Color effectColor = new Color(0.4f, 1f, 0.6f);
    [Tooltip("터지는 파티클 수")]
    public int burstCount = 40;
    [Tooltip("파티클이 퍼지는 반경(units)")]
    public float effectRadius = 1.2f;

    [Header("사운드")]
    [Tooltip("사라지는 순간 재생할 소리")]
    public AudioClip disappearClip;
    [Range(0f, 1f)]
    [Tooltip("소리 볼륨")]
    public float sfxVolume = 1f;

    SpriteRenderer[] renderers;
    Collider2D[] colliders;
    Animator[] animators;
    ParticleSystem ps;
    AudioSource sfxSrc;
    bool gone;

    void Awake()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        colliders = GetComponentsInChildren<Collider2D>(true);
        animators = GetComponentsInChildren<Animator>(true);
        sfxSrc = gameObject.AddComponent<AudioSource>();
        sfxSrc.playOnAwake = false;
        BuildParticles();
    }

    // 신호 조건(SignalReceiver)이 충족/해제될 때 호출 → 사라짐/복구
    protected override void OnSignalStateChanged(bool on)
    {
        if (on) Disappear();
        else if (reappearWhenOff) Reappear();
    }

    void Disappear()
    {
        if (gone) return;
        gone = true;
        if (!PuzzleSnapshot.Restoring) PlayEffect();
        if (disappearClip != null && sfxSrc != null && !PuzzleSnapshot.Restoring) sfxSrc.PlayOneShot(disappearClip, sfxVolume); // 사라지는 소리
        SetVisible(false);
    }

    void Reappear()
    {
        if (!gone) return;
        gone = false;
        SetVisible(true);
    }

    // ── IPuzzleState (죽으면 체크포인트 때 상태로, 이펙트 없이) ──
    public object SaveState() { return gone; }
    public void LoadState(object state)
    {
        bool g = (bool)state;
        if (g == gone) return;
        gone = g;
        SetVisible(!g);
    }

    void SetVisible(bool v)
    {
        foreach (var r in renderers) if (r != null) r.enabled = v;
        foreach (var c in colliders) if (c != null) c.enabled = v;
        foreach (var a in animators) if (a != null) a.enabled = v;
    }

    void PlayEffect()
    {
        if (ps == null) return;
        var ep = new ParticleSystem.EmitParams { startColor = effectColor };
        ps.transform.position = transform.position;
        ps.Emit(ep, burstCount);
    }

    void BuildParticles()
    {
        var go = new GameObject("DisappearFX");
        go.transform.SetParent(transform, false);
        ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 0.55f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.28f);
        main.gravityModifier = 0.3f;
        main.startColor = effectColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;

        var em = ps.emission; em.enabled = true; em.rateOverTime = 0f; // 버스트만
        var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = effectRadius;

        var col = ps.colorOverLifetime; col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var psr = ps.GetComponent<ParticleSystemRenderer>();
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = SquareTex(); // 동그라미 대신 또렷한 사각형
        psr.material = mat;
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.sortingOrder = 50;
    }

    static Texture2D squareTex;
    /// <summary>파티클을 네모로 보이게 할 흰색 solid 사각 텍스처(공유).</summary>
    static Texture2D SquareTex()
    {
        if (squareTex != null) return squareTex;
        squareTex = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var cols = new Color32[64];
        for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
        squareTex.SetPixels32(cols);
        squareTex.Apply();
        return squareTex;
    }
}
