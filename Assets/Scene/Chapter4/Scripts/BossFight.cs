using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 챕터 4 임시 보스전 (신). 피하기 위주 + 손목 부담이 적게: 패턴마다 예고가 길고, 피할 때 그림 한 번이면 되며, 패턴 사이에 쉬는 시간이 있다.
///  등장: 바닥 선 → 카메라가 뒤로 빠지며 거대한 신이 나타남 → 대사(introDialogue) → 시작.
///  패턴 (섞어서 돌아가고 탄은 3·4번째마다 꼭 나옴, 2단계부터 가끔 두 패턴이 겹쳐 나옴)
///   - 지우개 기둥: 바닥에 빨간 예고 → 하얀 기둥. ㅡ 이동으로 비켜서기.
///   - 탄: 느린 탄. 피하거나, 나뭇가지 ㄱ 으로 되받아치면 보스에 금이 간다.
///   - 가로 지우기: 아래 줄은 ㅣ 점프, 위 줄은 ㄴ 슬라이드.
///   - 신의 커서: 커서가 따라오다 멈추고 선택 박스를 끌어 지운다. 걸어서 빠지거나 9 + ⋉ 텔레포트.
///   - 내려찍기: 지우개가 위에서 따라오다 내려찍고, 바닥을 타고 퍼지는 충격파는 점프.
///  금이 reflectsToStun 번 가면 보스가 내려와 틈을 보인다 → § 를 그리면 1단계 깎임. bossPhases 단계면 끝.
///  플레이어 하트가 다 떨어지면 FallRespawn 으로 죽고, 보스전 처음부터 다시 (대사는 다시 안 나옴).
/// ProjectWindowInteract.onBlack 에서 Begin() 을 부른다. 무대·하트·패턴은 런타임에 만든다 (임시 그림).
/// </summary>
[DefaultExecutionOrder(1100)] // 카메라 고정이 CameraFollow·CameraClampToSprite 뒤에 오도록
public class BossFight : MonoBehaviour
{
    [Header("무대")]
    [Tooltip("무대 반폭 (units). 카메라를 뺀 화면 반폭(약 20)보다 좁게. 넓으면 피할 때 많이 그어야 함")]
    public float arenaHalfWidth = 18.5f;
    [Tooltip("화면 아래 끝에서 바닥까지 거리 (units, 카메라 빼기 전 기준). 스킬 UI 자리")]
    public float floorMargin = 2.5f;

    [Header("등장")]
    [Tooltip("보스가 나오기 전에 하는 대사 (신의 speakerId = God)")]
    public DialogueData introDialogue;
    [Tooltip("보스 등장 때 카메라를 뒤로 빼는 배율 (화면이 이만큼 넓어짐 → 메크로는 작아 보임)")]
    public float zoomOut = 1.4f;
    [Tooltip("카메라를 뒤로 빼는 시간 (초)")]
    public float zoomTime = 2.2f;
    [Tooltip("신 말풍선 크기 (메크로 말풍선 대비 배율)")]
    public float godBubbleScale = 1.8f;
    [Tooltip("신 말풍선 오른쪽 끝과 신 사이 간격 (units)")]
    public float godBubbleGap = 0.8f;

    [Header("보스 그림")]
    [Tooltip("보스 그림 (비우면 임시 눈 그림). 픽셀아트면 Filter Mode = Point 권장")]
    public Sprite bossSprite;
    [Tooltip("보스 크기 (그림 높이, units)")]
    public float bossSize = 8f;
    [Tooltip("보스 중심 높이 (바닥 위 units)")]
    public float bossHeight = 13.5f;

    [Header("체력")]
    public int hearts = 3;
    public Sprite[] healthFullFrames;
    public Sprite[] healthEmptyFrames;
    public Material afterimageMaterial;
    [Tooltip("맞은 뒤 무적 시간 (초)")]
    public float invulnTime = 1.5f;
    [Tooltip("§ 로 깎아야 하는 횟수")]
    public int bossPhases = 3;
    [Tooltip("틈이 생기기까지 되받아쳐야 하는 탄 수")]
    public int reflectsToStun = 2;
    [Tooltip("틈을 보이는 시간 (초). 이 안에 § 를 그리면 된다")]
    public float stunTime = 6f;

    [Header("박자 (넉넉하게)")]
    [Tooltip("패턴 사이 쉬는 시간 (초)")]
    public float restTime = 1.6f;
    [Tooltip("빨간 예고 시간 (초)")]
    public float telegraphTime = 1.1f;
    [Tooltip("단계마다 예고·쉬는 시간이 줄어드는 비율")]
    public float phaseSpeedUp = 0.1f;
    [Tooltip("2단계, 3단계에서 두 패턴이 겹쳐 나올 확률")]
    public Vector2 comboChance = new Vector2(0.4f, 0.65f);

    [Header("지우개 기둥")]
    public float columnWidth = 1.6f;
    public float columnActiveTime = 0.35f;

    [Header("가로 지우기")]
    public float sweepSpeed = 8f;
    public float sweepThickness = 0.7f;
    [Tooltip("아래 줄 (바닥 위 y 범위) → 점프로 피함")]
    public Vector2 lowLane = new Vector2(0f, 0.9f);
    [Tooltip("아래 줄 속도 (점프만 하면 돼서 위 줄보다 빠르게)")]
    public float lowSweepSpeed = 12f;
    [Tooltip("아래 줄 막대 개수 (연달아 옴)")]
    public int lowSweepCount = 2;
    [Tooltip("아래 줄 막대 사이 시간 간격 (초). 착지하고 다시 뛸 틈")]
    public float lowSweepGap = 0.8f;
    [Tooltip("위 줄 (바닥 위 y 범위) → 슬라이드로 피함. 슬라이드 키(약 0.76)보다 위, 선 키(약 1.9)보다 아래에서 시작")]
    public Vector2 highLane = new Vector2(1.15f, 3.2f);

    [Header("탄 (ㄱ 으로 되받아치기)")]
    public float orbSpeed = 6.5f;
    public float reflectSpeed = 14f;
    [Tooltip("탄을 모으는 시간 (초)")]
    public float orbCharge = 0.7f;
    [Tooltip("탄 사이 간격 (초)")]
    public float orbInterval = 1.4f;
    [Tooltip("ㄱ 에 맞는 탄 판정 반지름 (클수록 쳐내기 쉬움). 메크로가 맞는 판정과는 따로")]
    public float orbReflectRadius = 1.7f;
    [Tooltip("ㄱ 을 휘두르면 메크로 중심에서 이 거리 안, 휘두른 쪽(위·대각선 포함)에 있는 탄도 쳐냄")]
    public float orbAssistRange = 3.2f;

    [Header("신의 커서")]
    [Tooltip("커서가 메크로를 따라다니는 시간 (초)")]
    public float cursorFollowTime = 1.8f;
    public float cursorSpeed = 7f;
    [Tooltip("지우는 선택 박스 크기 (바닥부터)")]
    public Vector2 selectSize = new Vector2(3.6f, 3.4f);
    public float eraseTime = 0.3f;

    [Header("내려찍기 + 충격파")]
    [Tooltip("지우개가 메크로 위를 따라다니는 시간 (초)")]
    public float slamFollowTime = 1.6f;
    public float slamFollowSpeed = 7f;
    public Vector2 slamSize = new Vector2(3.2f, 2.2f);
    [Tooltip("지우개가 떠 있는 높이 (바닥 위)")]
    public float slamHover = 8f;
    [Tooltip("내려찍기 직전 덜덜 떠는 시간 (초)")]
    public float slamShakeTime = 0.5f;
    public float slamFallSpeed = 40f;
    [Tooltip("내려찍은 뒤 충격파가 출발하기까지 (초). 그동안 바닥 줄이 빨갛게 예고됨")]
    public float shockDelay = 0.7f;
    public float shockSpeed = 9f;
    public Vector2 shockSize = new Vector2(0.6f, 0.8f);

    [Header("지우개 비 (두 번째 페이즈부터)")]
    public int rainCount = 8;
    [Tooltip("조각 사이 간격 (초)")]
    public float rainInterval = 0.3f;
    [Tooltip("떨어질 자리 예고 시간 (초)")]
    public float rainTelegraph = 0.8f;
    public float rainFallSpeed = 26f;
    public Vector2 rainSize = new Vector2(1.2f, 0.8f);

    [Header("레이저 조준 (두 번째 페이즈부터)")]
    [Tooltip("조준선이 메크로를 따라오는 시간 (초)")]
    public float laserAimTime = 1.6f;
    [Tooltip("조준선이 돌아가는 속도 (도/초). 느릴수록 따라잡기 어려움")]
    public float laserTurnSpeed = 70f;
    [Tooltip("멈춘 뒤 쏘기까지 (초)")]
    public float laserLockTime = 0.45f;
    public float laserWidth = 1.1f;
    public float laserActiveTime = 0.35f;

    [Header("복붙 그림자 (두 번째 페이즈부터)")]
    [Tooltip("그림자가 따라오는 시간 (초)")]
    public float cloneTime = 6f;
    [Tooltip("그림자가 메크로의 몇 초 전 자리를 따라오는지. 이보다 오래 멈춰 있으면 잡힘")]
    public float cloneDelay = 1f;

    [Header("색")]
    public Color hazardColor = Color.white;
    public Color warnColor = new Color(1f, 0.15f, 0.15f, 0.3f);
    public Color eraserColor = new Color(1f, 0.72f, 0.78f);
    public Color bossColor = Color.white;
    public Color stunColor = new Color(1f, 0.25f, 0.25f);
    public Color reflectedColor = new Color(0.4f, 1f, 1f);

    [Header("튜토리얼 (처음 한 번, 슬로우 모션)")]
    [Tooltip("첫 탄이 가까이 올 때 / 첫 틈이 생길 때 느려지며 그릴 모양(ㄱ / §)을 보여준다. 맞게 그릴 때까지 기다림")]
    public bool tutorial = true;
    [Range(0.05f, 1f)] public float tutorialTimeScale = 0.25f;
    [Tooltip("첫 탄이 메크로와 이 거리 안에 오면 튜토리얼 시작")]
    public float tutorialDistance = 4f;
    [Tooltip("그릴 모양 시범 선 색")]
    public Color demoColor = new Color(1f, 0.9f, 0.2f);
    [Tooltip("느려지기까지 걸리는 시간 (초, 실제 시간). 점점 멈춰가는 느낌")]
    public float tutorialSlowIn = 0.3f;
    [Tooltip("되받아치기 튜토리얼 때 카메라 확대 (1 = 그대로, 작을수록 가까이)")]
    public float reflectZoom = 0.6f;
    [Tooltip("§ 튜토리얼 때 카메라 확대")]
    public float sectionZoom = 0.8f;

    [Header("이펙트")]
    [Tooltip("쳐내기·명중·피격 때 아주 잠깐 멈춤")]
    public bool hitStopOn = true;
    [Tooltip("공격 예고·내려찍기·페이즈 전환 때 화면 지지직")]
    public bool glitchOn = true;

    [Tooltip("보스를 쓰러뜨렸을 때")]
    public UnityEvent onDefeated;

    [Tooltip("테스트용: 누르면 보스 바로 처치 (에디터·개발 빌드에서만)")]
    public KeyCode debugDefeatKey = KeyCode.F7;

