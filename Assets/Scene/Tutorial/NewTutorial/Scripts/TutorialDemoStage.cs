using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 시범 창 속 '동영상'을 실제로 만들어 내는 작은 무대.
///  - 플레이 화면에서 멀리 떨어진 곳(Stage Origin)에 발판·메크로 인형·손가락을 만들고, 전용 카메라로 찍어 RenderTexture 에 담는다.
///    시범 창(TutorialDemoWindow)은 이 텍스처를 RawImage 로 보여 주기만 한다.
///  - 메크로 인형은 진짜 메크로와 같은 애니메이터·그림을 쓰고, 이동 속도·점프 높이·중력·나뭇가지 던지는 각도도
///    진짜 PlayerController / Branch 값에서 읽어 똑같이 움직인다 → 시범을 그대로 따라 하면 실제 게임에서도 된다.
///  - 물리 엔진 대신 단순한 사각형 충돌로 직접 계산한다 (시범은 매번 똑같이 재생돼야 해서, 결과가 흔들릴 수 있는 물리 엔진을 쓰지 않음).
/// </summary>
public class TutorialDemoStage : MonoBehaviour
{
    [Header("화면")]
    [Tooltip("시범 화면 해상도 (픽셀). 시범 창의 그림 영역 비율과 맞출 것")]
    public Vector2Int textureSize = new Vector2Int(960, 600);
    [Tooltip("무대를 만들 위치. 플레이 화면에 절대 보이지 않을 만큼 멀리")]
    public Vector3 stageOrigin = new Vector3(10000f, 10000f, 0f);
    [Tooltip("시범 배경색 (튜토리얼 맵 배경과 맞춤)")]
    public Color backgroundColor = Color.white;
    [Tooltip("재생 속도 배율. 1 = 실제 게임 속도. 조금 느리게 하면 따라 보기 쉽다")]
    [Range(0.3f, 1.5f)] public float playbackSpeed = 0.85f;

    [Header("발판 모양")]
    public Color blockFill = Color.white;
    public Color blockLine = new Color(0.13f, 0.13f, 0.16f);
    [Tooltip("발판 테두리 두께 (칸)")]
    [Min(0f)] public float lineWidth = 0.07f;
    public Color breakableFill = new Color(0.55f, 0.22f, 0.78f);
    public Color doorFill = new Color(0.98f, 0.78f, 0.3f);
    public Color leverColor = new Color(0.35f, 0.24f, 0.16f);

    [Header("손가락과 그린 선")]
    public Color strokeColor = new Color(0.12f, 0.12f, 0.16f);
    [Tooltip("그리기를 마친 순간 선이 바뀌는 색 (인식됨 표시)")]
    public Color strokeDoneColor = new Color(0.2f, 0.65f, 1f);
    [Tooltip("그린 선 두께 (칸, 화면 높이 6칸 기준). 씬에 플레이어 그리기 궤적(GestureTrail)이 있으면 안 쓰고, 플레이어가 그리는 선과 화면에서 같은 두께로 맞춘다")]
    [Min(0.01f)] public float strokeWidth = 0.11f;
    public Color cursorColor = new Color(1f, 0.45f, 0.35f);

    [Header("연결 (비우면 자동으로 찾음)")]
    [Tooltip("모양·애니메이션·이동 규칙을 빌려 올 진짜 메크로")]
    public PlayerController sourcePlayer;
    [Tooltip("모양·크기를 빌려 올 진짜 나뭇가지")]
    public Branch sourceBranch;

    // ── 외부에서 쓰는 값 ──
    public RenderTexture Texture { get; private set; }
    public TutorialDemoClip Current { get; private set; }
    /// <summary>지금 클립을 처음부터 끝까지 본 횟수 (클립이 바뀌면 0). 1 이상이면 '한 번은 다 봤다'.</summary>
    public int LoopCount { get; private set; }
    /// <summary>이번 반복에서 얼마나 진행됐는지 (0~1). 진행 막대 표시용.</summary>
    public float Progress { get; private set; }

    // ── 무대 구성물 ──
    Transform root;
    Camera cam;
    Transform puppet;
    SpriteRenderer puppetSr;
    Animator puppetAnim;
    SpriteRenderer branchSr;
    LineRenderer stroke;
    SpriteRenderer cursor, fade, slash;
    readonly List<PropRuntime> props = new List<PropRuntime>();
    static Sprite whiteSprite, dotSprite;

    class PropRuntime
    {
        public TutorialDemoClip.PropType type;
        public Rect rect;
        public bool gone;          // 부서진 상자 / 열린 문
        public bool on;            // 레버 당김
        public GameObject go;
        public Transform handle;   // 레버 손잡이
        public int hits, hitsToBreak = 1;
        public SpriteRenderer stageSr; // 부서지는 상자의 오류 무늬 (맞을 때마다 바뀜)
    }

    // 부서지는 상자 그림: [처음, 1대, 2대, 3대 …]. 씬의 진짜 Breakable 에서 읽어 와 게임과 같은 그림으로 보여 준다
    readonly List<Sprite> breakableStages = new List<Sprite>();

