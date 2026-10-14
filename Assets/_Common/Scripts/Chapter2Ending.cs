using System.Collections;
using UnityEngine;

/// <summary>
/// 챕터 2 끝 → 챕터 3 으로 넘어가는 연출. 추격 끝(출구)에 둔다.
///  1) 출구에 닿으면 조작을 잠그고 메크로가 오른쪽으로 계속 달린다. 카메라는 그 자리에 멈춘다.
///     백신은 멈추지 않고 계속 쫓아오지만, 이 연출 중엔 닿아도 죽지 않는다.
///  2) runTime 동안 달린 뒤 ChapterTransitionTrigger(팀원 것)로 검게 페이드 → 챕터 3.
/// ChapterTransitionTrigger 는 이 연출이 끝날 때 부르므로, 그 트리거 자체 콜라이더는 시작할 때 꺼 둔다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Chapter2Ending : MonoBehaviour
{
    [Header("다음 챕터")]
    [Tooltip("연출 끝에 부를 챕터 전환 (비우면 씬에서 찾음)")]
    public ChapterTransitionTrigger transition;

    [Header("달리기")]
    [Tooltip("출구 턱 왼쪽 끝 x. 연출이 시작될 때 메크로가 이보다 왼쪽(모서리)에 있으면 턱 위로 옮긴다")]
    public float ledgeLeft = 146.5f;
    [Tooltip("자동으로 달릴 때 속도 배율 (1 = 평소 이동 속도 그대로, 넘어갈 때 튀어나가지 않게)")]
    [Min(0.1f)] public float runSpeedMultiplier = 1f;
    [Tooltip("출구에 닿은 뒤 챕터 전환(검게 페이드)을 시작하기까지 달리는 시간(초)")]
    [Min(0f)] public float runTime = 2.2f;

    bool started;
    PlayerController player;
    float originalMoveSpeed;
    bool running;

    void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        if (transition == null) transition = FindAnyObjectByType<ChapterTransitionTrigger>();
        // 전환은 이 연출이 끝날 때 부른다 → 트리거 자체로는 안 넘어가게
        if (transition != null)
            foreach (var c in transition.GetComponents<Collider2D>()) c.enabled = false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (started) return;
        var pc = other.GetComponentInParent<PlayerController>();
        if (pc == null) return;
        started = true;
        player = pc;
        StartCoroutine(Routine());
    }

    IEnumerator Routine()
    {
        CutsceneLock.Lock(this);
        // 백신은 계속 쫓아오지만, 연출 중엔 닿아도 죽지 않게
        var wall = FindAnyObjectByType<VaccineWall>();
        if (wall != null) wall.harmless = true;
        var cam = FindAnyObjectByType<CameraFollow>();
        if (cam != null) cam.target = null; // 카메라는 그 자리에 → 메크로가 화면 밖으로 달려 나감

        PlaceOnRunFloor();

        // 오른쪽으로 계속 달리기
        player.Face(1);
        originalMoveSpeed = player.moveSpeed;
        player.moveSpeed = originalMoveSpeed * runSpeedMultiplier;
        running = true;

        if (runTime > 0f) yield return new WaitForSeconds(runTime);

        // 챕터 전환 (검게 페이드 → 챕터 3). 페이드가 끝날 때까지 계속 달린다
        if (transition != null) transition.StartTransition();
        else Debug.LogWarning("[Chapter2Ending] ChapterTransitionTrigger 를 못 찾음");
    }

    /// <summary>달릴 바닥(트리거 아랫면 = 출구 턱 윗면) 위에 메크로를 올려 둔다.
    /// 나뭇가지 텔레포트로 턱 모서리에 걸쳐 도착하면 왼쪽으로 떨어져 턱에 막히므로, 턱 안쪽으로 살짝 옮긴다.</summary>
    void PlaceOnRunFloor()
    {
        var body = player.GetComponent<Collider2D>();
        var rb = player.GetComponent<Rigidbody2D>();
        if (body == null || rb == null) return;
        Bounds area = GetComponent<Collider2D>().bounds;
        Bounds pb = body.bounds;
        float floorTop = area.min.y;
        float minX = ledgeLeft + pb.extents.x + 0.1f; // 턱 모서리 기준 (트리거 시작 위치와는 무관)
        Vector2 pos = rb.position;
        bool off = pb.min.y < floorTop - 0.05f || pos.x < minX;
        if (!off) return;
        pos.x = Mathf.Max(pos.x, minX);
        pos.y += floorTop - pb.min.y + 0.01f;
        rb.position = pos;
        player.transform.position = pos;
        rb.linearVelocity = Vector2.zero;
    }

    void Update()
    {
        // 자동 달리기: 남은 이동 거리가 떨어지지 않게 계속 앞쪽 목표를 다시 준다
        if (running && player != null && !player.IsMoving)
            player.WalkToX(player.transform.position.x + 50f);
    }

    void OnDisable()
    {
        // 씬이 바뀌며 사라질 때: 잠금은 챕터 전환이 이어서 잡고 있으므로 이 연출 몫만 푼다
        CutsceneLock.Unlock(this);
        if (running && player != null) player.moveSpeed = originalMoveSpeed;
        running = false;
    }
}
