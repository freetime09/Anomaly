using UnityEngine;

/// <summary>
/// 마을 분위기용 나비. 날갯짓 프레임을 돌려 가며, 놓인 자리 주변을 천천히 너울너울 날아다닌다.
///  - 프레임 애니메이션은 Animator 없이 스프라이트만 바꾼다 (프레임 4장짜리 단순 반복이라 이 편이 가볍고 설정이 쉽다).
///  - 비행 경로는 서로 다른 주기의 사인파 두 개를 섞어 매번 조금씩 다른 곡선을 그린다 (Random Seed 로 나비마다 다르게).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ButterflyFlutter : MonoBehaviour
{
    [Header("날갯짓")]
    [Tooltip("날갯짓 프레임들 (순서대로 반복). 예: nabee 의 Butterfly1_r0_c0 ~ c3")]
    public Sprite[] frames;
    [Tooltip("초당 프레임 수")]
    [Min(1f)] public float framesPerSecond = 10f;

    [Header("비행")]
    [Tooltip("처음 위치를 중심으로 좌우로 날아다니는 범위 (유닛)")]
    [Min(0f)] public float rangeX = 2.5f;
    [Tooltip("처음 위치를 중심으로 위아래로 날아다니는 범위 (유닛)")]
    [Min(0f)] public float rangeY = 0.8f;
    [Tooltip("비행 속도 배율. 클수록 빠르게 돈다")]
    [Min(0.01f)] public float speed = 0.35f;
    [Tooltip("나비마다 다른 경로를 그리게 하는 값. 같은 값이면 같은 경로")]
    public int randomSeed = 0;
    [Tooltip("켜면 날아가는 방향으로 좌우를 뒤집는다 (그림이 오른쪽을 보고 있다는 전제)")]
    public bool faceMoveDirection = true;

    SpriteRenderer sr;
    Vector3 home;
    float phaseX, phaseY, freqX, freqY, frameOffset;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        home = transform.position;
        var rng = new System.Random(randomSeed);
        phaseX = (float)rng.NextDouble() * 10f;
        phaseY = (float)rng.NextDouble() * 10f;
        freqX = 0.8f + (float)rng.NextDouble() * 0.6f;
        freqY = 1.7f + (float)rng.NextDouble() * 1.2f;
        frameOffset = (float)rng.NextDouble();
    }

    void Update()
    {
        float t = Time.time * speed;

        // 날갯짓
        if (frames != null && frames.Length > 0)
        {
            int i = (int)((Time.time + frameOffset) * framesPerSecond) % frames.Length;
            sr.sprite = frames[i];
        }

        // 너울너울: 큰 좌우 왕복 + 빠른 위아래 흔들림 + 느린 위아래 표류
        float x = Mathf.Sin(t * freqX + phaseX) * rangeX;
        float y = Mathf.Sin(t * freqY * 3f + phaseY) * rangeY * 0.35f
                + Mathf.Sin(t * freqY + phaseX) * rangeY * 0.65f;
        Vector3 next = home + new Vector3(x, y, 0f);

        if (faceMoveDirection)
        {
            float dx = next.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.0001f) sr.flipX = dx < 0f;
        }
        transform.position = next;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 c = Application.isPlaying ? home : transform.position;
        Gizmos.color = new Color(1f, 0.9f, 0.4f, 0.8f);
        Gizmos.DrawWireCube(c, new Vector3(rangeX * 2f, rangeY * 2f, 0f));
    }
}
