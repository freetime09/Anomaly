using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면 꾸미기 (TitleController 옆에 붙임). 다 런타임에 만든다.
///  - 모눈종이: 배경에 연한 격자 (공책에 그리는 느낌).
///  - 커서 반짝이: 마우스가 지나간 자리에 로고 색 점이 잠깐 남음.
///  - O 시범: 안내문 위에서 O 를 그리는 선이 반복 (그리는 중·시작 연출 중엔 숨김).
///  - 낙서 기호: 화면 양옆에 스킬 모양(ㄱ ㄴ ㅣ O § ⋉)이 공책 낙서처럼 그려지고 천천히 둥둥 뜸.
///  - 로고 지지직: 가끔 로고 위로 빨강·하늘색 잔상과 색 줄이 잠깐 지나감.
/// </summary>
public class TitleDecor : MonoBehaviour
{
    public TitleController title;

    [Header("모눈종이")]
    public bool grid = true;
    [Tooltip("칸 크기 (units)")]
    public float gridCell = 0.5f;
    public Color gridColor = new Color(0.55f, 0.48f, 0.38f, 0.13f);

    [Header("커서 반짝이")]
    public bool sparkles = true;
    [Tooltip("이 거리(units)마다 점 하나")]
    public float sparkleSpacing = 0.12f;
    public float sparkleLife = 0.5f;
    public Vector2 sparkleSize = new Vector2(0.06f, 0.14f);
    public Color[] sparkleColors =
    {
        new Color(0.75f, 0.2f, 1f), new Color(1f, 0.15f, 0.15f), new Color(0f, 0.85f, 1f), new Color(0.1f, 0.2f, 1f),
        new Color(1f, 0.85f, 0f), new Color(0.1f, 0.9f, 0.2f), new Color(1f, 0.55f, 0.1f), new Color(1f, 0.45f, 0.75f),
    };

    [Header("낙서 기호")]
    public bool doodles = true;
    [Tooltip("한쪽에 그릴 개수 (양옆이라 총 2배)")]
    public int doodlesPerSide = 4;
    [Tooltip("기호 크기 (units, 최소~최대)")]
    public Vector2 doodleSize = new Vector2(0.7f, 1.1f);
    public float doodleWidth = 0.06f;
    [Range(0f, 1f)] public float doodleAlpha = 0.45f;
    [Tooltip("하나를 그리는 시간 (초)")]
    public float doodleDrawTime = 0.5f;
    [Tooltip("둥둥 뜨는 높이 (units)")]
    public float doodleBob = 0.1f;

    [Header("O 시범")]
    public bool demo = true;
    [Tooltip("안내문 위로 O 중심까지 (units)")]
    public float demoAbove = 1.35f;
    public float demoRadius = 0.5f;
    public float demoWidth = 0.07f;
    public Color demoColor = new Color(1f, 0.72f, 0.1f);
    public float demoDrawTime = 1.1f;
    public float demoHold = 0.4f;
    public float demoFade = 0.4f;
    public float demoPause = 1.2f;

    [Header("로고 지지직")]
    public bool logoGlitch = true;
    [Tooltip("다음 지지직까지 (초, 최소~최대)")]
    public Vector2 glitchEvery = new Vector2(3f, 6f);
    public float glitchTime = 0.25f;
    [Tooltip("로고가 옆으로 튀는 폭 (px)")]
    public float glitchJitter = 10f;

    Camera cam;
    static Sprite square;

    struct Spark { public SpriteRenderer sr; public float age, life, size; public Vector2 vel; }
    readonly List<Spark> sparks = new List<Spark>();
    readonly Stack<SpriteRenderer> sparkPool = new Stack<SpriteRenderer>();
    Vector3 lastSparkPos;
    bool hasLast;

    LineRenderer demoLine;

