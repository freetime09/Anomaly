using UnityEngine;

/// <summary>
/// 검은 방(챕터 2) 추격전의 '백신 벽'. 한 덩어리 벽이 경로점(path)을 따라 밀려오고, 닿으면 플레이어가 죽는다.
///  - 벽의 '앞면'이 경로 위를 진행한다. 구간 방향을 따라 벽이 회전하므로 위로 오르는 구간에선 아래에서 차오른다.
///  - 속도: 기본 baseSpeed. 플레이어가 catchUpGap보다 멀어지면 maxGap까지 점점 빨라져 maxSpeed로 따라붙는다.
///  - 시작/끝: VaccineChaseTrigger(Start/End)가 Begin()/Stop()을 부른다.
///  - 죽으면 벽이 멈추고, 부활하면 부활 지점보다 respawnBackOff 만큼 뒤로 물러나 restartDelay 뒤 다시 쫓아온다.
///    부활 지점이 추격 시작 지점보다 앞(시작 트리거 전)이면 벽은 처음 상태로 돌아가고 시작 트리거를 다시 밟아야 출발한다.
/// 그림은 아트가 나오기 전까지 코드로 만든 하얀 글리치 덩어리(임시). bodySprite를 넣으면 그걸 쓴다.
/// </summary>
public class VaccineWall : MonoBehaviour
{
    [Header("경로")]
    [Tooltip("벽 앞면이 지나갈 경로점(순서대로). 첫 점은 화면 밖 뒤쪽에 두는 걸 권장")]
    public Transform[] path;

    [Header("속도")]
    [Tooltip("기본 속도 (유닛/초)")]
    public float baseSpeed = 3.5f;
    [Tooltip("최대 속도 (플레이어가 멀리 앞서 있을 때)")]
    public float maxSpeed = 9f;
    [Tooltip("벽과 플레이어 사이(경로 기준 거리)가 이보다 멀면 빨라지기 시작")]
    public float catchUpGap = 7f;
    [Tooltip("이 거리 이상 벌어지면 최대 속도")]
    public float maxGap = 14f;
    [Tooltip("구간이 꺾일 때 벽이 방향을 트는 속도 (도/초)")]
    public float turnSpeed = 180f;
    [Tooltip("추격 시작 연출 중 벽이 천천히 다가오는 속도 (유닛/초)")]
    public float introSpeed = 2.5f;

    [Header("모양")]
    [Tooltip("진행 방향과 수직인 벽의 폭 (화면을 덮을 만큼)")]
    public float span = 30f;
    [Tooltip("앞면 뒤로 이어지는 벽의 두께")]
    public float depth = 30f;
    [Tooltip("임시 그림 대신 쓸 스프라이트 (비우면 코드로 만든 하얀 글리치 덩어리)")]
    public Sprite bodySprite;
    [Tooltip("벽 색")]
    public Color color = Color.white;
    public int sortingOrder = 50;
    [Tooltip("닿음 판정을 보이는 앞면보다 이만큼 뒤로 (들쭉날쭉한 가장자리에 스치기만 해도 죽는 억울함 방지)")]
    public float killInset = 0.35f;
    [Tooltip("앞면 일렁임 크기 (유닛)")]
    public float wobble = 0.15f;

    [Header("부활")]
    [Tooltip("부활할 때 벽이 부활 지점보다 이만큼 뒤로 물러남 (경로 기준)")]
    public float respawnBackOff = 8f;
    [Tooltip("부활 후 벽이 다시 움직이기까지 대기 시간 (초)")]
    public float restartDelay = 1f;

    [Header("화면 흔들림")]
    [Tooltip("벽이 이 거리 안으로 다가오면 카메라를 살짝 흔든다 (0이면 끔)")]
    public float shakeGap = 3f;
    public float shakeMagnitude = 0.06f;

    float[] cum;          // 경로점까지의 누적 거리
    float progress;       // 앞면이 경로를 따라 간 거리
    float beginPlayerProgress; // 추격이 시작된 순간 플레이어의 경로 위치 (이보다 앞에서 부활하면 처음부터)
    bool running;         // 추격 중
    bool finished;        // 끝 지점 도달 → 멈춤
    bool paused;          // 플레이어가 죽어 있는 동안 + 부활 후 대기
    bool waitingRespawn;  // 죽음 연출 중 (아직 부활 전)
    bool introCreeping;   // 추격 시작 연출 중 천천히 다가오는 중
    float pauseTimer;
    float angle;          // 현재 벽 방향 (도)
    float shakeTimer;

    Transform body;
    SpriteRenderer bodySr;
    BoxCollider2D col;
    Transform player;
    FallRespawn respawn;
    CameraFollow cam;

