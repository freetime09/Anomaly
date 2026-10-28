using TMPro;
using UnityEngine;

/// <summary>
/// 배울 곳마다 서 있는 '메크로 홀로그램'. 가까이 다가가면 그 기술의 시범 창(TutorialDemoWindow)을 연다.
///  - 모습: 진짜 메크로와 같은 그림·애니메이션을 하늘색 홀로그램으로 그리고, 살짝 깜빡이며 가끔 지직거린다.
///    발밑엔 빛나는 고리, 머리 위엔 위아래로 떠다니는 '?' 표시.
///  - 처음 다가가면(트리거 범위) 시범 창이 저절로 열린다.
///  - 한 번 본 뒤에는 O(상호작용)를 그려야 다시 열린다. 레버·텔레포터와 같은 IInteractable 이라
///    가까이 가면 머리 위에 'O 다시 보기' 가 뜨고, O 를 그리면 PlayerController 가 Interact() 를 부른다.
///  - Ensure Branch Held 를 켜면, 열 때 나뭇가지가 손에 없으면 돌려받는다 (앞에서 실수로 던져 둔 경우에도 막히지 않게).
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
public class TutorialHologram : MonoBehaviour, IInteractable
{
    [Header("시범")]
    [Tooltip("열 시범 번호 (TutorialDemoWindow 의 Clips 순서, 0 부터)")]
    [Min(0)] public int lessonIndex;
    [Tooltip("이 거리(칸) 안으로 들어오면 시범 창이 열린다")]
    [Min(0.5f)] public float triggerRadius = 2f;
    [Tooltip("열 때 나뭇가지가 손에 없으면 돌려받는다 (나뭇가지를 쓰는 시범에서 켬)")]
    public bool ensureBranchHeld;
    [Tooltip("한 번 본 뒤 O 를 그려 다시 열 수 있는 거리 (칸, 홀로그램 발밑 기준)")]
    [Min(0.5f)] public float replayRadius = 2.2f;

    [Header("모습")]
    [Tooltip("바라보는 방향 (-1 = 왼쪽: 왼쪽에서 오는 메크로를 바라봄)")]
    public int facing = -1;
    [Tooltip("홀로그램 색 (알파 = 기본 투명도)")]
    public Color hologramColor = new Color(0.45f, 0.95f, 1f, 0.55f);
    [Tooltip("깜빡임 세기 (0 = 깜빡이지 않음)")]
    [Range(0f, 0.5f)] public float flicker = 0.18f;
    [Tooltip("가끔 옆으로 지직거리는 정도 (칸)")]
    [Min(0f)] public float glitchOffset = 0.08f;
    [Tooltip("머리 위 표시 글꼴")]
    public TMP_FontAsset font;

    PlayerController player;
    SpriteRenderer body, glowBody, ring;
    Animator anim;
    TextMeshPro icon;
    Vector3 bodyBase, iconBase;
    float glitchUntil, nextGlitch;
    bool inside, seen;
    bool promptShown;   // PlayerController 가 'O 로 다시 보기' 를 띄우라고 한 상태
    float seed;

#if UNITY_EDITOR
    void Reset()
    {
        GetComponent<CircleCollider2D>().isTrigger = true;
        font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DialogueSpeaker.DefaultFontPath);
    }