    // ── 진짜 게임에서 읽어 온 규칙 ──
    float moveSpeed = 7f, jumpVelocity = 9.5f, gravity = 12.3f, stepHeight = 0.5f, plantDistance = 0.75f;
    float throwSpeed = 12f, branchGravity = 9.81f, slideSpeed = 9f, slideDuration = 0.6f, attackReach = 1.5f;
    Vector3 puppetScale = Vector3.one;
    float spriteOffsetY;                 // 발 → 그림 피벗 높이
    float bodyHalfWidth = 0.4f, bodyHeight = 1.8f;
    Vector2 branchSize = new Vector2(0.35f, 2f);
    RuntimeAnimatorController baseCtrl, branchCtrl;

    // ── 인형 상태 ──
    Vector2 p;              // 발 위치 (무대 좌표)
    float vy;
    bool grounded;
    int facing = 1;
    float moveRemaining; int moveDir;
    bool sliding; float slideLeft;
    float shownVx;

    enum BranchState { Held, Flying, Stuck, Returning }
    BranchState bState;
    Vector2 bPos, bVel;     // 나뭇가지 중심 위치·속도
    float bAngle;
    float teleportTimer = -1f, attackTimer = -1f;
    bool passThroughBranch;  // 나뭇가지로 순간이동한 직후: 몸이 나뭇가지와 겹쳐 있으니 빠져나올 때까지 통과

    Coroutine playing;
    static Dictionary<string, List<Vector2>> templates;

    // 한 프레임에 진행하는 최대 시간. 프레임이 끊겨도(창이 뒤에 있거나 느린 PC) 시범이 건너뛰지 않고
    // 조금 느려질 뿐 매번 같은 순서·타이밍으로 재생되게 한다 (점프 중 공중 이동 같은 타이밍이 어긋나지 않게).
    const float MaxStep = 1f / 30f;
    static float RawDt => Mathf.Min(Time.deltaTime, MaxStep);
    float Dt => RawDt * playbackSpeed;

    // ───────────────────────── 준비 ─────────────────────────

    void Awake()
    {
        ReadGameRules();
        Build();
    }

    void OnDestroy()
    {
        if (Texture != null) { Texture.Release(); Destroy(Texture); }
        if (root != null) Destroy(root.gameObject);
    }

    /// <summary>진짜 메크로·나뭇가지의 값을 읽어 시범이 실제 게임과 똑같이 움직이게 한다.</summary>
    void ReadGameRules()
    {
        if (sourcePlayer == null) sourcePlayer = FindAnyObjectByType<PlayerController>();
        if (sourceBranch == null) sourceBranch = FindAnyObjectByType<Branch>();
        if (sourcePlayer != null)
        {
            var pc = sourcePlayer;
            moveSpeed = pc.moveSpeed; jumpVelocity = pc.jumpVelocity; stepHeight = pc.maxStepHeight;
            plantDistance = pc.plantDistance; throwSpeed = pc.throwSpeed;
            slideSpeed = pc.slideSpeed; slideDuration = pc.slideDuration; attackReach = pc.attackReach;
            baseCtrl = pc.baseController; branchCtrl = pc.withBranchController;
            var rb = pc.GetComponent<Rigidbody2D>();
            if (rb != null) gravity = -Physics2D.gravity.y * rb.gravityScale;
            var col = pc.GetComponent<Collider2D>();
            var anim = pc.GetComponentInChildren<Animator>();
            if (col != null)
            {
                bodyHalfWidth = col.bounds.extents.x;
                bodyHeight = col.bounds.size.y;
            }
            if (anim != null)
            {
                puppetScale = anim.transform.lossyScale;
                if (col != null) spriteOffsetY = anim.transform.position.y - col.bounds.min.y;
                if (baseCtrl == null) baseCtrl = anim.runtimeAnimatorController;
            }
        }
        var breakable = FindAnyObjectByType<Breakable>(FindObjectsInactive.Include);
        if (breakable != null)
        {
            var bsr = breakable.GetComponent<SpriteRenderer>();
            if (bsr != null && bsr.sprite != null) breakableStages.Add(bsr.sprite);
            if (breakable.hitStages != null) foreach (var s in breakable.hitStages) if (s != null) breakableStages.Add(s);
        }
        if (sourceBranch != null)
        {
            var bc = sourceBranch.GetComponent<BoxCollider2D>();
            var s = sourceBranch.transform.lossyScale;
            if (bc != null) branchSize = new Vector2(Mathf.Abs(bc.size.x * s.x), Mathf.Abs(bc.size.y * s.y));
            var brb = sourceBranch.GetComponent<Rigidbody2D>();
            branchGravity = -Physics2D.gravity.y * (brb != null && brb.gravityScale > 0f ? brb.gravityScale : 1f);
        }
    }

