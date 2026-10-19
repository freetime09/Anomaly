using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 챕터 4 임시 보스전 (신). 피하기 위주 + 손목 부담이 적게: 패턴마다 예고가 길고, 피할 때 그림 한 번이면 되며, 패턴 사이에 쉬는 시간이 있다.
///  등장: 바닥 선 → 카메라가 뒤로 빠지며 거대한 신이 나타남 → 대사(introDialogue) → 시작.
///  패턴 (차례로 돌아감, 2단계부터 가끔 두 패턴이 겹쳐 나옴)
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
    public float orbReflectRadius = 1.1f;

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

    [Tooltip("보스를 쓰러뜨렸을 때")]
    public UnityEvent onDefeated;

    class Hazard { public SpriteRenderer sr; public bool armed; }
    enum Pattern { Columns, Orbs, Sweep, Cursor, Slam }

    // 한 바퀴 순서 (탄은 되받아칠 기회라 두 번)
    static readonly Pattern[] order = { Pattern.Cursor, Pattern.Orbs, Pattern.Slam, Pattern.Sweep, Pattern.Orbs, Pattern.Columns };

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
    readonly List<SpriteRenderer> heartIcons = new List<SpriteRenderer>();
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
        var anim = player.GetComponentInChildren<Animator>();
        psr = anim != null ? anim.GetComponent<SpriteRenderer>() : null;
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
        if (fr != null)
        {
            fr.TrySetCheckpoint(int.MaxValue, player.transform.position); // 죽으면 무대에서 부활
            fr.Died += OnDied;
            fr.Respawned += OnRespawned;
        }
        player.BossSkillUsed += OnBossSkill;

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
        if (teaching) SetSlow(false);
        CutsceneLock.Unlock(this);
        if (fr != null) { fr.Died -= OnDied; fr.Respawned -= OnRespawned; }
        if (player != null) { player.BossSkillUsed -= OnBossSkill; player.bossSkillReady = false; player.combatOnly = false; }
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
        int idx = 0;
        yield return new WaitForSeconds(0.8f);
        while (!defeated)
        {
            float k = Mathf.Max(0.5f, 1f - phaseSpeedUp * phase);
            while (teaching) yield return null; // 튜토리얼 중엔 다음 패턴을 시작하지 않음
            Pattern p = order[idx++ % order.Length];
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
            default: return Slam(k);
        }
    }

    /// <summary>같이 나와도 되는 짝. 탄은 되받아치기에 집중하게 단독 (보스가 움직이는 패턴끼리도 겹치지 않음).</summary>
    static bool TryPartner(Pattern p, out Pattern partner)
    {
        switch (p)
        {
            case Pattern.Columns: partner = Pattern.Sweep; return true;
            case Pattern.Sweep: partner = Pattern.Cursor; return true;
            case Pattern.Slam: partner = Pattern.Columns; return true;
            case Pattern.Cursor: partner = Pattern.Sweep; return true;
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

            foreach (var h in list) { h.sr.color = hazardColor; h.sr.sortingOrder = 20; h.armed = true; }
            Shake(0.12f, 0.1f);
            yield return new WaitForSeconds(columnActiveTime);
            foreach (var h in list) h.armed = false;
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
            while (side > 0 ? last > end : last < end)
            {
                float dx = side * speed * Time.deltaTime;
                x -= dx; last -= dx;
                for (int i = 0; i < count; i++) barList[i].sr.transform.position = new Vector3(x + side * spacing * i, yc, 0f);
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
                StartCoroutine(BossFlash(new Color(1f, 0.4f, 0.4f), 0.15f));
                Shake(0.15f, 0.2f);
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
            yield return new WaitForSeconds(eraseTime);
            box.armed = false;
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
                    if (p.x < ArenaLeft - 1f || p.x > ArenaRight + 1f) RemoveHazard(wv);
                    else any = true;
                }
                yield return null;
            }
            RemoveHazard(band);
            if (s < slams - 1) yield return new WaitForSeconds(0.5f);
        }
    }

    // 틈 → §
    IEnumerator Stun()
    {
        stunned = true; stunHit = false; cracks = 0;
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
            Shake(0.6f, 0.6f);
            yield return BossFlash(Color.white, 0.6f, true);
            if (phase >= bossPhases) { yield return Defeat(); yield break; }
        }
        else cracks = reflectsToStun - 1; // 놓쳐도 한 번만 더 받아치면 다시 틈
        boss.color = bossColor;
        yield return MoveBoss(BossRest, 0.6f);
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
            yield return null;
        }
        boss.transform.localPosition = Vector3.zero;
        boss.color = bossColor;
        yield return Fade(boss, 1f, 0f, 1f);
        bossAnchor.gameObject.SetActive(false);
        Debug.Log("[보스] 쓰러짐");
        onDefeated.Invoke();
    }

    // ───────────── 피격 / 죽음 ─────────────

    void Update()
    {
        UpdateSlowFx();
        if (!started || player == null || dying || defeated || teaching || Time.time < invulnUntil) return;
        Bounds pb = PlayerHitBounds();
        bool hit = false;
        foreach (var h in hazards)
            if (h.armed && h.sr != null && h.sr.bounds.Intersects(pb)) { hit = true; break; }
        if (hit) Hurt(); // 목록을 도는 중에 지우지 않게 밖에서
    }

    /// <summary>판정은 플레이어 콜라이더보다 조금 작게 (스친 정도는 봐줌).</summary>
    Bounds PlayerHitBounds()
    {
        Bounds b = pcol.bounds;
        b.size = new Vector3(b.size.x * 0.7f, b.size.y * 0.85f, 10f);
        return b;
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

        // 하트: 화면 왼쪽 위 (설정 버튼 오른쪽). 카메라를 빼도 화면에서 같은 크기
        float h = cam.orthographicSize, w = h * cam.aspect, z = h / Mathf.Max(0.01f, baseOrtho);
        for (int i = 0; i < heartIcons.Count; i++)
        {
            heartIcons[i].transform.localPosition = new Vector3(-w + (2.0f + i * 0.9f) * z, h - 0.8f * z, 10f);
            heartIcons[i].transform.localScale = Vector3.one * 1.3f * z;
        }

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
        if (slowOn)
        {
            Time.timeScale = Mathf.Lerp(1f, tutorialTimeScale, slowFx);
            Time.fixedDeltaTime = baseFixedDt * Time.timeScale; // 느려도 물리가 끊겨 보이지 않게
        }
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

    /// <summary>§: 위 S → 가운데 고리 → 아래 S 를 한 획으로. 높이 약 2 (중심 0,0).</summary>
    static Vector2[] SectionPath()
    {
        var key = new[]
        {
            new Vector2(0.80f, 1.75f), new Vector2(0.55f, 1.98f), new Vector2(0.25f, 1.88f), new Vector2(0.18f, 1.60f),
            new Vector2(0.40f, 1.40f), new Vector2(0.72f, 1.22f), new Vector2(0.82f, 0.98f), new Vector2(0.62f, 0.80f),
            new Vector2(0.38f, 0.86f), new Vector2(0.20f, 1.06f), new Vector2(0.30f, 1.25f), new Vector2(0.55f, 1.20f),
            new Vector2(0.80f, 0.98f), new Vector2(0.82f, 0.70f), new Vector2(0.62f, 0.55f), new Vector2(0.30f, 0.38f),
            new Vector2(0.18f, 0.20f), new Vector2(0.25f, 0.02f), new Vector2(0.52f, -0.02f), new Vector2(0.75f, 0.12f),
        };
        var off = new Vector2(0.5f, 1f);
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

    void Shake(float duration, float magnitude)
    {
        shakeDur = duration;
        shakeLeft = Mathf.Max(shakeLeft, duration);
        shakeMag = Mathf.Max(shakeMag, magnitude);
    }

    void BuildHearts()
    {
        for (int i = 0; i < hearts; i++)
        {
            var sr = MakeSprite("Heart", cam.transform, Heart(), 1000, new Color(1f, 0.25f, 0.3f));
            sr.gameObject.SetActive(false);
            heartIcons.Add(sr);
        }
    }

    void SetHearts(int n)
    {
        heartsLeft = Mathf.Max(0, n);
        for (int i = 0; i < heartIcons.Count; i++)
        {
            heartIcons[i].gameObject.SetActive(true);
            heartIcons[i].color = i < heartsLeft ? new Color(1f, 0.25f, 0.3f) : new Color(0.4f, 0.4f, 0.4f, 0.35f);
        }
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
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float speed = t > time - 0.3f ? 40f : 14f; // 직전엔 빠르게 깜빡임
            float m = 0.55f + 0.45f * Mathf.Sin(t * speed);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].sr == null || list[i].armed) continue;
                var c = list[i].sr.color; c.a = baseA[i] * m; list[i].sr.color = c;
            }
            yield return null;
        }
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