    public bool IsRunning => running && !finished;

    void Awake()
    {
        BuildPath();
        BuildBody();
        var rb = gameObject.AddComponent<Rigidbody2D>(); // 트리거 감지용 (키네마틱)
        rb.bodyType = RigidbodyType2D.Kinematic;
        respawn = FindObjectOfType<FallRespawn>();
        if (respawn != null) player = respawn.transform;
        cam = FindObjectOfType<CameraFollow>();
        ResetToStart();
    }

    void OnEnable()
    {
        if (respawn == null) respawn = FindObjectOfType<FallRespawn>();
        if (respawn != null) { respawn.Died += OnPlayerDied; respawn.Respawned += OnPlayerRespawned; }
    }

    void OnDisable()
    {
        if (respawn != null) { respawn.Died -= OnPlayerDied; respawn.Respawned -= OnPlayerRespawned; }
    }

    /// <summary>추격 시작 (시작 트리거가 부름). 이미 쫓는 중이면 무시.</summary>
    public void Begin()
    {
        if (running || finished || cum == null) return;
        running = true;
        introCreeping = false;
        beginPlayerProgress = player != null ? Project(player.position) : 0f;
        SetVisible(true);
    }

    /// <summary>추격 시작 연출용: 벽을 경로 시작점에 보이게 하고 introSpeed로 천천히 다가오게 한다 (아직 죽이지 않음).
    /// 이후 Begin()을 부르면 그 자리에서 이어서 추격한다.</summary>
    public void ShowForIntro()
    {
        if (running || finished || cum == null) return;
        progress = 0f;
        angle = SegmentAngle(0f);
        Place();
        SetVisible(true);
        introCreeping = true;
    }

    /// <summary>시작 연출이 중간에 끊겼을 때(죽음 등) 벽을 처음 상태로 되돌린다.</summary>
    public void CancelIntro()
    {
        if (running) return;
        ResetToStart();
    }

    /// <summary>현재 벽 앞면 위치 (월드).</summary>
    public Vector2 FrontPosition => transform.position;

    /// <summary>추격 끝 (끝 트리거가 부름). 벽이 그 자리에 멈춘다.</summary>
    public void Stop()
    {
        finished = true;
    }

    void ResetToStart()
    {
        running = false;
        finished = false;
        paused = false;
        introCreeping = false;
        waitingRespawn = false;
        progress = 0f;
        angle = SegmentAngle(0f);
        Place();
        SetVisible(false);
    }

    void OnPlayerDied()
    {
        if (running && !finished) { paused = true; waitingRespawn = true; }
    }

    void OnPlayerRespawned(Vector3 pos)
    {
        if (!running || finished) return;
        float sp = Project(pos);
        // 추격이 시작된 지점보다 앞(시작 트리거 전)에서 부활 → 벽은 처음 상태로, 시작 트리거를 다시 밟으면 출발
        if (sp < beginPlayerProgress - 0.5f) { ResetToStart(); return; }
        progress = Mathf.Max(0f, sp - respawnBackOff);
        angle = SegmentAngle(progress);
        Place();
        paused = true;
        waitingRespawn = false;
        pauseTimer = restartDelay;
    }

    void Update()
    {
        if (introCreeping && !running && cum != null)
        {
            // 시작 연출 중: 천천히 다가옴 (닿아도 안 죽음 — TryKill은 running일 때만)
            progress = Mathf.Min(progress + introSpeed * Time.deltaTime, cum[cum.Length - 1]);
            Place();
            return;
        }
        if (!running || finished || cum == null) return;

        if (paused)
        {
            // 죽음 연출 중엔 계속 멈춤. 부활 후엔 restartDelay가 지나면 재출발
            if (waitingRespawn) return;
            pauseTimer -= Time.deltaTime;
            if (pauseTimer > 0f) return;
            paused = false;
        }

        float gap = player != null ? Project(player.position) - progress : 0f;
        float t = Mathf.InverseLerp(catchUpGap, maxGap, gap);
        float speed = Mathf.Lerp(baseSpeed, maxSpeed, t);
        progress = Mathf.Min(progress + speed * Time.deltaTime, cum[cum.Length - 1]);

        angle = Mathf.MoveTowardsAngle(angle, SegmentAngle(progress), turnSpeed * Time.deltaTime);
        Place();

        if (shakeGap > 0f && cam != null && gap < shakeGap)
        {
            shakeTimer -= Time.deltaTime;
            if (shakeTimer <= 0f) { cam.Shake(0.15f, shakeMagnitude); shakeTimer = 0.12f; }
        }
    }