    void Start()
    {
        cam = Camera.main;
        if (title == null) title = FindAnyObjectByType<TitleController>();
        if (grid) BuildGrid();
        if (doodles) StartCoroutine(DrawDoodles());
        if (demo) { BuildDemo(); StartCoroutine(DemoLoop()); }
        if (logoGlitch && title != null && title.logo != null) StartCoroutine(LogoGlitchLoop());
    }

    bool Starting => title != null && title.Starting;

    void Update()
    {
        if (sparkles) UpdateSparkles();
        if (doodles) UpdateDoodles();
    }

    // ── 모눈종이 ──
    void BuildGrid()
    {
        if (cam == null) return;
        const int px = 32;
        var tex = new Texture2D(px, px, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        var clear = new Color(1f, 1f, 1f, 0f);
        for (int y = 0; y < px; y++)
            for (int x = 0; x < px; x++)
                tex.SetPixel(x, y, x == 0 || y == 0 ? Color.white : clear);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), px / gridCell, 0, SpriteMeshType.FullRect);
        var go = new GameObject("TitleGrid");
        go.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 5f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        float h = cam.orthographicSize * 2f + 2f;
        sr.size = new Vector2(h * 2.5f, h); // 넓은 화면 비율까지 덮음
        sr.color = gridColor;
        sr.sortingOrder = -100;
    }

    // ── 커서 반짝이 ──
    void UpdateSparkles()
    {
        if (cam != null && !Starting)
        {
            Vector3 sp = Input.mousePosition;
            sp.z = -cam.transform.position.z;
            Vector3 p = cam.ScreenToWorldPoint(sp);
            p.z = 0f;
            if (!hasLast) { lastSparkPos = p; hasLast = true; }
            float d = Vector3.Distance(p, lastSparkPos);
            if (d >= sparkleSpacing)
            {
                int n = Mathf.Min(4, Mathf.FloorToInt(d / sparkleSpacing));
                for (int i = 1; i <= n; i++) AddSpark(Vector3.Lerp(lastSparkPos, p, (float)i / n));
                lastSparkPos = p;
            }
        }
        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            var s = sparks[i];
            s.age += Time.unscaledDeltaTime;
            float k = s.age / s.life;
            if (k >= 1f || Starting)
            {
                s.sr.gameObject.SetActive(false);
                sparkPool.Push(s.sr);
                sparks.RemoveAt(i);
                continue;
            }
            s.sr.transform.position += (Vector3)(s.vel * Time.unscaledDeltaTime);
            s.sr.transform.localScale = Vector3.one * s.size * (1f - k);
            var c = s.sr.color; c.a = 1f - k * k; s.sr.color = c;
            sparks[i] = s;
        }
    }

    void AddSpark(Vector3 pos)
    {
        var sr = sparkPool.Count > 0 ? sparkPool.Pop() : NewSquare("Sparkle", 40);
        sr.gameObject.SetActive(true);
        sr.transform.position = pos + (Vector3)(Random.insideUnitCircle * 0.05f);
        sr.transform.rotation = Quaternion.Euler(0f, 0f, Random.value < 0.5f ? 0f : 45f);
        sr.color = sparkleColors.Length > 0 ? sparkleColors[Random.Range(0, sparkleColors.Length)] : Color.white;
        sparks.Add(new Spark
        {
            sr = sr, age = 0f, life = sparkleLife * Random.Range(0.7f, 1.2f),
            size = Random.Range(sparkleSize.x, sparkleSize.y),
            vel = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(-0.6f, -0.1f)),
        });
    }

    // ── 낙서 기호 ──
    struct Doodle { public Transform t; public Vector3 home; public float phase, speed, tilt; }
    readonly List<Doodle> doodleList = new List<Doodle>();

    IEnumerator DrawDoodles()
    {
        if (cam == null) yield break;
        float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
        Vector3 c = cam.transform.position;
        int n = Mathf.Max(0, doodlesPerSide);
        // 양옆 세로 칸에 하나씩 (안 겹치게), 칸 안에서 조금씩 흔들어 둠
        var slots = new List<Vector3>();
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < n; i++)
            {
                float y = Mathf.Lerp(halfH * 0.75f, -halfH * 0.75f, n == 1 ? 0.5f : i / (float)(n - 1));
                float x = side * halfW * (i % 2 == 0 ? Random.Range(0.8f, 0.86f) : Random.Range(0.64f, 0.7f));
                slots.Add(new Vector3(c.x + x, c.y + y + Random.Range(-0.25f, 0.25f), 0f));
            }
        // 순서를 섞어 여기저기서 그려지게
        for (int i = slots.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); var tmp = slots[i]; slots[i] = slots[j]; slots[j] = tmp; }

        yield return new WaitForSeconds(title != null ? title.dropDuration * 0.6f : 0.6f);
        var shapes = DoodleShapes();
        int last = -1;
        for (int i = 0; i < slots.Count; i++)
        {
            int k = Random.Range(0, shapes.Count);
            if (k == last) k = (k + 1) % shapes.Count; // 같은 기호 연달아 X
            last = k;
            StartCoroutine(DrawDoodle(shapes[k], slots[i]));
            yield return new WaitForSeconds(0.12f);
        }
    }

    IEnumerator DrawDoodle(Vector2[] shape, Vector3 pos)
    {
        var go = new GameObject("Doodle");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        float tilt = Random.Range(-15f, 15f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.numCornerVertices = 4;
        lr.numCapVertices = 4;
        lr.startWidth = lr.endWidth = doodleWidth;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.sortingOrder = 30;
        var col = sparkleColors.Length > 0 ? sparkleColors[Random.Range(0, sparkleColors.Length)] : Color.gray;
        col.a = doodleAlpha;
        lr.startColor = lr.endColor = col;
        float size = Random.Range(doodleSize.x, doodleSize.y);
        doodleList.Add(new Doodle { t = go.transform, home = pos, phase = Random.value * 10f, speed = Random.Range(0.5f, 0.9f), tilt = tilt });

        // 한 획으로 그려짐 (꺾인 선도 매끄럽게 나오도록 점을 촘촘히)
        shape = Densify(shape, 0.05f);
        var pts = new List<Vector3>();
        for (float t = 0f; ; t += Time.deltaTime)
        {
            int upTo = Mathf.Clamp(Mathf.CeilToInt(shape.Length * Mathf.Clamp01(t / doodleDrawTime)), 1, shape.Length);
            while (pts.Count < upTo) pts.Add(shape[pts.Count] * size);
            lr.positionCount = pts.Count;
            lr.SetPositions(pts.ToArray());
            if (upTo >= shape.Length) break;
            yield return null;
        }
    }

    void UpdateDoodles()
    {
        float now = Time.time;
        foreach (var d in doodleList)
        {
            if (d.t == null) continue;
            d.t.position = d.home + Vector3.up * Mathf.Sin(now * d.speed + d.phase) * doodleBob;
            d.t.rotation = Quaternion.Euler(0f, 0f, d.tilt + Mathf.Sin(now * d.speed * 0.7f + d.phase) * 4f);
        }
    }

    // 기호 모양 (가운데 기준, 대략 -0.5~0.5). 한 획으로 그림
    static List<Vector2[]> DoodleShapes()
    {
        var list = new List<Vector2[]>();
        list.Add(new[] { new Vector2(-0.4f, 0.4f), new Vector2(0.4f, 0.4f), new Vector2(0.4f, -0.45f) });   // ㄱ
        list.Add(new[] { new Vector2(-0.4f, 0.45f), new Vector2(-0.4f, -0.4f), new Vector2(0.45f, -0.4f) }); // ㄴ
        list.Add(new[] { new Vector2(0f, 0.5f), new Vector2(0f, -0.5f) });                                   // ㅣ
        list.Add(Arc(Vector2.zero, 0.42f, 90f, 90f + 370f, 28));                                            // O
        // § : 왼쪽·오른쪽으로 번갈아 볼록한 반원 네 개
        var sec = new List<Vector2>();
        sec.AddRange(Arc(new Vector2(0f, 0.375f), 0.125f, 20f, 270f, 10));
        sec.AddRange(Arc(new Vector2(0f, 0.125f), 0.125f, 90f, -90f, 10));
        sec.AddRange(Arc(new Vector2(0f, -0.125f), 0.125f, 90f, 270f, 10));
        sec.AddRange(Arc(new Vector2(0f, -0.375f), 0.125f, 90f, -160f, 10));
        list.Add(sec.ToArray());
        list.Add(new[] { new Vector2(-0.4f, -0.4f), new Vector2(-0.4f, 0.4f), new Vector2(0.4f, -0.4f), new Vector2(0.4f, 0.4f), new Vector2(-0.4f, -0.4f) }); // ⋉
        return list;
    }

    static Vector2[] Densify(Vector2[] src, float step)
    {
        var o = new List<Vector2> { src[0] };
        for (int i = 1; i < src.Length; i++)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(src[i - 1], src[i]) / step));
            for (int j = 1; j <= n; j++) o.Add(Vector2.Lerp(src[i - 1], src[i], j / (float)n));
        }
        return o.ToArray();
    }

    static Vector2[] Arc(Vector2 c, float r, float fromDeg, float toDeg, int steps)
    {
        var pts = new Vector2[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float a = Mathf.Lerp(fromDeg, toDeg, i / (float)steps) * Mathf.Deg2Rad;
            pts[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        return pts;
    }

    // ── O 시범 ──
    void BuildDemo()
    {
        var go = new GameObject("TitleODemo");
        demoLine = go.AddComponent<LineRenderer>();
        demoLine.useWorldSpace = true;
        demoLine.positionCount = 0;
        demoLine.numCornerVertices = 4;
        demoLine.numCapVertices = 4;
        demoLine.startWidth = demoLine.endWidth = demoWidth;
        demoLine.material = new Material(Shader.Find("Sprites/Default"));
        demoLine.sortingOrder = 45;
    }

    Vector3 DemoCenter()
    {
        Vector3 c = Vector3.zero;
        if (cam != null && title != null && title.hint != null)
        {
            Vector3 sp = RectTransformUtility.WorldToScreenPoint(cam, title.hint.rectTransform.position);
            sp.z = -cam.transform.position.z;
            c = cam.ScreenToWorldPoint(sp);
        }
        c.z = 0f;
        return c + Vector3.up * demoAbove;
    }

    IEnumerator DemoLoop()
    {
        const int steps = 48;
        var pts = new List<Vector3>();
        yield return new WaitForSeconds(title != null ? title.dropDuration + 0.3f : 1f);
        while (true)
        {
            // 그리는 중이거나 시작 연출 중엔 쉼
            while (Starting || Input.GetMouseButton(0)) { demoLine.positionCount = 0; if (Starting) yield break; yield return null; }

            Vector3 center = DemoCenter();
            SetDemoAlpha(1f);
            pts.Clear();
            bool cancelled = false;
            // 위에서 시작해 반시계로 한 바퀴 (조금 겹치게)
            for (float t = 0f; t < demoDrawTime; t += Time.deltaTime)
            {
                if (Starting || Input.GetMouseButton(0)) { cancelled = true; break; }
                int upTo = Mathf.CeilToInt(steps * Mathf.SmoothStep(0f, 1f, t / demoDrawTime));
                while (pts.Count <= upTo)
                {
                    float a = Mathf.PI * 0.5f + pts.Count / (float)steps * Mathf.PI * 2.1f;
                    pts.Add(center + new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * demoRadius);
                }
                demoLine.positionCount = pts.Count;
                demoLine.SetPositions(pts.ToArray());
                yield return null;
            }
            if (cancelled) continue;
            yield return Wait(demoHold);
            for (float t = 0f; t < demoFade; t += Time.deltaTime)
            {
                if (Starting || Input.GetMouseButton(0)) break;
                SetDemoAlpha(1f - t / demoFade);
                yield return null;
            }
            demoLine.positionCount = 0;
            yield return Wait(demoPause);
        }
    }

    IEnumerator Wait(float time)
    {
        for (float t = 0f; t < time && !Starting && !Input.GetMouseButton(0); t += Time.deltaTime) yield return null;
    }

    void SetDemoAlpha(float a)
    {
        var c = demoColor; c.a *= a;
        demoLine.startColor = demoLine.endColor = c;
    }

    // ── 로고 지지직 ──
    // 로고 자체는 안 건드린다 (O 를 그리는 순간 TitleController 가 로고 위치·색을 기준으로 연출하므로).
    // 로고 위에 빨강·하늘색 잔상 두 장 + 색 줄을 잠깐 띄운다.
    IEnumerator LogoGlitchLoop()
    {
        var logo = title.logo;
        var logoImg = logo.GetComponent<Image>();
        var ghosts = new List<Image>();
        var bars = new List<Image>();
        Color[] ghostColors = { new Color(1f, 0.1f, 0.3f, 0.35f), new Color(0f, 0.9f, 1f, 0.35f) };
        Color[] barColors = { new Color(1f, 0.1f, 0.3f, 0.55f), new Color(0f, 0.9f, 1f, 0.55f), new Color(0f, 0f, 0f, 0.45f), new Color(1f, 1f, 1f, 0.6f) };
        if (logoImg != null)
            foreach (var c in ghostColors)
            {
                var g = UiImage("LogoGhost", logo);
                g.color = c;
                ghosts.Add(g);
            }
        yield return new WaitForSeconds(title.dropDuration + 1f); // 떨어지는 연출 뒤부터
        while (!Starting)
        {
            yield return new WaitForSeconds(Random.Range(glitchEvery.x, glitchEvery.y));
            if (Starting) break;
            Vector2 size = logo.rect.size;
            for (float t = 0f; t < glitchTime && !Starting; t += 0.05f)
            {
                foreach (var g in ghosts)
                {
                    g.sprite = logoImg.sprite;
                    g.rectTransform.sizeDelta = size;
                    g.rectTransform.anchoredPosition = new Vector2(Random.Range(-glitchJitter, glitchJitter), Random.Range(-2f, 2f));
                    g.gameObject.SetActive(Random.value < 0.8f);
                }
                int n = Random.Range(2, 5);
                while (bars.Count < n) bars.Add(UiImage("GlitchBar", logo));
                for (int i = 0; i < bars.Count; i++)
                {
                    bool on = i < n;
                    bars[i].gameObject.SetActive(on);
                    if (!on) continue;
                    var rt = bars[i].rectTransform;
                    rt.sizeDelta = new Vector2(size.x * Random.Range(0.4f, 1.1f), Random.Range(4f, 18f));
                    rt.anchoredPosition = new Vector2(Random.Range(-0.2f, 0.2f) * size.x, Random.Range(-0.4f, 0.4f) * size.y);
                    bars[i].color = barColors[Random.Range(0, barColors.Length)];
                }
                yield return new WaitForSeconds(0.05f);
            }
            foreach (var g in ghosts) g.gameObject.SetActive(false);
            foreach (var b in bars) b.gameObject.SetActive(false);
        }
        foreach (var g in ghosts) if (g != null) g.gameObject.SetActive(false);
        foreach (var b in bars) if (b != null) b.gameObject.SetActive(false);
    }

    static Image UiImage(string name, Transform parent)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        img.raycastTarget = false;
        img.gameObject.SetActive(false);
        return img;
    }

    SpriteRenderer NewSquare(string name, int order)
    {
        if (square == null)
        {
            var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            square = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.sortingOrder = order;
        return sr;
    }
}
