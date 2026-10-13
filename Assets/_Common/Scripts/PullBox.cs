using UnityEngine;

/// <summary>
/// 플레이어가 O(상호작용)로 잡고 밀거나 당길 수 있는 단단한 상자.
///  - 평소: X축을 고정해 벽처럼 안 밀리는 단단한 블록(중력은 받아 바닥에 놓임).
///  - O 로 잡으면: 잡은 순간의 (상자x - 플레이어x) 간격을 유지하도록 상자를 따라오게 함
///    → 플레이어가 상자 쪽으로 가면 밀고, 반대로 가면 당긴다(양방향).
///  - O 로 다시 놓으면: 그 자리에서 다시 X 고정(단단한 블록).
/// 벽(Ground)에 닿으면 물리로 멈추고, 플레이어가 너무 멀어지면 자동으로 놓는다.
/// 레버·텔레포터와 같은 IInteractable 이라 근처에서 O 프롬프트가 뜨고(겹치면 더 가까운 것만),
/// PlayerController.Interact() 가 잡기/놓기를 토글한다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PullBox : MonoBehaviour, IInteractable, IPuzzleState
{
    [Header("상호작용")]
    [Tooltip("O 제스처로 잡을 수 있는 반경(플레이어 기준, units)")]
    public float interactRadius = 1.5f;

    [Header("당기기/밀기")]
    [Tooltip("상자가 플레이어를 따라오는 최대 속도(units/초). 플레이어 이동속도보다 커야 딱 붙어 따라옴")]
    public float maxDragSpeed = 12f;
    [Tooltip("잡은 채 플레이어와 이 거리 이상 벌어지면 자동으로 놓는다(units). 텔레포트/리스폰 안전장치")]
    public float autoReleaseDistance = 5f;
    [Tooltip("멀리서 잡았을 때 플레이어가 박스 옆면에 붙을 때 남길 틈(units). 0이면 딱 붙음")]
    public float attachGap = 0.02f;
    [Tooltip("붙으러 걸어가는 최대 시간(초). 넘으면(막힘 등) 그 자리 간격으로 끌기 시작")]
    public float maxAttachTime = 0.6f;

    [Header("O 프롬프트 (근처에서 뜨는 O)")]
    [Tooltip("상자 중심에서 위로 얼마나 띄울지 (units)")]
    public float promptHeight = 1f;
    [Tooltip("프롬프트 O 색상")]
    public Color promptColor = Color.white;

    [Header("잡는 중 표시 (끌기 상태 시각화)")]
    [Tooltip("잡는 동안 O 프롬프트 색 (평소 promptColor보다 진하게)")]
    public Color grabPromptColor = new Color(1f, 0.45f, 0f);
    [Tooltip("잡는 동안 박스 위에 띄울 안내 문구")]
    public string grabHintText = "O를 다시 그리면 놓기";
    [Tooltip("안내 문구 폰트(한글 지원 TMP 폰트). 비우면 TMP 기본 폰트")]
    public TMPro.TMP_FontAsset grabHintFont;
    [Tooltip("안내 문구 크기")]
    public float grabHintFontSize = 4f;
    [Tooltip("안내 문구 높이(박스 중심 기준, units). O 프롬프트보다 위")]
    public float grabHintHeight = 2.6f;
    [Tooltip("안내 문구 색")]
    public Color grabHintColor = new Color(0.15f, 0.15f, 0.15f);

    /// <summary>현재 잡혀 있는 박스 (한 번에 하나만). null 이면 아무것도 안 잡은 상태.</summary>
    public static PullBox Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Current = null; } // 플레이 재시작 시 정적 참조 초기화

    Rigidbody2D rb;
    Rigidbody2D playerRb;
    PlayerController playerCtl; // 잡는 동안 이동 속도 늦추기용
    Collider2D[] myCols;   // 상자 콜라이더 전부 (켜고 끄는 콜라이더가 섞여 있어도 잡는 동안 모두 플레이어와 충돌 무시)
    Collider2D playerCol;  // 플레이어 콜라이더 (잡는 동안 충돌 무시용)
    bool grabbed;
    float grabOffsetX; // 잡은 순간의 (상자x - 플레이어x)
    bool attaching;    // 잡은 직후 플레이어가 박스 옆까지 걸어가 붙는 중
    float attachTimer;

    GameObject prompt;      // 근처에서 뜨는 'O' 표시
    SpriteRenderer promptSr;

    TMPro.TextMeshPro hint;     // 잡는 동안 '놓는 법' 안내 문구

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        myCols = GetComponents<Collider2D>();
        var player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            playerCtl = player;
            playerRb = player.GetComponent<Rigidbody2D>();
            playerCol = player.GetComponent<Collider2D>();
        }
        CreatePrompt();
        CreateGrabHint();
        Release(); // 시작은 놓인(고정) 상태
    }

    void LateUpdate()
    {
        if (!grabbed) return;
        // 박스가 움직이므로 문구를 매 프레임 따라가게 (부모로 두면 박스 스케일을 상속해 글자가 찌그러짐)
        if (hint != null) hint.transform.position = transform.position + Vector3.up * grabHintHeight;
    }

    /// <summary>잡는 중 표시를 켜고 끈다 — O를 진한 색으로 + 안내 문구.</summary>
    void SetGrabVisuals(bool on)
    {
        if (promptSr != null)
        {
            promptSr.color = on ? grabPromptColor : promptColor;
        }
        if (hint != null) hint.gameObject.SetActive(on);
        if (on) LateUpdate(); // 켜는 프레임에 바로 제자리
    }

    void CreateGrabHint()
    {
        // 안내 문구 (월드 공간 TMP)
        var hintGo = new GameObject("BoxGrabHint");
        hint = hintGo.AddComponent<TMPro.TextMeshPro>();
        if (grabHintFont != null) hint.font = grabHintFont;
        hint.text = grabHintText;
        hint.fontSize = grabHintFontSize;
        hint.color = grabHintColor;
        hint.alignment = TMPro.TextAlignmentOptions.Center;
        hint.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        hint.rectTransform.sizeDelta = new Vector2(10f, 2f);
        hint.sortingOrder = 100; // 항상 위에 보이게
        hintGo.SetActive(false);
    }

    void OnDestroy()
    {
        if (grabbed && playerCtl != null) playerCtl.SetCarrying(false); // 잡은 채 파괴돼도 속도 복구
        if (Current == this) Current = null;
        if (prompt != null) Destroy(prompt); // 부모 없이 만든 프롬프트 정리
        if (hint != null) Destroy(hint.gameObject);
    }

    void OnDisable()
    {
        if (grabbed && playerCtl != null) playerCtl.SetCarrying(false); // 잡은 채 비활성돼도 속도 복구
        if (Current == this) Current = null;
        if (grabbed) { grabbed = false; SetGrabVisuals(false); }
    }

    void FixedUpdate()
    {
        if (!grabbed || playerRb == null) return;

        // 너무 멀어지면(텔레포트/리스폰 등) 자동으로 놓기
        if (Vector2.Distance(rb.position, playerRb.position) > autoReleaseDistance) { Release(); return; }

        // 멀리서 잡았으면 먼저 플레이어가 박스 옆까지 걸어와 붙는다. 그동안 박스는 고정, 도착하면 그 간격으로 끌기 시작.
        if (attaching)
        {
            attachTimer += Time.fixedDeltaTime;
            if (playerCtl != null && playerCtl.IsMoving && attachTimer < maxAttachTime) return;
            attaching = false;
            grabOffsetX = rb.position.x - playerRb.position.x;     // 붙은 간격 저장(양방향)
            rb.constraints = RigidbodyConstraints2D.FreezeRotation; // X 풀어 끌리게(회전만 고정)
            if (playerCtl != null) playerCtl.SetCarrying(true);     // 끄는 동안 느리게
        }

        // 잡은 간격을 유지하도록 X 속도를 준다. Y(중력)는 그대로 둬 바닥에 얹혀 따라온다.
        float targetX = playerRb.position.x + grabOffsetX;
        float vx = Mathf.Clamp((targetX - rb.position.x) / Time.fixedDeltaTime, -maxDragSpeed, maxDragSpeed);
        rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
    }

    // ── IInteractable ──
    /// <summary>플레이어가 interactRadius 안에 있는가?</summary>
    public bool PlayerInRange(Vector2 playerPos)
    {
        return Vector2.Distance(playerPos, transform.position) <= interactRadius;
    }

    /// <summary>O 상호작용 — 잡고 있으면 놓고, 아니면 잡는다.</summary>
    public void Interact()
    {
        if (grabbed) Release();
        else Grab();
    }

    void Grab()
    {
        if (playerRb == null) return;
        if (Current != null && Current != this) Current.Release(); // 한 번에 하나만 — 이전 박스는 놓기
        grabbed = true;
        Current = this;
        // 잡는 동안 플레이어와의 물리 충돌만 무시 → 밀 때 솔버 밀기가 안 더해져 밀기/당기기 속도가 같아짐
        // (벽·바닥 충돌은 그대로라 여전히 단단하게 막힘)
        SetIgnorePlayer(true);
        SetGrabVisuals(true);

        // 상호작용 범위 끝에서 잡아도 떨어져 끌리지 않도록, 플레이어가 박스 옆면까지 걸어가 붙는다.
        // 붙는 동안 박스는 고정(X 고정 유지) — 도착하면 FixedUpdate에서 간격 저장 후 끌기 시작.
        attaching = true;
        attachTimer = 0f;
        if (playerCtl != null && playerCol != null)
        {
            Bounds bb = SolidBounds();
            float half = playerCol.bounds.extents.x;
            float targetX = playerRb.position.x < rb.position.x
                ? bb.min.x - half - attachGap   // 박스 왼쪽에 붙기
                : bb.max.x + half + attachGap;  // 박스 오른쪽에 붙기
            playerCtl.WalkToX(targetX);
        }
    }

    /// <summary>박스의 실제 몸통 범위 — 켜져 있는(비트리거) 콜라이더들의 합. 없으면 스프라이트 범위.</summary>
    void SetIgnorePlayer(bool ignore)
    {
        if (myCols == null || playerCol == null) return;
        foreach (var c in myCols)
            if (c != null) Physics2D.IgnoreCollision(c, playerCol, ignore);
    }

    Bounds SolidBounds()
    {
        bool has = false; Bounds b = new Bounds(transform.position, Vector3.zero);
        foreach (var c in GetComponents<Collider2D>())
        {
            if (!c.enabled || c.isTrigger) continue;
            if (!has) { b = c.bounds; has = true; } else b.Encapsulate(c.bounds);
        }
        if (!has)
        {
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) b = sr.bounds;
        }
        return b;
    }

    void Release()
    {
        grabbed = false;
        attaching = false;
        if (Current == this) Current = null;
        rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation; // 그 자리서 다시 단단히 고정
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); // 미끄러짐 방지(Y=중력 유지)
        SetIgnorePlayer(false); // 다시 단단한 블록으로(플레이어와 충돌 복구)
        if (playerCtl != null) playerCtl.SetCarrying(false); // 이동 속도 원래대로
        SetGrabVisuals(false);
    }

    // ── IPuzzleState (죽으면 체크포인트 때 위치로) ──
    struct BoxState { public Vector3 pos; public float rot; public bool active; }
    public object SaveState()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>(); // 꺼진 채 시작해 Awake 전일 수 있음
        return new BoxState { pos = transform.position, rot = rb.rotation, active = gameObject.activeSelf };
    }
    public void LoadState(object state)
    {
        var s = (BoxState)state;
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (grabbed) Release();
        gameObject.SetActive(s.active);
        transform.position = s.pos;
        rb.position = s.pos;
        rb.rotation = s.rot;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    /// <summary>상호작용 가능 표시(O)를 켜고 끈다. (레버/나뭇가지와 동일한 링, PlayerController 가 표시 여부 결정)</summary>
    public void ShowPrompt(bool show)
    {
        if (prompt == null) return;
        if (show)
        {
            prompt.transform.position = transform.position + Vector3.up * promptHeight;
            prompt.transform.rotation = Quaternion.identity;
        }
        if (prompt.activeSelf != show) prompt.SetActive(show);
    }

    void CreatePrompt()
    {
        prompt = new GameObject("BoxPrompt"); // 부모로 두지 않음 (스케일/회전 상속 방지)
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
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 128f);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.25f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
#endif
}