    [Header("보스 체력바")]
    [Tooltip("화면 위 보스 체력바 크기 (1920x1080 기준 px)")]
    public Vector2 bossBarSize = new Vector2(640f, 26f);
    [Tooltip("화면 위 끝에서 체력바까지 (px)")]
    public float bossBarTop = 40f;
    public Color bossBarColor = new Color(1f, 0.25f, 0.25f);
    [Tooltip("체력바 아래 실드 줄 높이 (px). 칸 수 = reflectsToStun, 되받아친 탄이 맞으면 한 칸씩 깎임")]
    public float shieldBarHeight = 16f;
    public Color shieldColor = Color.white;

    class Hazard
    {
        public SpriteRenderer sr; public bool armed;
        public float hit = 1f;                      // 판정 크기 배율 (그림보다 작게)
        public Vector2 beamFrom, beamTo; public float beamHalf; // 기울어진 레이저 판정 (beamHalf > 0 일 때)
    }
    struct TrailPoint { public float t; public Vector3 pos; public Sprite sprite; public bool flip; }
    class Particle { public SpriteRenderer sr; public Vector2 v; public float life, max, gravity, spin, a; }
    enum Pattern { Columns, Orbs, Sweep, Cursor, Slam, Rain, Laser, Clone }

    PlayerController player;
    BoxCollider2D pcol;
    SpriteRenderer psr;
    Color psrColor = Color.white;
    FallRespawn fr;
    Camera cam;
    float baseOrtho = 8f;

    Transform root, hazardRoot, bossAnchor;
    SpriteRenderer boss, floorLine;
    float bossScale = 1f;
    BossHealthHud healthHud;
    GameObject bossBar;
    readonly List<UnityEngine.UI.Image> bossBarCells = new List<UnityEngine.UI.Image>();
    readonly List<UnityEngine.UI.Image> shieldCells = new List<UnityEngine.UI.Image>();
    readonly List<Hazard> hazards = new List<Hazard>();
    readonly List<BossOrb> orbs = new List<BossOrb>();

    float cx, floorTop;
    int phase, heartsLeft, cracks;
    bool started, stunned, stunHit, dying, defeated, camLocked;
    float invulnUntil;
    Vector3 camPos;
    float shakeLeft, shakeDur, shakeMag;
    float glitchTimer, glitchHold;
    DialogueSpeaker godSpeaker;

    // 튜토리얼
    enum Demo { None, AttackRight, AttackLeft, Section }
    bool reflectTaught, skillTaught, teaching;
    BossOrb tutorialOrb;
    int holdSide = 1;
    float baseFixedDt = 0.02f;
    Demo demoKind;
    LineRenderer demo;
    SpriteRenderer demoTip;
    Vector2[] demoPath;
    float demoT;
    enum Focus { Reflect, Section }
    Focus focus;
    bool slowOn;
    float slowFx;               // 0 = 평소, 1 = 슬로우 모션 연출 다 들어감 (실제 시간으로 움직임)
    bool arenaCamSet;
    Vector3 arenaCamPos;
    float arenaOrtho;
    BossOrb firstOrb;           // 튜토리얼 첫 탄 (쏘는 순간부터 메크로는 ㄱ 만 가능)

    // 이펙트
    float hitStopLeft;
    bool hitStopWas;
    readonly List<Particle> particles = new List<Particle>();
    readonly List<TrailPoint> playerTrail = new List<TrailPoint>();
    float glitchLeft, glitchPower, glitchTick;
    readonly List<SpriteRenderer> glitchBars = new List<SpriteRenderer>();
    readonly List<SpriteRenderer> bossGhosts = new List<SpriteRenderer>();

    static Sprite squareSprite, eyeSprite, orbSprite, heartSprite, cursorSprite;

    public Vector2 BossPosition => bossAnchor != null ? (Vector2)bossAnchor.position : Vector2.zero;
    float BossRadius => bossSize * 0.5f;
    Vector2 BossRest => new Vector2(cx, floorTop + bossHeight);
    float ArenaLeft => cx - arenaHalfWidth;
    float ArenaRight => cx + arenaHalfWidth;

    // ───────────── 시작 ─────────────

    /// <summary>보스전 시작 (검은 화면 뒤 onBlack 에서 호출).</summary>
    public void Begin()
    {
        if (started) return;
        started = true;
        StartCoroutine(Intro());
    }

    IEnumerator Intro()
    {
        CutsceneLock.Lock(this);
        baseFixedDt = Time.fixedDeltaTime;
        player = FindAnyObjectByType<PlayerController>();
        if (player == null) { CutsceneLock.Unlock(this); yield break; }
        pcol = player.GetComponent<BoxCollider2D>();
        // 실제로 보이는 그림 (루트의 SpriteRenderer 는 꺼져 있고 자식 CharacterSprite 가 그림)
        psr = null;
        foreach (var r in player.GetComponentsInChildren<SpriteRenderer>())
            if (r.enabled && r.sprite != null) { psr = r; break; }
        if (psr != null) psrColor = psr.color;
        fr = player.GetComponent<FallRespawn>();

        // 발밑 바닥을 찾아 무대 위치를 정한다 (나뭇가지 콜라이더는 건너뜀)
        floorTop = pcol.bounds.min.y;
        float minX = -1e5f, maxX = 1e5f;
        foreach (var h in Physics2D.RaycastAll(pcol.bounds.center, Vector2.down, 20f, player.groundLayer))
        {
            if (h.collider.isTrigger || h.collider.GetComponentInParent<Branch>() != null) continue;
            floorTop = h.point.y; minX = h.collider.bounds.min.x; maxX = h.collider.bounds.max.x;
            break;
        }
        float lo = minX + arenaHalfWidth + 1f, hi = maxX - arenaHalfWidth - 1f;
        cx = lo <= hi ? Mathf.Clamp(player.transform.position.x, lo, hi) : (minX + maxX) * 0.5f;

        BuildArena();

        // 카메라를 무대에 고정 (먼저 원래 크기로 무대 쪽으로)
        cam = Camera.main;
        if (cam != null)
        {
            var follow = cam.GetComponent<CameraFollow>();
            if (follow != null) follow.enabled = false;
            baseOrtho = cam.orthographicSize;
            // 스킬 UI 가 카메라를 빼도 화면에서 같은 크기로 보이게
            foreach (var a in cam.GetComponentsInChildren<CameraHudAnchor>(true))
                if (a.referenceOrthoSize <= 0f) a.referenceOrthoSize = baseOrtho;
            Vector3 from = cam.transform.position;
            camPos = from; camLocked = true;
            BuildHearts();
            BuildBossBar();
            yield return MoveCamera(from, CamPosFor(baseOrtho), baseOrtho, baseOrtho, 1f);
        }

        yield return Fade(floorLine, 0f, 1f, 0.5f);
        yield return new WaitForSeconds(0.3f);

        // 등장: 땅이 울리며 카메라가 뒤로 빠지고, 신이 커지며 나타난다
        float big = baseOrtho * zoomOut;
        StartCoroutine(BossAppear(zoomTime));
        if (cam != null) yield return MoveCamera(camPos, CamPosFor(big), baseOrtho, big, zoomTime);
        else yield return new WaitForSeconds(zoomTime);
        Shake(0.4f, 0.5f);
        arenaCamPos = camPos; arenaOrtho = cam != null ? cam.orthographicSize : big; arenaCamSet = cam != null;
        yield return new WaitForSeconds(0.8f);

        if (introDialogue != null) yield return DialoguePlayer.Ensure().PlayAndWait(introDialogue);
        yield return new WaitForSeconds(0.4f);

        SetHearts(hearts);
        SetBossBar();
        if (fr != null)
        {
            fr.TrySetCheckpoint(int.MaxValue, player.transform.position); // 죽으면 무대에서 부활
            fr.Died += OnDied;
            fr.Respawned += OnRespawned;
        }
        player.BossSkillUsed += OnBossSkill;
        player.AttackSwung += OnAttackSwung;

        CutsceneLock.Unlock(this);
        StartCoroutine(Loop());
    }

    Vector3 CamPosFor(float ortho)
    {
        float z = cam != null ? cam.transform.position.z : -10f;
        return new Vector3(cx, floorTop + ortho - floorMargin * (ortho / baseOrtho), z);
    }

