using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 눌리는 버튼(발판). 감지 영역(버튼 위)에 뭔가 있으면 눌린다.
///  - 플레이어 또는 박스(PullBox)가 감지 영역에 얹히면 눌림(pressLayers로 1차 필터 후 컴포넌트로 확정).
///  - 돌아오는/날아가는/박힌 나뭇가지가 branchRadius 안에 있어도 눌림
///    (나뭇가지는 돌아올 때 콜라이더가 꺼지므로 콜라이더가 아니라 거리로 감지).
/// latch(인스펙터 토글)가 켜져 있으면 한 번 눌리면 계속 ON 유지(한 번만 누르면 됨).
/// onPressed/onReleased(UnityEvent)로 대상(문·플랫폼 등)을 인스펙터에서 배선한다.
/// </summary>
public class PressButton : MonoBehaviour
{
    [Header("상태 (읽기용)")]
    public bool pressed;

    [Header("래치 (인스펙터 토글)")]
    [Tooltip("켜면 한 번 눌리면 계속 ON 유지(래치, '한 번만 누르면 됨'). 끄면 위에 뭔가 있을 때만 ON(밟는 동안).")]
    public bool latch = false;

    [Header("스프라이트")]
    [Tooltip("표시할 SpriteRenderer (비우면 자식에서 자동 탐색)")]
    public SpriteRenderer sr;
    [Tooltip("안 눌린 상태 스프라이트 (튜토버튼1_0)")]
    public Sprite releasedSprite;
    [Tooltip("눌린 상태 스프라이트 (튜토버튼2_0)")]
    public Sprite pressedSprite;

    [Header("충돌판 높이 (박스가 얹히는 윗면, 버튼 기준 y)")]
    [Tooltip("안 눌렸을 때 윗면 높이 — 안 눌린 버튼 그림 윗면")]
    public float releasedTopY = -0.44f;
    [Tooltip("눌렸을 때 윗면 높이 — 눌린 버튼 그림 윗면. 눌리면 얹힌 박스도 이만큼 같이 내려감")]
    public float pressedTopY = -0.52f;

    [Header("누르는 대상 감지")]
    [Tooltip("이 레이어의 콜라이더가 감지 영역에 있으면 누른 것으로 침(플레이어·오브젝트). Ground 등은 빼둘 것.")]
    public LayerMask pressLayers;
    [Tooltip("감지 영역 크기(units). 버튼 윗면 부근을 덮게.")]
    public Vector2 detectSize = new Vector2(1.8f, 0.5f);
    [Tooltip("감지 영역 중심 오프셋(버튼 기준, units).")]
    public Vector2 detectOffset = new Vector2(0f, 0.1f);
    [Tooltip("박스가 경사를 넘으며 들썩여도 눌림이 안 끊기게, 박스만 감지 영역 위로 이만큼 더 봄(units)")]
    public float boxLiftTolerance = 0.6f;

    [Header("나뭇가지")]
    [Tooltip("나뭇가지(들고 있지 않은 상태)가 이 반경 안이면 누름")]
    public float branchRadius = 0.9f;

    [Header("신호 (채널)")]
    [Tooltip("이 버튼이 눌리면 이 번호 채널로 신호를 보냄. 같은 번호를 '받는' 오브젝트만 반응.")]
    public int signalId = 0;

    [Header("사운드")]
    [Tooltip("버튼이 눌리는 순간 재생할 소리")]
    public AudioClip pressClip;
    [Tooltip("버튼이 떼지는 순간 재생할 소리 (래치면 안 떼짐)")]
    public AudioClip releaseClip;
    [Range(0f, 1f)]
    [Tooltip("소리 볼륨")]
    public float sfxVolume = 1f;

    [Header("이벤트 (대상 배선)")]
    public UnityEvent onPressed;
    public UnityEvent onReleased;

    Branch branch;
    AudioSource sfxSrc;
    bool latched;
    PolygonCollider2D poly; // 경사 충돌판 (윗면 높이를 눌림 상태에 맞춰 바꿈)
    Vector2[] basePath;     // 프리팹에 저장된 원래 모양
    float baseTopY;         // 원래 모양의 윗면 y (이 높이의 점들만 옮김)
    static readonly Collider2D[] buf = new Collider2D[16];

    void Awake()
    {
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
        poly = GetComponent<PolygonCollider2D>();
        if (poly != null && poly.pathCount > 0)
        {
            basePath = poly.GetPath(0);
            baseTopY = float.MinValue;
            foreach (var p in basePath) baseTopY = Mathf.Max(baseTopY, p.y);
        }
        branch = FindObjectOfType<Branch>();
        sfxSrc = gameObject.AddComponent<AudioSource>();
        sfxSrc.playOnAwake = false;
        pressed = false;
        latched = false;
        ApplyVisual();
    }

    void Update()
    {
        bool physically = SomethingOnIt() || BranchOnIt();

        bool now;
        if (latch)
        {
            if (physically) latched = true;
            now = latched;
        }
        else now = physically;

        if (now != pressed)
        {
            pressed = now;
            ApplyVisual();
            SignalBus.Set(signalId, this, pressed); // 채널 신호 갱신
            var clip = pressed ? pressClip : releaseClip;
            if (clip != null && sfxSrc != null) sfxSrc.PlayOneShot(clip, sfxVolume); // 버튼 소리
            if (pressed) onPressed.Invoke();
            else onReleased.Invoke();
        }
    }

