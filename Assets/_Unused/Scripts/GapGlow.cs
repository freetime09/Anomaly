using UnityEngine;

/// <summary>
/// 낮은 틈에서 하얀 빛이 은은하게 새어 나오는 연출 — "여기 아래로 지나갈 수 있다"는 암시.
///  - 틈 중심에 두고 size로 빛 범위(가로·세로)를 정함. 가장자리로 갈수록 흐려지는 타원형 빛.
///  - signalId 채널이 켜져 있을 때만 보임 (-1이면 항상). 예: 발판을 밟아 문이 열렸을 때만.
/// 문 뒤에 그려지도록 sortingOrder를 문/외곽선보다 낮게 둘 것 (문이 닫히면 가려짐).
/// </summary>
public class GapGlow : MonoBehaviour
{
    [Tooltip("빛 범위 (월드 단위, 가로·세로)")]
    public Vector2 size = new Vector2(3f, 1.3f);
    public Color color = new Color(1f, 1f, 1f, 0.35f);
    [Tooltip("이 신호 채널이 켜져 있을 때만 보임. -1이면 항상")]
    public int signalId = -1;
    [Tooltip("켜지고 꺼지는 속도 (초당 알파 변화)")]
    public float fadeSpeed = 2f;
    [Tooltip("밝기 일렁임 속도 (초당 횟수)")]
    public float pulseRate = 0.6f;
    [Tooltip("밝기 일렁임 폭 (0~1)")]
    public float pulseAmount = 0.3f;
    public int sortingOrder = -4;

    static Sprite glowSprite;
    SpriteRenderer sr;
    float alpha;

    void Start()
    {
        var go = new GameObject("Glow");
        go.transform.SetParent(transform, false);
        sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetGlowSprite();
        sr.sortingOrder = sortingOrder;
        // 스프라이트는 1×1 유닛 → size로 늘림
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        alpha = (signalId < 0 || SignalBus.IsOn(signalId)) ? 1f : 0f;
        Apply();
    }

    void Update()
    {
        if (sr == null) return;
        bool want = signalId < 0 || SignalBus.IsOn(signalId);
        alpha = Mathf.MoveTowards(alpha, want ? 1f : 0f, Time.deltaTime * fadeSpeed);
        Apply();
    }

    void Apply()
    {
        float pulse = 1f - pulseAmount * 0.5f * (1f + Mathf.Sin(Time.time * pulseRate * Mathf.PI * 2f));
        var c = color;
        c.a *= alpha * pulse;
        sr.color = c;
    }

    /// <summary>가장자리로 갈수록 투명해지는 부드러운 타원 텍스처 (한 번만 만들어 공유).</summary>
    static Sprite GetGlowSprite()
    {
        if (glowSprite != null) return glowSprite;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f;
                float dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                px[y * n + x] = new Color(1f, 1f, 1f, a * a);
            }
        tex.SetPixels(px);
        tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        return glowSprite;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 1f, 1f, 0.5f);
        Gizmos.DrawWireCube(transform.position, new Vector3(size.x, size.y, 0f));
    }
}
