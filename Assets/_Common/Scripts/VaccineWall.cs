using UnityEngine;

/// <summary>
/// 검은 방(챕터 2) 추격전의 '백신 벽'. 벽 앞면이 경로점(path)을 따라 밀려오고, 닿으면 플레이어가 죽는다.
///  - 벽은 돌지 않는다. 구간 끝(모퉁이)에 오면 그 방향으로 천장/벽에 닿을 때까지 밀고 들어가 '쿵' 하고,
///    잠깐 뒤 다음 구간 방향으로 옆에서 새 앞면이 나와 이어서 쫓아온다.
///  - 지나간 곳은 하얗게 채워 둔다. 폭은 통로/방 크기에 맞춘다(fitToSpace, 최소 span). 블럭보다 뒤 정렬 레이어(sortingLayer)에 그려 빈 공간만 하얘진다.
///  - 속도: 기본 baseSpeed. 플레이어가 catchUpGap보다 멀어지면 maxGap까지 점점 빨라져 maxSpeed로 따라붙는다.
///  - 시작/끝: VaccineChaseTrigger(Start/End)가 Begin()/Stop()을 부른다.
///  - 죽으면 벽이 멈추고, 부활하면 부활 지점보다 respawnBackOff 만큼 뒤로 물러나 restartDelay 뒤 다시 쫓아온다.
///    부활 지점이 추격 시작 지점보다 앞(시작 트리거 전)이면 벽은 처음 상태로 돌아가고 시작 트리거를 다시 밟아야 출발한다.
/// 앞면 그림은 아트가 나오기 전까지 코드로 만든 하얀 글리치 띠(임시). bodySprite를 넣으면 그걸 쓴다.
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
    [Tooltip("추격 시작 연출 중 벽이 천천히 다가오는 속도 (유닛/초)")]
    public float introSpeed = 2.5f;

    [Header("모양")]
    [Tooltip("진행 방향과 수직인 벽의 폭. 통로보다 조금 넓게 (넓으면 옆 층까지 하얘지고 닿음 판정도 넓어짐)")]
    public float span = 14f;
    [Tooltip("첫 구간에서 시작점 뒤로 채워 둘 길이 (화면 밖에서 밀려오는 느낌)")]
    public float depth = 30f;
    [Tooltip("지지직거리는 앞면 띠의 두께")]
    public float edgeWidth = 2.5f;
    [Tooltip("임시 그림 대신 쓸 앞면 스프라이트 (비우면 코드로 만든 하얀 글리치 띠)")]
    public Sprite bodySprite;
    [Tooltip("벽 색")]
    public Color color = Color.white;
    [Tooltip("그릴 정렬 레이어 (블럭보다 뒤에 있는 레이어로 두면 블럭 뒤에 그려진다)")]
    public string sortingLayer = "Vaccine";
    public int sortingOrder = 50;
    [Tooltip("닿음 판정을 보이는 앞면보다 이만큼 뒤로 (들쭉날쭉한 가장자리에 스치기만 해도 죽는 억울함 방지)")]
    public float killInset = 0.35f;
    [Tooltip("앞면 뒤로 이 길이까지만 닿으면 죽음 (벽이 아래층을 지나갈 때 위층 플레이어가 안 죽게)")]
    public float killDepth = 4f;
    [Tooltip("닿음 판정 폭을 벽/다음 통로보다 이만큼 안쪽으로 (바닥 위에 선 플레이어가 아래 지나가는 벽에 안 닿게)")]
    public float killSideInset = 0.2f;
    [Tooltip("앞면 일렁임 크기 (유닛)")]
    public float wobble = 0.15f;
    [Tooltip("지나간 곳의 오브젝트(문·레버·버튼·상자·404 벽·부서지는 벽·체크포인트)도 하얗게 덮음. 블럭·전선은 그대로 보임")]
    public bool coverObjects = true;

    [Header("공간에 맞추기")]
    [Tooltip("벽 폭을 통로/방 크기에 맞춰 늘림 (양옆 벽까지). 끄면 항상 span")]
    public bool fitToSpace = true;
    [Tooltip("경로에서 옆으로 최대 이만큼까지 맞춤")]
    public float fitMax = 40f;
    [Tooltip("이 두께 이하의 발판 등은 무시하고 건너서 채움")]
    public float fitBridge = 1.5f;

    [Header("모퉁이 (천장까지 밀고 '쿵')")]
    [Tooltip("모퉁이에서 밀고 들어갈 때 멈출 벽/천장 레이어")]
    public LayerMask wallLayers = ~0;
    [Tooltip("모퉁이에서 최대로 밀고 들어가는 거리 (벽을 못 찾으면 span/2)")]
    public float maxOvershoot = 8f;
    [Tooltip("'쿵' 한 뒤 옆에서 다시 나오기까지 멈추는 시간 (초)")]
    public float thudPause = 0.25f;
    [Tooltip("'쿵' 화면 흔들림 세기 / 시간")]
    public float thudShake = 0.35f;
    public float thudShakeTime = 0.3f;
    [Tooltip("'쿵' 소리. 비우면 코드로 만든 임시 소리")]
    public AudioClip thudClip;
    [Range(0f, 1f)] public float thudVolume = 1f;
    [Tooltip("'쿵' 할 때 튀는 조각 수")]
    public int thudDebris = 30;

    [Header("부활")]
    [Tooltip("부활할 때 벽이 부활 지점보다 이만큼 뒤로 물러남 (경로 기준)")]
    public float respawnBackOff = 8f;
    [Tooltip("부활 후 벽이 다시 움직이기까지 대기 시간 (초)")]
    public float restartDelay = 1f;

    [Header("화면 흔들림 (거리에 따라)")]
    [Tooltip("벽이 이 거리 안으로 들어오면 흔들리기 시작 (0이면 끔)")]
    public float shakeFar = 8f;
    [Tooltip("이 거리 이하면 최대 세기")]
    public float shakeNear = 1.5f;
    [Tooltip("멀 때(shakeFar) 흔들림 세기")]
    public float shakeMin = 0.02f;
    [Tooltip("가까울 때(shakeNear) 흔들림 세기")]
    public float shakeMax = 0.2f;

    [Header("앞면 지지직 (임시 그림일 때)")]
    [Tooltip("앞면 모양 프레임 수 (번갈아 바뀌며 지지직)")]
    public int glitchFrames = 5;
    [Tooltip("앞면 모양이 바뀌는 간격 (초, 최소~최대 랜덤)")]
    public Vector2 glitchInterval = new Vector2(0.04f, 0.1f);

    [Header("앞면에서 튀는 조각")]
    [Tooltip("초당 튀어나오는 조각 수 (0이면 끔)")]
    public float debrisRate = 30f;
    [Tooltip("조각 중 0/1 숫자 비율 (나머지는 픽셀 조각)")]
    [Range(0f, 1f)] public float digitRatio = 0.3f;
    [Tooltip("조각이 앞으로 튀는 속도 (최소~최대)")]
    public Vector2 debrisSpeed = new Vector2(1.5f, 5f);
    [Tooltip("조각 수명 (초, 최소~최대)")]
    public Vector2 debrisLife = new Vector2(0.3f, 0.7f);
    [Tooltip("픽셀 조각 크기 (최소~최대)")]
    public Vector2 debrisSize = new Vector2(0.1f, 0.3f);

    float[] cum;          // 경로점까지의 누적 거리

    // 구간(leg): 벽 앞면이 실제로 지나가는 직선. 모퉁이마다 천장까지 밀고 들어간 뒤, 다음 구간은 옆에서 새로 시작
    Vector2[] legA;       // 구간 시작점
    Vector2[] legDir;     // 구간 방향
    float[] legLen;       // 구간 길이 (모퉁이에서 밀고 들어가는 거리 포함)
    float[] legS0;        // 구간 시작점의 경로 거리
    int leg;              // 현재 구간
    float legD;           // 현재 구간에서 간 거리
    float thudTimer;      // '쿵' 뒤 대기 (끝나면 다음 구간)

    float beginPlayerProgress; // 추격이 시작된 순간 플레이어의 경로 위치 (이보다 앞에서 부활하면 처음부터)
    bool running;         // 추격 중
    bool finished;        // 끝 지점 도달 → 멈춤
    bool paused;          // 플레이어가 죽어 있는 동안 + 부활 후 대기
    bool waitingRespawn;  // 죽음 연출 중 (아직 부활 전)
    bool introCreeping;   // 추격 시작 연출 중 천천히 다가오는 중
    float pauseTimer;
    float introCurSpeed;  // 시작 연출 중 다가오는 속도

    Sprite[] frames;      // 앞면 지지직 프레임
    float frameTimer;
    float debrisAcc;
    Transform debrisRoot;
    Sprite pixelSprite;
    readonly System.Collections.Generic.List<Debris> debris = new System.Collections.Generic.List<Debris>();

    class Debris
    {
        public Transform tr;
        public SpriteRenderer sr;
        public TMPro.TextMeshPro txt;
        public Vector2 vel;
        public float life, age, size;
    }

    Transform body;
    SpriteRenderer bodySr;
    SpriteMask bodyMask; // 앞면 띠 모양대로 오브젝트를 가림
    readonly System.Collections.Generic.List<SpriteRenderer> covered = new System.Collections.Generic.List<SpriteRenderer>(); // 덮일 오브젝트 그림
    // 지나간 곳을 채우는 하얀 사각형들. 구간마다 폭이 같은 부분끼리 묶은 조각(run)
    class Run { public float a0, a1, left, right, killL, killR; public SpriteRenderer sr; public SpriteMask mask; }
    System.Collections.Generic.List<Run>[] runs;
    float frontLeft, frontRight; // 현재 앞면의 양옆 폭 (진행 방향 왼쪽/오른쪽)
    float[][] bandL, bandR;      // 구간별·0.25 조각별 통로 폭 (양옆, 최대 span/2)
    Transform trailRoot;
    BoxCollider2D col;
    Transform player;
    FallRespawn respawn;
    CameraFollow cam;
    AudioSource sfx;
    AudioClip generatedThud;

    public bool IsRunning => running && !finished;
    /// <summary>실제로 쫓아오는 중 (죽음/부활 대기 아님).</summary>
    public bool IsChasing => running && !finished && !paused;
    /// <summary>벽 앞면과 플레이어 사이 경로 거리 (쫓는 중일 때만 의미 있음).</summary>
    public float Gap { get; private set; } = float.MaxValue;

    void Awake()
    {
        BuildPath();
        BuildBody();
        BuildTrail();
        var rb = gameObject.AddComponent<Rigidbody2D>(); // 트리거 감지용 (키네마틱)
        rb.bodyType = RigidbodyType2D.Kinematic;
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;
        respawn = FindObjectOfType<FallRespawn>();
        if (respawn != null) player = respawn.transform;
        cam = FindObjectOfType<CameraFollow>();
        ResetToStart();
    }

    void Start()
    {
        if (coverObjects) MaskObjects();
    }

    /// <summary>덮일 오브젝트 그림을 모아 둔다. 백신(하얀 채움·앞면 띠)에 겹친 것만 그때그때 가림 모드로 바꾼다
    /// (CoverObjects). 가려진 자리엔 그 뒤의 하얀 채움이 보인다 → 백신이 덮은 것처럼 보임.
    /// 전부 늘 가림 모드로 두면 플레이 중 씬 뷰에서 스프라이트가 깜빡여서, 겹친 것만 바꾼다.</summary>
    void MaskObjects()
    {
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!(mb is Lever || mb is PressButton || mb is PullBox || mb is MovingObject
                  || mb is SignalDisappear || mb is Breakable || mb is Checkpoint)) continue;
            // 전선(LineRenderer)은 블럭 안에 있는 것이라 그대로 둠
            foreach (var sr in mb.GetComponentsInChildren<SpriteRenderer>(true))
                if (!covered.Contains(sr)) covered.Add(sr);
        }
        CoverObjects();
    }

    /// <summary>백신에 겹친 오브젝트만 가림 모드, 나머지는 보통 모드.</summary>
    void CoverObjects()
    {
        bool vis = bodySr != null && bodySr.enabled;
        for (int i = 0; i < covered.Count; i++)
        {
            var o = covered[i];
            if (o == null) continue;
            bool hit = false;
            if (vis)
            {
                Bounds b = o.bounds;
                if (bodySr.bounds.Intersects(b)) hit = true;
                for (int l = 0; l < runs.Length && !hit; l++)
                    foreach (var r in runs[l])
                        if (r.sr.enabled && r.sr.bounds.Intersects(b)) { hit = true; break; }
            }
            var m = hit ? SpriteMaskInteraction.VisibleOutsideMask : SpriteMaskInteraction.None;
            if (o.maskInteraction != m) o.maskInteraction = m;
        }
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
        leg = 0; legD = 0f; thudTimer = 0f;
        Place();
        SetVisible(true);
        introCreeping = true;
        introCurSpeed = introSpeed;
    }

    /// <summary>시작 연출 중 다가오는 속도를 바꾼다 (솟구칠 때 빠르게 → 다시 천천히).</summary>
    public void SetIntroSpeed(float speed) { introCurSpeed = speed; }

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
        leg = 0; legD = 0f; thudTimer = 0f;
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
        SetFromPathDistance(Mathf.Max(0f, sp - respawnBackOff));
        thudTimer = 0f;
        Place();
        paused = true;
        waitingRespawn = false;
        pauseTimer = restartDelay;
    }

    void Update()
    {
        if (introCreeping && !running && cum != null)
        {
            // 시작 연출 중: 천천히 다가옴 (닿아도 안 죽음 — TryKill은 running일 때만). 첫 구간 안에서만
            legD = Mathf.Min(legD + introCurSpeed * Time.deltaTime, legLen[0]);
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

        float gap = player != null ? Project(player.position) - FrontS() : 0f;
        Gap = gap;

        // 이미 하얗게 칠해진 곳에 들어와도 죽음 (앞면 판정은 뒤로 killDepth 까지만이라 떨어지기·순간이동으로 들어오면 안 걸림)
        if (!harmless && respawn != null && InTrail(PlayerCenter())) { respawn.Die(); return; }

        if (thudTimer > 0f)
        {
            // '쿵' 뒤 잠깐 멈췄다가 옆에서 다음 구간 시작
            thudTimer -= Time.deltaTime;
            if (thudTimer <= 0f) NextLeg();
            return;
        }

        float t = Mathf.InverseLerp(catchUpGap, maxGap, gap);
        float speed = Mathf.Lerp(baseSpeed, maxSpeed, t);
        legD += speed * Time.deltaTime;
        if (legD >= legLen[leg])
        {
            legD = legLen[leg];
            if (leg < legLen.Length - 1)
            {
                Thud();
                if (thudPause > 0f) thudTimer = thudPause; else NextLeg();
            }
        }
        Place();
    }

    void NextLeg()
    {
        thudTimer = 0f;
        leg++;
        legD = 0f;
        Place();
    }

    /// <summary>모퉁이 천장/벽에 부딪힘: 흔들림 + 소리 + 조각이 옆으로 튐.</summary>
    void Thud()
    {
        if (cam != null) cam.Shake(thudShakeTime, thudShake);
        var clip = thudClip != null ? thudClip : (generatedThud != null ? generatedThud : (generatedThud = GenerateThud()));
        sfx.PlayOneShot(clip, thudVolume);
        for (int i = 0; i < thudDebris; i++) SpawnDebris(true);
    }

    void LateUpdate()
    {
        UpdateRumble();
        UpdateDebris();
        // 앞면 일렁임 (살아 있는 덩어리 느낌)
        if (body == null || !bodySr.enabled) return;
        float w = wobble * (Mathf.PerlinNoise(Time.time * 3f, 0f) - 0.5f) * 2f;
        body.localPosition = new Vector3(-edgeWidth * 0.5f + w, (frontLeft - frontRight) * 0.5f, 0f);

        // 앞면 지지직: 프레임을 랜덤 간격으로 바꿈
        if (frames != null && frames.Length > 1)
        {
            frameTimer -= Time.deltaTime;
            if (frameTimer <= 0f)
            {
                bodySr.sprite = frames[Random.Range(0, frames.Length)];
                if (bodyMask != null) bodyMask.sprite = bodySr.sprite;
                frameTimer = Random.Range(glitchInterval.x, glitchInterval.y);
            }
        }

        // 앞면에서 조각이 튐 ('쿵' 뒤 멈춰 있는 동안은 쉼)
        if (debrisRate > 0f && thudTimer <= 0f)
        {
            debrisAcc += debrisRate * Time.deltaTime;
            while (debrisAcc >= 1f) { debrisAcc -= 1f; SpawnDebris(false); }
        }
    }

    /// <summary>거리에 따라 카메라를 계속 흔든다 (가까울수록 세게). 쫓는 중이 아니면 끔.</summary>
    void UpdateRumble()
    {
        if (cam == null) return;
        if (!IsChasing || shakeFar <= 0f || Gap > shakeFar) { cam.SetRumble(this, 0f); return; }
        float k = Mathf.InverseLerp(shakeFar, shakeNear, Gap);
        cam.SetRumble(this, Mathf.Lerp(shakeMin, shakeMax, k));
    }

    // ── 튀는 조각 ──────────────────────────────

    /// <param name="sideways">'쿵' 할 때처럼 앞이 아니라 옆으로 튀게</param>
    void SpawnDebris(bool sideways)
    {
        if (debrisRoot == null)
        {
            debrisRoot = new GameObject("VaccineDebris").transform;
            if (pixelSprite == null) pixelSprite = MakePixelSprite();
        }
        Debris d = null;
        bool digit = Random.value < digitRatio;
        foreach (var x in debris) if (!x.tr.gameObject.activeSelf && (x.txt != null) == digit) { d = x; break; }
        if (d == null)
        {
            d = new Debris();
            var go = new GameObject(digit ? "Bit" : "Pixel");
            go.transform.SetParent(debrisRoot, false);
            if (digit)
            {
                d.txt = go.AddComponent<TMPro.TextMeshPro>(); // Transform 이 RectTransform 으로 바뀜 → tr 은 아래에서 다시 잡음
                d.txt.alignment = TMPro.TextAlignmentOptions.Center;
                d.txt.fontSize = 5f;
                d.txt.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                d.txt.rectTransform.sizeDelta = new Vector2(1f, 1f);
                var mr = d.txt.GetComponent<MeshRenderer>();
                mr.sortingLayerName = sortingLayer;
                mr.sortingOrder = sortingOrder + 1;
            }
            else
            {
                d.sr = go.AddComponent<SpriteRenderer>();
                d.sr.sprite = pixelSprite;
                d.sr.sortingLayerName = sortingLayer;
                d.sr.sortingOrder = sortingOrder + 1;
            }
            d.tr = go.transform;
            debris.Add(d);
        }
        // 앞면(로컬 x=0)의 span 범위 어딘가에서, 앞(+x)으로 (또는 옆으로) 튐
        float along = Random.Range(-frontRight, frontLeft);
        d.tr.position = transform.TransformPoint(new Vector3(Random.Range(-0.2f, 0.3f), along, 0f));
        d.tr.rotation = Quaternion.identity;
        Vector2 fwd = transform.right, side = transform.up;
        if (sideways)
            d.vel = side * (Mathf.Sign(along - (frontLeft - frontRight) * 0.5f + 0.01f) * Random.Range(debrisSpeed.x, debrisSpeed.y) * 1.5f) - fwd * Random.Range(0f, 2f);
        else
            d.vel = fwd * Random.Range(debrisSpeed.x, debrisSpeed.y) + side * Random.Range(-1.2f, 1.2f);
        d.life = Random.Range(debrisLife.x, debrisLife.y);
        d.age = 0f;
        d.size = Random.Range(debrisSize.x, debrisSize.y);
        if (d.txt != null) d.txt.text = Random.value < 0.5f ? "0" : "1";
        d.tr.gameObject.SetActive(true);
        ApplyDebris(d, 0f);
    }

    void UpdateDebris()
    {
        for (int i = 0; i < debris.Count; i++)
        {
            var d = debris[i];
            if (!d.tr.gameObject.activeSelf) continue;
            d.age += Time.deltaTime;
            if (d.age >= d.life) { d.tr.gameObject.SetActive(false); continue; }
            d.vel *= 1f - 2.5f * Time.deltaTime; // 금방 느려짐
            d.tr.position += (Vector3)(d.vel * Time.deltaTime);
            ApplyDebris(d, d.age / d.life);
        }
    }

    void ApplyDebris(Debris d, float t)
    {
        Color c = color;
        c.a = 1f - t;
        if (Random.value < 0.08f) c.a *= 0.2f; // 가끔 깜빡임 (글리치)
        if (d.sr != null) { d.sr.color = c; d.tr.localScale = Vector3.one * d.size * Mathf.Lerp(1f, 0.4f, t); }
        else { d.txt.color = c; d.tr.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, t); }
    }

    static Sprite MakePixelSprite()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    void OnTriggerEnter2D(Collider2D other) { TryKill(other); }
    void OnTriggerStay2D(Collider2D other)  { TryKill(other); }

    /// <summary>true 면 계속 쫓아오지만 닿아도 죽지 않는다 (챕터 끝 연출 중 등).</summary>
    [System.NonSerialized] public bool harmless;

    void TryKill(Collider2D other)
    {
        if (!running || paused || harmless) return;
        var fr = other.GetComponentInParent<FallRespawn>();
        if (fr != null) fr.Die();
    }

    Collider2D playerCol;

    Vector2 PlayerCenter()
    {
        if (playerCol == null && player != null) playerCol = player.GetComponent<Collider2D>();
        return playerCol != null ? (Vector2)playerCol.bounds.center : player != null ? (Vector2)player.position : Vector2.positiveInfinity;
    }

    /// <summary>p 가 지금까지 칠해진 하얀 채움 안(실제 빈 공간 폭 killL/killR 기준)인가.
    /// 지나온 구간은 끝까지, 현재 구간은 앞면 판정이 시작되는 곳까지.</summary>
    bool InTrail(Vector2 p)
    {
        if (runs == null || float.IsInfinity(p.x)) return false;
        for (int i = 0; i <= leg && i < runs.Length; i++)
        {
            float limit = i < leg ? float.MaxValue : legD - killInset;
            Vector2 u = legDir[i];
            Vector2 v = p - legA[i];
            float along = Vector2.Dot(v, u);
            float across = Vector2.Dot(v, new Vector2(-u.y, u.x)); // + = 진행 방향 왼쪽
            if (along > limit) continue;
            foreach (var r in runs[i])
                if (along >= r.a0 && along < Mathf.Min(r.a1, limit) && across <= r.killL && across >= -r.killR) return true;
        }
        return false;
    }

    // ── 경로 계산 ──────────────────────────────

    void BuildPath()
    {
        if (path == null || path.Length < 2) { cum = null; Debug.LogWarning("[백신 벽] 경로점이 2개 이상 필요"); return; }
        cum = new float[path.Length];
        for (int i = 1; i < path.Length; i++)
            cum[i] = cum[i - 1] + Vector2.Distance(path[i - 1].position, path[i].position);

        int n = path.Length - 1;
        float hw = span * 0.5f;
        legA = new Vector2[n]; legDir = new Vector2[n]; legLen = new float[n]; legS0 = new float[n];
        for (int i = 0; i < n; i++)
        {
            Vector2 a = path[i].position, b = path[i + 1].position;
            Vector2 dir = (b - a).sqrMagnitude > 1e-6f ? (b - a).normalized : Vector2.right;
            // 첫 구간은 경로 시작점부터, 나머지는 이전 구간이 채운 폭(span/2) 바로 바깥, 즉 '옆'에서 시작
            float skip = i == 0 ? 0f : JunctionSkip(a, dir);
            Vector2 start = a + dir * skip;
            // 마지막 구간이 아니면 모퉁이에서 그 방향 천장/벽까지 밀고 들어감
            float over = i == n - 1 ? 0f : Overshoot(b, dir);
            legA[i] = start;
            legDir[i] = dir;
            legS0[i] = cum[i] + skip;
            legLen[i] = Mathf.Max(0f, Vector2.Dot(b + dir * over - start, dir));
        }
        BuildFit();
    }

    /// <summary>구간마다 경로를 따라 0.25 간격으로 양옆 벽까지 거리를 재서 폭을 정한다.
    /// 얇은 발판은 건너뛰고, 다음 구간들의 통로에 닿으면 거기서 멈춘다 (앞서 하얘지지 않게).</summary>
    void BuildFit()
    {
        const float step0 = 0.25f;
        float hw = span * 0.5f;
        int n = legA.Length;
        runs = new System.Collections.Generic.List<Run>[n];
        // 1) 구간마다 양옆 벽까지 실제 거리
        var rawL = new float[n][]; var rawR = new float[n][];
        bandL = new float[n][]; bandR = new float[n][];
        for (int i = 0; i < n; i++)
        {
            int cnt = Mathf.Max(1, Mathf.CeilToInt(legLen[i] / step0));
            float step = legLen[i] / cnt;
            Vector2 perp = new Vector2(-legDir[i].y, legDir[i].x);
            rawL[i] = new float[cnt]; rawR[i] = new float[cnt];
            for (int k = 0; k < cnt; k++)
            {
                Vector2 pt = legA[i] + legDir[i] * ((k + 0.5f) * step);
                rawL[i][k] = fitToSpace ? WallDistance(pt, perp, fitMax) : hw;
                rawR[i][k] = fitToSpace ? WallDistance(pt, -perp, fitMax) : hw;
            }
            // 이 통로의 자리별 폭 (최대 span/2) — 앞 구간이 이 통로를 미리 칠하지 않게 막는 데 씀.
            // 문 같은 얇은 것에 막힌 자리는 주변 fitBridge 안에서 가장 넓은 값으로
            int win = Mathf.Max(0, Mathf.RoundToInt(fitBridge / Mathf.Max(step, 1e-3f)));
            bandL[i] = new float[cnt]; bandR[i] = new float[cnt];
            for (int k = 0; k < cnt; k++)
            {
                float l = 0f, r = 0f;
                for (int m = Mathf.Max(0, k - win); m <= Mathf.Min(cnt - 1, k + win); m++)
                {
                    l = Mathf.Max(l, rawL[i][m] < float.MaxValue ? rawL[i][m] : hw);
                    r = Mathf.Max(r, rawR[i][m] < float.MaxValue ? rawR[i][m] : hw);
                }
                bandL[i][k] = Mathf.Min(hw, l);
                bandR[i][k] = Mathf.Min(hw, r);
            }
        }
        for (int i = 0; i < n; i++)
        {
            var list = new System.Collections.Generic.List<Run>();
            runs[i] = list;
            if (i == 0 && depth > 0f) list.Add(new Run { a0 = -depth, a1 = 0f, left = hw, right = hw, killL = hw, killR = hw }); // 시작점 뒤 (화면 밖)
            int cnt = rawL[i].Length;
            float step = legLen[i] / cnt;
            Vector2 perp = new Vector2(-legDir[i].y, legDir[i].x); // 진행 방향 왼쪽 (= 로컬 +y)
            var L = new float[cnt]; var R = new float[cnt];
            for (int k = 0; k < cnt; k++)
            {
                L[k] = rawL[i][k] < float.MaxValue ? Mathf.Max(hw, rawL[i][k] + 0.5f) : hw; // 벽 속으로 조금 겹치게
                R[k] = rawR[i][k] < float.MaxValue ? Mathf.Max(hw, rawR[i][k] + 0.5f) : hw;
            }
            // 얇은 장애물 건너기: 주변 fitBridge 안에서 가장 넓은 값. 그다음 다음 구간 통로 앞에서 자르기
            int win = Mathf.Max(0, Mathf.RoundToInt(fitBridge / Mathf.Max(step, 1e-3f)));
            for (int k = 0; k < cnt; k++)
            {
                float l = 0f, r = 0f;
                for (int m = Mathf.Max(0, k - win); m <= Mathf.Min(cnt - 1, k + win); m++) { l = Mathf.Max(l, L[m]); r = Mathf.Max(r, R[m]); }
                Vector2 pt = legA[i] + legDir[i] * ((k + 0.5f) * step);
                l = Quantize(ClampByLater(pt, perp, l, i));
                r = Quantize(ClampByLater(pt, -perp, r, i));
                // 닿음 판정 폭: 실제 빈 공간만 (벽 속으로 겹치기·발판 건너기 없이, 다음 통로 앞에서 조금 더 안쪽)
                float kl = Mathf.Min(l, ClampByLater(pt, perp, rawL[i][k] < float.MaxValue ? rawL[i][k] : hw, i)) - killSideInset;
                float kr = Mathf.Min(r, ClampByLater(pt, -perp, rawR[i][k] < float.MaxValue ? rawR[i][k] : hw, i)) - killSideInset;
                kl = Mathf.Floor(kl * 4f) / 4f; kr = Mathf.Floor(kr * 4f) / 4f;
                // 같은 폭끼리 묶기
                var last = list.Count > 0 ? list[list.Count - 1] : null;
                if (last != null && last.a0 >= 0f && Mathf.Approximately(last.left, l) && Mathf.Approximately(last.right, r)
                    && Mathf.Approximately(last.killL, kl) && Mathf.Approximately(last.killR, kr))
                    last.a1 = (k + 1) * step;
                else
                    list.Add(new Run { a0 = k * step, a1 = (k + 1) * step, left = l, right = r, killL = kl, killR = kr });
            }
        }
    }

    /// <summary>모퉁이에서 새 구간 앞면이 나올 위치 (모퉁이부터 거리).
    /// 새 통로의 양옆 벽선을 따라 모퉁이 쪽 끝(통로 입구 모서리)을 찾아, 그 모서리에서 시작하게 한다
    /// → 이전 구간 채움과 새 구간 채움 사이에 틈이 안 생김. 못 찾으면 span/2.</summary>
    float JunctionSkip(Vector2 corner, Vector2 dir)
    {
        float hw = span * 0.5f;
        if (!fitToSpace) return hw;
        Vector2 perp = new Vector2(-dir.y, dir.x);
        // 새 통로 안쪽에서 양옆 폭
        Vector2 inside = corner + dir * (hw + 2f);
        float wl = Mathf.Min(hw, WallDistance(inside, perp, hw));
        float wr = Mathf.Min(hw, WallDistance(inside, -perp, hw));
        // 벽선 바로 바깥을 따라 모퉁이에서 통로 쪽으로 쏴서 입구 모서리까지 거리
        float best = 0f;
        float dl = WallDistance(corner + perp * (wl + 0.25f), dir, fitMax);
        float dr = WallDistance(corner - perp * (wr + 0.25f), dir, fitMax);
        if (dl < float.MaxValue && dl > 0.01f) best = Mathf.Max(best, dl);
        if (dr < float.MaxValue && dr > 0.01f) best = Mathf.Max(best, dr);
        return best > 0f ? best : hw;
    }

    static float Quantize(float v) { return Mathf.Ceil(v * 4f) / 4f; }

    /// <summary>옆으로 넓힌 폭이 다음 구간들의 통로(그 자리의 폭 band) 안으로 들어가면 거기까지로 줄임.</summary>
    float ClampByLater(Vector2 p, Vector2 side, float ext, int i)
    {
        float hw = span * 0.5f;
        if (ext <= hw) return ext;
        for (float d = hw; d < ext; d += 0.25f)
        {
            Vector2 q = p + side * d;
            for (int j = i + 1; j < legA.Length; j++)
            {
                Vector2 v = q - legA[j];
                float along = Vector2.Dot(v, legDir[j]);
                float across = Vector2.Dot(v, new Vector2(-legDir[j].y, legDir[j].x));
                if (along < -0.01f || along > legLen[j]) continue;
                int k = Mathf.Clamp(Mathf.FloorToInt(along / legLen[j] * bandL[j].Length), 0, bandL[j].Length - 1);
                if (across <= bandL[j][k] && across >= -bandR[j][k]) return d;
            }
        }
        return ext;
    }

    /// <summary>모퉁이 점에서 dir 방향으로 벽/천장까지 거리 (못 찾으면 span/2).</summary>
    float Overshoot(Vector2 from, Vector2 dir)
    {
        float d = WallDistance(from, dir, maxOvershoot);
        return d < float.MaxValue ? d : span * 0.5f;
    }

    /// <summary>from 에서 dir 방향으로 가장 가까운 벽/블럭까지 거리 (없으면 MaxValue). 트리거·움직이는 물체·플레이어 제외.</summary>
    float WallDistance(Vector2 from, Vector2 dir, float maxDist)
    {
        var filter = new ContactFilter2D();
        filter.SetLayerMask(wallLayers);
        filter.useTriggers = false;
        var hits = new RaycastHit2D[8];
        int cnt = Physics2D.Raycast(from, dir, filter, hits, maxDist);
        float best = float.MaxValue;
        for (int i = 0; i < cnt; i++)
        {
            var c = hits[i].collider;
            if (c == null || c.transform.IsChildOf(transform)) continue;
            var rb = c.attachedRigidbody;
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) continue; // 플레이어·상자 등 움직이는 것 제외
            if (c.GetComponentInParent<FallRespawn>() != null) continue;
            best = Mathf.Min(best, hits[i].distance);
        }
        return best;
    }

    /// <summary>현재 앞면의 경로 거리 (모퉁이에서 밀고 들어간 부분은 모퉁이로 침).</summary>
    float FrontS()
    {
        if (legS0 == null) return 0f;
        return Mathf.Min(legS0[leg] + legD, cum[leg + 1]);
    }

    /// <summary>경로 거리 s 에 해당하는 구간/위치로 앞면을 옮긴다 (부활 때 뒤로 물러날 때).</summary>
    void SetFromPathDistance(float s)
    {
        for (int i = legLen.Length - 1; i >= 0; i--)
        {
            if (i == 0 || s >= cum[i])
            {
                leg = i;
                legD = Mathf.Clamp(s - legS0[i], 0f, legLen[i]);
                return;
            }
        }
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
        Vector2 dir = legDir[leg];
        transform.position = legA[leg] + dir * legD;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg); // 구간 방향 (돌아가는 연출 없음)
        // 앞면 폭: 지금 위치의 공간 폭
        frontLeft = frontRight = span * 0.5f;
        float at = Mathf.Max(0f, legD - 0.01f);
        float killL = frontLeft, killR = frontRight;
        foreach (var r in runs[leg]) if (r.a0 >= 0f && at >= r.a0 && at < r.a1 + 1e-3f) { frontLeft = r.left; frontRight = r.right; killL = r.killL; killR = r.killR; break; }
        float width = frontLeft + frontRight;
        if (frames != null) body.localScale = new Vector3(edgeWidth / frames[0].bounds.size.x, width / frames[0].bounds.size.y, 1f);
        else if (bodySr != null && bodySprite != null) bodySr.size = new Vector2(edgeWidth, width);
        // 닿음 판정: 현재 구간 안에서만, 앞면 뒤로 killDepth 까지, 폭은 실제 빈 공간만 (블럭 너머 다른 층은 안 죽음)
        if (col != null)
        {
            float back = Mathf.Min(killDepth, legD + (leg == 0 ? depth : 0f)) - killInset;
            back = Mathf.Max(back, 0.05f);
            col.size = new Vector2(back, Mathf.Max(0.1f, killL + killR));
            col.offset = new Vector2(-killInset - back * 0.5f, (killL - killR) * 0.5f);
        }
        UpdateTrail();
    }

    /// <summary>지나간 곳을 하얗게 채운다. 지나온 구간은 끝(천장)까지, 현재 구간은 앞면 조금 뒤까지.
    /// 첫 구간만 시작점 뒤로 depth 만큼 더 채운다 (화면 밖에서 밀려오는 느낌).</summary>
    void UpdateTrail()
    {
        if (runs == null || trailRoot == null) return;
        bool vis = bodySr != null && bodySr.enabled;
        for (int i = 0; i < runs.Length; i++)
        {
            float limit = i < leg ? float.MaxValue : legD - edgeWidth * 0.6f; // 현재 구간은 앞면 띠의 꽉 찬 부분 밑까지
            Vector2 u = legDir[i];
            Vector2 perp = new Vector2(-u.y, u.x);
            float rot = Mathf.Atan2(u.y, u.x) * Mathf.Rad2Deg;
            foreach (var r in runs[i])
            {
                float a1 = Mathf.Min(r.a1, limit);
                bool on = vis && i <= leg && a1 - r.a0 > 0.01f;
                if (r.sr.enabled != on) r.sr.enabled = on;
                if (r.mask != null && r.mask.enabled != on) r.mask.enabled = on;
                if (!on) continue;
                var tr = r.sr.transform;
                tr.position = legA[i] + u * ((r.a0 + a1) * 0.5f) + perp * ((r.left - r.right) * 0.5f);
                tr.rotation = Quaternion.Euler(0f, 0f, rot);
                tr.localScale = new Vector3(a1 - r.a0, r.left + r.right, 1f);
            }
        }
        if (coverObjects) CoverObjects();
    }

    void BuildTrail()
    {
        if (cum == null) return;
        trailRoot = new GameObject("VaccineTrail").transform;
        if (pixelSprite == null) pixelSprite = MakePixelSprite();
        for (int i = 0; i < runs.Length; i++)
            foreach (var r in runs[i])
            {
                var go = new GameObject("Trail" + i);
                go.transform.SetParent(trailRoot, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = pixelSprite;
                sr.color = color;
                sr.sortingLayerName = sortingLayer;
                sr.sortingOrder = sortingOrder - 1; // 앞면 지지직이 위
                sr.enabled = false;
                r.sr = sr;
                if (coverObjects)
                {
                    r.mask = go.AddComponent<SpriteMask>();
                    r.mask.sprite = pixelSprite;
                    r.mask.enabled = false;
                }
            }
    }

    // ── 모양 ──────────────────────────────

    void BuildBody()
    {
        col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = true;

        var go = new GameObject("Body");
        body = go.transform;
        body.SetParent(transform, false);
        body.localPosition = new Vector3(-edgeWidth * 0.5f, 0f, 0f);
        bodySr = go.AddComponent<SpriteRenderer>();
        bodySr.sortingLayerName = sortingLayer;
        bodySr.sortingOrder = sortingOrder;
        bodySr.color = color;
        if (bodySprite != null)
        {
            bodySr.sprite = bodySprite;
            bodySr.drawMode = SpriteDrawMode.Sliced;
            bodySr.size = new Vector2(edgeWidth, span);
        }
        else
        {
            frames = new Sprite[Mathf.Max(1, glitchFrames)];
            for (int i = 0; i < frames.Length; i++) frames[i] = GenerateGlitchSprite(7 + i * 13);
            bodySr.sprite = frames[0];
            Vector2 sz = bodySr.sprite.bounds.size; // 스케일로 edgeWidth x span 크기에 맞춤
            body.localScale = new Vector3(edgeWidth / sz.x, span / sz.y, 1f);
            if (coverObjects)
            {
                bodyMask = go.AddComponent<SpriteMask>(); // 들쭉날쭉한 앞면 모양 그대로 가림 (슬라이스 스프라이트는 마스크가 못 따라가서 임시 그림일 때만)
                bodyMask.sprite = frames[0];
            }
        }
    }

    void SetVisible(bool on)
    {
        if (bodySr != null) bodySr.enabled = on;
        if (bodyMask != null) bodyMask.enabled = on;
        if (col != null) col.enabled = on;
        UpdateTrail();
    }

    void OnDestroy()
    {
        if (debrisRoot != null) Destroy(debrisRoot.gameObject);
        if (trailRoot != null) Destroy(trailRoot.gameObject);
        if (generatedThud != null) Destroy(generatedThud);
        if (cam != null) cam.SetRumble(this, 0f);
    }

    /// <summary>임시 그림: 오른쪽(앞면) 가장자리가 들쭉날쭉한 띠 (흰색 → color로 칠함).</summary>
    static Sprite GenerateGlitchSprite(int seed)
    {
        const int w = 40, h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[w * h];
        var rnd = new System.Random(seed);
        int edge = w - 6;
        for (int y = 0; y < h; y++)
        {
            // 몇 줄씩 묶어 앞면 길이를 들쭉날쭉하게 (글리치 블록). 띠의 오른쪽 40% 안에서
            if (y % 4 == 0) edge = w - 1 - rnd.Next(0, w * 2 / 5);
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
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), h);
    }

    /// <summary>임시 '쿵' 소리: 낮게 떨어지는 음 + 짧은 노이즈, 빠르게 사라짐.</summary>
    static AudioClip GenerateThud()
    {
        const int rate = 44100;
        int n = (int)(rate * 0.6f);
        var data = new float[n];
        var rnd = new System.Random(3);
        float phase = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float f = Mathf.Lerp(95f, 38f, Mathf.Clamp01(t / 0.25f));
            phase += 2f * Mathf.PI * f / rate;
            lp += ((float)rnd.NextDouble() * 2f - 1f - lp) * 0.08f;
            float env = Mathf.Exp(-t * 7f);
            float attack = Mathf.Clamp01(t / 0.004f);
            data[i] = (Mathf.Sin(phase) * 0.9f + lp * 1.6f * Mathf.Exp(-t * 18f)) * env * attack;
        }
        var clip = AudioClip.Create("VaccineThud_Temp", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void OnDrawGizmos()
    {
        if (path == null) return;
        Gizmos.color = Color.red;
        for (int i = 0; i < path.Length - 1; i++)
            if (path[i] != null && path[i + 1] != null) Gizmos.DrawLine(path[i].position, path[i + 1].position);
        if (legA == null) return;
        Gizmos.color = Color.yellow; // 실제 앞면이 지나가는 구간 (플레이 중)
        for (int i = 0; i < legA.Length; i++) Gizmos.DrawLine(legA[i], legA[i] + legDir[i] * legLen[i]);
    }
}
