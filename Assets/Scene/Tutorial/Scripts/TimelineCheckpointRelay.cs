using UnityEngine;

/// <summary>
/// 이 스크립트를 붙인 오브젝트(자기만의 Collider2D를 가진 별도 오브젝트)에
/// 플레이어가 들어가면, 지정된 TimelineTrigger의 체크포인트 하나를 발동시킵니다.
///
/// TimelineTrigger는 한 오브젝트의 콜라이더만 감지할 수 있기 때문에,
/// "다른 곳에 있는 오브젝트에 들어가면"을 구현하려면 이렇게 별도 오브젝트에
/// 작은 릴레이 스크립트를 붙여서 TimelineTrigger에 신호를 보내는 방식이 필요합니다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TimelineCheckpointRelay : MonoBehaviour
{
    [Tooltip("발동시킬 체크포인트가 들어있는 TimelineTrigger")]
    [SerializeField] private TimelineTrigger targetTimeline;

    [Tooltip("TimelineTrigger의 Checkpoints 배열에서, 발동시킬 항목의 Id와 정확히 같은 문자열")]
    [SerializeField] private string checkpointId = "Checkpoint1";

    [SerializeField] private string playerTag = "Player";

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (targetTimeline == null) return;

        targetTimeline.TriggerCheckpoint(checkpointId);
    }
}
