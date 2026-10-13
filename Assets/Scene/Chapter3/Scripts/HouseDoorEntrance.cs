using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 문 앞(트리거 범위)에 오면 자동으로 문을 열고 들어간다.
///  - 소용돌이 전환(VortexTransition)으로 화면을 가린 순간 메크로를 목적지(집 안)로 옮긴다.
///    소용돌이가 씬에 없으면 그냥 바로 옮긴다.
///  - 대화(독백 등)가 재생 중이면 끝날 때까지 기다렸다가 들어간다 → "집에 가 봐야 해!" 대사가 끊기지 않게.
///  - 다 들어가면 On Entered 이벤트를 부른다 (예: 집 안 컷신 시작).
/// 이동 중에는 조작을 잠근다. 기존 Teleporter/FallRespawn 은 이동이 끝나면 조작을 무조건 켜기 때문에
/// 컷신으로 바로 이어지는 이 문에는 CutsceneLock 을 쓰는 별도 스크립트로 만들었다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class HouseDoorEntrance : MonoBehaviour
{
    [Header("목적지")]
    [Tooltip("문을 통과하면 메크로가 서게 될 위치 (집 안 입구)")]
    public Transform destination;
    [Tooltip("이동 순간 바꿀 카메라 배경색 (집 안). 안 바꾸려면 알파를 0으로")]
    public Color destinationBackground = new Color(0.16f, 0.12f, 0.1f);

    [Header("동작")]
    [Tooltip("켜면 한 번만 들어간다 (다시 나와도 문이 반응하지 않음)")]
    public bool enterOnce = true;
    [Tooltip("문 앞에 도착한 뒤 들어가기까지 잠깐 기다리는 시간(초). 바로 빨려 들어가면 어색하다")]
    [Min(0f)] public float enterDelay = 0.3f;

    [Header("사운드 (선택)")]
    [Tooltip("문 여는 소리")]
    public AudioClip openClip;
    [Range(0f, 1f)] public float openVolume = 1f;

    [Header("이벤트")]
    [Tooltip("집 안으로 다 들어간 뒤 호출 (예: Chapter3HouseCutscene.Play)")]
    public UnityEvent onEntered = new UnityEvent();

    bool playerInside;
    bool entering;
    bool entered;
    AudioSource sfx;

    void Reset() { GetComponent<Collider2D>().isTrigger = true; }

    void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
    }

    void OnTriggerEnter2D(Collider2D other) { if (other.GetComponentInParent<PlayerController>() != null) playerInside = true; }
    void OnTriggerExit2D(Collider2D other)  { if (other.GetComponentInParent<PlayerController>() != null) playerInside = false; }

    void Update()
    {
        if (!playerInside || entering || (enterOnce && entered)) return;
        if (DialoguePlayer.Instance != null && DialoguePlayer.Instance.IsPlaying) return; // 대사가 끝날 때까지 대기
        StartCoroutine(EnterRoutine());
    }

    IEnumerator EnterRoutine()
    {
        entering = true;
        CutsceneLock.Lock(this);
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null) player.Face(1);
        if (enterDelay > 0f) yield return new WaitForSeconds(enterDelay);
        if (openClip != null) sfx.PlayOneShot(openClip, openVolume);

        var vortex = VortexTransition.Instance;
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
        CutsceneLock.Unlock(this); // 들어가는 도중 꺼져도 잠금이 남지 않게
    }

    void OnDrawGizmosSelected()
    {
        if (destination == null) return;
        Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.9f);
        Gizmos.DrawLine(transform.position, destination.position);
        Gizmos.DrawWireCube(destination.position, new Vector3(0.8f, 1.6f, 0f));
    }
}
