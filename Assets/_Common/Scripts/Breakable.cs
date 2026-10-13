using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 공격(나뭇가지 휘두르기)에 맞아 반응하는 간단한 대상. IDamageable 구현.
///  - maxHp 만큼 맞으면 죽어서 destroyOnDeath 면 파괴, 아니면 숨김(비활성).
///  - 맞을 때 잠깐 색이 번쩍이고, (설정 시) 흔들리며, 단계별 스프라이트로 바뀐다.
///  - 죽을 때 404 블럭처럼 파티클을 터뜨린다(burstOnDeath).
///  - onHit/onDeath(UnityEvent)로 대상(신호·문 등)을 인스펙터에서 배선.
/// 적/부술 수 있는 오브젝트에 붙여 쓴다.
/// </summary>
public class Breakable : MonoBehaviour, IDamageable, IPuzzleState
{
    [Header("체력")]
    [Tooltip("이만큼 맞으면 죽는다")]
    public int maxHp = 1;
    [Tooltip("죽으면 오브젝트를 파괴(체크) / 비활성으로 숨김(해제)")]
    public bool destroyOnDeath = false;

    [Header("단계별 스프라이트")]
    [Tooltip("한 대씩 맞을 때마다 순서대로 바뀌는 스프라이트. [0]=1대 맞은 뒤, [1]=2대 맞은 뒤 ... 비워두면 안 바뀜")]
    public Sprite[] hitStages;

    [Header("흔들림")]
    [Tooltip("맞을 때 흔들리는 크기(units). 0이면 안 흔들림")]
    public float shakeMagnitude = 0.12f;
    [Tooltip("흔들림 지속 시간(초)")]
    public float shakeDuration = 0.15f;

    [Header("피격 연출")]
    [Tooltip("맞는 순간 잠깐 이 색으로 번쩍")]
    public Color hitFlashColor = Color.white;
    [Tooltip("번쩍 지속 시간(초)")]
    public float hitFlashTime = 0.08f;
    [Tooltip("맞을 때 밀려나는 힘(Rigidbody2D 있을 때만). 0이면 안 밀림")]
    public float knockback = 0f;

    [Header("죽을 때 사운드")]
    [Tooltip("죽어서 터질 때 재생할 소리")]
    public AudioClip breakClip;
    [Range(0f, 1f)]
    [Tooltip("소리 볼륨")]
    public float breakVolume = 1f;

    [Header("죽을 때 파티클(404 블럭 방식)")]
    [Tooltip("죽을 때 파티클을 터뜨린다")]
    public bool burstOnDeath = true;
    [Tooltip("터지는 파티클 색")]
    public Color burstColor = new Color(0.4f, 1f, 0.6f);
    [Tooltip("터지는 파티클 수")]
    public int burstCount = 40;
    [Tooltip("파티클이 퍼지는 반경(units)")]
    public float burstRadius = 1.2f;

    [Header("이벤트 (대상 배선)")]
    public UnityEvent onHit;
    public UnityEvent onDeath;

    int hp;
    SpriteRenderer[] rends;
    SpriteRenderer mainSprite;   // 단계별 스프라이트를 바꿀 주 렌더러
    Color[] baseColors;
    float flashUntil;
    Rigidbody2D rb;

    Vector3 shakeBase;
    bool shaking;
    Coroutine shakeCo;