    IEnumerator MoveCamera(Vector3 from, Vector3 to, float fromSize, float toSize, float time)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / time);
            camPos = Vector3.Lerp(from, to, k);
            cam.orthographicSize = Mathf.Lerp(fromSize, toSize, k);
            yield return null;
        }
        camPos = to;
        cam.orthographicSize = toSize;
    }

    IEnumerator BossAppear(float time)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = t / time;
            var c = boss.color; c.a = Mathf.Clamp01(k * 1.6f); boss.color = c;
            boss.transform.localScale = Vector3.one * bossScale * Mathf.Lerp(0.5f, 1f, Mathf.SmoothStep(0f, 1f, k));
            Shake(0.1f, 0.18f); // 계속 울림
            yield return null;
        }
        boss.color = bossColor;
        boss.transform.localScale = Vector3.one * bossScale;
    }

    void OnDestroy()
    {
        if (teaching || hitStopWas) { slowOn = false; Time.timeScale = 1f; Time.fixedDeltaTime = baseFixedDt; }
        CutsceneLock.Unlock(this);
        if (healthHud != null) Destroy(healthHud.gameObject);
        if (bossBar != null) Destroy(bossBar);
        if (fr != null) { fr.Died -= OnDied; fr.Respawned -= OnRespawned; }
        if (player != null) { player.BossSkillUsed -= OnBossSkill; player.AttackSwung -= OnAttackSwung; player.bossSkillReady = false; player.combatOnly = false; }
    }

    void BuildArena()
    {
        root = new GameObject("BossArena").transform;
        hazardRoot = new GameObject("Hazards").transform;
        hazardRoot.SetParent(root, false);

        // 보이지 않는 양쪽 벽 (Ground 레이어 → 이동이 막힘)
        int ground = LayerMask.NameToLayer("Ground");
        foreach (int s in new[] { -1, 1 })
        {
            var w = new GameObject("ArenaWall");
            w.transform.SetParent(root, false);
            if (ground >= 0) w.layer = ground;
            w.transform.position = new Vector3(cx + s * (arenaHalfWidth + 0.5f), floorTop + 20f, 0f);
            w.AddComponent<BoxCollider2D>().size = new Vector2(1f, 40f);
        }

        floorLine = MakeSprite("FloorLine", root, Square(), 0, Color.white);
        floorLine.transform.position = new Vector3(cx, floorTop - 0.03f, 0f);
        floorLine.transform.localScale = new Vector3(arenaHalfWidth * 2f, 0.06f, 1f);
        floorLine.color = new Color(1f, 1f, 1f, 0f);

        bossAnchor = new GameObject("God").transform;
        bossAnchor.SetParent(root, false);
        bossAnchor.position = BossRest;
        var sprite = bossSprite != null ? bossSprite : Eye();
        boss = MakeSprite("Body", bossAnchor, sprite, 15, bossColor);
        bossScale = bossSize / Mathf.Max(0.01f, sprite.bounds.size.y);
        boss.transform.localScale = Vector3.one * bossScale;
        boss.color = new Color(bossColor.r, bossColor.g, bossColor.b, 0f);

        BuildGodSpeaker();
    }

    /// <summary>신의 말풍선 (speakerId = God). 메크로 말풍선 설정을 가져와 크기만 키우고 색을 뒤집는다.</summary>
    void BuildGodSpeaker()
    {
        var go = new GameObject("GodSpeaker");
        go.SetActive(false); // 설정을 다 넣은 뒤 켜야 말풍선이 그 설정으로 만들어짐
        go.transform.SetParent(bossAnchor, false);
        go.transform.localPosition = new Vector3(-(BossRadius + godBubbleGap), 0.5f, 0f); // 신 왼쪽 옆 (실제 자리는 KeepGodBubbleClear)
        var sp = go.AddComponent<DialogueSpeaker>();
        sp.speakerId = "God";
        sp.bubbleOffset = Vector2.zero;
        var src = player.GetComponent<DialogueSpeaker>();
        if (src != null)
        {
            sp.font = src.font;
            sp.fontSize = src.fontSize * godBubbleScale;
            sp.maxWidth = src.maxWidth * godBubbleScale;
            sp.padding = src.padding * godBubbleScale;
            sp.borderWidth = src.borderWidth * godBubbleScale;
            sp.tailSize = src.tailSize * godBubbleScale;
            sp.glitchJitter = src.glitchJitter * godBubbleScale;
            sp.sortingOrder = src.sortingOrder;
        }
        sp.backgroundColor = Color.black;
        sp.borderColor = Color.white;
        sp.textColor = Color.white;
        go.SetActive(true);
        godSpeaker = sp;
    }

    /// <summary>신 말풍선은 대사 길이마다 폭이 달라서, 오른쪽 끝을 신 왼쪽(간격 godBubbleGap)에 맞춰 옮긴다 → 신과 안 겹침.
    /// SpeechBubble 이 자리를 잡은 뒤(LateUpdate, 이 컴포넌트가 더 늦게 실행)에 옮긴다.</summary>
    void KeepGodBubbleClear()
    {
        if (godSpeaker == null || godSpeaker.Bubble == null || !godSpeaker.Bubble.gameObject.activeInHierarchy) return;
        var bt = godSpeaker.Bubble.transform;
        float maxX = float.MinValue;
        foreach (var r in bt.GetComponentsInChildren<Renderer>())
            if (r.enabled) maxX = Mathf.Max(maxX, r.bounds.max.x);
        if (maxX == float.MinValue) return;
        float halfW = boss != null && boss.sprite != null ? boss.sprite.bounds.extents.x * bossScale : BossRadius; // 그림 실제 폭 (지지직 흔들림은 무시)
        float right = BossPosition.x - halfW - godBubbleGap;
        bt.position += Vector3.right * (right - maxX);
    }

    // ───────────── 진행 ─────────────

    IEnumerator Loop()
    {
        List<Pattern> round = null;
        int ri = 0;
        Pattern? last = null;
        // 탄이 안 나오면 틈이 안 생겨 진행이 안 되므로, 다른 패턴 2~3개 뒤(3·4번째)에 꼭 탄
        int sinceOrbs = 0, orbsAfter = Random.Range(2, 4);
        yield return new WaitForSeconds(0.8f);
        while (!defeated)
        {
            float k = Mathf.Max(0.5f, 1f - phaseSpeedUp * phase);
            while (teaching) yield return null; // 튜토리얼 중엔 다음 패턴을 시작하지 않음
            Pattern p;
            if (sinceOrbs >= orbsAfter) { p = Pattern.Orbs; sinceOrbs = 0; orbsAfter = Random.Range(2, 4); }
            else
            {
                if (round == null || ri >= round.Count) { round = ShuffleRound(last); ri = 0; }
                p = round[ri++];
                last = p;
                sinceOrbs++;
            }
            float chance = phase >= 2 ? comboChance.y : phase >= 1 ? comboChance.x : 0f;
            Pattern partner;
            if (TryPartner(p, out partner) && Random.value < chance)
            {
                // 겹치기: 첫 패턴이 시작되고 조금 뒤 두 번째가 시작
                var a = StartCoroutine(Run(p, k));
                yield return new WaitForSeconds(0.6f);
                var b = StartCoroutine(Run(partner, k));
                yield return a;
                yield return b;
            }
            else yield return Run(p, k);

            if (cracks >= reflectsToStun) yield return Stun();
            if (defeated) yield break;
            yield return new WaitForSeconds(restTime * k);
        }
    }

    IEnumerator Run(Pattern p, float k)
    {
        switch (p)
        {
            case Pattern.Columns: return Columns(k);
            case Pattern.Orbs: return Orbs(k);
            case Pattern.Sweep: return Sweep(k);
            case Pattern.Cursor: return CursorErase(k);
            case Pattern.Rain: return Rain(k);
            case Pattern.Laser: return Laser(k);
            case Pattern.Clone: return Clone(k);
            default: return Slam(k);
        }
    }

    /// <summary>같이 나와도 되는 짝. 탄·레이저는 단독 (보스가 움직이는 패턴끼리도 겹치지 않음).</summary>
    static bool TryPartner(Pattern p, out Pattern partner)
    {
        switch (p)
        {
            case Pattern.Columns: partner = Pattern.Sweep; return true;
            case Pattern.Sweep: partner = Pattern.Cursor; return true;
            case Pattern.Slam: partner = Pattern.Columns; return true;
            case Pattern.Cursor: partner = Pattern.Sweep; return true;
            case Pattern.Rain: partner = Pattern.Sweep; return true;
            case Pattern.Clone: partner = Pattern.Columns; return true;
        }
        partner = p;
        return false;
    }

    // 지우개 기둥
    IEnumerator Columns(float k)
    {
        int waves = phase >= 1 ? 2 : 1;
        int count = phase >= 2 ? 3 : 2;
        const float height = 30f;
        for (int w = 0; w < waves; w++)
        {
            var list = new List<Hazard>();
            foreach (float x in PickColumnXs(count))
                list.Add(AddRect(new Vector2(x, floorTop + height * 0.5f), new Vector2(columnWidth, height), warnColor, 5));
            yield return Telegraph(list, telegraphTime * k);

            foreach (var h in list)
            {
                h.sr.color = hazardColor; h.sr.sortingOrder = 20; h.armed = true;
                Dust(new Vector2(h.sr.transform.position.x, floorTop), 0, 10, hazardColor);
            }
            Shake(0.12f, 0.1f);
            yield return new WaitForSeconds(columnActiveTime);
            foreach (var h in list)
            {
                h.armed = false;
                Dissolve(new Vector2(h.sr.transform.position.x, floorTop + 4f), new Vector2(columnWidth, 8f), hazardColor, 14);
            }
            yield return FadeOut(list, 0.25f);
            if (w < waves - 1) yield return new WaitForSeconds(0.45f);
        }
    }

    List<float> PickColumnXs(int count)
    {
        float left = ArenaLeft + columnWidth * 0.5f, right = ArenaRight - columnWidth * 0.5f;
        var xs = new List<float> { Mathf.Clamp(player.transform.position.x, left, right) }; // 하나는 꼭 플레이어 자리
        for (int tries = 0; xs.Count < count && tries < 40; tries++)
        {
            float x = Random.Range(left, right);
            bool ok = true;
            foreach (float o in xs) if (Mathf.Abs(o - x) < 3f) { ok = false; break; }
            if (ok) xs.Add(x);
        }
        return xs;
    }

    // 가로 지우기
    IEnumerator Sweep(float k)
    {
        int bars = phase >= 2 ? 2 : 1;
        bool low = Random.value < 0.5f;
        for (int b = 0; b < bars; b++)
        {
            int side = Random.value < 0.5f ? -1 : 1; // 출발 쪽
            Vector2 lane = low ? lowLane : highLane;
            float y0 = floorTop + lane.x, y1 = floorTop + lane.y, yc = (y0 + y1) * 0.5f, hh = y1 - y0;

            var band = AddRect(new Vector2(cx, yc), new Vector2(arenaHalfWidth * 2f, hh), warnColor, 5);
            var mark = AddRect(new Vector2(cx + side * (arenaHalfWidth - 0.3f), yc), new Vector2(0.6f, hh), new Color(warnColor.r, warnColor.g, warnColor.b, 0.9f), 6);
            yield return Telegraph(new List<Hazard> { band, mark }, telegraphTime * k);
            RemoveHazard(mark);

            // 아래 줄은 빠르게, 여러 개가 lowSweepGap 초 간격으로 연달아 옴
            float speed = low ? lowSweepSpeed : sweepSpeed;
            int count = low ? Mathf.Max(1, lowSweepCount) : 1;
            float spacing = speed * lowSweepGap;
            float x = cx + side * (arenaHalfWidth + 1f), end = cx - side * (arenaHalfWidth + 1f);
            var barList = new List<Hazard>();
            for (int i = 0; i < count; i++)
            {
                var bar = AddRect(new Vector2(x + side * spacing * i, yc), new Vector2(sweepThickness, hh), hazardColor, 20);
                bar.armed = true;
                barList.Add(bar);
            }
            float last = x + side * spacing * (count - 1);
            float trail = 0f;
            while (side > 0 ? last > end : last < end)
            {
                float dx = side * speed * Time.deltaTime;
                x -= dx; last -= dx;
                for (int i = 0; i < count; i++) barList[i].sr.transform.position = new Vector3(x + side * spacing * i, yc, 0f);
                if ((trail -= Time.deltaTime) <= 0f)
                {
                    trail = 0.04f;
                    for (int i = 0; i < count; i++)
                    {
                        AfterImage(barList[i].sr);
                        if (low) Dust(new Vector2(barList[i].sr.transform.position.x, floorTop), side, 1, hazardColor);
                    }
                }
                yield return null;
            }
            foreach (var bar in barList) RemoveHazard(bar);
            RemoveHazard(band);
            low = !low;
            if (b < bars - 1) yield return new WaitForSeconds(0.6f);
        }
    }

    // 탄
    IEnumerator Orbs(float k)
    {
        int n = phase >= 2 ? 3 : 2;
        for (int i = 0; i < n; i++)
        {
            while (teaching) yield return null; // 튜토리얼 중엔 다음 탄을 쏘지 않음
            // 플레이어 옆 위로 옮겨 가서 비스듬히 쏜다 (정수리로 떨어지면 ㄱ 으로 못 받음)
            float px = player.transform.position.x;
            float minX = ArenaLeft + BossRadius * 0.5f, maxX = ArenaRight - BossRadius * 0.5f;
            float s = px > cx ? -1f : 1f;
            float bx = Mathf.Clamp(px + s * 8f, minX, maxX);
            if (Mathf.Abs(bx - px) < 5f) bx = Mathf.Clamp(px - s * 8f, minX, maxX);
            yield return MoveBoss(new Vector2(bx, floorTop + bossHeight), 0.5f);

            var orb = SpawnOrb();
            for (float t = 0f; t < orbCharge * k; t += Time.deltaTime)
            {
                if (orb == null) yield break;
                float a = t / (orbCharge * k);
                orb.transform.position = BossPosition + Vector2.down * (BossRadius + 0.4f);
                orb.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, a);
                yield return null;
            }
            if (orb == null) yield break;
            orb.transform.localScale = Vector3.one;
            Vector2 target = pcol.bounds.center;
            orb.velocity = (target - (Vector2)orb.transform.position).normalized * orbSpeed * (1f + 0.1f * phase);
            orb.enabled = true;
            if (tutorial && !reflectTaught && firstOrb == null)
            {
                // 첫 탄: 쏘는 순간 메크로를 멈추고 ㄱ 만 받게 → 가까이 오면 슬로우 모션으로 알려줌 (TickOrb)
                firstOrb = orb;
                player.ResetMotion();
                player.combatOnly = true;
            }
            yield return new WaitForSeconds(orbInterval * k);
        }
        yield return MoveBoss(BossRest, 0.6f);
    }

    BossOrb SpawnOrb()
    {
        var sr = MakeSprite("BossOrb", hazardRoot, OrbSprite(), 18, new Color(1f, 0.3f, 0.3f, 1f));
        var go = sr.gameObject;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var orb = go.AddComponent<BossOrb>();
        orb.fight = this;
        var cc = go.AddComponent<CircleCollider2D>();
        cc.radius = orbReflectRadius; // ㄱ 히트박스에 맞기 쉽게 크게
        cc.isTrigger = true;
        orb.enabled = false; // 모으는 동안은 멈춤
        orbs.Add(orb);
        return orb;
    }

    /// <summary>BossOrb 가 매 프레임 호출: 플레이어/보스/바닥 판정.</summary>
    public void TickOrb(BossOrb o)
    {
        Vector2 p = o.transform.position;
        if (o.reflected)
        {
            if (Vector2.Distance(p, BossPosition) < BossRadius * 0.8f)
            {
                RemoveOrb(o);
                cracks++;
                SetBossBar();
                StartCoroutine(BossFlash(new Color(1f, 0.4f, 0.4f), 0.15f));
                Shake(0.15f, 0.2f);
                HitStop(0.07f);
                Glitch(0.15f, 0.5f);
                Burst(p, reflectedColor, 14, 8f, 0.25f);
            }
            return;
        }
        // 첫 탄: 가까이 오면 슬로우 모션 + ㄱ 시범. 탄은 메크로 앞(공격 판정 자리)에 멈춰서 맞게 칠 때까지 기다린다
        if (o == firstOrb && !reflectTaught && !teaching && Vector2.Distance(p, pcol.bounds.center) < tutorialDistance)
        {
            StartReflectTutorial(o);
            return;
        }
        if (o.held) return;
        if (!teaching && !dying && !defeated && Time.time >= invulnUntil && CircleHitsBox(p, o.radius * 0.8f, PlayerHitBounds()))
        {
            RemoveOrb(o);
            Hurt();
            return;
        }
        if (p.y < floorTop + 0.2f || p.x < ArenaLeft - 3f || p.x > ArenaRight + 3f) RemoveOrb(o);
    }

    public void OnOrbReflected(BossOrb o)
    {
        var sr = o.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = reflectedColor;
        Shake(0.1f, 0.08f);
        HitStop(0.08f);
        Burst(o.transform.position, Color.white, 6, 7f, 0.15f);
        Burst(o.transform.position, reflectedColor, 8, 5f, 0.2f);
        if (o == firstOrb && !reflectTaught)
        {
            reflectTaught = true;
            player.combatOnly = false;
        }
        if (o == tutorialOrb)
        {
            o.held = false;
            tutorialOrb = null;
            reflectTaught = true;
            teaching = false;
            SetSlow(false);
            HideDemo();
            invulnUntil = Time.time + 0.5f; // 풀리자마자 다른 탄에 맞지 않게
            Shake(0.2f, 0.25f); // 탁 풀리는 타격감
        }
    }

    /// <summary>튜토리얼 탄이 멈춰 있을 자리 = 메크로가 그쪽을 보고 ㄱ 할 때의 공격 판정 중심.</summary>
    public Vector2 OrbHoldPoint => (Vector2)player.transform.position + new Vector2(holdSide * player.attackReach, player.attackHeight);

    void StartReflectTutorial(BossOrb o)
    {
        teaching = true;
        tutorialOrb = o;
        o.held = true;
        holdSide = o.transform.position.x >= player.transform.position.x ? 1 : -1;
        if (player.branch != null && player.branch.CurrentState != Branch.State.Held) player.branch.Recover(); // 공격하려면 나뭇가지가 손에 있어야 함
        focus = Focus.Reflect;
        SetSlow(true);
        ShowDemo(holdSide > 0 ? Demo.AttackRight : Demo.AttackLeft);
    }

    // 신의 커서: 따라오다 멈추고 → 선택 박스를 끌어서 → 지운다
    IEnumerator CursorErase(float k)
    {
        int clicks = phase >= 2 ? 2 : 1;
        var cursor = AddSprite(CursorSprite(), BossPosition, 25, Color.white);
        Vector2 pos = BossPosition;
        for (int c = 0; c < clicks; c++)
        {
            for (float t = 0f; t < cursorFollowTime * k; t += Time.deltaTime)
            {
                Vector2 target = pcol.bounds.center;
                pos = Vector2.MoveTowards(pos, target, cursorSpeed * Time.deltaTime);
                cursor.sr.transform.position = pos;
                yield return null;
            }

            // 멈춘 자리를 중심으로 바닥부터 선택 박스
            float bx = Mathf.Clamp(pos.x, ArenaLeft + selectSize.x * 0.5f, ArenaRight - selectSize.x * 0.5f);
            Vector2 tl = new Vector2(bx - selectSize.x * 0.5f, floorTop + selectSize.y);
            Vector2 br = new Vector2(bx + selectSize.x * 0.5f, floorTop);
            var box = AddRect((tl + br) * 0.5f, selectSize, warnColor, 5);
            // 커서가 왼쪽 위 → 오른쪽 아래로 끌기 (예고 동안)
            float tel = telegraphTime * k;
            StartCoroutine(Telegraph(new List<Hazard> { box }, tel));
            Vector2 start = pos;
            for (float t = 0f; t < tel; t += Time.deltaTime)
            {
                float a = t / tel;
                pos = a < 0.2f ? Vector2.Lerp(start, tl, a / 0.2f) : Vector2.Lerp(tl, br, Mathf.SmoothStep(0f, 1f, (a - 0.2f) / 0.8f));
                cursor.sr.transform.position = pos;
                yield return null;
            }

            box.sr.color = hazardColor; box.sr.sortingOrder = 20; box.armed = true;
            Shake(0.15f, 0.15f);
            Glitch(0.15f, 0.6f);
            yield return new WaitForSeconds(eraseTime);
            box.armed = false;
            Dissolve(box.sr.transform.position, selectSize, hazardColor, 24);
            yield return FadeOut(new List<Hazard> { box }, 0.25f);
            if (c < clicks - 1) yield return new WaitForSeconds(0.3f);
        }
        yield return FadeOut(new List<Hazard> { cursor }, 0.3f);
    }

    // 내려찍기 + 충격파
    IEnumerator Slam(float k)
    {
        int slams = phase >= 2 ? 2 : 1;
        float halfW = slamSize.x * 0.5f;
        for (int s = 0; s < slams; s++)
        {
            float x = Mathf.Clamp(player.transform.position.x, ArenaLeft + halfW, ArenaRight - halfW);
            float hoverY = floorTop + slamHover;
            var er = AddRect(new Vector2(x, hoverY), slamSize, eraserColor, 19);
            var shadow = AddRect(Vector2.zero, Vector2.one, warnColor, 5);
            System.Action place = () =>
            {
                float bottom = er.sr.transform.position.y - slamSize.y * 0.5f;
                shadow.sr.transform.position = new Vector3(x, (floorTop + bottom) * 0.5f, 0f);
                shadow.sr.transform.localScale = new Vector3(slamSize.x, Mathf.Max(0.01f, bottom - floorTop), 1f);
            };

            // 따라다니기
            for (float t = 0f; t < slamFollowTime * k; t += Time.deltaTime)
            {
                float tx = Mathf.Clamp(player.transform.position.x, ArenaLeft + halfW, ArenaRight - halfW);
                x = Mathf.MoveTowards(x, tx, slamFollowSpeed * Time.deltaTime);
                er.sr.transform.position = new Vector3(x, hoverY, 0f);
                place();
                yield return null;
            }
            // 덜덜 (예고)
            for (float t = 0f; t < slamShakeTime * k; t += Time.deltaTime)
            {
                er.sr.transform.position = new Vector3(x + Random.Range(-0.12f, 0.12f), hoverY, 0f);
                var c = shadow.sr.color; c.a = warnColor.a * (0.6f + 0.4f * Mathf.Sin(t * 40f)); shadow.sr.color = c;
                yield return null;
            }
            RemoveHazard(shadow);

            // 내려찍기
            er.armed = true;
            float y = hoverY, landY = floorTop + slamSize.y * 0.5f;
            while (y > landY)
            {
                y = Mathf.Max(landY, y - slamFallSpeed * Time.deltaTime);
                er.sr.transform.position = new Vector3(x, y, 0f);
                yield return null;
            }
            Shake(0.4f, 0.4f);
            HitStop(0.05f);
            Glitch(0.25f, 0.8f);
            Dust(new Vector2(x - halfW, floorTop), -1, 12, Color.white);
            Dust(new Vector2(x + halfW, floorTop), 1, 12, Color.white);
            yield return new WaitForSeconds(0.1f);
            er.armed = false;

            // 충격파: 바닥 줄 예고 → 양쪽으로 퍼짐 (점프)
            float sy = floorTop + shockSize.y * 0.5f;
            var band = AddRect(new Vector2(cx, sy), new Vector2(arenaHalfWidth * 2f, shockSize.y), warnColor, 5);
            yield return Telegraph(new List<Hazard> { band }, Mathf.Max(0.5f, shockDelay * k));
            var waves = new List<Hazard>();
            foreach (int d in new[] { -1, 1 })
            {
                var wv = AddRect(new Vector2(x + d * (halfW + shockSize.x * 0.5f), sy), shockSize, hazardColor, 20);
                wv.armed = true;
                waves.Add(wv);
            }
            StartCoroutine(FadeOut(new List<Hazard> { er }, 0.6f));
            bool any = true;
            while (any)
            {
                any = false;
                for (int i = 0; i < waves.Count; i++)
                {
                    var wv = waves[i];
                    if (wv.sr == null) continue;
                    var p = wv.sr.transform.position;
                    p.x += (i == 0 ? -1f : 1f) * shockSpeed * Time.deltaTime;
                    wv.sr.transform.position = p;
                    if (Random.value < 0.4f) Dust(new Vector2(p.x, floorTop), i == 0 ? -1 : 1, 1, hazardColor);
                    if (Random.value < 0.5f) AfterImage(wv.sr);
                    if (p.x < ArenaLeft - 1f || p.x > ArenaRight + 1f) RemoveHazard(wv);
                    else any = true;
                }
                yield return null;
            }
            RemoveHazard(band);
            if (s < slams - 1) yield return new WaitForSeconds(0.5f);
        }
    }

    // 지우개 비: 떨어질 자리에 예고 → 위에서 지우개 조각이 떨어짐
    IEnumerator Rain(float k)
    {
        int count = phase >= 2 ? rainCount + 4 : rainCount;
        float left = ArenaLeft + rainSize.x * 0.5f, right = ArenaRight - rainSize.x * 0.5f;
        float topY = cam != null ? cam.transform.position.y + cam.orthographicSize + rainSize.y : floorTop + 16f;
        var recent = new List<float>();
        int running = 0;
        for (int i = 0; i < count; i++)
        {
            float x;
            if (i % 3 == 0) x = Mathf.Clamp(player.transform.position.x, left, right); // 세 번에 한 번은 메크로 자리
            else
            {
                x = Random.Range(left, right);
                for (int tries = 0; tries < 10; tries++)
                {
                    bool ok = true;
                    foreach (float r in recent) if (Mathf.Abs(r - x) < rainSize.x * 1.5f) { ok = false; break; }
                    if (ok) break;
                    x = Random.Range(left, right);
                }
            }
            recent.Add(x);
            if (recent.Count > 3) recent.RemoveAt(0);
            running++;
            StartCoroutine(RainDrop(x, topY, k, () => running--));
            yield return new WaitForSeconds(rainInterval * k);
        }
        while (running > 0) yield return null;
    }

    IEnumerator RainDrop(float x, float topY, float k, System.Action done)
    {
        // 예고: 바닥 표시 + 위에서 내려오는 가는 선
        var mark = AddRect(new Vector2(x, floorTop + 0.15f), new Vector2(rainSize.x, 0.3f), new Color(warnColor.r, warnColor.g, warnColor.b, 0.8f), 6);
        var line = AddRect(new Vector2(x, (floorTop + topY) * 0.5f), new Vector2(0.08f, topY - floorTop), warnColor, 5);
        yield return Telegraph(new List<Hazard> { mark, line }, rainTelegraph * k);
        RemoveHazard(line);

        var drop = AddRect(new Vector2(x, topY), rainSize, eraserColor, 19);
        drop.armed = true;
        float y = topY, landY = floorTop + rainSize.y * 0.5f, trail = 0f;
        while (y > landY)
        {
            y = Mathf.Max(landY, y - rainFallSpeed * Time.deltaTime);
            if (drop.sr == null) { done(); yield break; }
            drop.sr.transform.position = new Vector3(x, y, 0f);
            if ((trail -= Time.deltaTime) <= 0f) { trail = 0.03f; AfterImage(drop.sr); }
            yield return null;
        }
        RemoveHazard(mark);
        Shake(0.08f, 0.08f);
        Dust(new Vector2(x, floorTop), 0, 8, Color.white);
        yield return new WaitForSeconds(0.08f);
        drop.armed = false;
        if (drop.sr != null) Dissolve(drop.sr.transform.position, rainSize, eraserColor, 8);
        RemoveHazard(drop);
        done();
    }

    // 레이저: 신의 눈에서 조준선이 메크로를 따라오다 멈춤 → 굵은 레이저
    IEnumerator Laser(float k)
    {
        int shots = phase >= 2 ? 2 : 1;
        Vector2 from = BossPosition;
        Vector2 dir = ((Vector2)pcol.bounds.center - from).normalized;
        for (int s = 0; s < shots; s++)
        {
            var aim = AddRect(from, Vector2.one, new Color(warnColor.r, warnColor.g, warnColor.b, 0.6f), 6);
            float tAim = laserAimTime * k;
            for (float t = 0f; t < tAim; t += Time.deltaTime)
            {
                from = BossPosition;
                Vector2 want = ((Vector2)pcol.bounds.center - from).normalized;
                dir = Vector3.RotateTowards(dir, want, laserTurnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);
                PlaceBeam(aim, from, dir, 0.14f + 0.06f * Mathf.Sin(t * 30f));
                yield return null;
            }
            // 고정 → 깜빡이며 굵어짐
            float tLock = laserLockTime * k;
            for (float t = 0f; t < tLock; t += Time.deltaTime)
            {
                float a = t / tLock;
                PlaceBeam(aim, from, dir, Mathf.Lerp(0.1f, laserWidth * 0.6f, a));
                var c = warnColor; c.a = Mathf.Lerp(0.6f, 0.85f, a) * (0.6f + 0.4f * Mathf.Sin(t * 50f)); aim.sr.color = c;
                yield return null;
            }
            RemoveHazard(aim);

            var beam = AddRect(from, Vector2.one, hazardColor, 20);
            float len = PlaceBeam(beam, from, dir, laserWidth);
            beam.beamFrom = from; beam.beamTo = from + dir * len; beam.beamHalf = laserWidth * 0.5f * 0.8f;
            beam.armed = true;
            Shake(0.3f, 0.35f);
            HitStop(0.04f);
            Glitch(0.25f, 0.8f);
            Dust(beam.beamTo, 0, 16, hazardColor);
            for (float t = 0f; t < laserActiveTime; t += Time.deltaTime)
            {
                PlaceBeam(beam, from, dir, laserWidth * (1f + 0.15f * Mathf.Sin(t * 60f)));
                if (Random.value < 0.5f) Dust(beam.beamTo, 0, 1, hazardColor);
                yield return null;
            }
            beam.armed = false;
            yield return FadeOut(new List<Hazard> { beam }, 0.2f);
            if (s < shots - 1) yield return new WaitForSeconds(0.3f);
        }
    }

    /// <summary>from 에서 dir 쪽으로 바닥(또는 벽 너머)까지 가는 막대로 놓는다. 길이를 돌려줌.</summary>
    float PlaceBeam(Hazard h, Vector2 from, Vector2 dir, float width)
    {
        float len = 60f;
        if (dir.y < -0.01f) len = Mathf.Min(len, (from.y - floorTop) / -dir.y);
        Vector2 mid = from + dir * len * 0.5f;
        var t = h.sr.transform;
        t.position = new Vector3(mid.x, mid.y, 0f);
        t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        t.localScale = new Vector3(len, width, 1f);
        return len;
    }

    // 복붙: 메크로를 복사한 검은 그림자가 메크로가 지나간 길을 늦게 따라옴 → 멈춰 있으면 잡힘
    IEnumerator Clone(float k)
    {
        int n = phase >= 2 ? 2 : 1;
        // 예고: 메크로가 하얗게 번쩍 (복사됨) + 신이 지지직
        Glitch(0.2f, 0.6f);
        for (int i = 0; i < 3; i++)
        {
            if (psr != null) AddParticle(psr.bounds.center, psr.bounds.size, new Color(1f, 1f, 1f, 0.6f), Vector2.zero, 0.15f, 0f, 0f, psr.sortingOrder + 1);
            yield return new WaitForSeconds(0.24f * k);
        }
        var clones = new List<Hazard>();
        for (int i = 0; i < n; i++)
        {
            var c = AddSprite(psr != null ? psr.sprite : Square(), PlayerVisualPos, 21, new Color(1f, 1f, 1f, 0f));
            c.sr.transform.localScale = psr != null ? psr.transform.lossyScale : player.transform.lossyScale;
            c.hit = 0.6f;
            if (afterimageMaterial != null) c.sr.sharedMaterial = afterimageMaterial;
            clones.Add(c);
        }
        float dur = cloneTime;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            for (int i = 0; i < clones.Count; i++)
            {
                var c = clones[i];
                if (c.sr == null) continue;
                float delay = cloneDelay * (i + 1);
                Vector3 pos; Sprite sp; bool flip;
                SamplePlayer(Time.time - delay, out pos, out sp, out flip);
                c.sr.transform.position = pos;
                if (sp != null) c.sr.sprite = sp;
                c.sr.flipX = flip;
                float a = Mathf.Clamp01(t / 0.5f) * Mathf.Clamp01((dur - t) / 0.4f);
                c.sr.color = new Color(1f, 1f, 1f, 0.52f * a);
                c.armed = t > 0.5f && t < dur - 0.3f;
                if (Random.value < 0.25f) AddParticle(pos + (Vector3)Random.insideUnitCircle * 0.4f, Vector2.one * 0.12f, new Color(1f, 0.15f, 0.25f, 0.8f), Vector2.up * 1.5f, 0.3f, 0f, 0f, 22);
            }
            yield return null;
        }
        foreach (var c in clones)
        {
            if (c.sr != null) Dissolve(c.sr.transform.position, c.sr.bounds.size, new Color(1f, 1f, 1f, 0.52f), 14);
            RemoveHazard(c);
        }
    }

    Vector3 PlayerVisualPos => psr != null ? psr.transform.position : player.transform.position;

    /// <summary>메크로 위치를 기록 (복붙 그림자가 따라오는 길).</summary>
    void RecordPlayer()
    {
        if (player == null) return;
        playerTrail.Add(new TrailPoint { t = Time.time, pos = PlayerVisualPos, sprite = psr != null ? psr.sprite : null, flip = psr != null && psr.flipX });
        while (playerTrail.Count > 2 && playerTrail[1].t < Time.time - 4f) playerTrail.RemoveAt(0);
    }

    void SamplePlayer(float time, out Vector3 pos, out Sprite sprite, out bool flip)
    {
        pos = PlayerVisualPos; sprite = psr != null ? psr.sprite : null; flip = psr != null && psr.flipX;
        if (playerTrail.Count == 0) return;
        if (time <= playerTrail[0].t) { pos = playerTrail[0].pos; sprite = playerTrail[0].sprite; flip = playerTrail[0].flip; return; }
        for (int i = playerTrail.Count - 1; i >= 0; i--)
        {
            if (playerTrail[i].t > time) continue;
            var a = playerTrail[i];
            if (i + 1 < playerTrail.Count)
            {
                var b = playerTrail[i + 1];
                pos = Vector3.Lerp(a.pos, b.pos, Mathf.InverseLerp(a.t, b.t, time));
            }
            else pos = a.pos;
            sprite = a.sprite; flip = a.flip;
            return;
        }
    }

    /// <summary>한 바퀴 순서를 섞는다 (탄은 Loop 가 따로 넣음): 같은 패턴 연속 금지 (바퀴 사이도). 새 패턴은 두 번째 페이즈부터.</summary>
    List<Pattern> ShuffleRound(Pattern? last)
    {
        var pool = new List<Pattern> { Pattern.Cursor, Pattern.Slam, Pattern.Sweep, Pattern.Columns };
        if (phase >= 1) { pool.Add(Pattern.Rain); pool.Add(Pattern.Laser); pool.Add(Pattern.Clone); }
        for (int tries = 0; tries < 50; tries++)
        {
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
            }
            bool ok = !(last.HasValue && pool[0] == last.Value);
            for (int i = 1; ok && i < pool.Count; i++) if (pool[i] == pool[i - 1]) ok = false;
            if (ok) break;
        }
        return pool;
    }

    // 틈 → §
    IEnumerator Stun()
    {
        stunned = true; stunHit = false; cracks = 0;
        SetBossBar(); // 틈 동안 실드는 0
        boss.color = stunColor;
        yield return MoveBoss(BossRest + Vector2.down * 3.5f, 0.5f);
        player.bossSkillReady = true;
        bool teach = tutorial && !skillTaught; // 첫 틈: 슬로우 모션 + § 를 그릴 때까지 기다림
        if (teach) { teaching = true; focus = Focus.Section; player.ResetMotion(); player.combatOnly = true; SetSlow(true); }
        ShowDemo(Demo.Section); // § 시범은 틈마다 보여줌
        for (float t = 0f; (teach || t < stunTime) && !stunHit; t += Time.deltaTime) yield return null;
        player.bossSkillReady = false;
        HideDemo();
        if (teach) { teaching = false; player.combatOnly = false; SetSlow(false); if (stunHit) skillTaught = true; }
        stunned = false;

        if (stunHit)
        {
            phase++;
            HitStop(0.2f);
            Glitch(0.5f, 1f);
            Burst(BossPosition, Color.white, 20, 12f, 0.3f, 6f);
            Burst(BossPosition, stunColor, 24, 9f, 0.35f, 6f);
            Shake(0.6f, 0.6f);
            SetBossBar();
            yield return BossFlash(Color.white, 0.6f, true);
            if (phase >= bossPhases) { yield return Defeat(); yield break; }
        }
        else cracks = reflectsToStun - 1; // 놓쳐도 한 번만 더 받아치면 다시 틈
        SetBossBar();
        boss.color = bossColor;
        yield return MoveBoss(BossRest, 0.6f);
    }

    /// <summary>ㄱ 히트박스는 옆으로만 길어서 위에서 대각선으로 오는 탄이 잘 안 맞음 → 휘두른 쪽 반원 안의 탄은 같이 쳐냄.</summary>
    void OnAttackSwung(int dir)
    {
        if (!started || dying || defeated || pcol == null) return;
        Vector2 c = pcol.bounds.center;
        foreach (var o in orbs.ToArray())
        {
            if (o == null || o.reflected || !o.enabled) continue;
            Vector2 d = (Vector2)o.transform.position - c;
            if (d.x * dir > -0.5f && d.magnitude < orbAssistRange) o.TakeHit(1, new Vector2(dir, 0f));
        }
    }

    void OnBossSkill()
    {
        if (stunned && !stunHit) stunHit = true;
    }

    IEnumerator Defeat()
    {
        defeated = true;
        ClearHazards();
        for (float t = 0f; t < 1.5f; t += Time.deltaTime)
        {
            boss.transform.localPosition = Random.insideUnitCircle * 0.6f;
            boss.color = Random.value < 0.5f ? stunColor : reflectedColor;
            Shake(0.1f, 0.3f);
            Glitch(0.1f, 1f);
            if (Random.value < 0.3f) Burst(BossPosition + Random.insideUnitCircle * BossRadius, Random.value < 0.5f ? Color.white : stunColor, 6, 8f, 0.3f, 4f);
            yield return null;
        }
        boss.transform.localPosition = Vector3.zero;
        boss.color = bossColor;
        HitStop(0.25f);
        Dissolve(BossPosition, Vector2.one * bossSize * 0.8f, Color.white, 60);
        yield return Fade(boss, 1f, 0f, 1f);
        bossAnchor.gameObject.SetActive(false);
        Debug.Log("[보스] 쓰러짐");
        onDefeated.Invoke();
    }

    /// <summary>엔딩으로 넘어갈 때: 카메라 고정·하트·지지직을 풀고 보스전을 끈다.</summary>
    public void EndFight()
    {
        camLocked = false;
        slowOn = false; hitStopLeft = 0f; hitStopWas = false;
        Time.timeScale = 1f; Time.fixedDeltaTime = baseFixedDt;
        if (cam != null) cam.orthographicSize = baseOrtho;
        if (healthHud != null) healthHud.gameObject.SetActive(false);
        if (bossBar != null) bossBar.SetActive(false);
        foreach (var g in glitchBars) if (g != null) g.enabled = false;
        foreach (var g in bossGhosts) if (g != null) g.enabled = false;
        foreach (var p in particles) if (p.sr != null) Destroy(p.sr.gameObject);
        particles.Clear();
        if (floorLine != null) floorLine.enabled = false;
        enabled = false;
    }

    // ───────────── 피격 / 죽음 ─────────────

    void Update()
    {
        UpdateSlowFx();
        UpdateParticles();
        RecordPlayer();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 테스트용: 보스 바로 처치 (에디터·개발 빌드에서만)
        if (Input.GetKeyDown(debugDefeatKey) && started && !defeated && !dying)
        {
            StopAllCoroutines();
            StartCoroutine(Defeat());
            return;
        }
#endif
        if (!started || player == null || dying || defeated || teaching || Time.time < invulnUntil) return;
        Bounds pb = PlayerHitBounds();
        bool hit = false;
        foreach (var h in hazards)
            if (h.armed && h.sr != null && HazardHits(h, pb)) { hit = true; break; }
        if (hit) Hurt(); // 목록을 도는 중에 지우지 않게 밖에서
    }

    /// <summary>판정은 플레이어 콜라이더보다 조금 작게 (스친 정도는 봐줌).</summary>
    Bounds PlayerHitBounds()
    {
        Bounds b = pcol.bounds;
        b.size = new Vector3(b.size.x * 0.7f, b.size.y * 0.85f, 10f);
        return b;
    }

    static bool HazardHits(Hazard h, Bounds pb)
    {
        if (h.beamHalf > 0f)
        {
            // 레이저: 선분을 따라 원을 찍어 검사
            float len = Vector2.Distance(h.beamFrom, h.beamTo);
            int n = Mathf.Max(2, Mathf.CeilToInt(len / 0.3f));
            for (int i = 0; i <= n; i++)
                if (CircleHitsBox(Vector2.Lerp(h.beamFrom, h.beamTo, i / (float)n), h.beamHalf, pb)) return true;
            return false;
        }
        Bounds b = h.sr.bounds;
        if (h.hit != 1f) b.size = new Vector3(b.size.x * h.hit, b.size.y * h.hit, b.size.z);
        return b.Intersects(pb);
    }

    static bool CircleHitsBox(Vector2 c, float r, Bounds b)
    {
        float dx = Mathf.Max(b.min.x - c.x, 0f, c.x - b.max.x);
        float dy = Mathf.Max(b.min.y - c.y, 0f, c.y - b.max.y);
        return dx * dx + dy * dy <= r * r;
    }

    void Hurt()
    {
        if (dying || defeated || Time.time < invulnUntil) return;
        SetHearts(heartsLeft - 1);
        ScreenFeedback.Ensure().FailFeedback();
        Shake(0.25f, 0.3f);
        HitStop(0.1f);
        Glitch(0.2f, 0.7f);
        Burst(pcol.bounds.center, new Color(1f, 0.25f, 0.3f), 12, 6f, 0.18f);
        if (heartsLeft <= 0)
        {
            if (fr != null) fr.Die(); // → OnDied 에서 멈추고, OnRespawned 에서 처음부터
            else OnRespawned(player.transform.position);
            return;
        }
        invulnUntil = Time.time + invulnTime;
        StartCoroutine(Blink(invulnTime));
    }

    IEnumerator Blink(float time)
    {
        if (psr == null) yield break;
        for (float t = 0f; t < time; t += 0.08f)
        {
            psr.color = new Color(psrColor.r, psrColor.g, psrColor.b, (int)(t / 0.08f) % 2 == 0 ? 0.3f : 1f);
            yield return new WaitForSeconds(0.08f);
        }
        psr.color = psrColor;
    }

    void OnDied()
    {
        if (defeated) return;
        dying = true;
        StopAllCoroutines();
        ClearHazards();
        if (psr != null) psr.color = psrColor;
    }

    void OnRespawned(Vector3 pos)
    {
        if (defeated) return;
        StopAllCoroutines();
        ClearHazards();
        phase = 0; cracks = 0;
        SetHearts(hearts);
        SetBossBar();
        dying = false;
        invulnUntil = Time.time + 1.5f;
        StartCoroutine(Loop());
    }

    void ClearHazards()
    {
        foreach (var h in hazards) if (h.sr != null) Destroy(h.sr.gameObject);
        hazards.Clear();
        foreach (var o in orbs) if (o != null) Destroy(o.gameObject);
        orbs.Clear();
        stunned = false; stunHit = false;
        if (player != null) player.bossSkillReady = false;
        HideDemo();
        if (teaching) { teaching = false; SetSlow(false); }
        tutorialOrb = null;
        if (!reflectTaught) firstOrb = null;
        if (player != null) player.combatOnly = false;
        if (boss != null) { boss.color = bossColor; boss.transform.localPosition = Vector3.zero; boss.transform.localScale = Vector3.one * bossScale; }
        if (bossAnchor != null) bossAnchor.position = BossRest;
    }

    // ───────────── 카메라 / 하트 / 보스 흔들림 ─────────────

    void LateUpdate()
    {
        KeepGodBubbleClear();
        UpdateDemo();
        if (!camLocked || cam == null) return;
        Vector3 off = Vector3.zero;
        if (shakeLeft > 0f)
        {
            shakeLeft -= Time.deltaTime;
            off = (Vector3)(Random.insideUnitCircle * shakeMag * Mathf.Clamp01(shakeLeft / Mathf.Max(0.01f, shakeDur)));
            if (shakeLeft <= 0f) shakeMag = 0f;
        }
        cam.transform.position = camPos + off;
        UpdateGlitch();


        // 보스 지지직
        if (boss != null && !defeated)
        {
            if (stunned) boss.transform.localPosition = Random.insideUnitCircle * 0.15f;
            else
            {
                if (glitchHold > 0f && (glitchHold -= Time.deltaTime) <= 0f) boss.transform.localPosition = Vector3.zero;
                glitchTimer -= Time.deltaTime;
                if (glitchTimer <= 0f)
                {
                    glitchTimer = Random.Range(0.3f, 1.2f);
                    glitchHold = 0.07f;
                    boss.transform.localPosition = new Vector3(Random.Range(-0.4f, 0.4f), 0f, 0f);
                }
            }
        }
    }

    // ───────────── 튜토리얼: 슬로우 모션 / 그릴 모양 시범 ─────────────

    /// <summary>슬로우 모션 켜기/끄기. 켤 때는 tutorialSlowIn 동안 점점 느려지고(UpdateSlowFx), 끌 때는 바로 원래 속도.</summary>
    void SetSlow(bool on)
    {
        slowOn = on;
        if (!on)
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = baseFixedDt;
        }
    }

    /// <summary>슬로우 모션 연출 (실제 시간 기준): 점점 느려짐 + 카메라 확대.</summary>
    void UpdateSlowFx()
    {
        float prev = slowFx;
        slowFx = Mathf.MoveTowards(slowFx, slowOn ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, slowOn ? tutorialSlowIn : 0.2f));
        ApplyTimeScale();
        if (cam != null && arenaCamSet && player != null && (slowFx > 0f || prev > 0f))
        {
            float k = Mathf.SmoothStep(0f, 1f, slowFx);
            Vector3 fp; float fo;
            FocusView(out fp, out fo);
            camPos = Vector3.Lerp(arenaCamPos, fp, k);
            cam.orthographicSize = Mathf.Lerp(arenaOrtho, fo, k);
        }
    }

    void FocusView(out Vector3 pos, out float ortho)
    {
        float z = arenaCamPos.z;
        if (focus == Focus.Reflect)
        {
            ortho = arenaOrtho * reflectZoom;
            Vector2 c = (Vector2)player.transform.position + new Vector2(holdSide * 0.7f, 1.8f);
            pos = new Vector3(c.x, c.y, z);
        }
        else
        {
            ortho = arenaOrtho * sectionZoom;
            Vector2 b = BossPosition;
            pos = new Vector3(b.x + 1.5f, (b.y + floorTop) * 0.5f + 1f, z);
        }
    }



    void ShowDemo(Demo d)
    {
        if (demo == null)
        {
            var go = new GameObject("GestureDemo");
            go.transform.SetParent(root, false);
            demo = go.AddComponent<LineRenderer>();
            demo.useWorldSpace = true;
            demo.material = new Material(Shader.Find("Sprites/Default"));
            demo.widthMultiplier = 0.25f;
            demo.numCapVertices = 4;
            demo.numCornerVertices = 4;
            demo.sortingOrder = 300;
            demoTip = MakeSprite("Tip", go.transform, OrbSprite(), 301, Color.white);
            demoTip.transform.localScale = Vector3.one * 0.6f;
        }
        demo.startColor = demo.endColor = demoColor;
        demoPath = d == Demo.Section ? SectionPath() : AttackPath(d == Demo.AttackLeft);
        demoKind = d;
        demoT = 0f;
        demo.gameObject.SetActive(true);
        UpdateDemo();
    }

    void HideDemo()
    {
        demoKind = Demo.None;
        if (demo != null) demo.gameObject.SetActive(false);
    }

    /// <summary>시범 선을 펜으로 그리듯 그렸다가 잠깐 멈추고 다시 그린다 (실제 시간 기준이라 슬로우 모션에도 같은 속도).
    /// ㄱ 은 메크로 머리 위, § 는 보스 오른쪽 옆 (신 말풍선은 왼쪽).</summary>
    void UpdateDemo()
    {
        if (demoKind == Demo.None || demo == null || demoPath == null || player == null) return;
        bool section = demoKind == Demo.Section;
        Vector2 c = section ? BossPosition + Vector2.right * (BossRadius + 2.2f) : (Vector2)player.transform.position + Vector2.up * 3.8f;
        float s = section ? 1.5f : 2f;
        float drawTime = section ? 1.6f : 0.7f;
        demoT += Time.unscaledDeltaTime;
        float prog = Mathf.Clamp01((demoT % (drawTime + 0.7f)) / drawTime);
        int n = Mathf.Clamp(Mathf.CeilToInt(prog * (demoPath.Length - 1)) + 1, 2, demoPath.Length);
        demo.positionCount = n;
        for (int i = 0; i < n; i++) demo.SetPosition(i, c + demoPath[i] * s);
        demoTip.transform.position = c + demoPath[n - 1] * s;
    }

    /// <summary>ㄱ: 가로로 긋고 아래로 (왼쪽 공격이면 좌우 반전). 높이 약 1.2.</summary>
    static Vector2[] AttackPath(bool left)
    {
        var corners = new[] { new Vector2(-0.6f, 0.5f), new Vector2(0.6f, 0.5f), new Vector2(0.45f, -0.7f) };
        var pts = new List<Vector2>();
        for (int i = 0; i < corners.Length - 1; i++)
            for (int k = 0; k < 12; k++) pts.Add(Vector2.Lerp(corners[i], corners[i + 1], k / 12f));
        pts.Add(corners[corners.Length - 1]);
        if (left) for (int i = 0; i < pts.Count; i++) pts[i] = new Vector2(-pts[i].x, pts[i].y);
        return pts.ToArray();
    }

    /// <summary>§: 오른쪽 위에서 시작 → 위 S → 가운데 고리 → 오른쪽으로 내려와 왼쪽 아래에서 끝. 약 0.7×2 (중심 0,0).</summary>
    static Vector2[] SectionPath()
    {
        var key = new[]
        {
            new Vector2(0.31f, 0.77f), new Vector2(0.33f, 0.88f), new Vector2(0.26f, 0.97f), new Vector2(0.04f, 1.00f),
            new Vector2(-0.16f, 0.95f), new Vector2(-0.27f, 0.84f), new Vector2(-0.29f, 0.69f), new Vector2(-0.25f, 0.57f),
            new Vector2(-0.12f, 0.49f), new Vector2(0.08f, 0.41f), new Vector2(0.23f, 0.30f), new Vector2(0.33f, 0.18f),
            new Vector2(0.35f, 0.01f), new Vector2(0.33f, -0.19f), new Vector2(0.27f, -0.30f), new Vector2(0.16f, -0.40f),
            new Vector2(0.00f, -0.47f), new Vector2(-0.18f, -0.46f), new Vector2(-0.27f, -0.34f), new Vector2(-0.31f, -0.16f),
            new Vector2(-0.29f, -0.02f), new Vector2(-0.18f, 0.07f), new Vector2(0.00f, 0.10f), new Vector2(0.18f, 0.09f),
            new Vector2(0.27f, 0.01f), new Vector2(0.32f, -0.13f), new Vector2(0.31f, -0.38f), new Vector2(0.30f, -0.60f),
            new Vector2(0.26f, -0.73f), new Vector2(0.16f, -0.86f), new Vector2(0.02f, -0.97f), new Vector2(-0.16f, -1.00f),
            new Vector2(-0.27f, -0.94f), new Vector2(-0.35f, -0.80f),
        };
        var off = Vector2.zero;
        var pts = new List<Vector2>();
        for (int i = 0; i < key.Length - 1; i++)
        {
            Vector2 p0 = key[Mathf.Max(0, i - 1)], p1 = key[i], p2 = key[i + 1], p3 = key[Mathf.Min(key.Length - 1, i + 2)];
            for (int k = 0; k < 8; k++)
            {
                float u = k / 8f, u2 = u * u, u3 = u2 * u;
                pts.Add(0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (-p0 + 3f * p1 - 3f * p2 + p3) * u3) - off);
            }
        }
        pts.Add(key[key.Length - 1] - off);
        return pts.ToArray();
    }

    // ───────────── 이펙트: 타격 멈춤 / 파편 / 글리치 ─────────────

    /// <summary>아주 잠깐 시간을 거의 멈춘다 (실제 시간 기준). 겹치면 긴 쪽.</summary>
    void HitStop(float time)
    {
        if (!hitStopOn) return;
        hitStopLeft = Mathf.Max(hitStopLeft, time);
    }

    /// <summary>시간 배율 = 슬로우 모션 × 타격 멈춤. 일시정지 중엔 건드리지 않음.</summary>
    void ApplyTimeScale()
    {
        if (PauseMenu.IsPaused) return;
        bool stop = hitStopLeft > 0f;
        if (stop) hitStopLeft -= Time.unscaledDeltaTime;
        if (slowOn || stop || hitStopWas)
        {
            float ts = slowOn ? Mathf.Lerp(1f, tutorialTimeScale, slowFx) : 1f;
            if (stop) ts = Mathf.Min(ts, 0.03f);
            Time.timeScale = ts;
            Time.fixedDeltaTime = baseFixedDt * ts; // 느려도 물리가 끊겨 보이지 않게
        }
        hitStopWas = stop;
    }

    /// <summary>pos 에서 사방으로 튀는 네모 파편.</summary>
    void Burst(Vector2 pos, Color color, int count, float speed, float size, float gravity = 14f)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 v = Random.insideUnitCircle.normalized * speed * Random.Range(0.4f, 1f);
            float s = size * Random.Range(0.5f, 1.2f);
            AddParticle(pos, new Vector2(s, s), color, v, Random.Range(0.35f, 0.7f), gravity, Random.Range(-720f, 720f), 22);
        }
    }

    /// <summary>바닥에서 한쪽(dir)으로 흩날리는 가루.</summary>
    void Dust(Vector2 pos, int dir, int count, Color color)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 v = new Vector2((dir == 0 ? Random.Range(-1f, 1f) : dir * Random.Range(0.3f, 1f)) * 5f, Random.Range(1f, 4.5f));
            float s = Random.Range(0.08f, 0.2f);
            AddParticle(pos + new Vector2(Random.Range(-0.3f, 0.3f), 0f), new Vector2(s, s), color, v, Random.Range(0.3f, 0.6f), 10f, Random.Range(-360f, 360f), 22);
        }
    }

    /// <summary>사각형 영역이 네모 조각으로 부서져 흩어짐 (지워지는 느낌).</summary>
    void Dissolve(Vector2 center, Vector2 size, Color color, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 p = center + new Vector2(Random.Range(-0.5f, 0.5f) * size.x, Random.Range(-0.5f, 0.5f) * size.y);
            Vector2 v = new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(0.5f, 3f));
            float s = Random.Range(0.15f, 0.35f);
            AddParticle(p, new Vector2(s, s), color, v, Random.Range(0.3f, 0.6f), -2f, 0f, 21);
        }
    }

    /// <summary>예고가 끝나는 순간 영역 전체가 하얗게 번쩍.</summary>
    void FlashRect(SpriteRenderer src)
    {
        if (src == null) return;
        AddParticle(src.transform.position, src.transform.localScale, new Color(1f, 1f, 1f, 0.75f), Vector2.zero, 0.15f, 0f, 0f, 21);
    }

    /// <summary>움직이는 막대가 지나간 자리에 남는 잔상.</summary>
    void AfterImage(SpriteRenderer src)
    {
        if (src == null) return;
        var c = new Color(1f, 1f, 1f, 0.36f * src.color.a);
        AddParticle(src.transform.position, src.transform.localScale, c, Vector2.zero, 0.25f, 0f, 0f, src.sortingOrder - 1);
    }

    void AddParticle(Vector2 pos, Vector2 scale, Color color, Vector2 vel, float life, float gravity, float spin, int order)
    {
        if (root == null) return;
        var sr = MakeSprite("Fx", root, Square(), order, color);
        sr.transform.position = pos;
        sr.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        particles.Add(new Particle { sr = sr, v = vel, life = life, max = life, gravity = gravity, spin = spin, a = color.a });
    }

    /// <summary>파편 움직이기 (게임 시간 기준이라 타격 멈춤 때 같이 멈춤).</summary>
    void UpdateParticles()
    {
        float dt = Time.deltaTime;
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            var p = particles[i];
            p.life -= dt;
            if (p.sr == null || p.life <= 0f)
            {
                if (p.sr != null) Destroy(p.sr.gameObject);
                particles.RemoveAt(i);
                continue;
            }
            p.v.y -= p.gravity * dt;
            var t = p.sr.transform;
            t.position += (Vector3)(p.v * dt);
            if (p.spin != 0f) t.Rotate(0f, 0f, p.spin * dt);
            var c = p.sr.color; c.a = p.a * (p.life / p.max); p.sr.color = c;
        }
    }

    /// <summary>화면이 지지직: 가로 줄무늬가 어긋나고 신이 빨강·파랑으로 갈라진다. 겹치면 센 쪽.</summary>
    void Glitch(float time, float power)
    {
        if (!glitchOn) return;
        glitchLeft = Mathf.Max(glitchLeft, time);
        glitchPower = glitchLeft > 0f && glitchPower > power ? glitchPower : power;
    }

    void UpdateGlitch()
    {
        if (cam == null) return;
        bool on = glitchLeft > 0f && !PauseMenu.IsPaused;
        if (on) glitchLeft -= Time.unscaledDeltaTime;
        else glitchPower = 0f;

        if (glitchBars.Count == 0 && on)
            for (int i = 0; i < 7; i++)
            {
                var sr = MakeSprite("GlitchBar", cam.transform, Square(), 90, Color.clear);
                glitchBars.Add(sr);
            }
        if (bossGhosts.Count == 0 && on && boss != null)
            for (int i = 0; i < 2; i++)
            {
                var sr = MakeSprite("BossGhost", bossAnchor, boss.sprite, boss.sortingOrder - 1, Color.clear);
                bossGhosts.Add(sr);
            }

        // 줄무늬는 0.04초마다 새로 뿌림
        glitchTick -= Time.unscaledDeltaTime;
        bool reroll = glitchTick <= 0f;
        if (reroll) glitchTick = 0.04f;
        float h = cam.orthographicSize, w = h * cam.aspect;
        for (int i = 0; i < glitchBars.Count; i++)
        {
            var sr = glitchBars[i];
            if (!on || Random.value > glitchPower) { if (reroll || !on) sr.enabled = false; continue; }
            if (!reroll) continue;
            sr.enabled = true;
            float bh = Random.Range(0.05f, 0.6f) * h * 0.25f;
            sr.transform.localPosition = new Vector3(Random.Range(-0.6f, 0.6f) * glitchPower, Random.Range(-h, h), 10f);
            sr.transform.localScale = new Vector3(2f * w * Random.Range(0.4f, 1.2f), bh, 1f);
            Color c = i % 3 == 0 ? new Color(1f, 0.15f, 0.25f) : i % 3 == 1 ? new Color(0.2f, 0.9f, 1f) : Color.white;
            c.a = Random.Range(0.12f, 0.35f) * glitchPower;
            sr.color = c;
        }
        for (int i = 0; i < bossGhosts.Count; i++)
        {
            var g = bossGhosts[i];
            if (g == null) continue;
            if (!on || boss == null || defeated && !bossAnchor.gameObject.activeSelf) { g.enabled = false; continue; }
            g.enabled = true;
            g.sprite = boss.sprite;
            g.transform.localScale = boss.transform.localScale;
            if (reroll)
            {
                float off = (i == 0 ? -1f : 1f) * Random.Range(0.15f, 0.6f) * glitchPower;
                g.transform.localPosition = boss.transform.localPosition + new Vector3(off, Random.Range(-0.1f, 0.1f), 0f);
            }
            g.color = i == 0 ? new Color(1f, 0.15f, 0.25f, 0.55f * boss.color.a) : new Color(0.2f, 0.9f, 1f, 0.55f * boss.color.a);
        }
    }

    void Shake(float duration, float magnitude)
    {
        shakeDur = duration;
        shakeLeft = Mathf.Max(shakeLeft, duration);
        shakeMag = Mathf.Max(shakeMag, magnitude);
    }

    void BuildHearts()
    {
        if (healthHud != null) return;
        var menu = FindAnyObjectByType<PauseMenu>();
        var settings = menu != null && menu.settingsButton != null ? menu.settingsButton.transform as RectTransform : null;
        Transform parent = settings != null ? settings.parent : null;
        if (parent == null)
        {
            var canvas = new GameObject("BossHealthCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            parent = canvas.transform;
        }
        var go = new GameObject("BossHealthHud", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        // Render behind the settings menu overlay, without intercepting clicks.
        go.transform.SetAsFirstSibling();
        healthHud = go.AddComponent<BossHealthHud>();
        healthHud.Configure(healthFullFrames, healthEmptyFrames, hearts, settings);
        go.SetActive(false);
    }

    void SetHearts(int n)
    {
        heartsLeft = Mathf.Clamp(n, 0, hearts);
        if (healthHud == null) return;
        healthHud.gameObject.SetActive(true);
        healthHud.SetHealth(heartsLeft);
    }

    /// <summary>화면 위 가운데 보스 체력바 (칸 하나 = § 한 번) + 그 아래 하얀 실드 줄 (칸 하나 = 되받아친 탄 한 번).</summary>
    void BuildBossBar()
    {
        if (bossBar != null) return;
        bossBar = new GameObject("BossBarCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        var canvas = bossBar.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = bossBar.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        BuildBarRow("Health", bossBarTop, bossBarSize, bossPhases, bossBarColor, bossBarCells);
        BuildBarRow("Shield", bossBarTop + bossBarSize.y + 18f, new Vector2(bossBarSize.x, shieldBarHeight), reflectsToStun, shieldColor, shieldCells);
        bossBar.SetActive(false);
    }

    void BuildBarRow(string name, float top, Vector2 size, int count, Color color, List<UnityEngine.UI.Image> cells)
    {
        var frame = BarImage(name, bossBar.transform, Color.white, size + new Vector2(8f, 8f));
        frame.rectTransform.anchoredPosition = new Vector2(0f, -top);
        var back = BarImage("Back", frame.transform, Color.black, size);
        back.rectTransform.anchorMin = back.rectTransform.anchorMax = back.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        back.rectTransform.anchoredPosition = Vector2.zero;

        int n = Mathf.Max(1, count);
        float gap = Mathf.Min(6f, size.y * 0.25f), w = (size.x - gap * (n + 1)) / n, h = size.y - gap * 2f;
        cells.Clear();
        for (int i = 0; i < n; i++)
        {
            var cell = BarImage("Cell" + i, back.transform, color, new Vector2(w, h));
            var rt = cell.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(gap + i * (w + gap), 0f);
            cells.Add(cell);
        }
    }

    static UnityEngine.UI.Image BarImage(string name, Transform parent, Color color, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = size;
        var img = go.GetComponent<UnityEngine.UI.Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>체력 = bossPhases - phase, 실드 = reflectsToStun - cracks (틈 동안은 0). 깎인 칸은 어둡게.</summary>
    void SetBossBar()
    {
        if (bossBar == null) return;
        bossBar.SetActive(true);
        var empty = new Color(0.25f, 0.25f, 0.25f);
        int left = bossPhases - phase;
        for (int i = 0; i < bossBarCells.Count; i++) bossBarCells[i].color = i < left ? bossBarColor : empty;
        int shield = stunned ? 0 : reflectsToStun - cracks;
        for (int i = 0; i < shieldCells.Count; i++) shieldCells[i].color = i < shield ? shieldColor : empty;
    }

    // ───────────── 도우미 ─────────────

    IEnumerator MoveBoss(Vector2 to, float time)
    {
        Vector2 from = bossAnchor.position;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            bossAnchor.position = Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / time));
            yield return null;
        }
        bossAnchor.position = to;
    }

    IEnumerator BossFlash(Color c, float time, bool punch = false)
    {
        Color back = boss.color;
        boss.color = c;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            if (punch) boss.transform.localScale = Vector3.one * bossScale * (1f + 0.3f * (1f - t / time));
            yield return null;
        }
        boss.transform.localScale = Vector3.one * bossScale;
        boss.color = stunned ? stunColor : (punch ? bossColor : back);
    }

    IEnumerator Telegraph(List<Hazard> list, float time)
    {
        var baseA = new List<float>();
        foreach (var h in list) baseA.Add(h.sr != null ? h.sr.color.a : 0f);
        Glitch(0.12f, 0.4f);
        float phaseAcc = 0f;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float a = t / Mathf.Max(0.01f, time);
            phaseAcc += Time.deltaTime * Mathf.Lerp(8f, 55f, a * a); // 점점 빨라지는 깜빡임
            float m = Mathf.Lerp(0.55f, 0.8f, a) + Mathf.Lerp(0.45f, 0.2f, a) * Mathf.Sin(phaseAcc);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].sr == null || list[i].armed) continue;
                var c = list[i].sr.color; c.a = baseA[i] * m; list[i].sr.color = c;
            }
            yield return null;
        }
        foreach (var h in list) if (h.sr != null && !h.armed) FlashRect(h.sr); // 떨어지기 직전 번쩍
    }

    IEnumerator FadeOut(List<Hazard> list, float time)
    {
        var baseA = new List<float>();
        foreach (var h in list) baseA.Add(h.sr != null ? h.sr.color.a : 0f);
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].sr != null) { var c = list[i].sr.color; c.a = baseA[i] * (1f - t / time); list[i].sr.color = c; }
            yield return null;
        }
        foreach (var h in list) RemoveHazard(h);
    }

    static IEnumerator Fade(SpriteRenderer sr, float from, float to, float time)
    {
        if (sr == null) yield break;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            var c = sr.color; c.a = Mathf.Lerp(from, to, t / time); sr.color = c;
            yield return null;
        }
        var e = sr.color; e.a = to; sr.color = e;
    }

    Hazard AddRect(Vector2 center, Vector2 size, Color color, int order)
    {
        var h = AddSprite(Square(), center, order, color);
        h.sr.transform.localScale = new Vector3(size.x, size.y, 1f);
        return h;
    }

    /// <summary>패턴 그림 하나 (armed 일 때만 피해). 죽거나 끝나면 ClearHazards 가 같이 지운다.</summary>
    Hazard AddSprite(Sprite sprite, Vector2 pos, int order, Color color)
    {
        var sr = MakeSprite("Hazard", hazardRoot, sprite, order, color);
        sr.transform.position = pos;
        var h = new Hazard { sr = sr };
        hazards.Add(h);
        return h;
    }

    void RemoveHazard(Hazard h)
    {
        hazards.Remove(h);
        if (h.sr != null) Destroy(h.sr.gameObject);
    }

    void RemoveOrb(BossOrb o)
    {
        if (o == firstOrb && !reflectTaught) { firstOrb = null; if (player != null) player.combatOnly = false; } // 다음 탄에서 다시 알려줌
        orbs.Remove(o);
        if (o != null) Destroy(o.gameObject);
    }

    static SpriteRenderer MakeSprite(string name, Transform parent, Sprite sprite, int order, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        sr.color = color;
        return sr;
    }

    // 임시 그림 (코드로 그림)
    static Sprite Square()
    {
        if (squareSprite != null) return squareSprite;
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var cols = new Color32[16];
        for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(cols); tex.Apply();
        return squareSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }

    /// <summary>임시 신: 하얀 고리 + 눈동자.</summary>
    static Sprite Eye()
    {
        if (eyeSprite != null) return eyeSprite;
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = (d >= 52f && d <= 62f) || d <= 20f ? 1f : (d >= 30f && d <= 34f ? 0.5f : 0f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return eyeSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n / 3.2f);
    }

    static Sprite OrbSprite()
    {
        if (orbSprite != null) return orbSprite;
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / (n * 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 4f)));
            }
        tex.Apply();
        return orbSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n / 0.9f); // 지름 0.9 = radius 0.45
    }

    static Sprite FromRows(string[] rows, Color fill, Color edge, Vector2 pivot, float ppu)
    {
        int w = 0;
        foreach (var r in rows) w = Mathf.Max(w, r.Length);
        int h = rows.Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                char ch = x < rows[y].Length ? rows[y][x] : ' ';
                tex.SetPixel(x, h - 1 - y, ch == 'X' ? edge : ch == '.' ? fill : new Color(0f, 0f, 0f, 0f));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, ppu);
    }

    static Sprite Heart()
    {
        if (heartSprite != null) return heartSprite;
        return heartSprite = FromRows(new[]
        {
            " XX   XX ",
            "XXXX XXXX",
            "XXXXXXXXX",
            "XXXXXXXXX",
            " XXXXXXX ",
            "  XXXXX  ",
            "   XXX   ",
            "    X    ",
        }, Color.white, Color.white, new Vector2(0.5f, 0.5f), 16f);
    }

    /// <summary>마우스 커서 (끝이 왼쪽 위 = 피벗). 높이 약 1.6 units.</summary>
    static Sprite CursorSprite()
    {
        if (cursorSprite != null) return cursorSprite;
        return cursorSprite = FromRows(new[]
        {
            "X",
            "XX",
            "X.X",
            "X..X",
            "X...X",
            "X....X",
            "X.....X",
            "X......X",
            "X.......X",
            "X........X",
            "X.....XXXXX",
            "X..X..X",
            "X.X X..X",
            "XX  X..X",
            "X    X..X",
            "     X..X",
            "      XX",
        }, Color.white, new Color(0.45f, 0.45f, 0.45f), new Vector2(0f, 1f), 17f / 1.6f);
    }
}
