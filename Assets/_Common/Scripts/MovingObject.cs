using UnityEngine;

/// <summary>
/// 신호(버튼/레버의 UnityEvent)를 받으면 지정 방향으로 distance 만큼 이동하는 오브젝트.
///  - 활성화(Activate) → 시작 위치 + 오프셋 지점으로 moveSpeed 로 이동
///  - 비활성화(Deactivate) → returnWhenOff 면 시작 위치로 복귀, 아니면 그 자리에 머무름
/// 방향(왼/오/상/하), 이동 거리, 속도, 복귀 여부를 인스펙터에서 조절한다.
/// 배선: 레버 onTurnOn/버튼 onPressed → Activate(), onTurnOff/onReleased → Deactivate() (또는 Toggle()).
/// 플레이어가 타고 다니는 단단한 플랫폼이면 Kinematic Rigidbody2D + Collider2D(Ground)를 붙일 것.
/// </summary>
public class MovingObject : SignalReceiver
{
    public enum Dir { Left, Right, Up, Down }

    [Header("이동 설정")]
    [Tooltip("활성화되면 움직이는 방향")]
    public Dir direction = Dir.Left;
    [Tooltip("이동 거리 (월드 단위)")]
    public float distance = 3f;
    [Tooltip("이동 속도 (units/초)")]
    public float moveSpeed = 3f;
    [Tooltip("신호가 꺼지면 시작 위치로 되돌아온다. 끄면 이동한 자리에 머무름(한 번 열리면 유지).")]
    public bool returnWhenOff = true;
    [Tooltip("되돌아올 때 속도 (units/초). 0이면 moveSpeed와 같음 — 천천히 닫히는 문 등에 사용")]
    public float returnSpeed = 0f;

    [Header("상태 (읽기용)")]
    public bool activated;

    Rigidbody2D rb;
    Vector3 startPos;
    Vector3 target;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        startPos = transform.position;
        target = startPos;
    }

    // 신호 조건(SignalReceiver)이 충족/해제될 때 호출 → 이동/복귀
    protected override void OnSignalStateChanged(bool on) { SetActivated(on); }

    void FixedUpdate()
    {
        Vector3 cur = rb != null ? (Vector3)rb.position : transform.position;
        float speed = (!activated && returnSpeed > 0f) ? returnSpeed : moveSpeed;
        Vector3 next = Vector3.MoveTowards(cur, target, speed * Time.fixedDeltaTime);
        if (rb != null) rb.MovePosition(next); // Kinematic 플랫폼이면 부드럽게 밀기
        else transform.position = next;
    }

    // ── 신호 받는 진입점 (UnityEvent 로 배선) ──
    /// <summary>활성화 — 이동 지점으로.</summary>
    public void Activate() { SetActivated(true); }
    /// <summary>비활성화 — returnWhenOff면 시작 위치로.</summary>
    public void Deactivate() { SetActivated(false); }
    /// <summary>토글.</summary>
    public void Toggle() { SetActivated(!activated); }

    public void SetActivated(bool on)
    {
        activated = on;
        if (on) target = startPos + Offset();
        else if (returnWhenOff) target = startPos;
        // returnWhenOff=false 면 target 그대로 두어 이동한 자리에 머무름
    }

    Vector3 Offset()
    {
        switch (direction)
        {
            case Dir.Left:  return Vector3.left  * distance;
            case Dir.Right: return Vector3.right * distance;
            case Dir.Up:    return Vector3.up    * distance;
            case Dir.Down:  return Vector3.down  * distance;
        }
        return Vector3.zero;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // 시작 위치와 목표 위치를 표시 (에디터에선 현재 위치를 시작으로 간주)
        Vector3 s = Application.isPlaying ? startPos : transform.position;
        Vector3 t = s + Offset();
        Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.9f);
        Gizmos.DrawWireCube(t, Vector3.one * 0.3f);
        Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.5f);
        Gizmos.DrawLine(s, t);
    }
#endif
}
