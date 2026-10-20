using System.Collections;
using UnityEngine;

/// <summary>
/// 챕터 4 시작 연출: 메크로 확대 상태로 시작 → 두리번거림 → 카메라를 멀리 빼서 캡쳐(에디터 화면) 전체를 보여준 뒤
/// 메크로 말풍선 대사(챕터 3과 같은 방식) → 메크로 쪽으로 줌인하고 조작을 풀어준다.
/// 연출 동안은 CameraFollow 를 끄고 직접 카메라를 움직인다 (CameraClampToSprite 가 캡쳐 밖은 안 보이게 막음).
/// 카메라가 멀어져도 말풍선이 화면에서 같은 크기로 보이도록 연출 동안 말풍선을 카메라 거리만큼 키운다.
/// </summary>
[DefaultExecutionOrder(2000)] // 말풍선(SpeechBubble)이 자리를 잡은 뒤 크기를 맞춤
public class Chapter4Intro : MonoBehaviour
{
    [Tooltip("전체를 보여줄 캡쳐 배경")]
    public SpriteRenderer overviewArea;

    [Header("시간 (초)")]
    [Tooltip("시작(메크로 확대 상태)에서 두리번거리기 시작할 때까지")]
    public float startDelay = 0.5f;
    [Tooltip("카메라를 뒤로 빼는 시간")]
    public float zoomOutTime = 1.2f;
    [Tooltip("전체 화면을 보여주다가 대사가 나오기까지")]
    public float overviewHold = 1f;
    [Tooltip("대사가 끝난 뒤 줌인 시작까지")]
    public float afterDialogue = 0.2f;
    [Tooltip("줌인 시간")]
    public float zoomTime = 1.6f;

    [Header("두리번 (확대 상태에서, 카메라를 빼기 전)")]
    [Tooltip("좌우를 번갈아 보는 횟수 (0 이면 안 함)")]
    [Min(0)] public int lookCount = 4;
    [Tooltip("한 번 고개를 돌리고 다음까지의 간격(초)")]
    [Min(0.05f)] public float lookInterval = 0.45f;
    [Tooltip("두리번거린 뒤 바라볼 방향 (+1 오른쪽, -1 왼쪽)")]
    public int lookFinalDir = 1;
    [Tooltip("두리번거린 뒤 카메라를 빼기까지")]
    public float afterLook = 0.3f;

    [Header("대사 (챕터 3과 같은 말풍선)")]
    public DialogueData dialogue;

    PlayerController player;
    CameraFollow follow;
    Camera view;
    float zoomSize;
    bool running;
    DialogueSpeaker speaker;

    void Start()
    {
        player = FindAnyObjectByType<PlayerController>();
        follow = FindAnyObjectByType<CameraFollow>();
        view = follow != null ? follow.GetComponent<Camera>() : Camera.main;
        if (player == null || view == null || overviewArea == null) { enabled = false; return; }
        speaker = player.GetComponent<DialogueSpeaker>();
        StartCoroutine(Run());
    }

    void OnDestroy()
    {
        CutsceneLock.Unlock(this);
    }

    IEnumerator Run()
    {
        running = true;
        CutsceneLock.Lock(this);
        zoomSize = view.orthographicSize;
        if (follow != null) follow.enabled = false;

        // 1) 메크로 확대 상태로 시작 → 잠깐 뒤 캡쳐 전체가 들어오게 뒤로 뺌
        view.transform.position = FollowTarget();
        yield return new WaitForSeconds(startDelay);
        if (lookCount > 0)
        {
            int dir = -1;
            for (int i = 0; i < lookCount; i++)
            {
                player.Face(dir);
                dir = -dir;
                yield return new WaitForSeconds(lookInterval);
            }
            player.Face(lookFinalDir);
            yield return new WaitForSeconds(afterLook);
        }
        Bounds b = overviewArea.bounds;
        float overSize = Mathf.Max(b.size.y * 0.5f, b.size.x * 0.5f / view.aspect);
        Vector3 overPos = new Vector3(b.center.x, b.center.y, view.transform.position.z);
        yield return MoveCamera(view.transform.position, overPos, zoomSize, overSize, zoomOutTime, false);
        yield return new WaitForSeconds(overviewHold);

        // 2) 대사
        if (dialogue != null) yield return DialoguePlayer.Ensure().PlayAndWait(dialogue);
        yield return new WaitForSeconds(afterDialogue);

        // 3) 메크로 쪽으로 줌인 (줌이 끝날 때 CameraFollow 가 보던 자리와 같게)
        yield return MoveCamera(view.transform.position, Vector3.zero, overSize, zoomSize, zoomTime, true);
        if (follow != null) { follow.enabled = true; follow.SnapToTarget(); }

        CutsceneLock.Unlock(this);
        running = false;
        if (speaker != null && speaker.Bubble != null) speaker.Bubble.transform.localScale = Vector3.one;
    }

    /// <param name="toPlayer">true 면 도착점을 매 프레임 메크로 위치로 다시 계산</param>
    IEnumerator MoveCamera(Vector3 from, Vector3 to, float fromSize, float toSize, float time, bool toPlayer)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / time);
            view.orthographicSize = Mathf.Lerp(fromSize, toSize, k);
            view.transform.position = Vector3.Lerp(from, toPlayer ? FollowTarget() : to, k);
            yield return null;
        }
        view.orthographicSize = toSize;
        view.transform.position = toPlayer ? FollowTarget() : to;
    }

    void LateUpdate()
    {
        // 카메라가 멀어진 만큼 말풍선을 키움 (꼬리 끝은 머리 위 그대로)
        if (!running || speaker == null || speaker.Bubble == null || zoomSize <= 0f) return;
        // 말풍선이 켜져 있을 때만 (꺼져 있으면 말풍선이 자리를 다시 잡지 않아 곱한 값이 계속 쌓임)
        if (!speaker.Bubble.gameObject.activeInHierarchy) return;
        float k = view.orthographicSize / zoomSize;
        var bt = speaker.Bubble.transform;
        Vector3 anchor = speaker.BubbleAnchor;
        bt.position = anchor + (bt.position - anchor) * k;
        bt.localScale = Vector3.one * k;
    }

    Vector3 FollowTarget()
    {
        Vector3 p = player.transform.position + (follow != null ? follow.offset : Vector3.zero);
        p.z = view.transform.position.z;
        return p;
    }
}