    void Awake()
    {
        hp = maxHp;
        rends = GetComponentsInChildren<SpriteRenderer>(true);
        baseColors = new Color[rends.Length];
        for (int i = 0; i < rends.Length; i++) baseColors[i] = rends[i].color;
        mainSprite = GetComponent<SpriteRenderer>();
        if (mainSprite == null && rends.Length > 0) mainSprite = rends[0];
        rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>피격 — 체력을 깎고, 0 이하면 죽는다.</summary>
    public void TakeHit(int damage, Vector2 hitDir)
    {
        if (hp <= 0) return;
        hp -= Mathf.Max(1, damage);
        Flash();
        Shake();
        UpdateStageSprite();
        if (rb != null && knockback > 0f) rb.AddForce(hitDir.normalized * knockback, ForceMode2D.Impulse);
        onHit.Invoke();
        if (hp <= 0) Die();
    }

    // 맞은 횟수(=maxHp-hp)에 맞춰 단계 스프라이트로 교체. 죽는 타격에는 바꾸지 않음(어차피 사라짐).
    void UpdateStageSprite()
    {
        if (hp <= 0 || mainSprite == null || hitStages == null || hitStages.Length == 0) return;
        int idx = (maxHp - hp) - 1;                       // 1대=0, 2대=1 ...
        idx = Mathf.Clamp(idx, 0, hitStages.Length - 1);
        if (hitStages[idx] != null) mainSprite.sprite = hitStages[idx];
    }

    void Die()
    {
        if (burstOnDeath) SpawnBurst();                   // 사라지기 전에 파티클 (별도 오브젝트라 비활성돼도 유지)
        // 터지는 소리 — 비활성/파괴돼도 안 끊기게 독립 재생
        if (breakClip != null) AudioSource.PlayClipAtPoint(breakClip, transform.position, breakVolume);
        onDeath.Invoke();
        if (destroyOnDeath) Destroy(gameObject);
        else gameObject.SetActive(false);
    }

    // ── IPuzzleState (죽으면 체크포인트 때 체력·모습으로. 파괴된 건 못 되돌림) ──
    struct BreakState { public int hp; public bool active; public Sprite sprite; }
    public object SaveState()
    {
        if (rends == null) return new BreakState { hp = maxHp, active = gameObject.activeSelf }; // 꺼진 채 시작해 Awake 전
        return new BreakState { hp = hp, active = gameObject.activeSelf, sprite = mainSprite != null ? mainSprite.sprite : null };
    }
    public void LoadState(object state)
    {
        var s = (BreakState)state;
        if (rends == null) { hp = s.hp; gameObject.SetActive(s.active); return; }
        hp = s.hp;
        if (mainSprite != null) mainSprite.sprite = s.sprite;
        for (int i = 0; i < rends.Length; i++) if (rends[i] != null) rends[i].color = baseColors[i];
        flashUntil = 0f;
        if (shaking) { if (shakeCo != null) StopCoroutine(shakeCo); transform.localPosition = shakeBase; shaking = false; }
        gameObject.SetActive(s.active);
    }

    void Flash()
    {
        for (int i = 0; i < rends.Length; i++) if (rends[i] != null) rends[i].color = hitFlashColor;
        flashUntil = Time.time + hitFlashTime;
    }

    void Shake()
    {
        if (shakeMagnitude <= 0f || shakeDuration <= 0f) return;
        if (!shaking) shakeBase = transform.localPosition; // 새 흔들림 시작 시에만 기준 위치 저장(드리프트 방지)
        if (shakeCo != null) StopCoroutine(shakeCo);
        shakeCo = StartCoroutine(ShakeRoutine());
    }

    IEnumerator ShakeRoutine()
    {
        shaking = true;
        float t = 0f;
        while (t < shakeDuration)
        {
            t += Time.deltaTime;
            float damper = 1f - Mathf.Clamp01(t / shakeDuration);
            Vector2 off = Random.insideUnitCircle * (shakeMagnitude * damper);
            transform.localPosition = shakeBase + (Vector3)off;
            yield return null;
        }
        transform.localPosition = shakeBase;
        shaking = false;
    }

    void Update()
    {
        // 번쩍 후 원래 색 복구
        if (flashUntil > 0f && Time.time >= flashUntil)
        {
            for (int i = 0; i < rends.Length; i++) if (rends[i] != null) rends[i].color = baseColors[i];
            flashUntil = 0f;
        }
    }

    // ── 죽을 때 파티클 버스트 (SignalDisappear/404 블럭과 같은 방식, 독립 오브젝트로 생성) ──
    void SpawnBurst()
    {
        var go = new GameObject("BreakBurst");
        go.transform.position = transform.position;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        const float life = 0.55f;
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = life;
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.28f);
        main.gravityModifier = 0.3f;
        main.startColor = burstColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;

        var em = ps.emission; em.enabled = true; em.rateOverTime = 0f; // 버스트만
        var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = burstRadius;

        var col = ps.colorOverLifetime; col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var psr = ps.GetComponent<ParticleSystemRenderer>();
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = BurstTex(); // 또렷한 사각 파티클
        psr.material = mat;
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.sortingOrder = 50;

        ps.Emit(new ParticleSystem.EmitParams { startColor = burstColor }, burstCount);
        Destroy(go, life + 0.4f);
    }

    static Texture2D burstTex;
    static Texture2D BurstTex()
    {
        if (burstTex != null) return burstTex;
        burstTex = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var cols = new Color32[64];
        for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
        burstTex.SetPixels32(cols);
        burstTex.Apply();
        return burstTex;
    }
}
