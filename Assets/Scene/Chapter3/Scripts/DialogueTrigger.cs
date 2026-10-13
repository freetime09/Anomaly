using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 플레이어가 범위(트리거 콜라이더) 안에 들어오면 대화를 자동으로 시작한다.
///  - NPC: NPC 오브젝트에 CircleCollider2D(Is Trigger) + 이 컴포넌트 → 다가가면 말을 건다.
///  - 독백 구역: 빈 오브젝트에 BoxCollider2D(Is Trigger) + 이 컴포넌트 → 지나가면 메크로가 혼잣말을 한다.
///  - 다른 대화가 재생 중일 때 들어오면:
///      Queue When Busy 켬 → 대기열에 넣고 앞 대화가 끝나면 이어서 재생 (그 사이 범위를 벗어나도 재생됨).
///      Queue When Busy 끔 → 범위 안에 계속 있을 때만 앞 대화가 끝나기를 기다렸다가 시작.
///  - 반복 대화(Play Once 끔)는 범위 밖으로 나갔다가 다시 들어와야 다시 말한다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DialogueTrigger : MonoBehaviour
{
    [Header("대화")]
    [Tooltip("범위에 들어오면 재생할 대사 묶음")]
    public DialogueData dialogue;
    [Tooltip("켜면 한 번만 재생. 끄면 범위를 나갔다 다시 들어올 때마다 재생")]
    public bool playOnce = true;
    [Tooltip("켜면 대화 동안 플레이어 조작을 잠근다 (그 자리에 멈춰 듣게 됨). NPC 대화는 켜고, 걸으면서 하는 독백은 끈다")]
    public bool lockPlayerControl = false;
    [Tooltip("다른 대화 중에 들어왔을 때 대기열에 넣어 반드시 재생한다. 끄면 범위를 지나쳐 버리면 이 대화는 건너뛴다")]
    public bool queueWhenBusy = true;

    [Header("이벤트")]
    [Tooltip("대화가 시작(또는 대기열에 등록)될 때 호출")]
    public UnityEvent onDialogueStarted = new UnityEvent();
    [Tooltip("대화가 끝까지 재생되면 호출 (예: 다음 구역 열기, 컷신 시작, NPC 떠나기)")]
    public UnityEvent onDialogueFinished = new UnityEvent();

    bool playerInside;   // 플레이어가 범위 안에 있는가
    bool played;         // 한 번이라도 재생했는가
    bool waitingExit;    // 반복 대화: 이번 입장에서 이미 재생함 → 나갈 때까지 대기
    bool locked;         // 이 트리거가 조작을 잠그고 있음

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true; // 컴포넌트를 붙일 때 자동으로 트리거로
    }

    void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        if (dialogue == null) Debug.LogWarning($"[DialogueTrigger] {name}: Dialogue 가 비어 있음");
    }

    void OnTriggerEnter2D(Collider2D other) { if (IsPlayer(other)) playerInside = true; }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;
        playerInside = false;
        waitingExit = false;
    }

    // 범위 안에 있는 동안 매 프레임 시작을 시도 — 다른 대화가 끝나기를 기다리는 경우를 위해 Update 에서 확인
    // (Rigidbody2D 가 잠들면 OnTriggerStay2D 가 멈출 수 있어 Stay 대신 플래그 + Update 를 쓴다)
    void Update()
    {
        if (playerInside) TryStart();
    }

    static bool IsPlayer(Collider2D other) => other.GetComponentInParent<PlayerController>() != null;

    void TryStart()
    {
        if (dialogue == null) return;
        if (playOnce && played) return;
        if (waitingExit) return;

        var player = DialoguePlayer.Ensure();
        if (player.IsPlaying && !queueWhenBusy) return; // 범위 안에서 다른 대화가 끝날 때까지 대기

        played = true;
        waitingExit = true;
        onDialogueStarted.Invoke();

        // 조작 잠금은 '차례가 왔을 때'가 아니라 '범위에 들어온 지금' 건다.
        // 앞 대화(걸으며 하는 독백 등)를 기다리는 사이 메크로가 NPC 를 지나쳐 멀어지지 않고, 그 자리에 멈춰 차례를 기다리게 된다.
        if (lockPlayerControl) { CutsceneLock.Lock(this); locked = true; }
        player.PlayOrQueue(dialogue, false, OnFinished); // 바쁘면 대기열로
    }

    void OnFinished()
    {
        Unlock();
        onDialogueFinished.Invoke();
    }

    void Unlock()
    {
        if (!locked) return;
        CutsceneLock.Unlock(this);
        locked = false;
    }

    void OnDisable()
    {
        Unlock(); // 대화 도중 오브젝트가 꺼져도(NPC 퇴장 등) 잠금이 남지 않게
    }

    /// <summary>범위를 Scene 뷰에 표시 (노란 반투명).</summary>
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.25f);
        var col = GetComponent<Collider2D>();
        if (col is CircleCollider2D c)
        {
            float s = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
            Gizmos.DrawWireSphere(transform.TransformPoint(c.offset), c.radius * s);
        }
        else if (col is BoxCollider2D b)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(b.offset, b.size);
        }
    }
}