    void Build()
    {
        Texture = new RenderTexture(textureSize.x, textureSize.y, 16) { name = "TutorialDemoRT", antiAliasing = 1 };
        Texture.Create();

        root = new GameObject("TutorialDemoStage (runtime)").transform;
        root.position = stageOrigin;

        var camGo = new GameObject("DemoCamera");
        camGo.transform.SetParent(root, false);
        cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = backgroundColor;
        cam.targetTexture = Texture;
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 50f;
        var data = camGo.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = false;   // 화면 효과(노이즈 등)는 시범 화면에 넣지 않음

        // 메크로 인형: 진짜 메크로와 같은 애니메이터
        puppet = new GameObject("Puppet").transform;
        puppet.SetParent(root, false);
        var sprGo = new GameObject("Sprite");
        sprGo.transform.SetParent(puppet, false);
        sprGo.transform.localPosition = new Vector3(0f, spriteOffsetY, 0f);
        sprGo.transform.localScale = new Vector3(Mathf.Abs(puppetScale.x), Mathf.Abs(puppetScale.y), 1f);
        puppetSr = sprGo.AddComponent<SpriteRenderer>();
        puppetSr.sortingOrder = 10;
        if (sourcePlayer != null)
        {
            var srcSr = sourcePlayer.GetComponentInChildren<Animator>()?.GetComponent<SpriteRenderer>();
            if (srcSr != null) { puppetSr.sprite = srcSr.sprite; puppetSr.sharedMaterial = srcSr.sharedMaterial; }
        }
        puppetAnim = sprGo.AddComponent<Animator>();
        puppetAnim.runtimeAnimatorController = baseCtrl;

        // 나뭇가지
        var bGo = new GameObject("Branch");
        bGo.transform.SetParent(root, false);
        branchSr = bGo.AddComponent<SpriteRenderer>();
        branchSr.sortingOrder = 8;
        if (sourceBranch != null)
        {
            var srcB = sourceBranch.GetComponent<SpriteRenderer>();
            if (srcB != null) { branchSr.sprite = srcB.sprite; branchSr.sharedMaterial = srcB.sharedMaterial; }
            bGo.transform.localScale = sourceBranch.transform.lossyScale;
        }

        // 그린 선 + 손가락
        var lGo = new GameObject("Stroke");
        lGo.transform.SetParent(root, false);
        stroke = lGo.AddComponent<LineRenderer>();
        stroke.useWorldSpace = false;
        stroke.widthMultiplier = strokeWidth;
        stroke.numCapVertices = 4; stroke.numCornerVertices = 4;
        stroke.sortingOrder = 50;
        stroke.positionCount = 0;
        stroke.sharedMaterial = FindLineMaterial();

        cursor = MakeSprite("Cursor", DotSprite(), 52, cursorColor);
        cursor.transform.localScale = Vector3.one * 0.42f;
        cursor.enabled = false;

        slash = MakeSprite("Slash", WhiteSprite(), 40, new Color(1f, 1f, 1f, 0.9f));
        slash.enabled = false;

        // 처음부터 다시 재생할 때 잠깐 하얗게 덮는 막
        fade = MakeSprite("Fade", WhiteSprite(), 100, new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, 0f));
        fade.transform.localScale = new Vector3(200f, 200f, 1f);
    }

    /// <summary>실제 게임의 그리기 궤적(GestureTrail)과 같은 재질을 쓴다. 없으면 기본 스프라이트 셰이더.</summary>
    static Material FindLineMaterial()
    {
        var trail = FindAnyObjectByType<GestureTrail>();
        var lr = trail != null ? trail.GetComponentInChildren<LineRenderer>() : null;
        if (lr != null && lr.sharedMaterial != null) return lr.sharedMaterial;
        var sh = Shader.Find("Sprites/Default");
        return sh != null ? new Material(sh) : null;
    }

    SpriteRenderer MakeSprite(string name, Sprite s, int order, Color c)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s; sr.sortingOrder = order; sr.color = c;
        return sr;
    }

    // ───────────────────────── 재생 ─────────────────────────

    /// <summary>시범 화면을 찍을지 정한다. 창이 닫혀 있는 동안에는 꺼서 쓸데없이 그리지 않게 한다.</summary>
    public void SetRendering(bool on)
    {
        if (cam != null) cam.enabled = on;
    }

    /// <summary>클립을 처음부터 반복 재생한다. 같은 클립이면 무시.</summary>
    public void Play(TutorialDemoClip clip)
    {
        if (clip == Current && playing != null) return;
        Current = clip;
        LoopCount = 0; Progress = 0f;
        if (playing != null) StopCoroutine(playing);
        playing = clip != null ? StartCoroutine(Loop(clip)) : null;
    }

    IEnumerator Loop(TutorialDemoClip clip)
    {
        while (true)
        {
            ResetStage(clip);
            Progress = 0f;
            yield return Fade(0f, 0.2f);
            yield return Wait(0.4f);
            for (int i = 0; i < clip.steps.Count; i++)
            {
                var step = clip.steps[i];
                Progress = i / (float)clip.steps.Count;
                yield return Wait(step.waitBefore);
                if (step.shape != TutorialDemoClip.Shape.None)
                {
                    yield return Draw(clip, step);
                    Execute(step);
                }
                if (step.waitUntilIdle)
                {
                    float limit = 6f;
                    while (!IsIdle && limit > 0f) { limit -= RawDt; yield return null; }
                }
            }
            Progress = 1f;
            LoopCount++;          // 끝까지 한 번 봤음 → 창에서 ESC 안내가 나타남
            yield return Wait(clip.endPause);
            yield return Fade(1f, 0.25f);
        }
    }

    IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Dt) yield return null;
    }

    IEnumerator Fade(float target, float time)
    {
        Color c = fade.color; float from = c.a;
        for (float t = 0f; t < time; t += RawDt)
        {
            c.a = Mathf.Lerp(from, target, t / time); fade.color = c;
            yield return null;
        }
        c.a = target; fade.color = c;
    }

    /// <summary>무대를 클립 처음 상태로 되돌린다 (발판 다시 만들기, 메크로·나뭇가지 제자리).</summary>
    void ResetStage(TutorialDemoClip clip)
    {
        foreach (var pr in props) Destroy(pr.go);
        props.Clear();
        foreach (var spec in clip.props) props.Add(MakeProp(spec));

        p = clip.puppetStart; vy = 0f; grounded = true; facing = 1;
        moveRemaining = 0f; sliding = false; shownVx = 0f;
        teleportTimer = -1f; attackTimer = -1f; passThroughBranch = false;
        bState = clip.startWithBranch ? BranchState.Held : BranchState.Stuck;
        if (bState == BranchState.Stuck) bPos = new Vector2(clip.branchStart.x, clip.branchStart.y + branchSize.y * 0.5f - 0.15f); // 처음부터 꽂혀 있는 나뭇가지
        bAngle = 0f;
        stroke.positionCount = 0; cursor.enabled = false; slash.enabled = false;

        float h = Mathf.Max(2f, clip.viewHeight);
        cam.orthographicSize = h * 0.5f;
        cam.transform.localPosition = new Vector3(clip.viewCenter.x, clip.viewCenter.y, -10f);
        fade.transform.localPosition = new Vector3(clip.viewCenter.x, clip.viewCenter.y, 0f);
        ApplyVisuals();
    }

    PropRuntime MakeProp(TutorialDemoClip.Prop spec)
    {
        var pr = new PropRuntime { type = spec.type, rect = spec.rect, hitsToBreak = Mathf.Max(1, spec.hitsToBreak) };
        pr.go = new GameObject(spec.type.ToString());
        pr.go.transform.SetParent(root, false);
        Rect r = spec.rect;
        if (spec.type == TutorialDemoClip.PropType.Lever)
        {
            // 레버: 작은 받침 + 기울어진 손잡이 (발판이 아님)
            AddRect(pr.go.transform, new Rect(r.center.x - 0.3f, r.yMin, 0.6f, 0.25f), leverColor, blockLine, 3);
            var hGo = new GameObject("Handle");
            hGo.transform.SetParent(pr.go.transform, false);
            hGo.transform.localPosition = new Vector3(r.center.x, r.yMin + 0.2f, 0f);
            AddRect(hGo.transform, new Rect(-0.06f, 0f, 0.12f, 0.8f), leverColor, blockLine, 2);
            hGo.transform.localRotation = Quaternion.Euler(0f, 0f, 30f);
            pr.handle = hGo.transform;
            return pr;
        }
        if (spec.type == TutorialDemoClip.PropType.Breakable && breakableStages.Count > 0)
        {
            // 진짜 게임과 같은 오류 블럭 무늬를 칸마다 반복해서 깐다
            var sGo = new GameObject("ErrorBlock");
            sGo.transform.SetParent(pr.go.transform, false);
            sGo.transform.localPosition = new Vector3(r.center.x, r.center.y, 0f);
            pr.stageSr = sGo.AddComponent<SpriteRenderer>();
            pr.stageSr.sprite = breakableStages[0];
            pr.stageSr.drawMode = SpriteDrawMode.Tiled;
            pr.stageSr.size = r.size;
            return pr;
        }
        Color fill = spec.type == TutorialDemoClip.PropType.Breakable ? breakableFill
                   : spec.type == TutorialDemoClip.PropType.Door ? doorFill : blockFill;
        AddRect(pr.go.transform, r, fill, blockLine, 0);
        return pr;
    }

    void AddRect(Transform parent, Rect r, Color fill, Color line, int order)
    {
        var back = new GameObject("Line").AddComponent<SpriteRenderer>();
        back.transform.SetParent(parent, false);
        back.sprite = WhiteSprite(); back.color = line; back.sortingOrder = order;
        back.transform.localPosition = new Vector3(r.center.x, r.center.y, 0f);
        back.transform.localScale = new Vector3(r.width, r.height, 1f);
        var front = new GameObject("Fill").AddComponent<SpriteRenderer>();
        front.transform.SetParent(parent, false);
        front.sprite = WhiteSprite(); front.color = fill; front.sortingOrder = order + 1;
        front.transform.localPosition = new Vector3(r.center.x, r.center.y, 0f);
        front.transform.localScale = new Vector3(Mathf.Max(0f, r.width - lineWidth * 2f), Mathf.Max(0f, r.height - lineWidth * 2f), 1f);
    }

    // ───────────────────────── 그리기 ─────────────────────────

    /// <summary>손가락이 모양을 그린다. 다 그리면 선이 파랗게 바뀌어 '인식됨'을 보여 준 뒤 서서히 사라진다.</summary>
    IEnumerator Draw(TutorialDemoClip clip, TutorialDemoClip.Step step)
    {
        float viewW = cam.orthographicSize * 2f * cam.aspect;
        // 무대를 멀리서 비추는 시범(화면이 넓은 시범)에서도 선·손가락이 같은 크기로 보이도록 화면 높이에 비례시킨다 (기준: 높이 6칸)
        float k = cam.orthographicSize * 2f / 6f;
        stroke.widthMultiplier = StrokeWorldWidth(k);
        List<Vector2> pts = ShapePoints(step, viewW, k);
        Vector2 anchor = clip.viewCenter + clip.drawArea;
        // 모양의 가운데를 그리는 자리에 맞춤
        Vector2 min = pts[0], max = pts[0];
        foreach (var q in pts) { min = Vector2.Min(min, q); max = Vector2.Max(max, q); }
        Vector2 offset = anchor - (min + max) * 0.5f;
        for (int i = 0; i < pts.Count; i++) pts[i] += offset;

        stroke.startColor = stroke.endColor = strokeColor;
        stroke.positionCount = 0;
        cursor.enabled = true;
        cursor.transform.localPosition = (Vector3)pts[0] + Vector3.back;

        // 누르기 직전 손가락이 살짝 커졌다가 눌림
        for (float t = 0f; t < 0.15f; t += RawDt)
        {
            cursor.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 0.42f, t / 0.15f) * k;
            yield return null;
        }

        int shown = 0;
        for (float t = 0f; t < step.drawTime; t += Dt)
        {
            int target = Mathf.Clamp(Mathf.CeilToInt(pts.Count * (t / step.drawTime)), 1, pts.Count);
            while (shown < target)
            {
                stroke.positionCount = shown + 1;
                stroke.SetPosition(shown, pts[shown]);
                shown++;
            }
            cursor.transform.localPosition = (Vector3)pts[shown - 1] + Vector3.back;
            yield return null;
        }
        stroke.positionCount = pts.Count;
        for (int i = 0; i < pts.Count; i++) stroke.SetPosition(i, pts[i]);
        cursor.enabled = false;
        StartCoroutine(FadeStroke());
    }

    /// <summary>그린 선 두께 (무대 단위). 플레이어가 실제로 그리는 선(GestureTrail)과 화면에서 같은 두께가 되게 환산한다.</summary>
    float StrokeWorldWidth(float k)
    {
        var trail = FindAnyObjectByType<GestureTrail>();
        var main = Camera.main;
        var window = GetComponent<TutorialDemoWindow>();
        if (trail == null || main == null || !main.orthographic || window == null || window.videoSize.y <= 0f) return strokeWidth * k;
        // 게임 화면 높이 대비 두께 = 플레이어 선 두께 / 게임 카메라 높이. 영상은 화면(1080 기준)보다 작게 보이므로 그만큼 키운다
        float screenFraction = trail.width / (main.orthographicSize * 2f);
        return screenFraction * (1080f / window.videoSize.y) * cam.orthographicSize * 2f;
    }

    IEnumerator FadeStroke()
    {
        stroke.startColor = stroke.endColor = strokeDoneColor;
        yield return new WaitForSeconds(0.25f);
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            var c = strokeDoneColor; c.a = 1f - t / 0.35f;
            stroke.startColor = stroke.endColor = c;
            yield return null;
        }
        stroke.positionCount = 0;
    }

    /// <summary>모양 → 선의 점들 (무대 좌표, 아직 위치 맞추기 전).</summary>
    List<Vector2> ShapePoints(TutorialDemoClip.Step step, float viewW, float k)
    {
        float size = 1.5f * k; // 모양의 크기 (칸, 화면 높이 6칸 기준 1.5칸)
        var list = new List<Vector2>();
        switch (step.shape)
        {
            case TutorialDemoClip.Shape.Right:
            case TutorialDemoClip.Shape.Left:
            {
                // 길게 이동할수록 길게 긋는다 (게임: 그은 길이에 비례해 이동)
                float len = Mathf.Clamp(step.moveDistance * 0.45f, 0.7f * k, viewW * 0.42f);
                float dir = step.shape == TutorialDemoClip.Shape.Right ? 1f : -1f;
                AddLine(list, Vector2.zero, new Vector2(len * dir, 0f), 16);
                break;
            }
            case TutorialDemoClip.Shape.Up:
                AddLine(list, Vector2.zero, new Vector2(0f, size), 14);
                break;
            case TutorialDemoClip.Shape.SlideRight: // ㄴ: 아래로 → 오른쪽
                AddLine(list, new Vector2(0f, size), Vector2.zero, 10);
                AddLine(list, Vector2.zero, new Vector2(size, 0f), 10);
                break;
            case TutorialDemoClip.Shape.AttackRight: // ㄱ: 오른쪽 → 아래로
                AddLine(list, new Vector2(0f, size), new Vector2(size, size), 10);
                AddLine(list, new Vector2(size, size), new Vector2(size, 0f), 10);
                break;
            default:
            {
                var t = Template(TemplateName(step.shape));
                if (t != null) { foreach (var q in t) list.Add(q * size); }
                else AddLine(list, Vector2.zero, new Vector2(size, size), 10);
                break;
            }
        }
        return list;
    }

    static void AddLine(List<Vector2> list, Vector2 a, Vector2 b, int n)
    {
        for (int i = 0; i <= n; i++) list.Add(Vector2.Lerp(a, b, i / (float)n));
    }

    static string TemplateName(TutorialDemoClip.Shape s)
    {
        switch (s)
        {
            case TutorialDemoClip.Shape.N: return "N";
            case TutorialDemoClip.Shape.Nine: return "9";
            case TutorialDemoClip.Shape.S: return "S";
            case TutorialDemoClip.Shape.O: return "O(상호작용)";
            case TutorialDemoClip.Shape.X: return "⋉";
        }
        return null;
    }

    /// <summary>실제 게임 인식에 쓰는 녹화 템플릿에서 모양을 가져온다 → 진짜로 인식되는 획 순서 그대로 보여 줌.
    /// 크기는 가로세로 중 긴 쪽이 1 이 되도록 맞춘다.</summary>
    static List<Vector2> Template(string name)
    {
        if (name == null) return null;
        if (templates == null)
        {
            templates = new Dictionary<string, List<Vector2>>();
            var ta = Resources.Load<TextAsset>("GestureRecordings/gesture_templates");
            if (ta != null)
            {
                var file = JsonUtility.FromJson<TemplateFile>(ta.text);
                if (file != null && file.items != null)
                    foreach (var it in file.items)
                    {
                        if (it == null || it.points == null || it.points.Count < 2 || templates.ContainsKey(it.name)) continue;
                        templates[it.name] = Normalize(it.points);
                    }
            }
        }
        List<Vector2> pts;
        return templates.TryGetValue(name, out pts) ? new List<Vector2>(pts) : null;
    }

    static List<Vector2> Normalize(List<TemplatePoint> raw)
    {
        Vector2 min = new Vector2(raw[0].x, raw[0].y), max = min;
        foreach (var q in raw) { min = Vector2.Min(min, new Vector2(q.x, q.y)); max = Vector2.Max(max, new Vector2(q.x, q.y)); }
        float s = Mathf.Max(max.x - min.x, max.y - min.y, 0.001f);
        var outList = new List<Vector2>(raw.Count);
        foreach (var q in raw) outList.Add((new Vector2(q.x, q.y) - min) / s);
        return outList;
    }

    [System.Serializable] class TemplateFile { public List<TemplateItem> items; }
    [System.Serializable] class TemplateItem { public string name; public List<TemplatePoint> points; }
    [System.Serializable] class TemplatePoint { public float x, y; }

    // ───────────────────────── 명령 (실제 게임과 같은 결과) ─────────────────────────

    void Execute(TutorialDemoClip.Step step)
    {
        switch (step.shape)
        {
            case TutorialDemoClip.Shape.Right: moveDir = 1; facing = 1; moveRemaining = step.moveDistance; break;
            case TutorialDemoClip.Shape.Left: moveDir = -1; facing = -1; moveRemaining = step.moveDistance; break;
            case TutorialDemoClip.Shape.Up: if (grounded) { vy = jumpVelocity; grounded = false; } break;
            case TutorialDemoClip.Shape.SlideRight:
                if (grounded) { sliding = true; slideLeft = slideDuration; facing = 1; moveRemaining = 0f; }
                break;
            case TutorialDemoClip.Shape.AttackRight:
                if (bState == BranchState.Held) { facing = 1; attackTimer = 0.25f; HitBreakables(); }
                break;
            case TutorialDemoClip.Shape.Nine: PlantBranch(); break;
            case TutorialDemoClip.Shape.N: ThrowBranch(); break;
            case TutorialDemoClip.Shape.S:
                if (bState == BranchState.Stuck || bState == BranchState.Flying) bState = BranchState.Returning;
                break;
            case TutorialDemoClip.Shape.O: Interact(); break;
            case TutorialDemoClip.Shape.X: if (bState == BranchState.Stuck) teleportTimer = 0.25f; break;
        }
    }

    /// <summary>시범이 다음 단계로 넘어가도 되는가 (메크로가 멈춰 서 있고, 나뭇가지도 자리를 잡음).</summary>
    bool IsIdle => grounded && moveRemaining <= 0f && !sliding && teleportTimer < 0f && attackTimer < 0f
                   && bState != BranchState.Flying && bState != BranchState.Returning;

    void PlantBranch()
    {
        if (bState != BranchState.Held) return;
        float x = p.x + plantDistance * facing;
        float top;
        if (!GroundTopAt(x, p.y + 0.6f, p.y - 1f, out top)) return; // 밑에 바닥이 없으면 세우지 않음 (게임과 동일)
        bPos = new Vector2(x, top + branchSize.y * 0.5f - 0.15f);
        bAngle = 0f;
        bState = BranchState.Stuck;
    }

    void ThrowBranch()
    {
        if (bState != BranchState.Held) return;
        bPos = p + new Vector2(0.5f * facing, 1f);
        bVel = throwSpeed * new Vector2(0.7071f * facing, 0.7071f);
        bState = BranchState.Flying;
    }

    void Interact()
    {
        foreach (var pr in props)
        {
            if (pr.type != TutorialDemoClip.PropType.Lever || pr.on) continue;
            if (Mathf.Abs(pr.rect.center.x - p.x) <= 2f) { pr.on = true; OpenDoors(); return; }
        }
        if (bState == BranchState.Stuck && Vector2.Distance(bPos, p + Vector2.up) <= 1.5f + branchSize.y * 0.5f)
            bState = BranchState.Held; // 가까이 박힌 나뭇가지 줍기
    }

    void OpenDoors() { foreach (var pr in props) if (pr.type == TutorialDemoClip.PropType.Door) pr.gone = true; }

    void HitBreakables()
    {
        float x0 = facing > 0 ? p.x : p.x - attackReach - bodyHalfWidth;
        var hit = new Rect(x0, p.y, attackReach + bodyHalfWidth, bodyHeight);
        foreach (var pr in props)
        {
            if (pr.type != TutorialDemoClip.PropType.Breakable || pr.gone || !hit.Overlaps(pr.rect)) continue;
            pr.hits++;
            if (pr.hits >= pr.hitsToBreak) pr.gone = true;                  // 마지막 그림을 때리면 부서짐
            else if (pr.stageSr != null)                                     // 아니면 더 깨진 무늬로
                pr.stageSr.sprite = breakableStages[Mathf.Min(pr.hits, breakableStages.Count - 1)];
        }
    }

    // ───────────────────────── 움직임 계산 ─────────────────────────

    void Update()
    {
        if (Current == null) return;
        float dt = Dt;
        if (dt <= 0f) return;

        UpdateTimers(dt);
        UpdatePuppet(dt);
        UpdateBranch(dt);
        ApplyVisuals();
    }

    void UpdateTimers(float dt)
    {
        if (teleportTimer >= 0f)
        {
            teleportTimer -= dt;
            if (teleportTimer < 0f && bState == BranchState.Stuck)
            {
                p = new Vector2(bPos.x, bPos.y - branchSize.y * 0.5f + 0.15f); // 나뭇가지 밑동에 발을 맞춤 (게임과 동일)
                vy = 0f; moveRemaining = 0f;
                passThroughBranch = true;
            }
        }
        if (attackTimer >= 0f) attackTimer -= dt;
        if (passThroughBranch && !OverlapsBranch()) passThroughBranch = false;
    }

    void UpdatePuppet(float dt)
    {
        float h = sliding ? bodyHeight * 0.4f : bodyHeight;

        // 가로 이동
        float dx = 0f;
        if (sliding)
        {
            dx = facing * slideSpeed * dt;
            slideLeft -= dt;
            if (slideLeft <= 0f && !Blocked(p.x, p.y, bodyHeight)) sliding = false; // 천장 밑에서는 빠져나올 때까지 계속
        }
        else if (moveRemaining > 0f)
        {
            float stepLen = Mathf.Min(moveSpeed * dt, moveRemaining);
            moveRemaining -= stepLen;
            dx = moveDir * stepLen;
        }
        if (dx != 0f)
        {
            float nx = p.x + dx;
            Rect body = new Rect(nx - bodyHalfWidth, p.y + 0.01f, bodyHalfWidth * 2f, h - 0.02f);
            foreach (var s in Solids())
            {
                if (!body.Overlaps(s)) continue;
                if (grounded && s.yMax - p.y <= stepHeight && !Blocked(nx, s.yMax, h)) { p.y = s.yMax; continue; } // 낮은 턱은 올라섬
                nx = dx > 0f ? s.xMin - bodyHalfWidth - 0.001f : s.xMax + bodyHalfWidth + 0.001f;          // 벽에 막힘
                moveRemaining = 0f;
                body.x = nx - bodyHalfWidth;
            }
            shownVx = (nx - p.x) / dt;
            p.x = nx;
        }
        else shownVx = 0f;

        // 세로 (중력·착지·천장)
        vy -= gravity * dt;
        float ny = p.y + vy * dt;
        grounded = false;
        foreach (var s in Solids())
        {
            if (p.x + bodyHalfWidth <= s.xMin || p.x - bodyHalfWidth >= s.xMax) continue;
            if (vy <= 0f && p.y >= s.yMax - 0.001f && ny <= s.yMax) { ny = s.yMax; vy = 0f; grounded = true; }
            else if (vy > 0f && p.y + h <= s.yMin + 0.001f && ny + h > s.yMin) { ny = s.yMin - h; vy = 0f; }
        }
        p.y = ny;
        if (p.y < Current.viewCenter.y - Current.viewHeight) ResetStage(Current); // 떨어지면 처음 상태로 (시범이 잘못 짜였을 때 안전장치)
    }

    void UpdateBranch(float dt)
    {
        if (bState == BranchState.Flying)
        {
            Vector2 prev = bPos;
            bVel.y -= branchGravity * dt;
            bPos += bVel * dt;
            bAngle -= 540f * Mathf.Sign(bVel.x) * dt;
            foreach (var s in Solids(false))
            {
                // 아래로 떨어지며 발판 윗면을 지나면 그 자리에 박힘 (벽에 부딪히면 튕기지 않고 떨어짐)
                float tipPrev = prev.y - branchSize.y * 0.5f, tip = bPos.y - branchSize.y * 0.5f;
                if (bPos.x < s.xMin || bPos.x > s.xMax) continue;
                if (bVel.y <= 0f && tipPrev >= s.yMax - 0.05f && tip <= s.yMax)
                {
                    bPos.y = s.yMax + branchSize.y * 0.5f - 0.15f; bAngle = 0f; bState = BranchState.Stuck; return;
                }
                if (bPos.y > s.yMin && bPos.y < s.yMax)
                {
                    // 벽 옆면에 부딪힘: 벽 바로 앞으로 되돌리고 가로 속도를 없앤다 → 그대로 떨어져 바닥에 꽂힘
                    if (prev.x <= s.xMin) bPos.x = s.xMin - 0.05f;
                    else if (prev.x >= s.xMax) bPos.x = s.xMax + 0.05f;
                    bVel.x = 0f;
                }
            }
        }
        else if (bState == BranchState.Returning)
        {
            Vector2 target = p + Vector2.up * (bodyHeight * 0.5f);
            bPos = Vector2.MoveTowards(bPos, target, 18f * dt);
            bAngle += 720f * dt;
            if (Vector2.Distance(bPos, target) < 0.3f) bState = BranchState.Held;
        }
    }

    /// <summary>지금 몸을 막는 사각형들 (땅·벽·닫힌 문·부서지지 않은 상자·박힌 나뭇가지).</summary>
    IEnumerable<Rect> Solids(bool includeBranch = true)
    {
        foreach (var pr in props)
        {
            if (pr.gone || pr.type == TutorialDemoClip.PropType.Lever) continue;
            yield return pr.rect;
        }
        if (includeBranch && bState == BranchState.Stuck && !passThroughBranch) yield return BranchRect();
    }

    Rect BranchRect() => new Rect(bPos.x - branchSize.x * 0.5f, bPos.y - branchSize.y * 0.5f, branchSize.x, branchSize.y);

    bool OverlapsBranch()
    {
        if (bState != BranchState.Stuck) return false;
        return new Rect(p.x - bodyHalfWidth, p.y + 0.01f, bodyHalfWidth * 2f, bodyHeight - 0.02f).Overlaps(BranchRect());
    }

    bool Blocked(float x, float feetY, float height)
    {
        var body = new Rect(x - bodyHalfWidth, feetY + 0.01f, bodyHalfWidth * 2f, height - 0.02f);
        foreach (var s in Solids()) if (body.Overlaps(s)) return true;
        return false;
    }

    bool GroundTopAt(float x, float fromY, float toY, out float top)
    {
        top = float.MinValue; bool found = false;
        foreach (var s in Solids(false))
            if (x >= s.xMin && x <= s.xMax && s.yMax <= fromY && s.yMax >= toY && s.yMax > top) { top = s.yMax; found = true; }
        return found;
    }

    // ───────────────────────── 화면 반영 ─────────────────────────

    void ApplyVisuals()
    {
        puppet.localPosition = new Vector3(p.x, p.y, 0f);
        puppetSr.flipX = facing < 0;
        bool held = bState == BranchState.Held;
        var wantCtrl = held && branchCtrl != null ? branchCtrl : baseCtrl;
        if (puppetAnim.runtimeAnimatorController != wantCtrl) puppetAnim.runtimeAnimatorController = wantCtrl;
        if (puppetAnim.runtimeAnimatorController != null)
        {
            puppetAnim.SetFloat("Speed", sliding ? 0f : Mathf.Abs(shownVx));
            puppetAnim.SetFloat("YVel", vy);
            puppetAnim.SetFloat("Grounded", grounded ? 1f : 0f);
            puppetAnim.SetBool("Sliding", sliding);
        }

        branchSr.enabled = !held;
        branchSr.transform.localPosition = new Vector3(bPos.x, bPos.y, 0f);
        branchSr.transform.localRotation = Quaternion.Euler(0f, 0f, bAngle);

        // ㄱ 공격: 앞쪽에 짧게 번쩍이는 베기 자국
        slash.enabled = attackTimer > 0f;
        if (slash.enabled)
        {
            float k = 1f - attackTimer / 0.25f;
            slash.transform.localPosition = new Vector3(p.x + facing * (bodyHalfWidth + attackReach * 0.5f), p.y + bodyHeight * 0.55f, 0f);
            slash.transform.localScale = new Vector3(attackReach, 0.12f, 1f);
            slash.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(50f, -40f, k) * facing);
            slash.color = new Color(strokeDoneColor.r, strokeDoneColor.g, strokeDoneColor.b, 1f - k);
        }

        foreach (var pr in props)
        {
            if (pr.handle != null)
                pr.handle.localRotation = Quaternion.Lerp(pr.handle.localRotation, Quaternion.Euler(0f, 0f, pr.on ? -30f : 30f), 0.3f);
            if (pr.gone && pr.go.activeSelf)
            {
                // 부서지거나 열린 것은 작아지며 사라짐
                pr.go.transform.localScale = Vector3.Lerp(pr.go.transform.localScale, Vector3.zero, 0.25f);
                if (pr.go.transform.localScale.x < 0.03f) pr.go.SetActive(false);
            }
        }
    }

    // ───────────────────────── 기본 그림 ─────────────────────────

    static Sprite WhiteSprite()
    {
        if (whiteSprite != null) return whiteSprite;
        var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
        tex.SetPixel(0, 0, Color.white); tex.Apply();
        whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return whiteSprite;
    }

    /// <summary>손가락 표시용 동그라미 (가운데는 반투명, 테두리는 진하게).</summary>
    static Sprite DotSprite()
    {
        if (dotSprite != null) return dotSprite;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = d > 1f ? 0f : d > 0.78f ? 1f : 0.45f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        dotSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        return dotSprite;
    }
}
