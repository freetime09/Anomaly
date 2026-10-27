using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 검은방 배경 꾸미기 (카메라에 붙임). 각각 켜고 끌 수 있다. 모두 블럭·백신보다 뒤에 그린다.
///  - 숫자 비: 멀리서 0/1 이 세로로 천천히 흘러내림
///  - 먼지: 작은 픽셀 조각이 여러 깊이에서 떠다님
///  - 격자: 아주 옅은 격자선 (멀리 있는 회로판 느낌)
/// 셋 다 카메라보다 느리게 움직여(parallax) 멀리 있는 것처럼 보인다. 그림 없이 코드로 만든다.
/// </summary>
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(1000)] // 카메라 이동(CameraFollow) 뒤에 따라감
public class BackgroundDecor : MonoBehaviour
{
    [Tooltip("그릴 정렬 레이어 (블럭·백신보다 뒤)")]
    public string sortingLayer = "Vaccine";

    [Header("숫자 비 (0/1)")]
    public bool digits = true;
    [Tooltip("숫자 색 (알파 = 진하기)")]
    public Color digitColor = new Color(0.35f, 0.75f, 0.5f, 0.12f);
    [Tooltip("세로줄 사이 간격 (유닛)")]
    public float digitSpacing = 1.6f;
    [Tooltip("글자 크기")]
    public float digitSize = 3f;
    [Tooltip("흘러내리는 속도 (유닛/초, 최소~최대)")]
    public Vector2 digitSpeed = new Vector2(0.4f, 1.2f);
    [Tooltip("한 줄 글자 수 (최소~최대)")]
    public Vector2Int digitLength = new Vector2Int(6, 16);
    [Range(0f, 1f)] public float digitParallax = 0.85f;
    public int digitOrder = -120;

    [Header("먼지")]
    public bool dust = true;
    public Color dustColor = new Color(0.7f, 0.72f, 0.8f, 0.35f);
    public int dustCount = 45;
    [Tooltip("조각 크기 (최소~최대, 유닛)")]
    public Vector2 dustSize = new Vector2(0.04f, 0.12f);
    [Tooltip("떠다니는 속도 (유닛/초)")]
    public float dustSpeed = 0.25f;
    [Tooltip("깊이 (parallax 최소~최대). 작을수록 가까이")]
    public Vector2 dustParallax = new Vector2(0.4f, 0.85f);
    public int dustOrder = -90;

    [Header("격자")]
    public bool grid = true;
    public Color gridColor = new Color(0.45f, 0.5f, 0.65f, 0.07f);
    [Tooltip("격자 한 칸 크기 (유닛)")]
    public float gridSize = 2f;
    [Range(0f, 1f)] public float gridParallax = 0.9f;
    public int gridOrder = -130;

    Camera cam;
    Transform root;
    Sprite pixel;

    class Column { public TMPro.TextMeshPro txt; public float bx, by, speed, len, retext; }
    readonly List<Column> columns = new List<Column>();

    class Mote { public SpriteRenderer sr; public float bx, by, p, size, phase; public Vector2 vel; }
    readonly List<Mote> motes = new List<Mote>();

    SpriteRenderer gridSr;
    Texture2D gridTex;

    float W, H; // 감쌀 영역 (화면 + 여유)
    float builtOrtho; // 이 크기 화면까지 덮게 만들어 둠 (카메라를 더 빼면 다시 만듦)

    void OnEnable()
    {
        cam = GetComponent<Camera>();
        Build();
    }

    void OnDisable()
    {
        if (root != null) Destroy(root.gameObject);
        root = null;
        columns.Clear(); motes.Clear(); gridSr = null;
    }

    void OnDestroy()
    {
        if (gridTex != null) Destroy(gridTex);
        if (pixel != null) Destroy(pixel.texture);
    }

    void Build()
    {
        root = new GameObject("BackgroundDecor").transform;
        root.gameObject.hideFlags = HideFlags.DontSave;
        builtOrtho = cam.orthographicSize * 1.3f; // 카메라를 조금 빼도 다시 안 만들게 여유
        float h = builtOrtho * 2f, w = h * cam.aspect;
        W = w + 6f; H = h + 6f;
        var rnd = new System.Random(5);

        if (digits)
        {
            int n = Mathf.CeilToInt(W / Mathf.Max(0.3f, digitSpacing));
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Digits");
                go.transform.SetParent(root, false);
                var t = go.AddComponent<TMPro.TextMeshPro>();
                t.fontSize = digitSize;
                t.alignment = TMPro.TextAlignmentOptions.Top;
                t.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                t.lineSpacing = -20f;
                t.rectTransform.sizeDelta = new Vector2(1f, 1f);
                t.rectTransform.pivot = new Vector2(0.5f, 1f);
                var mr = t.GetComponent<MeshRenderer>();
                mr.sortingLayerName = sortingLayer;
                mr.sortingOrder = digitOrder;
                var c = new Column
                {
                    txt = t,
                    bx = i * digitSpacing + (float)rnd.NextDouble() * 0.3f,
                    by = (float)rnd.NextDouble() * H,
                    speed = Mathf.Lerp(digitSpeed.x, digitSpeed.y, (float)rnd.NextDouble()),
                };
                Retext(c, rnd);
                columns.Add(c);
            }
        }