    void OnEnable()  { SignalBus.Set(signalId, this, pressed); }
    void OnDisable() { SignalBus.Set(signalId, this, false); }

    /// <summary>감지 영역에 '누를 수 있는' 것(플레이어 또는 박스 PullBox)이 있는가?
    /// 나뭇가지는 여기서 제외하고 BranchOnIt에서 거리로 따로 감지한다.</summary>
    bool SomethingOnIt()
    {
        Vector2 c = (Vector2)transform.position + detectOffset;
        // 박스는 경사를 넘으며 살짝 들썩여 감지 영역 위로 뜰 수 있으니, 위쪽으로 boxLiftTolerance 만큼 넓혀서 찾는다
        // (플레이어는 원래 감지 영역에 겹칠 때만 인정)
        bool upright = poly != null && transform.up.y >= 0.5f;
        float lift = upright ? boxLiftTolerance : 0f;
        Bounds area = new Bounds(c, detectSize);
        int n = Physics2D.OverlapBoxNonAlloc(c + Vector2.up * lift * 0.5f, detectSize + Vector2.up * lift, 0f, buf, pressLayers);
        for (int i = 0; i < n; i++)
        {
            var col = buf[i];
            if (col == null) continue;
            if (col.transform == transform || col.transform.IsChildOf(transform)) continue;          // 버튼 자신 제외
            // 플레이어와 박스(PullBox)만 누름으로 인정 — 그 외(다른 Default 오브젝트 등)는 무시
            bool isPlayer = col.GetComponentInParent<PlayerController>() != null;
            if (!isPlayer && col.GetComponentInParent<PullBox>() == null) continue;
            if (isPlayer)
            {
                Bounds cb = col.bounds;
                cb.center = new Vector3(cb.center.x, cb.center.y, 0f); // z 차이로 Intersects가 빗나가지 않게
                if (!area.Intersects(cb)) continue;
                return true;
            }
            // 박스는 몸통이 커서 옆에 닿기만 해도 감지되므로, 윗면에 실제로 얹혔을 때만 누름
            // → 안 눌린 높이에 먼저 얹힌 뒤 눌리면서 같이 내려가 보임
            if (!RestsOnTop(col)) continue;
            return true;
        }
        return false;
    }

    /// <summary>콜라이더 밑면이 현재 충돌판 윗면 높이(근처)까지 올라와 있는가? 경사충돌판·똑바로 선 버튼일 때만 검사.</summary>
    bool RestsOnTop(Collider2D col)
    {
        if (poly == null || transform.up.y < 0.5f) return true;
        float bottom = col.bounds.min.y;
        var bc = col as BoxCollider2D;
        if (bc != null) bottom -= bc.edgeRadius * Mathf.Abs(col.transform.lossyScale.y); // bounds엔 둥근 모서리가 빠져 있음
        float top = transform.position.y + (pressed ? pressedTopY : releasedTopY) * transform.lossyScale.y;
        if (bottom < top - 0.02f) return false; // 경사 중간이 아니라 윗면까지 다 올라왔을 때만
        if (pressed) return true;                // 이미 눌렸으면 들썩여도 유지
        // 처음 누를 땐 윗면에 '내려앉은' 뒤에만 → 안 눌린 높이에 얹혔다가 눌리며 같이 내려가는 게 보임
        var rb = col.attachedRigidbody;
        bool settled = rb == null || Mathf.Abs(rb.linearVelocity.y) < 0.05f;
        return settled && bottom <= top + 0.03f;
    }

    /// <summary>들고 있지 않은 나뭇가지가 반경 안에 있는가? (돌아오는 중엔 콜라이더가 꺼져 거리로 감지)</summary>
    bool BranchOnIt()
    {
        if (branch == null || branch.CurrentState == Branch.State.Held) return false;
        return Vector2.Distance(branch.transform.position, transform.position) <= branchRadius;
    }

    void ApplyVisual()
    {
        ApplyColliderHeight();
        if (sr == null) return;
        var s = pressed ? pressedSprite : releasedSprite;
        if (s != null) sr.sprite = s;
    }

    /// <summary>충돌판 윗면(경사 사이 평평한 부분)을 눌림 상태 높이로 맞춘다 → 얹힌 박스가 버튼과 같이 내려감/올라감.</summary>
    void ApplyColliderHeight()
    {
        if (poly == null || basePath == null) return;
        float top = pressed ? pressedTopY : releasedTopY;
        var path = (Vector2[])basePath.Clone();
        for (int i = 0; i < path.Length; i++)
            if (path[i].y >= baseTopY - 0.001f) path[i].y = top;
        poly.SetPath(0, path);

        // 위에 얹혀 잠든 물체(박스)를 깨워 바뀐 높이로 바로 내려앉게
        Vector2 c = (Vector2)transform.position + detectOffset;
        int n = Physics2D.OverlapBoxNonAlloc(c, detectSize, 0f, buf, pressLayers);
        for (int i = 0; i < n; i++)
            if (buf[i] != null && buf[i].attachedRigidbody != null) buf[i].attachedRigidbody.WakeUp();
    }

    /// <summary>래치를 풀어 다시 뗄 수 있게 초기화(다음 프레임부터 실제 눌림 상태 반영).</summary>
    public void ResetButton() { latched = false; }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.6f);
        Gizmos.DrawWireCube((Vector2)transform.position + detectOffset, detectSize);
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, branchRadius);
    }
#endif
}
