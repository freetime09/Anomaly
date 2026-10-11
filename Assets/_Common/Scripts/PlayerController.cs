using System.Collections;
using UnityEngine;

/// <summary>
/// 인식된 제스처 명령을 받아 Rigidbody2D로 캐릭터를 움직인다.
/// 좌/우 이동은 일정 시간 동안 속도를 부여하고, 점프는 바닥에 있을 때만 실행한다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerController : MonoBehaviour
{
    public enum Command { None, MoveLeft, MoveRight, Jump, SlideLeft, SlideRight, ThrowBranch, Interact, PlantBranch, ReturnBranch, TeleportBranch, AttackLeft, AttackRight }

    [Header("이동")]
    [Tooltip("좌우 이동 속도 (units/초)")]
    public float moveSpeed = 5f;
    [Tooltip("박스를 잡고 끌 때 이동 속도 배율(느리게). 1=평소 속도. 밀기/당기기 모두 이 속도로 일정하게 느려진다")]
    public float carrySpeedScale = 0.6f;
    [Tooltip("그은 가로 길이 100px당 이동하는 거리 (units). 클수록 같은 길이로 더 멀리 감")]
    public float unitsPer100px = 1.2f;
    [Tooltip("한 번에 이동 가능한 최소 거리 (units)")]
    public float minMoveDistance = 0.5f;
    [Tooltip("한 번에 이동 가능한 최대 거리 (units)")]
    public float maxMoveDistance = 8f;
    [Tooltip("길이 정보 없이 이동 명령이 올 때 쓰는 기본 거리 (units)")]
    public float defaultMoveDistance = 2.5f;
    [Tooltip("걸어서 올라갈 수 있는 최대 경사 각도(도). 이보다 가파르면 벽으로 취급해 막힘")]
    public float maxSlopeAngle = 45f;
    [Tooltip("자동으로 올라설 수 있는 턱 최대 높이(units). 이보다 높으면 벽으로 막혀 점프해야 함. 완만한 계단 슬로프(한 칸 rise≈0.3)는 오르되, 1칸(1유닛) 블럭은 못 오르게 0.5로 둠")]
    public float maxStepHeight = 0.5f;
    [Tooltip("계단을 오를 때 벽면을 타고 올라서는 속도(units/초). 클수록 계단을 빨리(덜 부드럽게) 오름")]
    public float stepClimbSpeed = 8f;

    [Header("점프")]
    [Tooltip("점프 시 위로 주는 순간 속도")]
    public float jumpVelocity = 8f;
    [Tooltip("바닥 판정용 발밑 검사 거리")]
    public float groundCheckDistance = 0.05f;
    [Tooltip("바닥으로 인정할 레이어 (예: Ground)")]
    public LayerMask groundLayer;
    [Tooltip("바닥을 떠난 뒤에도 이 시간(초) 안엔 점프 허용 (코요테 타임). 지터로 접지 프레임을 놓쳐도 점프가 씹히지 않게")]
    public float coyoteTime = 0.1f;
    [Tooltip("공중에서 점프를 그어도 이 시간(초) 안에 착지하면 착지 순간 자동으로 점프 (점프 버퍼)")]
    public float jumpBufferTime = 0.12f;

    [Header("나뭇가지 던지기")]
    [Tooltip("던질 나뭇가지 (씬의 Branch 오브젝트)")]
    public Branch branch;
    [Tooltip("던지는 초기 속도. 위 45° 방향으로 발사")]
    public float throwSpeed = 12f;
    [Tooltip("발사 지점 오프셋. x는 바라보는 방향으로, y는 위로")]
    public Vector2 throwOffset = new Vector2(0.5f, 1f);
    [Tooltip("이 거리 안에서 O를 그리면 박혀 있는 나뭇가지를 회수. 범위 안이면 O 프롬프트가 뜬다")]
    public float pickupRange = 1.5f;
    [Tooltip("O(상호작용)로 레버를 작동시킬 수 있는 탐색 반경. 레버 각자의 interactRadius 안이어야 최종 작동")]
    public float interactRange = 2f;
    [Tooltip("9로 세울 때 플레이어 앞으로 떨어뜨릴 거리 (바라보는 방향)")]
    public float plantDistance = 1f;
    [Tooltip("제스처를 그리는 동안 나뭇가지가 세워질 자리에 보여줄 단색 실루엣 색 (세울 수 있음)")]
    public Color plantPreviewColor = new Color(0.1f, 0.1f, 0.1f, 0.75f);
    [Tooltip("세울 수 없는 자리(밑에 바닥 없음)일 때 단색 실루엣 색")]
    public Color plantPreviewBlockedColor = new Color(1f, 0.1f, 0.1f, 0.7f);
    [Tooltip("제스처를 그리는 동안 이동 도착 지점에 보여줄 캐릭터 단색 실루엣 색")]
    public Color movePreviewColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);

    [Header("나뭇가지 유무 애니메이션")]
    [Tooltip("나뭇가지 없을 때(던짐/세움/돌아오는중) 쓰는 컨트롤러 (기본 PlayerAnimator)")]
    public RuntimeAnimatorController baseController;
    [Tooltip("나뭇가지 들고 있을 때 쓰는 컨트롤러 (22 버전 오버라이드)")]
    public RuntimeAnimatorController withBranchController;

    [Header("공격 (ㄱ 나뭇가지 휘두르기)")]
    [Tooltip("나뭇가지를 들고 있을 때만 공격 가능. 앞쪽 히트박스 안의 IDamageable 을 때린다")]
    public int attackDamage = 1;
    [Tooltip("타격 판정 히트박스 크기(units)")]
    public Vector2 attackHitboxSize = new Vector2(1.2f, 1.2f);
    [Tooltip("바라보는 방향으로 히트박스 중심을 얼마나 앞에 둘지(units)")]
    public float attackReach = 0.9f;
    [Tooltip("히트박스 세로 중심 오프셋(트랜스폼 기준, units)")]
    public float attackHeight = 0.5f;
    [Tooltip("스윙 시작 후 타격 판정이 나기까지의 시간(초). 애니 중간 타점에 맞춤")]
    public float attackHitDelay = 0.15f;
    [Tooltip("한 번 휘두른 뒤 다시 휘두를 수 있기까지의 시간(초)")]
    public float attackCooldown = 0.4f;
    [Tooltip("타격 대상으로 검사할 레이어. 이 안의 콜라이더에서 IDamageable 을 찾아 때린다")]
    public LayerMask attackLayers = ~0;
    [Tooltip("공격 시 바라보는 방향 앞에 생성되는 타격 이펙트 프리팹. 플레이어 그림은 그대로 두고 이 이펙트만 재생된다")]
    public GameObject attackEffectPrefab;
    [Tooltip("타격 이펙트 크기 배율. 플레이어의 비균일 스케일을 상쇄한 '균일' 월드 크기 기준")]
    public float attackEffectScale = 1f;

    [Header("슬라이드 (임시: 캐릭터 회전으로 표현)")]
    [Tooltip("슬라이드 동안 앞으로 미끄러지는 속도")]
    public float slideSpeed = 7f;
    [Tooltip("슬라이드 지속 시간 (초)")]
    public float slideDuration = 0.5f;
    [Tooltip("슬라이드가 끝난 뒤 다시 슬라이드할 수 있기까지의 쿨타임(초)")]
    public float slideCooldown = 0.6f;
    [Tooltip("눕히는 각도(도). 진행 방향 앞으로 회전. 부호를 바꾸면 반대로 눕음")]
    public float slideAngle = 90f;
    [Tooltip("슬라이드 중 콜라이더 높이 비율(원래 대비). 예: 0.4 = 40% 높이로 납작. 발은 땅에 유지됨")]
    public float slideHeightScale = 0.4f;
    [Tooltip("슬라이드가 끝날 때 머리 위가 막혀 있으면(낮은 통로) 일어설 공간이 날 때까지 엎드린 채 계속 미끄러지는 최대 추가 시간(초). 넘으면 그 자리에 엎드려 멈춤(안전장치)")]
    public float slideMaxExtraTime = 2f;

    [Header("사운드")]
    [Tooltip("뛸 때 발소리. 바닥에서 이동하는 동안 일정 간격으로 반복 재생")]
    public AudioClip footstepClip;
    [Tooltip("발소리 재생 간격(초). 작을수록 빠르게 통통")]
    public float footstepInterval = 0.32f;
    [Tooltip("점프 소리")]
    public AudioClip jumpClip;
    [Tooltip("착지 소리 (공중에 있다가 바닥에 닿는 순간)")]
    public AudioClip landClip;
    [Tooltip("나뭇가지 던지는 소리")]
    public AudioClip throwClip;
    [Tooltip("나뭇가지 휘두르는(ㄱ 공격) 소리")]
    public AudioClip attackClip;
    [Tooltip("슬라이드(ㄴ) 동안 나는 소리. 슬라이드 끝나면 멈춤")]
    public AudioClip slideClip;
    [Min(0f)]
    [Tooltip("슬라이드 소리를 클립의 이 지점(초)부터 재생 — 앞부분이 비어있으면 그만큼 건너뜀")]
    public float slideClipStartTime = 0f;
    [Tooltip("죽을 때 소리")]
    public AudioClip deathClip;
    [Tooltip("나뭇가지 위치로 텔레포트(⋉)할 때 소리")]
    public AudioClip branchTeleportClip;
    [Min(0f)]
    [Tooltip("나뭇가지 텔레포트(⋉)를 입력한 뒤 실제 이동까지의 지연(초)")]
    public float branchTeleportDelay = 0.3f;
    [Range(0f, 1f)]
    [Tooltip("효과음 볼륨")]
    public float sfxVolume = 1f;

    Rigidbody2D rb;
    BoxCollider2D col;
    Animator anim;      // 자식 CharacterSprite의 Animator
    SpriteRenderer sr;  // 좌우 반전용
    bool dead;          // 죽음 애니 재생 중 (상태 전환/컨트롤러 교체 잠금)
    Transform visual;   // 회전(슬라이드)용 캐릭터 그림 트랜스폼
    float moveRemaining;  // 남은 이동 거리 예산 (units)
    bool stopMoveNext;    // 마지막 이동 스텝이 끝났음 → 다음 FixedUpdate에서 수평 정지
    float moveDir; // -1 왼쪽, +1 오른쪽
    float moveSpeedMul = 1f; // 이동 속도 배율 (박스 끌 때 carrySpeedScale 로 낮춤)
    int facing = 1; // +1 오른쪽, -1 왼쪽 (바라보는 방향)
    bool? lastHeld;  // 직전 프레임의 나뭇가지 소지 여부 (바뀔 때만 컨트롤러 교체)
    AudioSource sfx;      // 점프·던지기 등 순간 효과음(PlayOneShot)용
    AudioSource footSrc;  // 발소리 전용 (멈추면 즉시 Stop 하려고 분리)
    AudioSource slideSfx; // 슬라이드 소리 전용 (슬라이드 끝나면 즉시 Stop)
    float footstepTimer;  // 다음 발소리까지 남은 시간
    bool stepClimbing;    // 계단(턱)을 타고 오르는 중 (발이 떠도 오르기 유지하려는 래치)
    bool attacking;       // 공격 스윙 중 (중복 스윙 방지)
    bool wasGrounded = true; // 직전 프레임 접지 여부 (착지 순간 감지용)
    float lastJumpTime = -999f; // 마지막 점프 시각 (직후 이 시간 동안은 안티바운스가 점프 상승 속도를 죽이지 않게 보호)
    float lastGroundedTime = -999f; // 마지막으로 바닥에 있던 시각 (코요테 타임 판정용)
    float jumpBufferedTime = -999f; // 마지막으로 점프를 입력한 시각 (점프 버퍼 판정용)
    float lastAttackTime = -999f;   // 마지막 공격 시각 (쿨타임 표시용)
    float lastSlideTime = -999f;    // 마지막 슬라이드 종료 시각 (쿨타임 게이트/표시용)

    // 슬라이드 상태 (임시: 캐릭터 회전으로 표현)
    bool sliding;
    float slideTimer;
    int slideDir;
    float slideExtraTimer;  // 천장 밑이라 슬라이드를 연장한 시간
    bool slideStopped;      // 천장 밑에서 앞이 막혀(또는 시간 초과) 엎드린 채 멈춘 상태
    float lastSlideX;       // 연장 슬라이드 중 앞이 막혔는지 판단용 직전 x
    Vector2 origColSize;   // 평소 콜라이더 크기/오프셋 (슬라이드 후 복구용)
    Vector2 origColOffset;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<BoxCollider2D>();
        origColSize = col.size;
        origColOffset = col.offset;
        anim = GetComponentInChildren<Animator>();
        sr = anim != null ? anim.GetComponent<SpriteRenderer>() : null;
        visual = anim != null ? anim.transform : null;
        sfx = GetComponent<AudioSource>();
        if (sfx == null) sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        footSrc = gameObject.AddComponent<AudioSource>();
        footSrc.playOnAwake = false; footSrc.loop = false;
        slideSfx = gameObject.AddComponent<AudioSource>();
        slideSfx.playOnAwake = false; slideSfx.loop = false;
    }

    void Start()
    {
        // 나뭇가지에 플레이어 콜라이더를 알려줌 → 날아가는 동안에만 충돌 무시(몸 통과),
        // 박히거나 세워지면 다시 단단해져 밟거나 부딪힐 수 있다.
        if (branch != null) branch.SetOwner(col);
        gesture = FindObjectOfType<GestureRecognizer>();
    }

    GestureRecognizer gesture;    // 그리는 중인지 확인용 (미리보기 표시 조건)
    SpriteRenderer plantPreview;  // 나뭇가지 세울 자리 미리보기 (반투명 실루엣)

    SpriteRenderer movePreview;   // 이동 도착 지점 미리보기 (캐릭터 단색 실루엣)

    void LateUpdate()
    {
        UpdatePlantPreview();
        UpdateMovePreview();
    }

    /// <summary>그림 색은 무시하고 모양(알파)만 쓰는 단색 실루엣용 SpriteRenderer를 만든다.</summary>
    static SpriteRenderer CreateSilhouette(string name)
    {
        var go = new GameObject(name);
        var r = go.AddComponent<SpriteRenderer>();
        var sh = Shader.Find("GUI/Text Shader"); // 텍스처 알파 × color → color 그대로의 단색
        if (sh != null) r.material = new Material(sh);
        return r;
    }

    /// <summary>제스처를 그리는 동안, 나뭇가지를 들고 있으면 9로 세워질 자리에 단색 실루엣을 보여준다.
    /// 세울 수 없는 자리(밑에 바닥 없음)면 빨간색.</summary>
    void UpdatePlantPreview()
    {
        bool show = !dead && gesture != null && gesture.IsDrawing && branch != null && branch.CanThrow;
        if (!show)
        {
            if (plantPreview != null) plantPreview.enabled = false;
            return;
        }
        var bsr = branch.GetComponent<SpriteRenderer>();
        if (bsr == null || bsr.sprite == null) return;
        if (plantPreview == null) plantPreview = CreateSilhouette("PlantPreview");
        plantPreview.sprite = bsr.sprite;
        plantPreview.sortingLayerID = bsr.sortingLayerID;
        plantPreview.sortingOrder = bsr.sortingOrder;
        plantPreview.transform.localScale = branch.transform.lossyScale;

        Vector2 pos;
        bool ok = TryGetPlantSpot(out pos);
        plantPreview.transform.position = pos + Vector2.down * branch.stickDepth; // 실제 세워질 때처럼 살짝 꽂힌 위치
        plantPreview.color = ok ? plantPreviewColor : plantPreviewBlockedColor;
        plantPreview.enabled = true;
    }

    /// <summary>제스처를 그리는 동안, 획이 가로 방향이면 그 길이만큼 이동했을 때 도착할 자리에
    /// 캐릭터 단색 실루엣을 보여준다. 벽·박스 등에 막히면 막혀서 멈출 자리에 표시.</summary>
    void UpdateMovePreview()
    {
        int dir = 0; float px = 0f;
        bool show = !dead && !sliding && sr != null && sr.sprite != null && gesture != null
                    && gesture.TryGetMovePreview(out dir, out px);
        if (!show)
        {
            if (movePreview != null) movePreview.enabled = false;
            return;
        }
        if (movePreview == null) movePreview = CreateSilhouette("MovePreview");

        float dist = Mathf.Min(MoveDistanceFor(px), FreeDistance(dir));
        movePreview.sprite = sr.sprite;
        movePreview.flipX = dir < 0;
        movePreview.sortingLayerID = sr.sortingLayerID;
        movePreview.sortingOrder = sr.sortingOrder - 1; // 캐릭터와 겹치면 캐릭터가 위
        movePreview.transform.localScale = sr.transform.lossyScale;
        movePreview.transform.rotation = Quaternion.identity;
        movePreview.transform.position = sr.transform.position + Vector3.right * (dir * dist);
        movePreview.color = movePreviewColor;
        movePreview.enabled = true;
    }

    /// <summary>dir 방향으로 걸었을 때 막히기 전까지 갈 수 있는 거리. 몸 앞면에서 발(낮은 턱은 제외)·허리·머리
    /// 높이로 가로 레이를 쏴서 가장 가까운 막힘까지의 거리. (이 씬에선 BoxCastAll이 큰 트리거 안에서 시작하면
    /// 다른 콜라이더를 못 잡아서 RaycastAll 여러 줄로 검사)</summary>
    float FreeDistance(int dir)
    {
        const float stepTolerance = 0.15f; // 이보다 낮은 턱은 걸어서 넘으므로 막힘으로 보지 않음
        Bounds b = col.bounds;
        float x0 = b.center.x + dir * b.extents.x;
        float yLow = b.min.y + stepTolerance, yHigh = b.max.y - 0.05f;
        int mask = Physics2D.GetLayerCollisionMask(gameObject.layer);
        float best = maxMoveDistance;
        for (int i = 0; i < 3; i++)
        {
            float y = Mathf.Lerp(yLow, yHigh, i * 0.5f);
            foreach (var h in Physics2D.RaycastAll(new Vector2(x0, y), new Vector2(dir, 0f), maxMoveDistance, mask))
            {
                var c = h.collider;
                if (c == null || c.isTrigger || c.transform.IsChildOf(transform)) continue;
                if (Physics2D.GetIgnoreCollision(c, col)) continue; // 잡고 있는 박스, 날아가는 나뭇가지 등
                // 몸 앞면이 이미 겹쳐 있는 것(바로 앞에 세운 나뭇가지 등)은 distance 0 → 못 감. 실제로도 밀려나며 막힘.
                if (h.distance < best) best = h.distance;
            }
        }
        return Mathf.Max(0f, best);
    }

    void OnDestroy()
    {
        foreach (var r in new[] { plantPreview, movePreview })
        {
            if (r == null) continue;
            if (r.material != null) Destroy(r.material);
            Destroy(r.gameObject);
        }
    }

    // 매 프레임 현재 움직임 상태를 Animator로 전달 (idle/run/jump/fall 전환)
    void Update()
    {
        UpdateFootsteps(); // 발소리는 애니메이터 없어도 동작
        if (anim == null) return;
        if (dead) return;   // 죽음 애니 재생 중엔 idle/run/jump 전환·컨트롤러 교체를 멈춰 덮이지 않게
        UpdateBranchAnim(); // 나뭇가지 유무에 따라 애니메이션 세트 전환
        float vx = rb.linearVelocity.x;
        // 걷는 중엔 가려는 방향을 본다 — 경사(버튼 등)에 닿는 순간 솔버가 vx를 살짝 반대로 튕겨 한 프레임 뒤돌아보는 것 방지
        if (IsMoving && moveDir != 0f) facing = moveDir < 0f ? -1 : 1;
        else if (Mathf.Abs(vx) > 0.01f) facing = vx < 0f ? -1 : 1;
        // 슬라이드 중엔 눕힌 자세 유지를 위해 이동(Run) 전환을 막음 → Speed 0
        anim.SetFloat("Speed", sliding ? 0f : Mathf.Abs(vx));
        anim.SetFloat("YVel", rb.linearVelocity.y);
        anim.SetFloat("Grounded", IsGrounded() ? 1f : 0f);
        if (sr != null) sr.flipX = facing < 0; // 왼쪽 방향이면 스프라이트 뒤집기

        // 근처 레버/회수 가능한 나뭇가지 중 더 가까운 것에만 'O' 프롬프트 표시
        UpdateInteractPrompts();
    }

    IInteractable lastPromptInteractable; // 지난 프레임에 O를 띄운 상호작용 대상 (겹칠 때 한 번에 하나만 유지)
    static readonly Collider2D[] overlapBuf = new Collider2D[16];

    /// <summary>상호작용 후보(레버·텔레포터 등 IInteractable·회수 가능한 나뭇가지) 중 더 가까운 쪽에만 O 프롬프트를 띄운다.</summary>
    void UpdateInteractPrompts()
    {
        // 잡고 있는 박스가 있으면 그 박스 O를 최우선 표시 (거리 무관, 항상 놓을 수 있게)
        if (PullBox.Current != null)
        {
            if (branch != null) branch.ShowPrompt(false);
            IInteractable held = PullBox.Current;
            if (lastPromptInteractable != null && !ReferenceEquals(lastPromptInteractable, held)) lastPromptInteractable.ShowPrompt(false);
            held.ShowPrompt(true);
            lastPromptInteractable = held;
            return;
        }

        Vector2 p = transform.position;

        bool branchOk = BranchInPickupRange();
        float branchDist = branchOk ? Vector2.Distance(p, branch.transform.position) : float.MaxValue;

        float itDist;
        IInteractable it = NearestInteractableInRange(p, out itDist);

        bool itWins = it != null && itDist <= branchDist;
        bool branchWins = branchOk && !itWins;

        if (branch != null) branch.ShowPrompt(branchWins);

        // 상호작용 대상은 한 번에 하나만 — 이긴 것만 켜고, 직전에 켰던 것은 끈다.
        IInteractable winner = itWins ? it : null;
        if (lastPromptInteractable != null && !ReferenceEquals(lastPromptInteractable, winner)) lastPromptInteractable.ShowPrompt(false);
        if (winner != null) winner.ShowPrompt(true);
        lastPromptInteractable = winner;
    }

    /// <summary>interactRange 안에서 각자의 조건(PlayerInRange)을 만족하는 가장 가까운 IInteractable(레버·텔레포터)을 찾는다.</summary>
    IInteractable NearestInteractableInRange(Vector2 p, out float dist)
    {
        IInteractable best = null; dist = float.MaxValue;
        int n = Physics2D.OverlapCircleNonAlloc(p, interactRange, overlapBuf);
        for (int i = 0; i < n; i++)
        {
            var c = overlapBuf[i];
            if (c == null) continue;
            var it = c.GetComponentInParent<IInteractable>();
            if (it == null || !it.PlayerInRange(p)) continue;
            var mb = it as MonoBehaviour;
            if (mb == null) continue;
            float d = Vector2.Distance(p, mb.transform.position);
            if (d < dist) { dist = d; best = it; }
        }
        return best;
    }

    /// <summary>나뭇가지를 들고 있으면(Held) 22(나뭇가지 있는) 애니메이션, 아니면 기본으로 컨트롤러를 전환.
    /// 상태가 바뀌는 순간에만 교체한다 (매 프레임 교체하면 애니메이터가 리셋됨).</summary>
    void UpdateBranchAnim()
    {
        if (branch == null) return;
        bool held = branch.CurrentState == Branch.State.Held;
        if (lastHeld.HasValue && lastHeld.Value == held) return;
        lastHeld = held;
        var want = held ? withBranchController : baseController;
        if (want != null && anim.runtimeAnimatorController != want)
            anim.runtimeAnimatorController = want;
    }

    /// <summary>박혀 있는 나뭇가지가 회수 가능 거리 안에 있는가?</summary>
    bool BranchInPickupRange()
    {
        return branch != null
            && branch.CurrentState == Branch.State.Stuck
            && Vector2.Distance(transform.position, branch.transform.position) <= pickupRange;
    }

    /// <summary>제스처 인식 결과를 실제 동작으로 실행.</summary>
    public void ExecuteCommand(Command cmd)
    {
        switch (cmd)
        {
            case Command.MoveRight: StartMove(1f); break;
            case Command.MoveLeft:  StartMove(-1f); break;
            case Command.Jump:      TryJump(); break;
            case Command.SlideRight: StartSlide(1); break;
            case Command.SlideLeft:  StartSlide(-1); break;
            case Command.ThrowBranch: ThrowBranch(); break;
            case Command.Interact:    Interact(); break;
            case Command.PlantBranch: PlantBranch(); break;
            case Command.ReturnBranch: ReturnBranch(); break;
            case Command.TeleportBranch: TeleportToBranch(); break;
            case Command.AttackLeft:  Attack(-1); break;
            case Command.AttackRight: Attack(1); break;
        }
    }

    static readonly Collider2D[] hitBuf = new Collider2D[16];

    /// <summary>ㄱ(오른쪽 공격)/ㄱ반대(왼쪽 공격). 나뭇가지를 들고 있을 때만, dir 방향을 바라보며 휘둘러 앞쪽 IDamageable 을 때린다.</summary>
    void Attack(int dir)
    {
        if (dead || attacking) return;
        if (branch == null || branch.CurrentState != Branch.State.Held) return; // 들고 있을 때만
        facing = dir;                          // 공격 방향을 바라봄 (슬라이드처럼)
        if (sr != null) sr.flipX = facing < 0; // 즉시 좌우 반전 반영
        lastAttackTime = Time.time;            // 쿨타임 시작
        StartCoroutine(AttackRoutine());
    }

    IEnumerator AttackRoutine()
    {
        attacking = true;
        SpawnAttackEffect(); // 플레이어는 그대로 두고, 앞(보는 방향)에 타격 이펙트만 생성
        PlaySfx(attackClip); // 휘두르는 소리 (스윙 시작)
        if (attackHitDelay > 0f) yield return new WaitForSeconds(attackHitDelay);
        DoAttackHit();
        float rest = attackCooldown - attackHitDelay;
        if (rest > 0f) yield return new WaitForSeconds(rest);
        attacking = false;
    }

    /// <summary>바라보는 방향 앞 히트박스 안의 IDamageable 을 모두 때린다.</summary>
    void DoAttackHit()
    {
        Vector2 center = (Vector2)transform.position + new Vector2(attackReach * facing, attackHeight);
        int n = Physics2D.OverlapBoxNonAlloc(center, attackHitboxSize, 0f, hitBuf, attackLayers);
        for (int i = 0; i < n; i++)
        {
            var c = hitBuf[i];
            if (c == null) continue;
            if (c.transform.IsChildOf(transform)) continue; // 자기 자신/자식 제외
            var d = c.GetComponentInParent<IDamageable>();
            if (d != null) d.TakeHit(attackDamage, new Vector2(facing, 0f));
        }
    }

    /// <summary>바라보는 방향 앞에 타격 이펙트를 생성한다. 플레이어를 부모로 달아 스윙 동안 따라오게 하고, facing 으로 좌우 반전.</summary>
    void SpawnAttackEffect()
    {
        if (attackEffectPrefab == null) return;
        var fx = Instantiate(attackEffectPrefab);
        fx.transform.SetParent(transform, true); // 플레이어를 따라오되 월드 위치 유지
        fx.transform.position = transform.position + new Vector3(attackReach * facing, attackHeight, 0f);
        fx.transform.rotation = Quaternion.identity;
        // 플레이어 루트가 (1.2, 2.4)처럼 비균일 스케일이라 그대로 상속하면 이펙트가 늘어난다 → 상쇄해 균일 크기로
        Vector3 ls = transform.lossyScale;
        fx.transform.localScale = new Vector3(
            attackEffectScale / Mathf.Max(0.0001f, ls.x),
            attackEffectScale / Mathf.Max(0.0001f, ls.y),
            1f);
        var ae = fx.GetComponent<AttackEffect>();
        if (ae != null) ae.SetFacing(facing);
    }

    /// <summary>S(돌아오기). 던졌거나 박혀 있는 나뭇가지를 거리 무관하게 불러들인다.</summary>
    void ReturnBranch()
    {
        if (branch != null) branch.Return();
    }

    /// <summary>⋉(텔레포트). 박혀/세워져 있는 나뭇가지 위치로 순간이동. 나뭇가지는 그대로 남는다.
    /// 도착 시 발이 나뭇가지 밑동에 서도록 y를 보정하고 속도는 0으로 리셋한다.</summary>
    bool branchTeleporting;

    void TeleportToBranch()
    {
        if (branch == null || branch.CurrentState != Branch.State.Stuck) return; // 놓인 나뭇가지가 있을 때만
        if (branchTeleporting) return;   // 이미 대기 중이면 무시
        StartCoroutine(TeleportToBranchRoutine());
    }

    IEnumerator TeleportToBranchRoutine()
    {
        branchTeleporting = true;
        yield return new WaitForSeconds(branchTeleportDelay); // 입력 후 지연

        // 지연 뒤에도 여전히 박혀 있고 살아 있을 때만 이동
        if (branch != null && branch.CurrentState == Branch.State.Stuck && !dead)
        {
            if (sliding) EndSlide();        // 슬라이드 중이면 원래 자세/콜라이더로 복구 후 이동
            moveRemaining = 0f;             // 진행 중이던 이동 취소
            float baseY = branch.transform.position.y - branch.WorldHalfHeight; // 나뭇가지 밑동 높이
            float feetToPivot = transform.position.y - col.bounds.min.y;        // 피벗~발 거리(위치 무관)
            transform.position = new Vector3(
                branch.transform.position.x, baseY + feetToPivot, transform.position.z);
            rb.linearVelocity = Vector2.zero;
            PlaySfx(branchTeleportClip); // 나뭇가지로 텔레포트 소리
        }
        branchTeleporting = false;
    }

    /// <summary>바라보는 방향 앞 바닥에 나뭇가지를 똑바로 세운다 (발판/장애물).</summary>
    void PlantBranch()
    {
        if (branch == null || !branch.CanThrow) return; // 회수 전까지 재사용 불가
        Vector2 pos;
        if (!TryGetPlantSpot(out pos)) return; // 밑에 바닥 없음 → 세우기 취소
        branch.Plant(pos);
    }

    /// <summary>9로 세울 자리 계산 (실제 세우기와 미리보기가 같은 계산을 쓴다).
    /// 세울 자리 밑에 바닥이 있을 때만 true. 발 높이 기준으로 위 0.6~아래 1.0 범위에서
    /// 바닥(groundLayer) 윗면을 찾아 거기에 밑동을 맞춘다. 바닥이 없으면(공중/앞이 낭떠러지)
    /// false → 하늘에 박히는 것 방지. false일 때 pos는 발 높이 기준 자리(미리보기용).</summary>
    bool TryGetPlantSpot(out Vector2 pos)
    {
        float x = transform.position.x + plantDistance * facing;
        float feetY = col.bounds.min.y;
        RaycastHit2D g = Physics2D.Raycast(new Vector2(x, feetY + 0.6f), Vector2.down, 0.6f + 1.0f, groundLayer);
        float halfH = branch != null ? branch.WorldHalfHeight : 0.5f;
        if (g.collider == null) { pos = new Vector2(x, feetY + halfH); return false; }
        pos = new Vector2(x, g.point.y + halfH); // 찾은 바닥 윗면에 밑동을 맞춰 세움
        return true;
    }

    /// <summary>O(상호작용). 근처의 레버를 토글하거나 박힌 나뭇가지를 회수한다.
    /// 레버와 회수 가능한 나뭇가지가 둘 다 범위 안이면 더 가까운 쪽을 작동.</summary>
    void Interact()
    {
        // 잡고 있는 박스가 있으면 O는 그 박스를 먼저 놓는다 (한 번에 하나만, 다른 상호작용보다 우선)
        if (PullBox.Current != null) { PullBox.Current.Interact(); return; }

        Vector2 p = transform.position;

        bool branchOk = BranchInPickupRange();
        float branchDist = branchOk ? Vector2.Distance(p, branch.transform.position) : float.MaxValue;

        float itDist;
        IInteractable it = NearestInteractableInRange(p, out itDist);

        // 프롬프트와 동일한 판정: 더 가까운 쪽 작동 (동률이면 상호작용 대상)
        if (it != null && itDist <= branchDist) it.Interact();
        else if (branchOk) branch.Recover();
    }

    /// <summary>바라보는 방향의 위 45°로 나뭇가지를 던진다.</summary>
    void ThrowBranch()
    {
        if (branch == null || !branch.CanThrow) return; // 회수 전까지 재사용 불가
        Vector2 spawn = (Vector2)transform.position + new Vector2(throwOffset.x * facing, throwOffset.y);
        // 위 45°: (cos45, sin45) = (0.707, 0.707), 좌우는 facing으로 반전
        Vector2 vel = throwSpeed * new Vector2(0.70710678f * facing, 0.70710678f);
        branch.Throw(spawn, vel);
        PlaySfx(throwClip);
    }

    /// <summary>그은 가로 길이(px)에 비례해 이동한다. 길게 그을수록 멀리 간다.</summary>
    /// <summary>그은 가로 길이(px) → 이동 거리(유닛). 실제 이동과 미리보기가 같은 계산을 쓴다.</summary>
    float MoveDistanceFor(float pixelLength)
    {
        return Mathf.Clamp(pixelLength / 100f * unitsPer100px, minMoveDistance, maxMoveDistance);
    }

    public void StartMoveByLength(int dir, float pixelLength)
    {
        float dist = MoveDistanceFor(pixelLength);
        moveDir = dir;
        moveRemaining = dist;
    }

    /// <summary>길이 정보 없이 부르는 기본 이동(기존 ExecuteCommand 경로용).</summary>
    void StartMove(float dir)
    {
        moveDir = dir;
        moveRemaining = defaultMoveDistance;
    }

    void TryJump()
    {
        // 접지 중이거나 코요테 타임 내면 즉시 점프, 아니면 버퍼(착지 직후 FixedUpdate가 실행)
        if (IsGrounded() || Time.time - lastGroundedTime <= coyoteTime) DoJump();
        else jumpBufferedTime = Time.time;
    }

    /// <summary>실제 점프 실행. 코요테·버퍼를 소진해 공중 재점프를 막는다.</summary>
    void DoJump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpVelocity);
        lastJumpTime = Time.time;        // 점프 직후 수직 속도 제거(솟구침 방지) 로직이 점프를 죽이지 않게 시간 보호
        lastGroundedTime = -999f;        // 코요테 소진 (더블 점프 방지)
        jumpBufferedTime = -999f;        // 버퍼 소진
        PlaySfx(jumpClip);
    }

    /// <summary>바닥에서 이동 중이면 일정 간격으로 발소리를 재생. 멈추면 타이머를 리셋해 다음 첫 발소리가 바로 나게 한다.</summary>
    void UpdateFootsteps()
    {
        bool running = !sliding && IsGrounded() && Mathf.Abs(rb.linearVelocity.x) > 0.1f;
        if (running)
        {
            footstepTimer -= Time.deltaTime;
            if (footstepTimer <= 0f)
            {
                if (footstepClip != null && footSrc != null)
                {
                    footSrc.clip = footstepClip;
                    footSrc.volume = sfxVolume;
                    footSrc.Play(); // Play는 도중에 Stop 가능 (PlayOneShot과 달리)
                }
                footstepTimer = footstepInterval;
            }
        }
        else
        {
            footstepTimer = 0f;
            if (footSrc != null && footSrc.isPlaying) footSrc.Stop(); // 멈추는 순간 발소리 즉시 정지
        }
    }

    /// <summary>효과음 1회 재생 (클립이 없으면 무시).</summary>
    void PlaySfx(AudioClip clip)
    {
        if (clip != null && sfx != null) sfx.PlayOneShot(clip, sfxVolume);
    }

    /// <summary>박스(PullBox)를 잡고 끄는 동안 이동 속도를 늦춘다 → 밀기/당기기가 같은 느린 속도로 일정.</summary>
    /// <summary>목표 x까지 걸어간다(일반 이동과 같은 걷기/턱 오르기). 박스를 멀리서 잡았을 때 옆에 붙는 용도.</summary>
    public void WalkToX(float x)
    {
        if (sliding || dead) return;
        float dx = x - rb.position.x;
        if (Mathf.Abs(dx) < 0.02f) return;
        moveDir = Mathf.Sign(dx);
        moveRemaining = Mathf.Abs(dx);
    }

    /// <summary>진행 중인 이동 거리가 남아 있는가? (걸어서 붙는 중인지 판단용)</summary>
    public bool IsMoving => moveRemaining > 0f;

    public void SetCarrying(bool on) { moveSpeedMul = on ? Mathf.Max(0.05f, carrySpeedScale) : 1f; }

    /// <summary>스킬 슬롯(스킬바 UI용). 아이콘 순서와 무관하게 기능으로 구분.</summary>
    public enum SkillId { Return, Plant, Attack, Teleport, Slide, Interact, Throw, Boss }

    /// <summary>스킬의 '쿨타임/사용불가' 정도를 0~1 로 반환 (1=지금 못 씀/막 사용함, 0=사용 가능).
    /// 시간 쿨타임(공격·슬라이드)은 서서히 줄고, 상태 기반(나뭇가지 스킬)은 1/0 으로 딱딱 바뀐다. SkillBar 가 오버레이 표시에 사용.</summary>
    public float SkillCooldown01(SkillId id)
    {
        switch (id)
        {
            case SkillId.Throw:
            case SkillId.Plant:    // 손에 들고 있을 때만 던지기/세우기 가능
                return (branch != null && branch.CanThrow) ? 0f : 1f;
            case SkillId.Return:   // 날아가는 중이거나 박혀 있을 때 회수 가능
                return (branch != null && (branch.CurrentState == Branch.State.Flying || branch.CurrentState == Branch.State.Stuck)) ? 0f : 1f;
            case SkillId.Teleport: // 박혀 있을 때만 텔레포트 가능
                return (branch != null && branch.CurrentState == Branch.State.Stuck) ? 0f : 1f;
            case SkillId.Attack:   // 나뭇가지 든 상태 + 시간 쿨타임
                if (branch == null || branch.CurrentState != Branch.State.Held) return 1f;
                return Mathf.Clamp01((attackCooldown - (Time.time - lastAttackTime)) / Mathf.Max(0.0001f, attackCooldown));
            case SkillId.Slide:    // 슬라이드 중이면 꽉, 아니면 시간 쿨타임
                if (sliding) return 1f;
                return Mathf.Clamp01((slideCooldown - (Time.time - lastSlideTime)) / Mathf.Max(0.0001f, slideCooldown));
            case SkillId.Interact: return 0f; // 상호작용은 항상 가능
            case SkillId.Boss:     return 1f; // 아직 잠김
        }
        return 0f;
    }

    /// <summary>진행 중이던 이동·슬라이드를 모두 취소하고 속도를 0으로 만든다. (리스폰 등에서 사용)</summary>
    /// <summary>바라보는 방향을 바꾼다 (+1 오른쪽, -1 왼쪽). 연출에서 뒤돌아보기 등에 사용.</summary>
    public void Face(int dir)
    {
        facing = dir < 0 ? -1 : 1;
        if (sr != null) sr.flipX = facing < 0;
    }

    public void ResetMotion()
    {
        moveRemaining = 0f;
        stepClimbing = false;
        if (sliding) EndSlide();
        rb.linearVelocity = Vector2.zero;
    }

    /// <summary>죽음 애니메이션 재생 시작. dead 동안 Update의 상태 전환이 멈춰 죽음 클립이 덮이지 않는다.</summary>
    public void PlayDeath()
    {
        if (!dead) PlaySfx(deathClip); // 죽는 소리 (죽음 진입 시 한 번만)
        dead = true;
        if (anim != null) anim.Play("메크로Death", 0, 0f);
        if (hideAfterDeathCo != null) StopCoroutine(hideAfterDeathCo);
        hideAfterDeathCo = StartCoroutine(HideAfterDeathAnim());
    }

    Coroutine hideAfterDeathCo;

    // 죽음 클립 마지막 프레임(작은 점)이 부활 전까지 남아 보이지 않게, 클립이 끝나면 캐릭터 그림을 숨긴다
    IEnumerator HideAfterDeathAnim()
    {
        // 죽음 클립이 끝까지 재생될 때까지 대기 (안전장치: 최대 2초)
        float limit = 2f;
        while (dead && anim != null && limit > 0f)
        {
            var st = anim.GetCurrentAnimatorStateInfo(0);
            if (st.IsName("메크로Death") && st.normalizedTime >= 1f) break;
            limit -= Time.deltaTime;
            yield return null;
        }
        if (dead && sr != null) sr.enabled = false;
        hideAfterDeathCo = null;
    }

    /// <summary>죽음 상태 해제 → 기본(Idle)으로 복귀. 이후 Update가 다시 idle/run/jump 전환을 이어간다.</summary>
    public void EndDeath()
    {
        dead = false;
        if (hideAfterDeathCo != null) { StopCoroutine(hideAfterDeathCo); hideAfterDeathCo = null; }
        if (sr != null) sr.enabled = true;
        if (anim != null) anim.Play("Idle", 0, 0f);
    }

    // 슬라이드 시작: 지정 방향(dir: +1 오른쪽 / -1 왼쪽)으로 캐릭터를 눕히고 미끄러짐
    void StartSlide(int dir)
    {
        if (sliding || Time.time - lastSlideTime < slideCooldown) return; // 슬라이드 중이거나 쿨타임이면 무시
        slideDir = dir;
        facing = dir; // 슬라이드 방향으로 바라봄(스프라이트 반전 반영)
        sliding = true;
        slideTimer = slideDuration;
        slideExtraTimer = 0f;
        slideStopped = false;
        moveRemaining = 0f; // 진행 중이던 일반 이동은 취소
        if (anim != null) anim.SetBool("Sliding", true); // 슬라이드 애니메이션 재생 (90° 회전 대신)
        // 슬라이드 소리: 시작 오프셋부터 재생, 슬라이드 끝나면 EndSlide 에서 멈춤
        if (slideClip != null && slideSfx != null)
        {
            slideSfx.clip = slideClip;
            slideSfx.volume = sfxVolume;
            slideSfx.time = Mathf.Clamp(slideClipStartTime, 0f, Mathf.Max(0f, slideClip.length - 0.01f));
            slideSfx.Play();
        }

        // 콜라이더를 납작하게: 높이를 줄이되 발(하단)은 땅에 그대로 두도록 오프셋 보정
        float newH = origColSize.y * slideHeightScale;
        col.size = new Vector2(origColSize.x, newH);
        col.offset = new Vector2(origColOffset.x, origColOffset.y - (origColSize.y - newH) * 0.5f);
    }

    static readonly Collider2D[] standBuf = new Collider2D[16];

    /// <summary>지금 자리에서 원래 키(서 있는 콜라이더)로 일어설 공간이 있는가?
    /// 지형(groundLayer)이나 박스(PullBox)가 서 있는 몸 영역에 겹치면 false.</summary>
    bool CanStandUp()
    {
        Vector2 center = transform.TransformPoint(origColOffset);
        Vector3 ls = transform.lossyScale;
        // 바닥/옆벽에 닿아 있는 건 무시하도록 살짝 줄이고, 아래쪽은 발이 바닥에 걸리지 않게 띄운다
        Vector2 size = new Vector2(Mathf.Abs(origColSize.x * ls.x) * 0.9f, Mathf.Abs(origColSize.y * ls.y) - 0.1f);
        center.y += 0.05f;
        int n = Physics2D.OverlapBoxNonAlloc(center, size, 0f, standBuf, Physics2D.AllLayers);
        for (int i = 0; i < n; i++)
        {
            var c = standBuf[i];
            if (c == null || c.isTrigger) continue;
            if (c.transform.IsChildOf(transform)) continue;
            if (((1 << c.gameObject.layer) & groundLayer) != 0) return false;
            if (c.GetComponentInParent<PullBox>() != null) return false;
        }
        return true;
    }

    void EndSlide()
    {
        sliding = false;
        lastSlideTime = Time.time; // 여기서부터 슬라이드 쿨타임 시작
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); // 수평 정지
        if (anim != null) anim.SetBool("Sliding", false); // 슬라이드 애니메이션 종료
        if (slideSfx != null && slideSfx.isPlaying) slideSfx.Stop(); // 슬라이드 끝나면 소리도 멈춤
        if (visual != null) visual.localEulerAngles = Vector3.zero; // 혹시 남은 회전 초기화(안전)
        // 콜라이더 원래대로 복구 (FixedUpdate는 CanStandUp일 때만 여기로 옴 — 천장 밑 끼임 방지)
        col.size = origColSize;
        col.offset = origColOffset;
    }

    void FixedUpdate()
    {
        bool grounded = IsGrounded();
        // 착지 소리: 공중에 좀 있다가(직전 접지로부터 0.12초 초과) 바닥에 닿는 순간 1회 (미세 지터 오재생 방지)
        if (grounded && !wasGrounded && Time.time - lastGroundedTime > 0.12f) PlaySfx(landClip);
        wasGrounded = grounded;

        // 코요테/버퍼용 접지 시각 갱신 + 착지 직후 버퍼된 점프 실행
        if (grounded)
        {
            lastGroundedTime = Time.time;
            if (Time.time - jumpBufferedTime <= jumpBufferTime) DoJump();
        }

        if (sliding)
        {
            if (slideTimer > 0f)
            {
                rb.linearVelocity = new Vector2(slideDir * slideSpeed, rb.linearVelocity.y);
                slideTimer -= Time.fixedDeltaTime;
                lastSlideX = rb.position.x;
                return; // 슬라이드 중엔 일반 이동 무시
            }
            if (CanStandUp()) { EndSlide(); return; }

            // 머리 위가 막혀 있음(낮은 통로) — 여기서 일어서면 천장에 끼므로 엎드린 채 앞으로 계속 미끄러져 빠져나온다.
            // 엎드려 멈춘 상태에서 이동 명령이 오면 그 방향으로 다시 엎드려 미끄러진다.
            if (moveRemaining > 0f)
            {
                slideDir = moveDir > 0f ? 1 : -1;
                facing = slideDir;
                moveRemaining = 0f;
                slideStopped = false;
                slideExtraTimer = 0f;
                lastSlideX = rb.position.x;
            }
            else if (!slideStopped)
            {
                // 앞이 벽이라 거의 못 움직였거나 너무 오래 미끄러지면 그 자리에 엎드려 멈춤
                bool blocked = slideExtraTimer > 0f && Mathf.Abs(rb.position.x - lastSlideX) < 0.005f;
                if (blocked || slideExtraTimer > slideMaxExtraTime) slideStopped = true;
            }
            slideExtraTimer += Time.fixedDeltaTime;
            lastSlideX = rb.position.x;
            rb.linearVelocity = new Vector2(slideStopped ? 0f : slideDir * slideSpeed, rb.linearVelocity.y);
            return;
        }

        if (stopMoveNext)
        {
            stopMoveNext = false;
            if (moveRemaining <= 0f) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); // 이동 끝 → 수평 정지
        }
        else if (moveRemaining <= 0f)
        {
            // 이동 명령 없이 서 있으면 수평 정지 — 마찰 0이라 경사(버튼 등)에서 멈추면 중력으로 미끄러져
            // 평지까지 계속 밀려가던 것 방지
            bool onSlope = StandingOnSomething(out Vector2 n) && n.y < 0.999f;
            if (grounded || onSlope) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            if (onSlope)
            {
                // 경사면 방향 중력 성분을 상쇄 (속도만 0으로 하면 매 스텝 중력이 다시 경사 아래로 조금씩 밀어냄)
                Vector2 g = Physics2D.gravity * rb.gravityScale;
                Vector2 tangent = new Vector2(n.y, -n.x);
                rb.AddForce(-Vector2.Dot(g, tangent) * tangent * rb.mass);
            }
        }

        if (moveRemaining > 0f)
        {
            int dir = (int)Mathf.Sign(moveDir);
            float ms = moveSpeed * moveSpeedMul; // 박스 끌 때 느려진 실효 이동 속도
            float stepRise = StepRiseAhead(dir);
            // 점프로 위로 솟는 중이면 벽을 타고 오르지 않게 막는다(그 외 걷기/하강 중엔 낮은 턱을 오름).
            bool rising = rb.linearVelocity.y > 0.5f;
            if (stepRise > 0f && (IsGrounded() || stepClimbing || !rising))
            {
                // 앞에 올라설 수 있는 낮은 턱(계단)이 있다. 톱니 계단을 수평 속도로 밀면 물리 솔버가 45°로 튕겨
                // (점프처럼 솟구침) 버리므로, 속도로 오르지 않고 위치를 조금씩(최대 stepClimbSpeed*dt) 직접 올려
                // 계단 위로 걸어 올라선다. 벽 감지와 무관하게 '올라설 턱이 있으면' 능동적으로 오른다.
                stepClimbing = true;
                float up = Mathf.Min(stepRise, stepClimbSpeed * Time.fixedDeltaTime);
                float fwd = ms * Time.fixedDeltaTime;
                // 위+앞을 모두 위치로 직접 이동시킨다. 앞쪽을 속도로 밀면 턱(계단) 모서리에
                // 몸 아래 모서리가 걸릴 때 물리 솔버가 수평 속도를 막아버려(vel.x=0) 위로만 떴다
                // 도로 떨어지며 이음새에 끼인다. 위치로 옮기면 모서리에 막히지 않고 확실히 올라선다.
                rb.position += new Vector2(moveDir * fwd, up);
                rb.linearVelocity = Vector2.zero;
                moveRemaining -= fwd; // 거리 예산 소진
                if (moveRemaining <= 0f) stepClimbing = false;
            }
            else if (WallAhead(dir))
            {
                // 올라설 수 없는(maxStepHeight보다 높은) 진짜 벽 → 남은 이동 예산 취소(잔여 움직임 제거)
                stepClimbing = false;
                moveRemaining = 0f;
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
            else
            {
                stepClimbing = false; // 앞이 트임 → 계단 오르기 종료
                // 수평 속도만 준다(수직은 유지해 중력/낙하 반영). 오르막(계단)은 위 step-up 분기가 처리.
                // 마지막 스텝은 남은 거리만큼만 가는 속도로 → 그은 길이(=미리보기 실루엣)와 정확히 같은 거리를 간다.
                // (예전엔 마지막 스텝에서 예산을 다 쓰자마자 같은 프레임에 속도를 0으로 만들어 한 스텝만큼 덜 갔다)
                float step = Mathf.Min(ms * Time.fixedDeltaTime, moveRemaining);
                rb.linearVelocity = new Vector2(moveDir * step / Time.fixedDeltaTime, rb.linearVelocity.y);
                moveRemaining -= step; // 거리 예산 소진
                if (moveRemaining <= 0f) stopMoveNext = true; // 이 스텝 이동이 반영된 뒤 다음 FixedUpdate에서 수평 정지
            }
        }

        // 걷는 도중 톱니(계단) 지형에서 물리 솔버가 캐릭터를 램프처럼 위로 튕겨 점프같이 솟구치는 것을 막는다.
        // 바닥에 있고(=걷는 중) 최근에 점프하지 않았다면, 솔버가 만든 '위로 향한' 속도를 매 프레임 제거한다.
        // (속도가 누적되지 못해, 계단을 다 오른 순간 잔여 상승 없이 바로 멈춘다. 점프는 lastJumpFrame로 보호.)
        if (rb.linearVelocity.y > 0f && Time.time - lastJumpTime > 0.12f && IsGrounded())
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
    }

    static readonly Collider2D[] groundBuf = new Collider2D[8];
    static readonly ContactPoint2D[] contactBuf = new ContactPoint2D[8];

    /// <summary>Ground 레이어가 아니어도(버튼 경사 등) 발밑에서 위로 받쳐주는 접촉이 있는가?</summary>
    bool StandingOnSomething(out Vector2 normal)
    {
        normal = Vector2.up;
        bool found = false;
        int n = rb.GetContacts(contactBuf);
        for (int i = 0; i < n; i++)
        {
            if (contactBuf[i].collider.isTrigger || contactBuf[i].normal.y <= 0.5f) continue;
            // 가장 평평한 접촉면 기준 (평지에도 닿아 있으면 경사 보정 안 함)
            if (!found || contactBuf[i].normal.y > normal.y) normal = contactBuf[i].normal;
            found = true;
        }
        return found;
    }

    /// <summary>발밑(콜라이더 하단)에 얇은 박스를 겹쳐 Ground 레이어 콜라이더가 있으면 바닥으로 판정.
    /// 바닥에 딱 붙어(살짝 파묻혀) 있을 때 BoxCast가 자기 콜라이더에 걸려 오작동하므로 OverlapBox를 사용.
    /// 원웨이 플랫폼(PlatformEffector2D)은 '위로 상승 중(vy>0)'엔 바닥으로 치지 않는다 — 아래에서 뚫고
    /// 올라가는 동안 발이 윗면에 닿아도 접지로 보면, 안티바운스가 상승 속도를 죽여 윗면 바로 밑에서
    /// 걸려버리기 때문. 떨어지거나 서 있을 때만(vy≤0) 바닥으로 인정 → 착지/점프는 정상, 통과는 안 걸림.
    /// (정지 중 미세 지터로 한두 프레임 접지를 놓쳐도 코요테 타임이 커버)</summary>
    bool IsGrounded()
    {
        Bounds b = col.bounds;
        Vector2 size = new Vector2(b.size.x * 0.9f, groundCheckDistance * 2f);
        Vector2 center = new Vector2(b.center.x, b.min.y); // 발밑 중심
        int n = Physics2D.OverlapBoxNonAlloc(center, size, 0f, groundBuf, groundLayer);
        bool rising = rb.linearVelocity.y > 0.05f;
        for (int i = 0; i < n; i++)
        {
            var c = groundBuf[i];
            if (c == null) continue;
            if (rising)
            {
                var eff = c.GetComponent<PlatformEffector2D>();
                if (eff != null && eff.useOneWay) continue; // 상승 중엔 원웨이 통과(바닥 아님)
            }
            return true;
        }
        return false;
    }

    /// <summary>진행 방향 바로 앞의 막힌 면이 '올라설 수 있는 낮은 계단(턱)'이면 그 높이(발 위로 얼마나 높은지, units)를 반환. 아니면 -1.
    /// 콜라이더 옆면 바로 바깥에서, 발보다 maxStepHeight 위 지점에서 아래로 레이를 쏴 윗면 높이를 잰다.
    /// 윗면이 발보다 0~maxStepHeight 만큼만 높으면 계단. 그보다 높으면 진짜 벽(-1).</summary>
    float StepRiseAhead(int dir)
    {
        Bounds b = col.bounds;
        float feetY = b.min.y;
        // 전방 탐지 거리를 벽 감지(WallAhead reach 0.12)보다 크게 둔다. 그래야 벽으로 막히기 전에
        // '올라설 수 있는 낮은 턱'을 먼저 인식해 계단 오르기가 우선된다(0.06이면 벽 감지가 먼저 걸려
        // 턱 0.06~0.12 앞에서 멈춰 못 올라가는 데드존이 생김).
        float aheadX = (dir > 0 ? b.max.x : b.min.x) + dir * 0.14f;
        Vector2 top = new Vector2(aheadX, feetY + maxStepHeight + 0.05f);
        RaycastHit2D d = Physics2D.Raycast(top, Vector2.down, maxStepHeight + 0.1f, groundLayer);
        if (d.collider == null) return -1f;        // 앞에 올라설 바닥 없음(낭떠러지/트인 공간)
        float rise = d.point.y - feetY;
        return (rise > 0.02f && rise <= maxStepHeight) ? rise : -1f; // 계단 높이 범위면 그 높이, 아니면 -1
    }

    /// <summary>진행 방향(dir: +1 오른쪽 / -1 왼쪽)에 '걸어 오를 수 없는' 벽이 있는지 검사.
    /// 콜라이더 옆으로 여러 높이에서 앞으로 레이를 쏴, 맞은 면의 각도가 maxSlopeAngle보다 크면(가파른 벽) 막는다.
    /// 걸을 수 있는 경사면(≤ maxSlopeAngle)은 벽으로 치지 않아 그대로 타고 올라간다.
    /// 레이 시작점은 콜라이더 안쪽(플레이어는 Ground 레이어가 아니라 자기 자신엔 안 걸림)이라 지형 면 법선을 깨끗이 얻는다.</summary>
    bool WallAhead(int dir)
    {
        Bounds b = col.bounds;
        float edgeX = dir > 0 ? b.max.x : b.min.x;
        float startInset = 0.05f;   // 시작점을 안쪽으로 살짝
        float reach = 0.12f;        // 옆면 바깥 검사 거리
        float startX = edgeX - dir * startInset;
        Vector2 castDir = new Vector2(dir, 0f);
        float dist = startInset + reach;
        // 발 바로 위 / 중간 / 머리 밑 세 높이에서 검사 (한 곳이라도 가파른 면이면 벽).
        // 가장 낮은 레이는 발 바로 위에 둔다 → 계단을 오를 때 발이 턱을 '완전히' 넘을 때까지 벽 판정이 유지돼야
        // 오르기가 중간에 끊겨 도로 떨어지지 않는다.
        float[] hs = { b.min.y + 0.05f, b.center.y, b.max.y - 0.1f };
        for (int i = 0; i < hs.Length; i++)
        {
            RaycastHit2D hit = Physics2D.Raycast(new Vector2(startX, hs[i]), castDir, dist, groundLayer);
            if (hit.collider == null || hit.normal == Vector2.zero) continue; // 안 맞거나 겹침 글리치 → 통과
            if (Vector2.Angle(hit.normal, Vector2.up) > maxSlopeAngle) return true; // 가파른 벽
        }
        return false;
    }

    /// <summary>Scene 뷰에 발밑 바닥 판정 박스를 그려 튜닝을 돕는다.
    /// 플레이 중: 초록=바닥 감지(점프 가능), 빨강=미감지. 에디터: 노랑.</summary>
    void OnDrawGizmos()
    {
        var c = GetComponent<BoxCollider2D>();
        if (c == null) return;
        Bounds b = c.bounds;
        Vector2 size = new Vector2(b.size.x * 0.9f, groundCheckDistance * 2f);
        Vector2 center = new Vector2(b.center.x, b.min.y);

        bool grounded = Application.isPlaying
            && Physics2D.OverlapBox(center, size, 0f, groundLayer) != null;
        Gizmos.color = Application.isPlaying ? (grounded ? Color.green : Color.red) : Color.yellow;
        Gizmos.DrawWireCube(center, size);
    }

#if UNITY_EDITOR
    /// <summary>선택 시 공격 히트박스를 그려 튜닝을 돕는다(빨강). 에디터에선 오른쪽 기준.</summary>
    void OnDrawGizmosSelected()
    {
        Vector2 attackCenter = (Vector2)transform.position + new Vector2(attackReach * facing, attackHeight);
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
        Gizmos.DrawWireCube(attackCenter, attackHitboxSize);
    }
#endif
}