        if (dust)
        {
            if (pixel == null)
            {
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }
            for (int i = 0; i < dustCount; i++)
            {
                var go = new GameObject("Dust");
                go.transform.SetParent(root, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = pixel;
                sr.sortingLayerName = sortingLayer;
                sr.sortingOrder = dustOrder;
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                motes.Add(new Mote
                {
                    sr = sr,
                    bx = (float)rnd.NextDouble() * W,
                    by = (float)rnd.NextDouble() * H,
                    p = Mathf.Lerp(dustParallax.x, dustParallax.y, (float)rnd.NextDouble()),
                    size = Mathf.Lerp(dustSize.x, dustSize.y, (float)rnd.NextDouble()),
                    phase = (float)rnd.NextDouble() * 10f,
                    vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dustSpeed * Mathf.Lerp(0.4f, 1f, (float)rnd.NextDouble()),
                });
            }
        }

        if (grid)
        {
            if (gridTex == null) gridTex = MakeGridTexture(64);
            var go = new GameObject("Grid");
            go.transform.SetParent(root, false);
            gridSr = go.AddComponent<SpriteRenderer>();
            gridSr.sprite = Sprite.Create(gridTex, new Rect(0, 0, gridTex.width, gridTex.height), new Vector2(0.5f, 0.5f), gridTex.width, 0, SpriteMeshType.FullRect);
            gridSr.drawMode = SpriteDrawMode.Tiled;
            gridSr.tileMode = SpriteTileMode.Continuous;
            gridSr.sortingLayerName = sortingLayer;
            gridSr.sortingOrder = gridOrder;
        }
    }

    void Retext(Column c, System.Random rnd)
    {
        int len = rnd.Next(digitLength.x, digitLength.y + 1);
        var sb = new System.Text.StringBuilder();
        for (int k = 0; k < len; k++)
        {
            // 아래(머리)로 갈수록 진하게
            int a = Mathf.RoundToInt(Mathf.Lerp(40f, 255f, (k + 1f) / len));
            sb.Append("<alpha=#").Append(a.ToString("X2")).Append('>').Append(rnd.Next(2) == 0 ? '0' : '1');
            if (k < len - 1) sb.Append('\n');
        }
        c.txt.text = sb.ToString();
        c.len = len * digitSize * 0.11f; // 대략적인 줄 높이 (감쌀 때 여유)
        c.retext = 0.15f + (float)rnd.NextDouble() * 0.4f;
    }

    // 카메라 기준 감싸기: base 값이 카메라보다 (1 - parallax) 만큼 느리게 움직이고, 영역 밖으로 나가면 반대편에서 나옴
    static float Wrap(float camPos, float b, float parallax, float size)
    {
        return camPos + Mathf.Repeat(b - camPos * (1f - parallax), size) - size * 0.5f;
    }

    readonly System.Random live = new System.Random(9);

    void LateUpdate()
    {
        if (root == null) return;
        if (cam.orthographicSize > builtOrtho) { OnDisable(); Build(); } // 보스전처럼 카메라를 크게 빼면 넓게 다시 만듦
        Vector3 c = cam.transform.position;
        float t = Time.time;

        foreach (var col in columns)
        {
            col.txt.color = digitColor;
            col.by -= col.speed * Time.deltaTime;
            float x = Wrap(c.x, col.bx, digitParallax, W);
            float y = Wrap(c.y, col.by, digitParallax, H + col.len) + col.len * 0.5f;
            col.txt.transform.position = new Vector3(x, y, 0f);
            // 가끔 숫자가 바뀜
            col.retext -= Time.deltaTime;
            if (col.retext <= 0f) Retext(col, live);
        }

        foreach (var m in motes)
        {
            Vector2 drift = m.vel * t + new Vector2(Mathf.Sin(t * 0.7f + m.phase), Mathf.Cos(t * 0.5f + m.phase * 1.3f)) * 0.3f;
            float x = Wrap(c.x, m.bx + drift.x, m.p, W);
            float y = Wrap(c.y, m.by + drift.y, m.p, H);
            m.sr.transform.position = new Vector3(x, y, 0f);
            m.sr.transform.localScale = Vector3.one * m.size;
            var col = dustColor;
            col.a *= 0.5f + 0.5f * Mathf.Sin(t * 1.3f + m.phase * 3f); // 천천히 반짝임
            m.sr.color = col;
        }

        if (gridSr != null)
        {
            float g = Mathf.Max(0.25f, gridSize);
            gridSr.color = gridColor;
            gridSr.transform.localScale = new Vector3(g, g, 1f);
            gridSr.size = new Vector2(W / g + 2f, H / g + 2f);
            float ox = Mathf.Repeat(c.x * (1f - gridParallax), g);
            float oy = Mathf.Repeat(c.y * (1f - gridParallax), g);
            gridSr.transform.position = new Vector3(c.x - ox + g * 0.5f, c.y - oy + g * 0.5f, 0f);
        }
    }

    /// <summary>격자 한 칸: 왼쪽·아래 가장자리에 1픽셀 선 (이어 붙이면 격자).</summary>
    static Texture2D MakeGridTexture(int size)
    {
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = new Color32(255, 255, 255, (byte)(x == 0 || y == 0 ? 255 : 0));
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Point;
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