    void LateUpdate()
    {
        // 앞면 일렁임 (살아 있는 덩어리 느낌)
        if (body == null || !bodySr.enabled) return;
        float w = wobble * (Mathf.PerlinNoise(Time.time * 3f, 0f) - 0.5f) * 2f;
        body.localPosition = new Vector3(-depth * 0.5f + w, 0f, 0f);
    }

    void OnTriggerEnter2D(Collider2D other) { TryKill(other); }
    void OnTriggerStay2D(Collider2D other)  { TryKill(other); }

    void TryKill(Collider2D other)
    {
        if (!running || paused) return;
        var fr = other.GetComponentInParent<FallRespawn>();
        if (fr != null) fr.Die();
    }

    // ── 경로 계산 ──────────────────────────────

    void BuildPath()
    {
        if (path == null || path.Length < 2) { cum = null; Debug.LogWarning("[백신 벽] 경로점이 2개 이상 필요"); return; }
        cum = new float[path.Length];
        for (int i = 1; i < path.Length; i++)
            cum[i] = cum[i - 1] + Vector2.Distance(path[i - 1].position, path[i].position);
    }

    int SegmentAt(float s)
    {
        for (int i = 1; i < cum.Length; i++)
            if (s <= cum[i]) return i - 1;
        return cum.Length - 2;
    }

    Vector2 PointAt(float s)
    {
        int i = SegmentAt(s);
        float len = cum[i + 1] - cum[i];
        float t = len > 1e-4f ? (s - cum[i]) / len : 0f;
        return Vector2.Lerp(path[i].position, path[i + 1].position, Mathf.Clamp01(t));
    }

    float SegmentAngle(float s)
    {
        if (cum == null) return 0f;
        int i = SegmentAt(s);
        Vector2 d = path[i + 1].position - path[i].position;
        return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
    }

    /// <summary>위치를 경로에 투영했을 때 경로 시작부터의 거리.</summary>
    float Project(Vector2 p)
    {
        if (cum == null) return 0f;
        float best = float.MaxValue, bestS = 0f;
        for (int i = 0; i < path.Length - 1; i++)
        {
            Vector2 a = path[i].position, b = path[i + 1].position;
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            float d = (a + ab * t - p).sqrMagnitude;
            if (d < best) { best = d; bestS = cum[i] + ab.magnitude * t; }
        }
        return bestS;
    }

    void Place()
    {
        if (cum == null) return;
        transform.position = PointAt(progress);
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    // ── 모양 ──────────────────────────────

    void BuildBody()
    {
        col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(depth - killInset, span);
        col.offset = new Vector2(-(depth + killInset) * 0.5f, 0f); // 앞면이 transform 위치, 몸통은 뒤쪽

        var go = new GameObject("Body");
        body = go.transform;
        body.SetParent(transform, false);
        body.localPosition = new Vector3(-depth * 0.5f, 0f, 0f);
        bodySr = go.AddComponent<SpriteRenderer>();
        bodySr.sortingOrder = sortingOrder;
        bodySr.color = color;
        if (bodySprite != null)
        {
            bodySr.sprite = bodySprite;
            bodySr.drawMode = SpriteDrawMode.Sliced;
            bodySr.size = new Vector2(depth, span);
        }
        else
        {
            bodySr.sprite = GenerateGlitchSprite();
            Vector2 sz = bodySr.sprite.bounds.size; // 스케일로 depth x span 크기에 맞춤
            body.localScale = new Vector3(depth / sz.x, span / sz.y, 1f);
        }
    }

    void SetVisible(bool on)
    {
        if (bodySr != null) bodySr.enabled = on;
        if (col != null) col.enabled = on;
    }

    /// <summary>임시 그림: 오른쪽(앞면) 가장자리가 들쭉날쭉한 덩어리 (흰색 → color로 칠함).</summary>
    static Sprite GenerateGlitchSprite()
    {
        const int w = 256, h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[w * h];
        var rnd = new System.Random(7);
        int edge = w - 6;
        for (int y = 0; y < h; y++)
        {
            // 몇 줄씩 묶어 앞면 길이를 들쭉날쭉하게 (글리치 블록)
            if (y % 4 == 0) edge = w - 1 - rnd.Next(0, 7);
            for (int x = 0; x < w; x++)
            {
                byte a = (byte)(x <= edge ? 255 : 0);
                // 앞면 근처에 드문드문 빈 픽셀 → 부서지는 가장자리
                if (x > edge - 4 && x <= edge && rnd.NextDouble() < 0.25) a = 0;
                px[y * w + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
    }

    void OnDrawGizmos()
    {
        if (path == null) return;
        Gizmos.color = Color.red;
        for (int i = 0; i < path.Length - 1; i++)
            if (path[i] != null && path[i + 1] != null) Gizmos.DrawLine(path[i].position, path[i + 1].position);
    }
}
