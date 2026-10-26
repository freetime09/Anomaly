using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;
using TMPro;

/// <summary>
/// 메인 타이틀. 흐름:
///  1) 시작하면 Title2 로고가 위에서 아래로 떨어지며 등장(DOTween).
///  2) 마우스로 궤적을 그리면 LineRenderer로 실시간 표시.
///  3) 'O'(원)로 인식되면 줄무늬·검은 구멍으로 화면이 지지직 깨지고, 검은 화면에 "An§maly 실행 중..." 이 타자 치듯 나온 뒤 게임(Tutorial) 씬 로드.
/// 제스처 인식은 게임과 동일한 UnistrokeRecognizer + 번들 'O(상호작용)' 템플릿을 재사용한다.
/// </summary>
public class TitleController : MonoBehaviour
{
    [Header("씬 전환")]
    [Tooltip("O를 그리면 이동할 씬 이름")]
    public string startScene = "Tutorial";

    [Header("로고 낙하 연출")]
    [Tooltip("떨어질 로고(Title2). RectTransform")]
    public RectTransform logo;
    [Tooltip("시작 시 로고의 화면 밖 시작 높이(위쪽, +y)")]
    public float dropStartY = 720f;
    [Tooltip("로고가 멈출 최종 높이")]
    public float dropRestY = 120f;
    [Tooltip("낙하 시간(초)")]
    public float dropDuration = 1.0f;

    [Header("안내문")]
    [Tooltip("깜빡일 안내문 (선택)")]
    public Graphic hint;

    [Header("O 인식")]
    [Tooltip("이 이름의 번들 템플릿을 'O 시작'으로 사용")]
    public string startTemplateName = "O(상호작용)";
    [Tooltip("이 점수 이상이면 O로 인정 (0~1)")]
    public float recognizeThreshold = 0.80f;
    [Tooltip("번들 기본 템플릿 에셋(json). 지정하면 이걸로 로드 → 패키지·빌드에 자동 포함(누락 없음). 비우면 StreamingAssets 경로에서 읽음(구버전 호환)")]
    public TextAsset bundledTemplates;
    [Tooltip("이 거리(px)보다 가까운 점은 버려 손떨림 제거")]
    public float minGap = 5f;