#endif

    void Awake()
    {
        var col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = triggerRadius;
        col.offset = new Vector2(0f, 1f);
        player = FindAnyObjectByType<PlayerController>();
        seed = Random.value * 100f;
        Build();
    }

    /// <summary>진짜 메크로의 그림·애니메이터를 빌려 반투명 하늘색으로 만든다. 이 오브젝트 위치 = 발 위치.</summary>
    void Build()
    {
        var bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(transform, false);
        body = bodyGo.AddComponent<SpriteRenderer>();
        body.sortingOrder = 4; // 진짜 메크로(10)보다 뒤
        body.flipX = facing < 0;
        if (player != null)
        {
            var srcAnim = player.GetComponentInChildren<Animator>();
            var pcol = player.GetComponent<Collider2D>();
            if (srcAnim != null)
            {
                var srcSr = srcAnim.GetComponent<SpriteRenderer>();
                if (srcSr != null) { body.sprite = srcSr.sprite; body.sharedMaterial = srcSr.sharedMaterial; }
                var s = srcAnim.transform.lossyScale;
                bodyGo.transform.localScale = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), 1f);
                if (pcol != null) bodyGo.transform.localPosition = new Vector3(0f, srcAnim.transform.position.y - pcol.bounds.min.y, 0f);
                anim = bodyGo.AddComponent<Animator>();
                anim.runtimeAnimatorController = player.baseController != null ? player.baseController : srcAnim.runtimeAnimatorController;
            }
        }
        bodyBase = bodyGo.transform.localPosition;

        // 하늘색 단색 실루엣: 흰 배경에서도 '빛나는 홀로그램'으로 보이게 그림 모양만 단색으로 칠한다.
        // (게임의 이동 미리보기 실루엣과 같은 방식: 글자용 셰이더 = 그림의 알파 × 색)
        var glowGo = new GameObject("Glow");
        glowGo.transform.SetParent(bodyGo.transform, false);
        glowBody = glowGo.AddComponent<SpriteRenderer>();
        glowBody.flipX = body.flipX;
        glowBody.sortingOrder = body.sortingOrder - 1;
        var sh = Shader.Find("GUI/Text Shader");
        if (sh != null) glowBody.material = new Material(sh);

        // 발밑 빛 고리 (납작한 타원)
        var ringGo = new GameObject("Ring");
        ringGo.transform.SetParent(transform, false);
        ringGo.transform.localScale = new Vector3(1.6f, 0.4f, 1f);
        ring = ringGo.AddComponent<SpriteRenderer>();
        ring.sprite = RingSprite();
        ring.sortingOrder = 3;

        // 머리 위 '?' 표시
        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(transform, false);
        iconBase = new Vector3(0f, 2.7f, 0f);
        iconGo.transform.localPosition = iconBase;
        icon = iconGo.AddComponent<TextMeshPro>();
        if (font != null) icon.font = font;
        icon.text = "?";
        icon.fontSize = 8f;
        icon.alignment = TextAlignmentOptions.Bottom;
        icon.rectTransform.sizeDelta = new Vector2(4f, 2.2f);
        icon.rectTransform.pivot = new Vector2(0.5f, 0f); // 글 줄 수가 늘어도 아래(머리 위)는 고정되게
        // 흰·회색 배경에서도 읽히도록 굵게 + 흰 테두리
        icon.fontStyle = FontStyles.Bold;
        icon.outlineColor = Color.white;
        icon.outlineWidth = 0.22f;
        icon.sortingOrder = 5;
    }

    void Update()
    {
        float t = Time.time;
        // 깜빡임: 부드러운 잡음으로 투명도를 살짝 흔든다
        float a = hologramColor.a * (1f - flicker + flicker * Mathf.PerlinNoise(seed, t * 6f) * 2f);
        // 가끔 짧게 옆으로 지직
        if (t > nextGlitch) { glitchUntil = t + 0.08f; nextGlitch = t + Random.Range(1.2f, 3.5f); }
        bool glitching = t < glitchUntil;
        body.transform.localPosition = bodyBase + (glitching ? new Vector3(Random.Range(-glitchOffset, glitchOffset), 0f, 0f) : Vector3.zero);
        float dim = seen ? 0.75f : 1f; // 한 번 본 홀로그램은 조금 흐리게
        float alpha = (glitching ? a * 0.5f : a) * dim;
        glowBody.sprite = body.sprite;                                   // 애니메이션 프레임을 실루엣도 따라감
        glowBody.color = new Color(hologramColor.r, hologramColor.g, hologramColor.b, alpha);
        body.color = new Color(1f, 1f, 1f, alpha * 0.45f);               // 원래 그림은 옅게 겹쳐 얼굴·옷 모양만 비치게
        ring.color = new Color(hologramColor.r * 0.6f, hologramColor.g * 0.85f, hologramColor.b, (0.55f + 0.2f * Mathf.Sin(t * 3f)) * dim);
        if (anim != null && anim.runtimeAnimatorController != null) anim.SetFloat("Grounded", 1f); // 서 있는 자세로 (공중 자세 방지)

        icon.transform.localPosition = iconBase + Vector3.up * (Mathf.Sin(t * 2.5f) * 0.12f);
        if (!seen)
        {
            icon.text = "?";
            icon.color = new Color(0.95f, 0.6f, 0.08f, 0.8f + 0.2f * Mathf.Sin(t * 5f));
        }
        else if (promptShown)
        {
            // 가까이 있을 때: 'O 를 그리면 다시 볼 수 있다' 를 또렷하게
            icon.text = "O\n<size=45%>다시 보기</size>";
            icon.color = new Color(0.1f, 0.5f, 0.9f, 0.85f + 0.15f * Mathf.Sin(t * 6f));
        }
        else
        {
            icon.text = "↻";
            icon.color = new Color(0.3f, 0.55f, 0.8f, 0.7f);
        }
    }

    // 처음 다가갈 때만 저절로 연다. 그 뒤로는 O(Interact)로 연다.
    void OnTriggerEnter2D(Collider2D other)
    {
        if (inside || other.GetComponentInParent<PlayerController>() == null) return;
        inside = true;
        if (!seen) OpenDemo();
    }

    void OpenDemo()
    {
        var window = TutorialDemoWindow.Instance;
        if (window == null) { Debug.LogWarning("[TutorialHologram] 씬에 TutorialDemoWindow 가 없음"); return; }
        if (ensureBranchHeld)
        {
            var branch = FindAnyObjectByType<Branch>();
            if (branch != null && !branch.CanThrow) branch.Return(); // 손에 없으면 부메랑처럼 돌아오게
        }
        window.Open(lessonIndex);
        seen = true;
    }

    // ── IInteractable: 한 번 본 홀로그램 앞에서 O 를 그리면 다시 열기 ──

    public bool PlayerInRange(Vector2 playerPos)
    {
        if (!seen || (TutorialDemoWindow.Instance != null && TutorialDemoWindow.Instance.IsOpen)) return false;
        return Mathf.Abs(playerPos.x - transform.position.x) <= replayRadius && Mathf.Abs(playerPos.y - transform.position.y) <= 3f;
    }

    public void ShowPrompt(bool show) { promptShown = show; }

    public void Interact()
    {
        if (seen) OpenDemo();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() != null) inside = false;
    }

    static Sprite ringSprite;
    static Sprite RingSprite()
    {
        if (ringSprite != null) return ringSprite;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                // 가운데는 은은하게, 가장자리는 밝은 테두리
                float a = d > 1f ? 0f : Mathf.Lerp(0.25f, 1f, Mathf.SmoothStep(0.6f, 0.95f, d)) * (1f - Mathf.SmoothStep(0.95f, 1f, d));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        return ringSprite;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.45f, 0.95f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up, triggerRadius);
    }
}
