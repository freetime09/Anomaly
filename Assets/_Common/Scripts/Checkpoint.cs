using UnityEngine;

/// <summary>
/// 체크포인트. 플레이어가 트리거 영역에 들어오면 이 지점을 '마지막 부활 지점'으로 등록한다.
///  - order(순서)가 현재 활성 체크포인트보다 뒤일 때만 갱신된다(앞으로만 진행, 뒤로 돌아가도 안 내려감).
///  - 비활성일 땐 CheckPointOff 애니, 활성화되면 CheckPoint 애니로 전환(Animator "Activated" bool).
/// 부활 로직 자체는 플레이어의 FallRespawn 이 담당하고, 이 컴포넌트는 지점만 등록한다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    [Tooltip("체크포인트 순서. 클수록 나중 지점. 이 값이 현재보다 클 때만 부활 지점으로 갱신(앞으로만). 배치 순서대로 0,1,2… 로 지정")]
    public int order = 0;
    [Tooltip("여기서 부활할 때 표식 기준 위치 보정. 플레이어가 살짝 어긋나면 이 값으로 조절")]
    public Vector2 respawnOffset = Vector2.zero;

    [Header("사운드")]
    [Tooltip("체크포인트가 새로 활성화될 때 재생할 소리")]
    public AudioClip activateClip;
    [Range(0f, 1f)]
    [Tooltip("소리 볼륨")]
    public float sfxVolume = 1f;

    Animator anim;
    AudioSource sfxSrc;

    void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        var c = GetComponent<Collider2D>();
        if (c != null) c.isTrigger = true; // 트리거로 강제 (충돌 아님)
        sfxSrc = gameObject.AddComponent<AudioSource>();
        sfxSrc.playOnAwake = false;
        SetActivated(false);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var fr = other.GetComponentInParent<FallRespawn>();
        if (fr == null) return; // 플레이어(FallRespawn 보유)만 인식
        Vector3 pos = transform.position + (Vector3)respawnOffset;
        if (fr.TrySetCheckpoint(order, pos)) // 새로 활성화될 때(앞으로 진행)만 true
        {
            SetActivated(true);
            if (activateClip != null && sfxSrc != null) sfxSrc.PlayOneShot(activateClip, sfxVolume); // 체크포인트 소리
        }
    }

    /// <summary>비활성(CheckPointOff)/활성(CheckPoint) 애니메이션으로 전환.</summary>
    public void SetActivated(bool on)
    {
        if (anim != null) anim.SetBool("Activated", on);
    }
}