    [Header("그리는 궤적")]
    [Tooltip("궤적 선 색 (밝은 배경이라 어두운 색 권장)")]
    public Color trailColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);
    [Tooltip("궤적 두께(월드 단위)")]
    public float trailWidth = 0.12f;

    [Header("시작 연출")]
    [Tooltip("O 를 그린 뒤 화면이 지지직 깨지는 시간(초). 줄무늬·검은 구멍이 점점 늘어남 (3챕터·엔딩과 같은 느낌)")]
    public float breakTime = 1.0f;
    [Tooltip("깨질 때 최대 줄무늬 수")]
    public int breakBars = 16;
    [Tooltip("다 깨진 뒤 글자가 나오기 전까지 검은 화면 시간(초)")]
    public float blackHold = 0.3f;

    [Header("실행 중 글자")]
    [Tooltip("검은 화면에 타자 치듯 나오는 글자 (끝의 점은 따로 깜빡임). 비우면 글자 없이 바로 로드")]
    public string bootText = "An§maly 실행 중";
    public TMP_FontAsset bootFont;
    public float bootFontSize = 40f;
    public Color bootColor = new Color(0.85f, 0.85f, 0.85f);
    [Tooltip("1초에 나오는 글자 수")]
    public float bootCharsPerSecond = 14f;
    [Tooltip("글자 뒤 점(. .. ...)이 도는 횟수")]
    public int bootDotLoops = 2;
    [Tooltip("점 하나가 늘어나는 간격(초)")]
    public float bootDotInterval = 0.25f;
    [Tooltip("다 나온 뒤 로드까지(초)")]
    public float bootHold = 0.3f;

    [Header("배경 오류 깜빡임")]
    [Tooltip("배경이 오류처럼 천천히 깜빡이게")]
    public bool bgFlicker = true;
    [Tooltip("느린 밝기 호흡 세기 (0~1)")]
    public float bgBreatheAmp = 0.05f;
    [Tooltip("호흡 속도")]
    public float bgBreatheSpeed = 1.1f;

    readonly List<Vector2> points = new List<Vector2>();
    Vector2 origin;
    bool drawing;
    bool starting;
    /// <summary>O 를 인식해 시작 연출 중인가 (TitleDecor 가 꾸미기를 멈출 때 씀)</summary>
    public bool Starting => starting;

    readonly List<UnistrokeRecognizer.Template> templates = new List<UnistrokeRecognizer.Template>();

    Camera cam;
    LineRenderer line;
    readonly List<Vector3> worldPts = new List<Vector3>();

    Color bgBaseColor;     // 카메라 원래 배경색 (깜빡임 기준)
    float nextBlip;        // 다음 오류 블립 시각
    bool blipping;
    float blipEnd;

    void Awake()
    {
        cam = Camera.main;
        if (cam != null) bgBaseColor = cam.backgroundColor;
        nextBlip = Random.Range(1.5f, 3f);
        if (logo != null)
        {
            PromoteLogoToTopOverlay();
        }
        LoadStartTemplates();
        BuildTrail();
    }

    void Start()
    {
        // 로고 낙하 등장
        if (logo != null)
        {
            Vector2 rest = new Vector2(logo.anchoredPosition.x, dropRestY);
            logo.anchoredPosition = new Vector2(rest.x, dropStartY);
            logo.DOAnchorPos(rest, dropDuration).SetEase(Ease.OutBounce);
        }
        // 안내문 은은하게 깜빡
        if (hint != null)
            hint.DOFade(0.25f, 0.9f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
    }

    void Update()
    {
        if (bgFlicker && !starting) UpdateBgFlicker();
        if (starting) return;
        if (Input.GetMouseButtonDown(0)) Begin();
        else if (Input.GetMouseButton(0) && drawing) Add();
        else if (Input.GetMouseButtonUp(0) && drawing) End();
    }

    // 배경을 오류처럼: 느린 밝기 호흡 + 가끔 짧게 툭 어두워지는 블립
    void UpdateBgFlicker()
    {
        if (cam == null) return;
        float now = Time.unscaledTime;
        if (now >= nextBlip)
        {
            blipping = true;
            blipEnd = now + Random.Range(0.03f, 0.09f);
            nextBlip = now + Random.Range(1.2f, 3.2f);
        }
        if (blipping && now >= blipEnd) blipping = false;

        float breathe = Mathf.Sin(now * bgBreatheSpeed) * bgBreatheAmp;
        float blip = blipping ? -0.10f : 0f;
        float k = breathe + blip;
        cam.backgroundColor = new Color(
            Mathf.Clamp01(bgBaseColor.r + k),
            Mathf.Clamp01(bgBaseColor.g + k),
            Mathf.Clamp01(bgBaseColor.b + k),
            1f);
    }

    // ── 그리기 ──────────────────────────────
    void Begin()
    {
        drawing = true;
        origin = Input.mousePosition;
        points.Clear();
        points.Add(Vector2.zero);
        worldPts.Clear();
        line.positionCount = 0;
        AddWorldPoint();
    }

    void Add()
    {
        Vector2 rel = (Vector2)Input.mousePosition - origin;
        if (Vector2.Distance(rel, points[points.Count - 1]) >= minGap)
        {
            points.Add(rel);
            AddWorldPoint();
        }
    }

    void AddWorldPoint()
    {
        if (cam == null) return;
        Vector3 sp = Input.mousePosition;
        sp.z = -cam.transform.position.z; // 카메라 → z=0 평면 거리
        Vector3 wp = cam.ScreenToWorldPoint(sp);
        wp.z = 0f;
        worldPts.Add(wp);
        line.positionCount = worldPts.Count;
        line.SetPosition(worldPts.Count - 1, wp);
    }

    void End()
    {
        drawing = false;
        var (name, score) = UnistrokeRecognizer.Recognize(points, templates);
        if (name != null && score >= recognizeThreshold)
        {
            Debug.Log($"[타이틀] 'O' 인식 (점수 {score:F2}) → {startScene} 시작");
            StartCoroutine(GlitchAndLoad());
        }
        else
        {
            Debug.Log($"[타이틀] O 인식 실패 (best {name}, {score:F2}) — 더 둥글게 그려보세요");
            StartCoroutine(ClearTrailSoon());
        }
    }

    IEnumerator ClearTrailSoon()
    {
        yield return new WaitForSeconds(0.15f);
        if (!starting) { line.positionCount = 0; worldPts.Clear(); }
    }

    // ── 지지직 → 씬 로드 ──────────────────────────────
    IEnumerator GlitchAndLoad()
    {
        starting = true;

        // 로고까지 덮는 줄무늬·검은 구멍이 점점 늘며 지지직 → 검은 화면 (페이드 없이 뚝)
        yield return BreakScreen();
        if (hint != null) hint.enabled = false;
        line.positionCount = 0; worldPts.Clear();

        yield return new WaitForSecondsRealtime(blackHold);
        yield return BootText();
        Debug.Log("[타이틀] 씬 로드 → " + startScene);
        SceneManager.LoadScene(startScene);
    }

    // ── 셋업 ──────────────────────────────
    void LoadStartTemplates()
    {
        templates.Clear();
        try
        {
            // 녹화 폴더(Resources/GestureRecordings, 모든 사람 파일)에서 O 를 다 모음.
            // 폴더가 비었으면 예전 방식: 지정된 번들 에셋 → 없으면 StreamingAssets 경로.
            var files = Resources.LoadAll<TextAsset>("GestureRecordings");
            foreach (var f in files)
            {
                var s = JsonUtility.FromJson<SerStore>(f.text);
                if (s == null || s.items == null || s.version < UnistrokeRecognizer.FormatVersion) continue;
                foreach (var it in s.items)
                    if (it.name == startTemplateName)
                        templates.Add(new UnistrokeRecognizer.Template { name = it.name, points = it.points });
            }
            string json = null;
            if (files.Length > 0) { } // 녹화 폴더를 썼으면 예전 번들 파일은 안 읽음
            else if (bundledTemplates != null) json = bundledTemplates.text;
            else
            {
                string path = System.IO.Path.Combine(Application.streamingAssetsPath, "gesture_templates.json");
                if (System.IO.File.Exists(path)) json = System.IO.File.ReadAllText(path);
            }
            if (!string.IsNullOrEmpty(json))
            {
                var store = JsonUtility.FromJson<SerStore>(json);
                if (store != null && store.version < UnistrokeRecognizer.FormatVersion)
                    Debug.LogWarning("[타이틀] 옛날 형식 템플릿이라 O 인식 불가 — 게임 씬에서 F9 로 다시 녹화하세요.");
                else if (store != null && store.items != null)
                    foreach (var it in store.items)
                        if (it.name == startTemplateName)
                            templates.Add(new UnistrokeRecognizer.Template { name = it.name, points = it.points });
            }
        }
        catch (System.Exception e) { Debug.LogWarning($"[타이틀] 템플릿 읽기 실패: {e.Message}"); }
        Debug.Log($"[타이틀] O 템플릿 {templates.Count}개 로드");
    }

    [System.Serializable] class SerTemplate { public string name; public List<Vector2> points; }
    [System.Serializable] class SerStore { public int version; public List<SerTemplate> items = new List<SerTemplate>(); }

    // 로고를 별도 Overlay 캔버스(sortingOrder 1000)로 옮겨 글리치 오버레이(999)보다 항상 위에 그린다.
    // 이러면 반전/글리치가 아무리 화면을 덮어도 로고는 계속 선명하게 보인다.
    // (Overlay 캔버스는 카메라 스냅샷에 안 찍히므로 스냅샷 속 로고와 이중으로 겹치지도 않는다.)
    void PromoteLogoToTopOverlay()
    {
        var lc = new GameObject("LogoOverlay");
        var canvas = lc.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var sc = lc.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.matchWidthOrHeight = 0.5f;
        logo.SetParent(lc.transform, false);
    }

    void BuildTrail()
    {
        var go = new GameObject("DrawTrail");
        line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 0;
        line.numCornerVertices = 6;
        line.numCapVertices = 6;
        line.startWidth = trailWidth;
        line.endWidth = trailWidth;
        line.startColor = trailColor;
        line.endColor = trailColor;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.sortingOrder = 50;
    }

    // 로고(1000) 위에서 화면이 깨지는 연출: 줄무늬가 지지직, 검은 구멍이 쌓이고, 로고가 흔들림.
    IEnumerator BreakScreen()
    {
        var go = new GameObject("BreakOverlay");
        var canvas = go.AddComponent<Canvas>();
        breakRoot = go.transform; // Canvas 를 붙이면 Transform 이 RectTransform 으로 바뀌므로 그 뒤에 잡음
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1100;
        var sc = go.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.matchWidthOrHeight = 0.5f;
        var area = (RectTransform)go.transform;

        Color[] palette = { Color.white, Color.black, new Color(0.2f, 1f, 1f), new Color(1f, 0.25f, 0.85f), new Color(0.6f, 0.6f, 0.65f) };
        var bars = new List<Image>();
        for (int i = 0; i < breakBars; i++) bars.Add(BreakImage(go.transform));
        var black = BreakImage(go.transform);
        black.color = Color.black;
        var rtB = black.rectTransform;
        rtB.anchorMin = Vector2.zero; rtB.anchorMax = Vector2.one; rtB.offsetMin = rtB.offsetMax = Vector2.zero;
        black.transform.SetAsLastSibling();
        black.gameObject.SetActive(false);

        Vector2 logoPos = logo != null ? logo.anchoredPosition : Vector2.zero;
        float t = 0f, tick = 0f;
        while (t < breakTime)
        {
            t += Time.unscaledDeltaTime;
            tick -= Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / breakTime);
            if (tick <= 0f)
            {
                tick = 0.05f;
                Vector2 size = area.rect.size;
                int n = Mathf.RoundToInt(Mathf.Lerp(0.3f, 1f, k) * breakBars);
                for (int i = 0; i < bars.Count; i++)
                {
                    bool on = i < n && Random.value < 0.8f;
                    bars[i].gameObject.SetActive(on);
                    if (!on) continue;
                    var rt = bars[i].rectTransform;
                    rt.sizeDelta = new Vector2(size.x * Random.Range(0.3f, 1.2f), Random.Range(4f, 40f) * (0.6f + k));
                    rt.anchoredPosition = new Vector2(Random.Range(-0.4f, 0.4f) * size.x, Random.Range(-0.5f, 0.5f) * size.y);
                    var c = palette[Random.Range(0, palette.Length)];
                    c.a = Random.Range(0.5f, 1f);
                    bars[i].color = c;
                }
                // 검은 구멍: 점점 자주, 점점 크게 쌓임 (지워지는 느낌)
                if (Random.value < Mathf.Lerp(0.2f, 1f, k))
                {
                    var hole = BreakImage(go.transform);
                    hole.color = Color.black;
                    hole.gameObject.SetActive(true);
                    var rt = hole.rectTransform;
                    rt.sizeDelta = new Vector2(Random.Range(80f, 500f) * (0.5f + k), Random.Range(30f, 220f) * (0.5f + k));
                    rt.anchoredPosition = new Vector2(Random.Range(-0.5f, 0.5f) * size.x, Random.Range(-0.5f, 0.5f) * size.y);
                }
                if (logo != null) logo.anchoredPosition = logoPos + Random.insideUnitCircle * Mathf.Lerp(4f, 24f, k);
            }
            yield return null;
        }
        if (logo != null) logo.anchoredPosition = logoPos;
        black.gameObject.SetActive(true);
        foreach (var b in bars) b.gameObject.SetActive(false);
    }

    Transform breakRoot;

    // 검은 화면 가운데에 "An§maly 실행 중" 을 타자 치듯 쓰고, 뒤의 점을 . .. ... 로 돌린다
    IEnumerator BootText()
    {
        if (string.IsNullOrEmpty(bootText) || breakRoot == null) yield break;
        var go = new GameObject("BootText", typeof(RectTransform));
        go.transform.SetParent(breakRoot, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (bootFont != null) tmp.font = bootFont;
        tmp.fontSize = bootFontSize;
        tmp.color = bootColor;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        // 다 쓴 글자 폭에 맞춰 가운데 두고 왼쪽부터 써 나감 → 쓰는 동안 글자가 옆으로 밀리지 않음
        var rt = tmp.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(tmp.GetPreferredValues(bootText + "...").x + 4f, 120f);
        float cps = Mathf.Max(1f, bootCharsPerSecond);
        for (int i = 1; i <= bootText.Length; i++)
        {
            tmp.text = bootText.Substring(0, i);
            yield return new WaitForSecondsRealtime(1f / cps);
        }
        for (int loop = 0; loop < Mathf.Max(1, bootDotLoops); loop++)
            for (int d = 0; d <= 3; d++)
            {
                tmp.text = bootText + new string('.', d);
                yield return new WaitForSecondsRealtime(bootDotInterval);
            }
        tmp.text = bootText + "...";
        yield return new WaitForSecondsRealtime(bootHold);
    }

    static Image BreakImage(Transform parent)
    {
        var img = new GameObject("Bar", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.raycastTarget = false;
        img.gameObject.SetActive(false);
        return img;
    }
}
