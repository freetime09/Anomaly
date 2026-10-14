using System.Collections;
using UnityEngine;

/// <summary>
/// 챕터 2 끝 → 챕터 3 으로 넘어가는 연출. 추격 끝(출구)에 둔다.
///  1) 출구에 닿으면 조작을 잠그고 메크로가 오른쪽으로 계속 달린다 (평소보다 빠르게). 카메라는 그 자리에 멈춘다.
///     백신은 멈추지 않고 계속 쫓아오지만, 이 연출 중엔 닿아도 죽지 않는다.
///  2) 달리는 동안 화면이 지지직 깨지다가(GlitchBreakEffect) 하얗게 번쩍인다.
///  3) 번쩍이는 순간 메크로 뒤 세상이 검게 사라진다 (깨진 조각만 남음). 막힌 벽은 치우고 보이지 않는 바닥을 깔아 계속 달릴 수 있게 한다.
///  4) 어둠 속을 달려 나가다가 ChapterTransitionTrigger(팀원 것)로 검게 페이드 → 챕터 3.
///     챕터 3 은 검은 공간에서 달리는 장면 + 배경 깨짐으로 시작하므로 그대로 이어진다.
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

    [Header("깨짐")]
    [Tooltip("출구에 닿은 뒤 번쩍이기까지 깨짐이 차오르는 시간(초)")]
    [Min(0f)] public float glitchRampTime = 1f;
    [Tooltip("번쩍이기 직전 깨짐 세기 (0~1)")]
    [Range(0f, 1f)] public float glitchPeak = 0.9f;
    [Tooltip("어둠 속을 달리는 동안의 깨짐 세기 (0~1)")]
    [Range(0f, 1f)] public float glitchInDark = 0.45f;

    [Header("어둠")]
    [Tooltip("세상이 사라진 뒤 배경색 (챕터 3 검은 공간과 같게)")]
    public Color darkColor = new Color(0.07f, 0.07f, 0.07f);
    [Tooltip("어둠 속을 달린 뒤 챕터 전환(검게 페이드)을 시작하기까지 시간(초)")]
    [Min(0f)] public float runInDarkTime = 1.2f;

    bool started;
    PlayerController player;
    float originalMoveSpeed;
    bool running;
    GameObject curtain, floor;

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

        // 깨짐: 조각이 검은 막 위·메크로 아래에 보이게 순서를 정한 뒤 켠다 (Awake 에서 조각을 만듦)
        int playerOrder = PlayerSortingOrder();
        var glitchGo = new GameObject("Chapter2EndingGlitch");
        glitchGo.SetActive(false);
        var glitch = glitchGo.AddComponent<GlitchBreakEffect>();
        glitch.fragmentSortingOrder = playerOrder - 1;
        glitchGo.SetActive(true);
        glitch.RampTo(glitchPeak, glitchRampTime);
        if (glitchRampTime > 0f) yield return new WaitForSeconds(glitchRampTime);

        // 하얗게 번쩍 → 가장 밝은 순간 세상이 사라짐
        yield return glitch.Flash(() => VanishWorld(cam, playerOrder - 2));
        glitch.RampTo(glitchInDark, 0.3f);

        if (runInDarkTime > 0f) yield return new WaitForSeconds(runInDarkTime);

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

    /// <summary>메크로 뒤 세상을 검게 덮고, 막힌 길을 열어 계속 달릴 수 있게 한다.</summary>
    void VanishWorld(CameraFollow cam, int curtainOrder)
    {
        var view = cam != null ? cam.GetComponent<Camera>() : Camera.main;
        if (view != null) view.backgroundColor = darkColor;

        // 검은 막: 카메라 화면을 넉넉히 덮는 큰 사각형 (메크로·깨진 조각보다 아래, 나머지 전부보다 위)
        curtain = new GameObject("Chapter2EndingCurtain");
        var sr = curtain.AddComponent<SpriteRenderer>();
        var tex = Texture2D.whiteTexture;
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        sr.color = darkColor;
        sr.sortingOrder = curtainOrder;
        Vector3 c = view != null ? view.transform.position : player.transform.position;
        curtain.transform.position = new Vector3(c.x, c.y, 0f);
        curtain.transform.localScale = new Vector3(400f, 400f, 1f);

        // 앞을 막는 벽 콜라이더를 끄고, 발밑 높이로 보이지 않는 바닥을 길게 깐다
        var body = player.GetComponent<Collider2D>();
        float feet = body != null ? body.bounds.min.y : player.transform.position.y;
        float x0 = player.transform.position.x;
        var blockers = Physics2D.OverlapBoxAll(new Vector2(x0 + 30f, feet + 1.5f), new Vector2(58f, 2.6f), 0f, player.groundLayer);
        foreach (var b in blockers) if (!b.isTrigger) b.enabled = false;
        floor = new GameObject("Chapter2EndingFloor");
        floor.layer = LayerFromMask(player.groundLayer);
        var fc = floor.AddComponent<BoxCollider2D>();
        fc.size = new Vector2(80f, 2f);
        floor.transform.position = new Vector3(x0 + 38f, feet - 1f, 0f);
    }

    void Update()
    {
        // 자동 달리기: 남은 이동 거리가 떨어지지 않게 계속 앞쪽 목표를 다시 준다
        if (running && player != null && !player.IsMoving)
            player.WalkToX(player.transform.position.x + 50f);
    }

    int PlayerSortingOrder()
    {
        int order = 10;
        foreach (var r in player.GetComponentsInChildren<SpriteRenderer>())
            order = Mathf.Min(order, r.sortingOrder);
        return order;
    }

    static int LayerFromMask(LayerMask mask)
    {
        for (int i = 0; i < 32; i++) if ((mask.value & (1 << i)) != 0) return i;
        return 0;
    }

    void OnDisable()
    {
        // 씬이 바뀌며 사라질 때: 잠금은 챕터 전환이 이어서 잡고 있으므로 이 연출 몫만 푼다
        CutsceneLock.Unlock(this);
        if (running && player != null) player.moveSpeed = originalMoveSpeed;
        running = false;
    }
}
