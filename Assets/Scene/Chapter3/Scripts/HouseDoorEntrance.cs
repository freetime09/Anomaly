using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 집 문. 문 앞(트리거 범위)에서 O(상호작용) 제스처를 그리면 문을 열고 들어간다.
///  - 레버·텔레포터와 같은 IInteractable 이라, 범위에 들어가면 PlayerController 가 문 위에 O 프롬프트를 띄우고
///    O 를 그리면 Interact() 를 부른다 (별도 입력 코드 없이 기존 상호작용 시스템을 그대로 사용).
///  - 소용돌이 전환(VortexTransition)으로 화면을 가린 순간 메크로를 목적지(집 안)로 옮긴다.
///    소용돌이가 씬에 없으면 그냥 바로 옮긴다.
///  - 대화(독백 등)가 재생 중에 O 를 그리면 대사가 끝날 때까지 기다렸다가 들어간다 → 대사가 끊기지 않게.
///  - 다 들어가면 On Entered 이벤트를 부른다 (예: 집 안 컷신 시작).
/// 이동 중에는 조작을 잠근다. 기존 Teleporter/FallRespawn 은 이동이 끝나면 조작을 무조건 켜기 때문에
/// 컷신으로 바로 이어지는 이 문에는 CutsceneLock 을 쓰는 별도 스크립트로 만들었다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class HouseDoorEntrance : MonoBehaviour, IInteractable
{
    [Header("목적지")]
    [Tooltip("문을 통과하면 메크로가 서게 될 위치 (집 안 입구)")]
    public Transform destination;
    [Tooltip("이동 순간 바꿀 카메라 배경색 (집 안). 안 바꾸려면 알파를 0으로")]
    public Color destinationBackground = new Color(0.16f, 0.12f, 0.1f);

    [Header("동작")]
    [Tooltip("켜면 문 앞에서 O(상호작용)를 그려야 들어간다. 끄면 문 앞에 다가가기만 해도 자동으로 들어간다")]
    public bool requireInteract = true;
    [Tooltip("켜면 한 번만 들어간다 (다시 나와도 문이 반응하지 않음)")]
    public bool enterOnce = true;
    [Tooltip("들어가기로 한 뒤(O 를 그리거나 문 앞 도착) 실제로 들어가기까지 잠깐 기다리는 시간(초). 바로 빨려 들어가면 어색하다")]
    [Min(0f)] public float enterDelay = 0.3f;
    [Tooltip("켜면 소용돌이 전환으로 화면을 가리고 이동. 끄면 전환 없이 바로 On Entered (예: 다음 챕터로 넘어가는 문은 자체 페이드를 씀)")]
    public bool useVortex = true;

    [Header("O 프롬프트")]
    [Tooltip("문 위로 O 를 띄우는 높이 (이 오브젝트 위치 기준, units)")]
    public float promptHeight = 2.2f;
    [Tooltip("프롬프트 O 색상")]
    public Color promptColor = Color.white;

    [Header("사운드 (선택)")]
    [Tooltip("문 여는 소리")]
    public AudioClip openClip;
    [Tooltip("문 여는 소리 볼륨")]
    [Range(0f, 1f)] public float openVolume = 1f;

    [Header("이벤트")]
    [Tooltip("집 안으로 다 들어간 뒤 호출 (예: Chapter3HouseCutscene.Play)")]
    public UnityEvent onEntered = new UnityEvent();

    bool playerInside;
    bool requested;   // O 를 그려 들어가기로 함 (대사가 끝나기를 기다리는 중일 수 있음)
    bool entering;
    bool entered;
    AudioSource sfx;
    GameObject prompt;

    /// <summary>지금 문으로 들어갈 수 있는 상태인가? (이미 들어가는 중이거나, 한 번만 들어가는 문을 이미 지났으면 false)</summary>
    bool CanEnter => !entering && !(enterOnce && entered);

    void Reset() { GetComponent<Collider2D>().isTrigger = true; }

    void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        CreatePrompt(); // Require Interact 를 플레이 중에 켜도 프롬프트가 보이도록 항상 만들어 둔다
    }

    void OnDestroy()
    {
        if (prompt != null) Destroy(prompt);
    }

    void OnTriggerEnter2D(Collider2D other) { if (other.GetComponentInParent<PlayerController>() != null) playerInside = true; }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        playerInside = false;
        requested = false; // 대사를 기다리는 사이 문에서 멀어지면 요청 취소 → 멀리서 빨려 들어가지 않게
    }

    // ── IInteractable (PlayerController 가 O 프롬프트 표시·O 제스처 처리에 사용) ──

    /// <summary>문 트리거 범위 안에 있고 아직 들어갈 수 있을 때만 상호작용 대상이 된다.</summary>
    public bool PlayerInRange(Vector2 playerPos) => requireInteract && playerInside && CanEnter && !requested;

    /// <summary>O 를 그렸을 때: 들어가기를 요청한다. 실제 진입은 Update 에서 대사가 끝난 뒤 시작.</summary>
    public void Interact()
    {
        if (!requireInteract || !playerInside || !CanEnter) return;
        requested = true;
        ShowPrompt(false);    }

    public void ShowPrompt(bool show)
    {
        if (prompt == null) return;
        if (show) prompt.transform.position = transform.position + Vector3.up * promptHeight;
        if (prompt.activeSelf != show) prompt.SetActive(show);
    }

    void Update()
    {
        if (!CanEnter) return;
        bool wantsEnter = requireInteract ? requested : playerInside;
        if (!wantsEnter) return;
        if (DialoguePlayer.Instance != null && DialoguePlayer.Instance.IsPlaying) return; // 대사가 끝날 때까지 대기
        requested = false;
        StartCoroutine(EnterRoutine());
    }

    IEnumerator EnterRoutine()
    {
        entering = true;
        ShowPrompt(false);
        CutsceneLock.Lock(this);
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            float dx = transform.position.x - player.transform.position.x;
            player.Face(Mathf.Abs(dx) > 0.1f ? (dx > 0f ? 1 : -1) : 1); // 문 쪽을 봄
        }
        if (enterDelay > 0f) yield return new WaitForSeconds(enterDelay);
        if (openClip != null) sfx.PlayOneShot(openClip, openVolume);

        var vortex = useVortex ? VortexTransition.Instance : null;
        if (vortex != null)
        {
            vortex.Play(() => MovePlayer(player)); // 화면이 완전히 가려진 순간 이동
            yield return null;                     // 재생이 시작된 프레임을 넘긴 뒤
            while (vortex.IsPlaying) yield return null;
        }
        else MovePlayer(player);

        entered = true;
        entering = false;
        onEntered.Invoke();          // 컷신이 먼저 조작을 잠근 다음에
        CutsceneLock.Unlock(this);   // 문의 잠금을 푼다 → 조작이 풀리는 틈이 생기지 않음
    }

    void MovePlayer(PlayerController player)
    {
        if (player == null || destination == null) return;
        player.ResetMotion();
        player.transform.position = destination.position;
        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null) rb.position = destination.position;
        var cam = FindAnyObjectByType<CameraFollow>();
        if (cam != null)
        {
            cam.SnapToTarget();
            if (destinationBackground.a > 0f) cam.GetComponent<Camera>().backgroundColor = destinationBackground;
        }
    }

    void OnDisable()
    {
        ShowPrompt(false);
        CutsceneLock.Unlock(this); // 들어가는 도중 꺼져도 잠금이 남지 않게
    }

    /// <summary>레버·텔레포터와 같은 모양의 O 프롬프트(흰 고리)를 만든다. 부모 없이 둬서 스케일/회전을 물려받지 않게 한다.</summary>
    void CreatePrompt()
    {
        prompt = new GameObject("DoorPrompt");
        var sr = prompt.AddComponent<SpriteRenderer>();
        sr.sprite = GenerateRingSprite();
        sr.color = promptColor;
        sr.sortingOrder = 100;
        prompt.SetActive(false);
    }

    static Sprite GenerateRingSprite()
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

    void OnDrawGizmosSelected()
    {
        if (destination == null) return;
        Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.9f);
        Gizmos.DrawLine(transform.position, destination.position);
        Gizmos.DrawWireCube(destination.position, new Vector3(0.8f, 1.6f, 0f));
    }
}
