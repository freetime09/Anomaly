using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 챕터3 컷신들의 공통 부모. Timeline(PlayableDirector)의 신호가 부를 '동작'을 만들 때 자주 쓰는 도우미를 모아 두었다.
///  - 조작 잠금/해제 (CutsceneLock)
///  - 대사가 끝날 때까지 Timeline 일시정지 → 대사 길이가 바뀌어도 뒤 연출 타이밍이 어긋나지 않음
///  - 카메라 초점 옮기기/되돌리기
///  - 메크로 순간 이동, 카메라 배경색 바꾸기, 두리번거리기
/// 자식 클래스(Chapter3IntroCutscene, Chapter3HouseCutscene)는 장면마다 다른 동작만 추가한다.
/// </summary>
[RequireComponent(typeof(PlayableDirector))]
public abstract class Chapter3CutsceneBase : MonoBehaviour
{
    [Header("대상 (비우면 자동으로 찾음)")]
    [Tooltip("메크로 (PlayerController)")]
    public PlayerController player;

    [Header("카메라 초점")]
    [Tooltip("카메라가 초점 대상으로 천천히 이동하는 부드러움 (클수록 느림)")]
    [Min(0.01f)] public float focusSmoothTime = 0.6f;

    protected PlayableDirector director;
    protected CameraFollow cam;
    protected Camera view;

    Transform focusPoint;       // 카메라 초점용 빈 오브젝트 (CameraFollow 는 Transform 만 따라가므로)
    float originalSmoothTime;
    bool focusing;              // 카메라가 메크로가 아닌 곳을 비추는 중
    bool locked;                // 이 컷신이 조작을 잠그고 있음

    protected virtual void Awake()
    {
        director = GetComponent<PlayableDirector>();
        director.playOnAwake = false; // 언제 재생할지는 각 컷신이 정한다
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        cam = FindAnyObjectByType<CameraFollow>();
        view = cam != null ? cam.GetComponent<Camera>() : Camera.main;
    }

    /// <summary>조작을 잠그고 Timeline 을 처음부터 재생한다. (문·트리거 등 외부에서 컷신을 시작할 때)</summary>
    public void Play()
    {
        LockControl();
        director.Play();
    }

    /// <summary>[신호] 카메라를 다시 메크로에게 돌려준다.</summary>
    public void ReleaseCamera()
    {
        if (cam == null || !focusing) return;
        cam.target = player.transform;
        cam.smoothTime = originalSmoothTime;
        focusing = false;
    }

    // ───────── 자식 클래스용 도우미 ─────────

    protected void LockControl()
    {
        if (locked) return;
        CutsceneLock.Lock(this);
        locked = true;
    }

    protected void UnlockControl()
    {
        if (!locked) return;
        CutsceneLock.Unlock(this);
        locked = false;
    }

    /// <summary>대사를 재생하는 동안 Timeline 을 일시정지했다가, 끝나면 이어서 재생한다.</summary>
    protected void PlayDialogueHolding(DialogueData data)
    {
        if (data == null) return;
        director.Pause();
        DialoguePlayer.Ensure().Play(data, false, () => director.Resume());
    }

    /// <summary>카메라가 월드 위치 point 를 천천히 비추게 한다.</summary>
    protected void FocusCameraOn(Vector3 point)
    {
        if (cam == null) return;
        if (focusPoint == null) focusPoint = new GameObject("CameraFocusPoint").transform;
        focusPoint.position = point;
        if (!focusing) originalSmoothTime = cam.smoothTime; // 처음 초점을 옮길 때만 원래 값 기억
        cam.smoothTime = focusSmoothTime;
        cam.target = focusPoint;
        focusing = true;
    }

    /// <summary>좌우를 count 번 번갈아 본 뒤 finalDir(+1 오른쪽 / -1 왼쪽)을 본다.</summary>
    protected IEnumerator LookAroundRoutine(int count, float interval, int finalDir)
    {
        int dir = -1;
        for (int i = 0; i < count; i++)
        {
            player.Face(dir);
            dir = -dir;
            yield return new WaitForSeconds(interval);
        }
        player.Face(finalDir);
    }

    /// <summary>메크로를 point 위치로 옮기고 카메라도 즉시 따라붙인다.</summary>
    protected void MovePlayer(Transform point)
    {
        if (point == null || player == null) return;
        var rb = player.GetComponent<Rigidbody2D>();
        player.ResetMotion();
        player.transform.position = point.position;
        if (rb != null) rb.position = point.position; // 물리 위치도 같이 (보간 때문에 한 프레임 튀는 것 방지)
        if (cam != null) cam.SnapToTarget();
    }

    protected void SetBackground(Color c)
    {
        if (view != null) view.backgroundColor = c;
    }

    protected virtual void OnDisable()
    {
        // 컷신 도중 씬이 바뀌거나 꺼져도 조작 잠금이 남지 않게 정리
        UnlockControl();
    }
}
