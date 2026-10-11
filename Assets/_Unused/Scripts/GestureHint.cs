using UnityEngine;

/// <summary>
/// 퍼즐 힌트: 어떤 제스처를 쓰면 되는지 아이콘(스킬 아이콘 등)을 그 자리에 깜빡이며 보여준다.
///  - signalId 채널이 켜져 있을 때만 보임 (-1이면 신호 상관없이)
///  - 플레이어가 showRadius 안에 있을 때만 보임
/// 예: 슬라이딩 문 — 발판을 밟아 문이 조금 열리면 문 앞에 ㄴ(슬라이드) 아이콘이 깜빡임.
/// </summary>
public class GestureHint : MonoBehaviour
{
    [Tooltip("보여줄 아이콘 (예: 스킬바의 ㄴ 아이콘)")]
    public Sprite icon;
    [Tooltip("아이콘 크기 (월드 단위, 가로 기준)")]
    public float size = 1.2f;
    [Tooltip("이 신호 채널이 켜져 있을 때만 보임. -1이면 항상")]
    public int signalId = -1;
    [Tooltip("플레이어가 이 거리 안에 있을 때만 보임")]
    public float showRadius = 7f;
    [Tooltip("깜빡임 속도 (초당 횟수)")]
    public float blinkRate = 2f;
    [Tooltip("위아래로 흔들리는 폭")]
    public float bob = 0.12f;
    public int sortingOrder = 20;

    SpriteRenderer sr;
    Transform player;
    float alpha;

    void Start()
    {
        var pc = FindObjectOfType<PlayerController>();
        if (pc != null) player = pc.transform;
        var go = new GameObject("HintIcon");
        go.transform.SetParent(transform, false);
        sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = icon;
        sr.sortingOrder = sortingOrder;
        if (icon != null)
        {
            float w = icon.bounds.size.x;
            go.transform.localScale = Vector3.one * (w > 0f ? size / w : 1f);
        }
        sr.color = new Color(1f, 1f, 1f, 0f);
    }

    void Update()
    {
        if (sr == null) return;
        bool want = (signalId < 0 || SignalBus.IsOn(signalId))
                    && player != null
                    && Vector2.Distance(player.position, transform.position) <= showRadius;
        alpha = Mathf.MoveTowards(alpha, want ? 1f : 0f, Time.deltaTime * 4f);
        float blink = 0.55f + 0.45f * Mathf.Cos(Time.time * blinkRate * Mathf.PI * 2f);
        sr.color = new Color(1f, 1f, 1f, alpha * blink);
        sr.transform.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 3f) * bob, 0f);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 1f, 0.3f, 0.6f);
        Gizmos.DrawWireCube(transform.position, Vector3.one * size);
    }
}
