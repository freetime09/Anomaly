using UnityEngine;

/// <summary>
/// 카메라가 대상(플레이어)을 부드럽게 따라간다.
/// 바로 스냅하지 않고 SmoothDamp로 살짝 지연되며 쫓아간다. (플랫포머 표준)
/// target이 비어 있으면 자동으로 PlayerController를 찾아 붙는다.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("대상")]
    [Tooltip("따라갈 대상. 비워두면 자동으로 PlayerController를 찾음")]
    public Transform target;

    [Header("따라가기")]
    [Tooltip("따라잡는 데 걸리는 대략 시간(초). 클수록 더 느긋/부드럽게 따라옴")]
    public float smoothTime = 0.25f;
    [Tooltip("대상 기준 카메라 오프셋(월드). z는 카메라 깊이 유지용(-10 권장)")]
    public Vector3 offset = new Vector3(0f, 1f, -10f);

    [Header("축 잠금")]
    [Tooltip("가로(X)로 따라갈지")]
    public bool followX = true;
    [Tooltip("세로(Y)로 따라갈지 (플랫포머에서 Y를 고정하고 싶으면 끄기)")]
    public bool followY = true;

    Vector3 velocity; // SmoothDamp 내부 상태

    // ── 화면 흔들림 (실패 피드백 등에서 호출) ──
    float shakeTimeLeft;
    float shakeDur;
    float shakeMag;

    // ── 지속 흔들림 (백신 벽 접근 등). 요청자별 세기 중 가장 큰 값을 매 프레임 적용 ──
    readonly System.Collections.Generic.Dictionary<Object, float> rumbles = new System.Collections.Generic.Dictionary<Object, float>();

    /// <summary>계속 흔들리는 세기를 설정한다 (0이면 해제). 같은 요청자가 매 프레임 갱신해도 됨.</summary>
    public void SetRumble(Object owner, float magnitude)
    {
        if (owner == null) return;
        if (magnitude <= 0f) rumbles.Remove(owner); else rumbles[owner] = magnitude;
    }

    /// <summary>카메라를 duration(초) 동안 magnitude(units) 크기로 흔든다. 시간에 따라 서서히 잦아든다.
    /// 겹쳐 들어오면 더 큰 세기로 갱신한다.</summary>
    public void Shake(float duration, float magnitude)
    {
        if (duration <= 0f || magnitude <= 0f) return;
        shakeDur = duration;
        shakeMag = Mathf.Max(shakeMag, magnitude);
        shakeTimeLeft = duration;
    }

    void Awake()
    {
        if (target == null)
        {
            var pc = FindObjectOfType<PlayerController>();
            if (pc != null) target = pc.transform;
        }
    }

    // 시작 시 대상 위치로 즉시 스냅한다. (에디터에서 카메라를 아무 데나 주차해 둬도
    //  플레이 시작하자마자 플레이어를 비춘다. 시작 팬/시차 렁치 방지)
    void Start()
    {
        SnapToTarget();
    }

    /// <summary>대상 위치로 카메라를 즉시 이동(스냅)한다. 리스폰 등에서 카메라를 바로 붙일 때 사용.</summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        Vector3 p = target.position + offset;
        if (!followX) p.x = transform.position.x;
        if (!followY) p.y = transform.position.y;
        p.z = target.position.z + offset.z;
        transform.position = p;
    }

    // 플레이어 이동(FixedUpdate/Update) 이후에 카메라를 옮겨 떨림을 줄임
    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + offset;
        // 따라가지 않기로 한 축은 현재 카메라 값을 유지
        if (!followX) desired.x = transform.position.x;
        if (!followY) desired.y = transform.position.y;
        desired.z = target.position.z + offset.z; // z는 항상 오프셋 유지(깊이 고정)

        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);

        // 흔들림: 팔로우 위치 위에 감쇠하는 랜덤 오프셋을 얹는다 (매 프레임 새로 계산 → 드리프트 없음)
        if (shakeTimeLeft > 0f)
        {
            shakeTimeLeft -= Time.unscaledDeltaTime;
            float k = shakeDur > 0f ? Mathf.Clamp01(shakeTimeLeft / shakeDur) : 0f;
            Vector2 off = Random.insideUnitCircle * (shakeMag * k);
            transform.position += new Vector3(off.x, off.y, 0f);
            if (shakeTimeLeft <= 0f) shakeMag = 0f;
        }

        // 지속 흔들림
        if (rumbles.Count > 0)
        {
            float m = 0f;
            foreach (var r in rumbles.Values) m = Mathf.Max(m, r);
            Vector2 off = Random.insideUnitCircle * m;
            transform.position += new Vector3(off.x, off.y, 0f);
        }
    }
}
