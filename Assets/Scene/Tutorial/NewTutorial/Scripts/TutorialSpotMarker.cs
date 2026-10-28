using TMPro;
using UnityEngine;

/// <summary>
/// "여기서 하세요" 바닥 표시. 위치가 헷갈리는 곳(나뭇가지 세울 자리, 던질 자리)에 둔다.
///  - 바닥에 은은하게 빛나는 자리 + 위아래로 움직이는 ▼ 화살표 + 짧은 명령형 글씨.
///  - 이 오브젝트 위치 = 바닥(서야 할 곳). 충돌 없음 (보이기만 함).
/// </summary>
public class TutorialSpotMarker : MonoBehaviour
{
    [Tooltip("화살표 위에 띄울 글씨 (짧은 명령형)")]
    [TextArea(1, 2)] public string label = "여기서 9";
    [Tooltip("표시 색")]
    public Color color = new Color(0.1f, 0.5f, 0.9f, 1f);
    [Tooltip("바닥 빛의 너비 (칸)")]
    [Min(0.2f)] public float width = 1.4f;
    [Tooltip("글씨 크기")]
    [Min(1f)] public float fontSize = 6.5f;
    public TMP_FontAsset font;

    SpriteRenderer glow;
    TextMeshPro arrow, text;
    static Sprite glowSprite;

#if UNITY_EDITOR
    void Reset() { font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DialogueSpeaker.DefaultFontPath); }
#endif

    void Awake()
    {
        var g = new GameObject("Glow");
        g.transform.SetParent(transform, false);
        g.transform.localScale = new Vector3(width, 0.35f, 1f);
        glow = g.AddComponent<SpriteRenderer>();
        glow.sprite = GlowSprite();
        glow.sortingOrder = 3;

        arrow = MakeText("Arrow", "▼", new Vector3(0f, 0.9f, 0f), fontSize * 0.9f);
        text = MakeText("Label", label, new Vector3(0f, 1.7f, 0f), fontSize);
    }

    TextMeshPro MakeText(string name, string s, Vector3 pos, float size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        var t = go.AddComponent<TextMeshPro>();
        if (font != null) t.font = font;
        t.text = s; t.fontSize = size; t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        t.rectTransform.sizeDelta = new Vector2(6f, 1.5f);
        t.sortingOrder = 6;
        // 흰·회색 배경 위에서도 읽히도록 흰 테두리 (이 글씨만의 재질 사본에 적용됨)
        t.outlineColor = Color.white;
        t.outlineWidth = 0.22f;
        return t;
    }

    void Update()
    {
        float t = Time.time;
        arrow.transform.localPosition = new Vector3(0f, 0.9f + Mathf.Sin(t * 4f) * 0.12f, 0f);
        float pulse = 0.65f + 0.35f * Mathf.Sin(t * 3f);
        arrow.color = new Color(color.r, color.g, color.b, color.a * pulse);
        text.color = color;
        glow.color = new Color(color.r, color.g, color.b, 0.45f * pulse);
    }

    /// <summary>가운데가 밝고 가장자리로 갈수록 사라지는 둥근 빛.</summary>
    static Sprite GlowSprite()
    {
        if (glowSprite != null) return glowSprite;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d)));
            }
        tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        return glowSprite;
    }
}
