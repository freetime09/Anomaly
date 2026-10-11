using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

/// <summary>
/// 메인 타이틀. 흐름:
///  1) 시작하면 Title2 로고가 위에서 아래로 떨어지며 등장(DOTween).
///  2) 마우스로 궤적을 그리면 LineRenderer로 실시간 표시.
///  3) 'O'(원)로 인식되면 화면 전체를 "색상 반전 + 글리치"로 연출한 뒤 게임(Tutorial) 씬 로드.
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

    [Header("로고 지직 교체")]
    [Tooltip("O 인식 시 지직대며 바뀔 로고 (Title1)")]
    public Sprite glitchLogo;
    [Tooltip("로고가 지직대는 시간(초)")]
    public float logoGlitchTime = 0.5f;

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

    [Header("반전 글리치 연출")]
    [Tooltip("글리치 연출 총 시간(초)")]
    public float glitchDuration = 0.65f;
    [Tooltip("마지막에 초록색으로 물드는 시간(초)")]
    public float greenTime = 0.25f;
    [Tooltip("초록 상태로 멈춰 있다 로드까지의 시간(초)")]
    public float greenHold = 0.15f;

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

    readonly List<UnistrokeRecognizer.Template> templates = new List<UnistrokeRecognizer.Template>();

    Camera cam;
    LineRenderer line;
    readonly List<Vector3> worldPts = new List<Vector3>();

    Image logoImage;       // logo 의 Image (지직 교체용)
    Sprite baseLogoSprite; // 원래 로고(Title2) — 지직 중 왕복 표시용

    Color bgBaseColor;     // 카메라 원래 배경색 (깜빡임 기준)
    float nextBlip;        // 다음 오류 블립 시각
    bool blipping;
    float blipEnd;

    Material glitchMat;
    RawImage glitchImg;
    Canvas glitchCanvas;

    void Awake()
    {
        cam = Camera.main;
        if (cam != null) bgBaseColor = cam.backgroundColor;
        nextBlip = Random.Range(1.5f, 3f);
        if (logo != null)
        {
            PromoteLogoToTopOverlay();   // 로고를 반전 글리치보다 위에 → 연출 내내 항상 보임
            logoImage = logo.GetComponent<Image>();
            if (logoImage != null) baseLogoSprite = logoImage.sprite;
        }
        LoadStartTemplates();
        BuildTrail();
        BuildGlitchOverlay();
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

    // 로고가 흔들리고 알파가 껌뻑이며 Title2 ↔ Title1 을 왕복하다 Title1 로 고정
    IEnumerator LogoGlitchSwap()
    {
        if (logoImage == null) yield break;
        var rt = logoImage.rectTransform;
        Vector2 basePos = rt.anchoredPosition;
        Color baseCol = logoImage.color;
        float dur = logoGlitchTime;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = t / dur;
            float amp = 10f * (0.5f + k);
            rt.anchoredPosition = basePos + new Vector2(Random.Range(-amp, amp), Random.Range(-amp, amp));
            logoImage.color = new Color(1f, 1f, 1f, Random.Range(0.75f, 1f)); // 깜빡여도 안 사라지게
            if (k > 0.35f && glitchLogo != null)
                logoImage.sprite = (Random.value < 0.7f) ? glitchLogo : baseLogoSprite;
            // 배경도 같이 번쩍 (정상 ↔ 반전색)
            if (cam != null)
                cam.backgroundColor = (Random.value < 0.5f)
                    ? bgBaseColor
                    : new Color(1f - bgBaseColor.r, 1f - bgBaseColor.g, 1f - bgBaseColor.b, 1f);
            yield return null;
        }
        rt.anchoredPosition = basePos;
        logoImage.color = baseCol;
        if (cam != null) cam.backgroundColor = bgBaseColor;
        if (glitchLogo != null) logoImage.sprite = glitchLogo; // 최종 Title1
    }

    // ── 반전 글리치 → 씬 로드 ──────────────────────────────
    IEnumerator GlitchAndLoad()
    {
        starting = true;

        // 0) 로고가 지직대며 Title2 → Title1 로 교체
        yield return LogoGlitchSwap();
        Debug.Log("[타이틀] 1) 로고 교체 완료");

        // 1) 현재 화면을 RenderTexture로 스냅샷 (UI 포함 → SS-Camera 캔버스 사용)
        yield return new WaitForEndOfFrame();
        var rt = new RenderTexture(Screen.width, Screen.height, 16);
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        yield return null;              // URP가 이 프레임을 rt에 렌더
        cam.targetTexture = prev;       // 화면 렌더 복구
        Debug.Log("[타이틀] 2) 스냅샷 완료 rt=" + rt.width + "x" + rt.height + " mat=" + (glitchMat != null) + " img=" + (glitchImg != null));

        // 2) 스냅샷을 반전 글리치 오버레이에 띄움
        glitchImg.texture = rt;
        glitchCanvas.enabled = true;
        glitchImg.enabled = true;

        // 3) 글리치/반전 강도를 0→1로 올리며 연출 (중간에 몇 번 깜빡)
        float t = 0f;
        while (t < glitchDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / glitchDuration);
            glitchMat.SetFloat("_Glitch", k);
            // 반전은 강하게 껌뻑이다 마지막엔 완전 반전으로 고정
            float invert = (k < 0.75f) ? (Mathf.Sin(t * 45f) > 0f ? 1f : 0f) : 1f;
            glitchMat.SetFloat("_Invert", invert);
            yield return null;
        }
        glitchMat.SetFloat("_Glitch", 1f);
        glitchMat.SetFloat("_Invert", 1f);

        // 마지막: 배경을 초록으로, 로고(Title1)는 위에 계속 보이게.
        // 반전 글리치 오버레이를 서서히 걷어내 → 뒤의 초록 배경 + 로고가 드러남.
        Color green = new Color(0.13f, 0.9f, 0.25f, 1f);
        if (cam != null) cam.backgroundColor = green;
        if (hint != null) hint.enabled = false;      // 안내문 숨김
        line.positionCount = 0; worldPts.Clear();      // 그린 궤적 정리
        if (logoImage != null) logoImage.color = Color.white; // 로고 확실히 보이게
        float g = 0f;
        while (g < greenTime)
        {
            g += Time.unscaledDeltaTime;
            var c = glitchImg.color;
            c.a = Mathf.Lerp(1f, 0f, g / greenTime); // 오버레이 서서히 투명 → 초록+로고 드러남
            glitchImg.color = c;
            yield return null;
        }
        glitchCanvas.enabled = false;

        yield return new WaitForSecondsRealtime(greenHold);
        Debug.Log("[타이틀] 3) 씬 로드 → " + startScene);
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

    void BuildGlitchOverlay()
    {
        var canvasGO = new GameObject("GlitchOverlay");
        glitchCanvas = canvasGO.AddComponent<Canvas>();
        glitchCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        glitchCanvas.sortingOrder = 999;
        canvasGO.AddComponent<CanvasScaler>();
        glitchCanvas.enabled = false;

        var imgGO = new GameObject("Snapshot");
        imgGO.transform.SetParent(canvasGO.transform, false);
        glitchImg = imgGO.AddComponent<RawImage>();
        glitchMat = new Material(Shader.Find("UI/InvertGlitch"));
        glitchImg.material = glitchMat;
        glitchImg.enabled = false;
        var rt = glitchImg.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
