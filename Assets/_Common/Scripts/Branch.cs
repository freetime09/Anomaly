using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 나뭇가지(임시 오브젝트). 플레이어의 핵심 도구로, 상태에 따라 동작이 바뀐다.
///  - Held  : 들고 있음(정지). 물리 영향 없음.
///  - Flying : 던져져 포물선으로 날아가는 중(중력 O).
///  - Stuck  : 바닥/벽에 박혀 그 자리에 고정됨. 이후 상호작용/텔레포트 대상이 된다.
///  - Returning : 플레이어에게 부메랑처럼 날아 돌아오는 중. 도착하면 회수(Held).
/// 던지기(N) → 세우기(9) → 돌아오기(S) → 텔레포트(⋉)가 모두 이 상태머신을 공유한다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class Branch : MonoBehaviour
{
    public enum State { Held, Flying, Stuck, Returning }

    [Tooltip("현재 나뭇가지 상태 (읽기용)")]
    public State state = State.Held;

    [Header("던지기 (N)")]
    [Tooltip("날아가는 동안 회전(텀블링) 속도(도/초). 0이면 회전 없이 진행 방향으로 정렬만. 부호 바꾸면 반대로 회전")]
    public float spinSpeed = 540f;

    [Header("돌아오기 (S)")]
    [Tooltip("돌아올 때 플레이어에게 날아오는 속도 (units/초)")]
    public float returnSpeed = 18f;
    [Tooltip("플레이어 중심에 이 거리(units)까지 오면 회수 완료")]
    public float returnCatchDistance = 0.2f;
    [Tooltip("돌아올 때 벽 앞에서 멈추는 여유 간격(units). 벽에 살짝 안 파묻히게 함")]
    public float returnWallSkin = 0.1f;
    [Tooltip("벽을 타고 미끄러질 때 속도 비율(0~1). 낮을수록 벽에선 덜 미끄럽고 천천히 이동")]
    [Range(0f, 1f)]
    public float returnSlideFactor = 0.4f;

    [Header("회수 프롬프트 (근처에서 뜨는 O)")]
    [Tooltip("나뭇가지 중심에서 위로 얼마나 띄울지 (units)")]
    public float promptHeight = 0.7f;
    [Tooltip("프롬프트 O 색상")]
    public Color promptColor = Color.white;

    [Header("사운드")]
    [Tooltip("날아가는 동안 반복 재생되는 소리(loop). 던지면 시작, 박히거나 회수하면 멈춤")]
    public AudioClip flyingClip;
    [Tooltip("날아가다 벽/바닥에 부딪히는 순간 소리")]
    public AudioClip wallHitClip;
    [Tooltip("바닥에 꽂혀 '박히는' 순간 소리 (던져 박힐 때 / 9로 세울 때)")]
    public AudioClip stickClip;
    [Tooltip("돌아오는(S) 동안 반복 재생되는 소리(loop). 회수되면 멈춤")]
    public AudioClip returnClip;
    [Tooltip("나뭇가지를 잡을(회수) 때 소리 (부메랑으로 돌아와 잡히거나 O로 회수할 때). 시작 시엔 안 울림")]
    public AudioClip catchClip;
    [Tooltip("이 속도(units/초) 이상으로 부딪힐 때만 부딪힘 소리 재생 (약한 접촉 무시)")]
    public float wallHitMinSpeed = 2f;
    [Range(0f, 1f)]
    [Tooltip("나뭇가지 효과음 볼륨")]
    public float sfxVolume = 1f;

    [Header("이벤트")]
    [Tooltip("던져지는 순간(Throw 호출 시) 발생합니다. Inspector에서 원하는 함수를 연결하세요.\n" +
             "예: TimelineTrigger의 TriggerEvent(string)를 연결하고 파라미터에 \"BranchThrown\"을 입력하면,\n" +
             "던질 때마다 그 이름의 이벤트가 발동됩니다.")]
    public UnityEvent OnThrown;

    [Tooltip("나뭇가지가 '던져져서' 바닥에 박히는 순간(Stick)에만 발생합니다. 9로 세워 꽂는 경우(Plant)에는 발동하지 않습니다.")]
    public UnityEvent OnStuck;

    Rigidbody2D rb;
    BoxCollider2D col;
    SpriteRenderer sr;
    AudioSource loopSrc;    // 날아가는 소리(loop) 전용
    AudioSource sfxSrc;     // 부딪힘 소리(oneshot) 전용
    TrailRenderer trail;    // 날아가는 동안 잔상 궤적
    Collider2D ownerCol;    // 플레이어 콜라이더 (날아갈 때만 충돌 무시)
    int terrainMask;        // 돌아올 때 막히는 지형 레이어 (나뭇가지 자신의 레이어 = Ground)

    GameObject prompt;      // 근처에서 뜨는 'O' 표시
    SpriteRenderer promptSr;

    public State CurrentState => state;
    /// <summary>지금 던지거나 세울 수 있는가? (손에 들고 있을 때만 가능)</summary>
    public bool CanThrow => state == State.Held;
    /// <summary>스케일 반영한 실제 반높이 (세울 때 바닥 맞춤용). 콜라이더가 꺼져 있어도 계산됨.</summary>
    public float WorldHalfHeight =>
        (sr != null && sr.sprite != null) ? sr.sprite.bounds.extents.y * Mathf.Abs(transform.lossyScale.y) : 0.5f;

    /// <summary>플레이어 콜라이더를 등록한다. 날아가는 동안만 이 콜라이더와 충돌을 무시한다.</summary>
    public void SetOwner(Collider2D playerCol) { ownerCol = playerCol; }

    void SetPassThroughOwner(bool ignore)
    {
        if (ownerCol != null && col != null) Physics2D.IgnoreCollision(col, ownerCol, ignore);
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // 빠르게 날아갈 때 얇은 지형을 뚫고 박히는 것 방지
        col = GetComponent<BoxCollider2D>();
        sr = GetComponent<SpriteRenderer>();
        terrainMask = 1 << gameObject.layer; // Ground 레이어만 막음 (플레이어는 다른 레이어라 안 막힘)
        loopSrc = gameObject.AddComponent<AudioSource>(); loopSrc.playOnAwake = false; loopSrc.loop = true;
        sfxSrc = gameObject.AddComponent<AudioSource>(); sfxSrc.playOnAwake = false;
        trail = GetComponent<TrailRenderer>();
        if (trail != null) { trail.emitting = false; trail.Clear(); }
        CreatePrompt();
        SetHeld(); // 시작 시엔 손에 든(숨김) 상태
    }

    /// <summary>지정 위치에서 지정 속도로 던진다. 중력을 받아 포물선으로 날아간다.
    /// 손에 들고 있을 때(Held)만 던져지며, 회수 전까지는 다시 던질 수 없다.</summary>
    public void Throw(Vector2 position, Vector2 velocity)
    {
        if (state != State.Held) return; // 던진 뒤엔 회수(S) 전까지 재사용 불가
        if (sr != null) sr.enabled = true;   // 던지는 순간 보이게
        col.enabled = true;
        SetPassThroughOwner(true);           // 날아가는 동안 플레이어 몸 통과
        transform.position = position;
        transform.rotation = Quaternion.identity;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 1f;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep; // 벽에 기대 멈춰도 Stay 콜백 유지
        rb.angularVelocity = -spinSpeed * Mathf.Sign(velocity.x); // 진행 방향으로 텀블링(회전)
        rb.linearVelocity = velocity;
        state = State.Flying;
        if (flyingClip != null && loopSrc != null) { loopSrc.clip = flyingClip; loopSrc.volume = sfxVolume; loopSrc.Play(); }
        if (trail != null) { trail.Clear(); trail.emitting = true; } // 잔상 궤적 시작
        OnThrown?.Invoke(); // 던져지는 순간 외부에 알림 (Inspector에서 연결한 이벤트 실행)
    }

    void FixedUpdate()
    {
        // 날아가는 동안 진행 방향으로 막대를 기울여 '날아가는 느낌'을 준다.
        if (state == State.Flying)
        {
            // spinSpeed != 0 이면 angularVelocity가 물리적으로 회전시키므로 여기선 손대지 않음(텀블링).
            // spinSpeed == 0 이면 옛 동작: 세로 막대를 진행 방향으로 정렬만.
            if (Mathf.Approximately(spinSpeed, 0f))
            {
                Vector2 v = rb.linearVelocity;
                if (v.sqrMagnitude > 0.01f)
                {
                    float ang = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg - 90f;
                    rb.MoveRotation(ang);
                }
            }
        }
        else if (state == State.Returning)
        {
            // 플레이어(움직이는 목표) 중심으로 호밍. 도착하면 회수.
            Vector2 target = ownerCol.bounds.center;
            Vector2 cur = rb.position;
            Vector2 to = target - cur;
            float toDist = to.magnitude;
            Vector2 dirN = toDist > 0.0001f ? to / toDist : Vector2.zero;
            float step = Mathf.Min(returnSpeed * Time.fixedDeltaTime, toDist);

            // 벽(지형)에 막히면 통과하지 않고, 벽면을 타고 미끄러지며 조금씩 플레이어 쪽으로 돈다.
            // (collide-and-slide) 벽 정면에 막혀도 접선 방향으로 흘러 벽 가장자리를 돌아 나간다.
            Vector2 next = MoveWithSlide(cur, dirN, step);

            rb.MovePosition(next);
            // 회전은 '플레이어 방향'만 바라봄 (실제 이동 방향으로 맞추면 미끄러질 때 매 프레임 홱홱 돌아 어지러움).
            if (dirN != Vector2.zero)
                rb.MoveRotation(Mathf.Atan2(dirN.y, dirN.x) * Mathf.Rad2Deg - 90f);
            if (Vector2.Distance(next, target) <= returnCatchDistance) SetHeld();
        }
    }

    /// <summary>cur에서 dir 방향으로 step만큼 이동하되, 지형(terrainMask)에 막히면 통과하지 않고
    /// 벽면을 따라 미끄러진다(법선 성분 제거). 미끄러진 경로도 한 번 더 벽 검사해 파묻힘을 막는다.</summary>
    Vector2 MoveWithSlide(Vector2 cur, Vector2 dir, float step)
    {
        if (dir == Vector2.zero || step <= 0f) return cur;
        RaycastHit2D hit = Physics2D.Raycast(cur, dir, step + returnWallSkin, terrainMask);
        if (hit.collider == null) return cur + dir * step; // 앞이 트여 있음 → 그대로 전진

        float toWall = Mathf.Max(0f, hit.distance - returnWallSkin); // 벽 앞까지만 전진
        Vector2 atWall = cur + dir * toWall;
        float remaining = step - toWall;
        if (remaining <= 0.0001f) return atWall;

        // 남은 이동은 벽면 접선 방향으로 미끄러뜨림 → 정면에 막혀도 벽을 타고 조금씩 이동
        Vector2 slide = dir - Vector2.Dot(dir, hit.normal) * hit.normal;
        if (slide.sqrMagnitude < 1e-6f) return atWall; // 완전 정면 충돌이면 미끄러질 방향 없음
        Vector2 slideDir = slide.normalized;

        float slideBudget = remaining * returnSlideFactor; // 벽을 탈 땐 더 천천히 (덜 미끄럽게)
        RaycastHit2D hit2 = Physics2D.Raycast(atWall, slideDir, slideBudget + returnWallSkin, terrainMask);
        float slideLen = hit2.collider != null ? Mathf.Max(0f, hit2.distance - returnWallSkin) : slideBudget;
        return atWall + slideDir * slideLen;
    }

    [Header("박히기 (바닥에 꽂힘)")]
    [Tooltip("이 값 이상으로 위를 향한 면이면 '바닥'으로 보고 박힘. (1=완전 수평 바닥, 0=수직 벽)")]
    public float groundNormalMin = 0.5f;
    [Tooltip("나뭇가지가 땅에 꽂힐 때(던져 박히거나 9로 세울 때) 바닥 속으로 파고드는 깊이(units). 0이면 바닥 위에 그대로, 클수록 깊이 꽂힌 느낌")]
    public float stickDepth = 0.15f;

    void OnCollisionEnter2D(Collision2D collision)
    {
        bool wasFlying = state == State.Flying;
        float impact = collision.relativeVelocity.magnitude;
        TryStickToGround(collision); // 바닥이면 여기서 박힘(Stuck) → 박히는 소리 재생
        // 박히지 않고 '벽/천장에 튕긴' 경우에만 부딪힘 소리 (세게 부딪힐 때만). 꽂힐 땐 안 나게.
        if (wasFlying && state == State.Flying && wallHitClip != null && sfxSrc != null && impact >= wallHitMinSpeed)
            sfxSrc.PlayOneShot(wallHitClip, sfxVolume);
    }
    void OnCollisionStay2D(Collision2D collision) { TryStickToGround(collision); }

    /// <summary>충돌 면이 '바닥'(위를 향한 면)일 때만 박는다. 벽·천장이면 무시하고 계속 날아간다.
    /// 위를 향한 면인지(normal.y)뿐 아니라, 접점이 막대 중심보다 '아래'에 있어야(밑에서 받쳐주는 진짜 바닥)
    /// 박는다. 벽 옆면은 접점이 중심 옆/위라서 걸러지므로, 미끄러지다 생기는 법선 글리치로 벽에 박히지 않는다.</summary>
    void TryStickToGround(Collision2D collision)
    {
        if (state != State.Flying) return;
        float centerY = col.bounds.center.y;
        for (int i = 0; i < collision.contactCount; i++)
        {
            var ct = collision.GetContact(i);
            if (ct.normal.y >= groundNormalMin && ct.point.y < centerY)
            {
                Stick(ct.point.y); // 접점 y = 바닥 윗면 높이
                return;
            }
        }
    }

    /// <summary>날아가던 나뭇가지를 그 자리에 박아 고정한다. 똑바로 세워(경사면 제거) 밟거나 텔레포트해도 안 미끄러지게 한다.
    /// groundTopY = 부딪힌 바닥의 윗면 높이(접점 y). 이 높이에 밑동을 맞춘다.</summary>
    void Stick(float groundTopY)
    {
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        transform.rotation = Quaternion.identity; // 날아가던 각도 대신 수직으로 세움 (세우기 9와 동일)
        // 세우면 막대 아래 절반이 중심 밑으로 내려간다. 중심을 그냥 내리면 아래 절반이 땅속에 파묻히므로,
        // 밑동(center.y - halfH)이 바닥 윗면에 오도록 배치한 뒤 stickDepth만큼만 살짝 꽂는다(9로 세우기 Plant와 동일).
        // rb가 Interpolate라 transform만 바꾸면 보간이 되돌리므로 rb.position까지 같이 옮긴다.
        Vector2 sunk = new Vector2(transform.position.x, groundTopY + WorldHalfHeight - stickDepth);
        rb.position = sunk;
        transform.position = sunk;
        SetPassThroughOwner(false); // 박히면 다시 단단해짐 (밟거나 부딪힘)
        if (loopSrc != null) loopSrc.Stop(); // 날아가는 소리 멈춤
        if (stickClip != null && sfxSrc != null) sfxSrc.PlayOneShot(stickClip, sfxVolume); // 박히는 소리
        if (trail != null) trail.emitting = false; // 착지하며 잔상은 서서히 사라짐
        state = State.Stuck;
        OnStuck?.Invoke(); // 바닥에 박힌 순간 외부에 알림
    }

    /// <summary>지정 위치에 나뭇가지를 똑바로 세운다. 단단한 발판/장애물이 되며, 회수 전까지 재사용 불가.</summary>
    public void Plant(Vector2 position)
    {
        if (state != State.Held) return;
        if (sr != null) sr.enabled = true;
        col.enabled = true;
        SetPassThroughOwner(false); // 세운 즉시 단단하게 (밟을 수 있음)
        transform.rotation = Quaternion.identity; // 똑바로
        rb.bodyType = RigidbodyType2D.Kinematic;
        // stickDepth만큼 바닥 속으로 파고들어 '꽂힌' 느낌. rb가 Interpolate라 rb.position까지 내려야 실제로 박힌다.
        Vector2 sunk = position + Vector2.down * stickDepth;
        rb.position = sunk;
        transform.position = sunk;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        if (stickClip != null && sfxSrc != null) sfxSrc.PlayOneShot(stickClip, sfxVolume); // 세워 박히는 소리
        state = State.Stuck; // 박힌 것과 동일 취급 (O로 회수)
    }

    /// <summary>박혀 있던 나뭇가지를 회수해 다시 손에 든(숨김) 상태로 되돌린다.</summary>
    public void Recover()
    {
        SetHeld();
    }

    /// <summary>어디에 있든(날아가는 중이거나 박혀 있을 때) 플레이어에게 부메랑처럼 날아 돌아오게 한다.
    /// 도착하면 자동으로 회수(Held)된다. 이미 손에 들고 있으면 아무 일도 없다.</summary>
    public void Return()
    {
        if (state == State.Held || state == State.Returning) return;
        if (ownerCol == null) { SetHeld(); return; } // 목표(플레이어)를 모르면 즉시 회수
        if (sr != null) sr.enabled = true;
        if (col != null) col.enabled = false;   // 물리 충돌은 끄고, 이동은 레이캐스트로 벽에 막음(통과 X)
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        if (loopSrc != null)
        {
            loopSrc.Stop(); // 날아가는 소리 멈추고
            // 돌아오는 동안 loop 재생 (회수되면 SetHeld 에서 멈춤)
            if (returnClip != null) { loopSrc.clip = returnClip; loopSrc.volume = sfxVolume; loopSrc.Play(); }
        }
        if (trail != null) { trail.emitting = false; trail.Clear(); }
        if (prompt != null) prompt.SetActive(false);
        state = State.Returning;
    }

    /// <summary>회수 가능 표시(O)를 켜고 끈다. 켜질 땐 나뭇가지 위에 똑바로 띄운다.</summary>
    public void ShowPrompt(bool show)
    {
        if (prompt == null) return;
        if (show)
        {
            prompt.transform.position = transform.position + Vector3.up * promptHeight;
            prompt.transform.rotation = Quaternion.identity; // 나뭇가지가 기울어도 O는 똑바로
        }
        if (prompt.activeSelf != show) prompt.SetActive(show);
    }

    // ── 프롬프트 생성 (폰트 의존 없이 링 스프라이트를 코드로 그림) ──
    void CreatePrompt()
    {
        prompt = new GameObject("PickupPrompt"); // 부모로 두지 않음 (스케일/회전 상속 방지)
        promptSr = prompt.AddComponent<SpriteRenderer>();
        promptSr.sprite = GenerateRingSprite();
        promptSr.color = promptColor;
        promptSr.sortingOrder = 100; // 항상 위에 보이게
        prompt.SetActive(false);
    }

    Sprite GenerateRingSprite()
    {
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float c = (size - 1) / 2f;
        float outer = size * 0.5f;
        float inner = size * 0.34f;
        var clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                tex.SetPixel(x, y, (d <= outer && d >= inner) ? Color.white : clear);
            }
        tex.Apply();
        // PPU 128 → 지름 약 0.5 units
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 128f);
    }

    /// <summary>손에 든(정지·숨김) 상태로 만든다. 물리·표시 모두 꺼서 화면에 안 보인다.</summary>
    void SetHeld()
    {
        State prev = state; // 잡기 전 상태 (Held면 시작/초기화라 소리 안 냄)
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        if (loopSrc != null) loopSrc.Stop(); // 회수/초기화 시 날아가는 소리 멈춤
        if (trail != null) { trail.emitting = false; trail.Clear(); }
        if (sr != null) sr.enabled = false; // 평소엔 안 보이게
        if (col != null) col.enabled = false;
        SetPassThroughOwner(true);          // 다음 던지기 대비
        if (prompt != null) prompt.SetActive(false); // 회수 시 O 표시 즉시 끔
        // 실제로 잡은 경우(날아가던/박힌/돌아오던 것 → 손)만 소리. 시작 시(Held→Held)는 제외.
        if (prev != State.Held && catchClip != null && sfxSrc != null) sfxSrc.PlayOneShot(catchClip, sfxVolume);
        state = State.Held;
    }
}