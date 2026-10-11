using UnityEngine;

/// <summary>
/// 백신 추격전 시작/끝 지점. 플레이어(FallRespawn 보유)가 트리거에 들어오면 연결된 VaccineWall을 시작(Begin)하거나 멈춘다(Stop).
/// 시작 트리거는 다시 밟아도 되므로(추격 전 지점에서 부활한 경우) 매번 Begin을 부른다 — 이미 쫓는 중이면 벽이 무시.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class VaccineChaseTrigger : MonoBehaviour
{
    public enum Mode { Start, End }

    [Tooltip("Start: 추격 시작 / End: 추격 끝 (벽이 멈춤)")]
    public Mode mode = Mode.Start;
    [Tooltip("연결할 백신 벽 (비우면 씬에서 자동으로 찾음)")]
    public VaccineWall wall;
    [Tooltip("Start 전용: 처음 밟을 때 재생할 추격 시작 연출 (비우면 바로 추격). 한 번 본 뒤엔 바로 추격")]
    public VaccineChaseIntro intro;

    void Awake()
    {
        var c = GetComponent<Collider2D>();
        if (c != null) c.isTrigger = true;
        if (wall == null) wall = FindObjectOfType<VaccineWall>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (wall == null || other.GetComponentInParent<FallRespawn>() == null) return;
        if (mode == Mode.Start)
        {
            if (intro != null && !intro.Played) intro.Play(wall);
            else if (intro == null || !intro.Playing) wall.Begin();
        }
        else wall.Stop();
    }

    void OnDrawGizmos()
    {
        var c = GetComponent<BoxCollider2D>();
        if (c == null) return;
        Gizmos.color = mode == Mode.Start ? new Color(1f, 0.3f, 0.3f, 0.35f) : new Color(0.3f, 1f, 0.3f, 0.35f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(c.offset, c.size);
    }
}
