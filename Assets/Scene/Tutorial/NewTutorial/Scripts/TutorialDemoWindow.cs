using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 가운데에 크게 뜨는 '시범 창'. 배울 동작을 짧은 동영상처럼 끝없이 반복해서 보여 준다.
///  - 튜토리얼 곳곳의 홀로그램(TutorialHologram)에 다가가면 Open(번호) 로 열린다.
///  - 열려 있는 동안: 뒤의 게임 화면을 흐리고 어둡게 덮어 시범에 집중하게 하고, 메크로 조작을 잠근다.
///  - 영상은 끝없이 반복되고, 아래 진행 막대가 재생 위치를 보여 준다. 오른쪽 위 X 를 누르면 언제든 닫힌다 (ESC 도 됨).
///  - 닫은 뒤 다시 보려면 홀로그램 앞에서 O 를 그린다 (창 아래에 안내).
///  - 창 양옆 화살표를 누르면 지금까지 열린 다른 시범도 다시 볼 수 있다 (A / D, ← / → 도 됨).
///  - 창은 시작할 때 코드로 만든다 → 씬에 UI 를 꾸밀 필요 없이 Inspector 값(크기·색·글꼴)만 고치면 된다.
/// </summary>
[RequireComponent(typeof(TutorialDemoStage))]
public class TutorialDemoWindow : MonoBehaviour
{
    public static TutorialDemoWindow Instance { get; private set; }

    [Header("시범 목록 (배우는 순서대로)")]
    [Tooltip("튜토리얼에서 배우는 순서대로 넣는다. 번호는 0 부터 (홀로그램의 Lesson Index 와 같은 번호)")]
    public List<TutorialDemoClip> clips = new List<TutorialDemoClip>();

    [Header("조작 (X·화살표를 누르는 것 말고 키보드로도)")]
    [Tooltip("이전 시범으로")]
    public KeyCode prevKey = KeyCode.A;
    [Tooltip("다음 시범으로")]
    public KeyCode nextKey = KeyCode.D;
    [Tooltip("창 닫기 (언제든 가능)")]
    public KeyCode closeKey = KeyCode.Escape;

    [Header("창 모양")]
    [Tooltip("글꼴 (한글이 있는 글꼴). 비우면 TextMeshPro 기본 글꼴")]
    public TMP_FontAsset font;
    [Tooltip("둥근 모서리 그림 (9-slice). 비우면 네모")]
    public Sprite roundedSprite;
    [Tooltip("영상 크기 (1920x1080 화면 기준 픽셀). 시범 무대 해상도 비율과 맞출 것")]
    public Vector2 videoSize = new Vector2(960f, 600f);
    public Color panelColor = new Color(0.07f, 0.08f, 0.11f, 0.97f);
    public Color accentColor = new Color(0.35f, 0.85f, 1f);
    public Color titleColor = Color.white;
    public Color instructionColor = new Color(1f, 0.93f, 0.6f);
    public Color tipColor = new Color(0.68f, 0.72f, 0.8f);
    [Tooltip("뒤 화면을 어둡게 덮는 정도 (0 = 안 어둡게, 1 = 검게)")]
    [Range(0f, 1f)] public float backdropDarkness = 0.45f;
    [Tooltip("뒤 화면을 흐리게 하는 단계. 클수록 더 흐림 (단계마다 해상도를 반으로 줄였다 늘림)")]
    [Range(1, 6)] public int blurSteps = 4;
    [Tooltip("다른 UI 보다 위에 그려지는 순서 (설정 메뉴보다는 아래)")]
    public int canvasSortingOrder = 900;

    public bool IsOpen { get; private set; }
    public int CurrentIndex { get; private set; } = -1;
    public int UnlockedCount { get; private set; }

    TutorialDemoStage stage;
    Canvas canvas;
    CanvasGroup group;
    RectTransform panelRt;
    RawImage backdrop, video;
    Image progressFill;
    TextMeshProUGUI badgeText, titleText, pageText, instructionText, tipText, replayText;
    Image closeButton, prevButton, nextButton;
    Graphic closeIcon, prevIcon, nextIcon;
    RenderTexture backdropRt;
    Coroutine anim;
    int closedFrame = -1;

    static readonly string[] Circled = { "①", "②", "③", "④", "⑤", "⑥", "⑦", "⑧", "⑨", "⑩", "⑪", "⑫" };

#if UNITY_EDITOR
    void Reset()
    {
        font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DialogueSpeaker.DefaultFontPath); // 대사와 같은 Galmuri11
        roundedSprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }
#endif

    void Awake()
    {
        Instance = this;
        stage = GetComponent<TutorialDemoStage>();
        BuildUI();
        canvas.gameObject.SetActive(false);
        // 설정 메뉴도 ESC 를 쓰므로, 시범 창이 ESC 를 쓰는 동안(열려 있거나 방금 닫힌 프레임)에는 설정 메뉴가 무시하게 한다
        PauseMenu.EscapeBlocker = () => IsOpen || closedFrame == Time.frameCount;
    }

    void Start()
    {
        video.texture = stage.Texture; // 같은 오브젝트의 Awake 순서는 정해져 있지 않아 무대가 다 만들어진 Start 에서 연결
        stage.SetRendering(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        PauseMenu.EscapeBlocker = null;
        ReleaseBackdrop();
        if (IsOpen) CutsceneLock.Unlock(this);
    }

    void Update()
    {
        if (!IsOpen || PauseMenu.IsPaused) return;

        Vector2 m = Input.mousePosition;
        bool click = Input.GetMouseButtonDown(0);
        bool overPrev = Hover(prevButton, prevIcon, m, CurrentIndex > 0);
        bool overNext = Hover(nextButton, nextIcon, m, CurrentIndex < UnlockedCount - 1);
        bool overClose = Hover(closeButton, closeIcon, m, true);
        if (UnlockedCount > 1)
        {
            if ((click && overPrev) || Input.GetKeyDown(prevKey) || Input.GetKeyDown(KeyCode.LeftArrow)) ShowPage(CurrentIndex - 1);
            else if ((click && overNext) || Input.GetKeyDown(nextKey) || Input.GetKeyDown(KeyCode.RightArrow)) ShowPage(CurrentIndex + 1);
        }

        // 진행 막대: 처음 한 번은 끝까지 차오르는 걸 보여 주고, 다 본 뒤엔 반복 진행을 얇게 보여 준다
        float target = stage.LoopCount > 0 ? 1f : stage.Progress;
        progressFill.fillAmount = Mathf.MoveTowards(progressFill.fillAmount, target, Time.unscaledDeltaTime * 0.8f);

        // X 는 언제든 닫기. 한 번 끝까지 본 뒤에는 X 가 은은하게 깜빡여 '이제 해 보세요' 를 알려 준다
        if (!overClose && stage.LoopCount > 0) SetIconColor(closeIcon, new Color(1f, 1f, 1f, 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 4f)));
        if ((click && overClose) || Input.GetKeyDown(closeKey)) Close();
    }

    /// <summary>마우스가 버튼 위면 밝게 (강조색). 누를 수 없으면 흐리게.</summary>
    bool Hover(Image button, Graphic icon, Vector2 mouse, bool usable)
    {
        if (!button.gameObject.activeInHierarchy) return false;
        bool over = usable && RectTransformUtility.RectangleContainsScreenPoint(button.rectTransform, mouse, null);
        button.color = new Color(1f, 1f, 1f, !usable ? 0.04f : over ? 0.22f : 0.1f);
        var c = over ? accentColor : Color.white;
        c.a = usable ? 1f : 0.25f;
        SetIconColor(icon, c);
        return over;
    }

    static void SetIconColor(Graphic icon, Color c)
    {
        foreach (var g in icon.GetComponentsInChildren<Graphic>()) g.color = c;
    }

    // ───────────────────────── 외부에서 부르는 함수 ─────────────────────────

    /// <summary>index 번 시범을 (처음이면 열어서) 크게 보여 준다. 홀로그램이 부른다.</summary>
    public void Open(int index)
    {
        if (index < 0 || index >= clips.Count) { Debug.LogWarning($"[TutorialDemoWindow] 없는 시범 번호: {index}"); return; }
        UnlockedCount = Mathf.Max(UnlockedCount, index + 1);
        if (!IsOpen)
        {
            IsOpen = true;
            CutsceneLock.Lock(this);       // 시범을 보는 동안 메크로 조작 잠금 (그리던 획도 취소됨)
            stage.SetRendering(true);
            StartAnim(OpenRoutine());
        }
        ShowPage(index);
    }

    /// <summary>창을 닫고 조작을 돌려준다.</summary>
    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        closedFrame = Time.frameCount;
        CutsceneLock.Unlock(this);
        StartAnim(CloseRoutine());
    }

    void ShowPage(int index)
    {
        if (UnlockedCount == 0) return;
        index = Mathf.Clamp(index, 0, UnlockedCount - 1);
        if (index == CurrentIndex && stage.Current == clips[index]) return;
        CurrentIndex = index;
        var clip = clips[index];
        badgeText.text = $"TUTORIAL  {index + 1:00}";
        titleText.text = $"{(index < Circled.Length ? Circled[index] : "")} {clip.title}";
        pageText.text = UnlockedCount > 1 ? Dots(index) : "";
        instructionText.text = clip.instruction;
        tipText.text = clip.tip;
        prevButton.gameObject.SetActive(UnlockedCount > 1);
        nextButton.gameObject.SetActive(UnlockedCount > 1);
        progressFill.fillAmount = 0f;
        stage.Play(clip);
        StartCoroutine(FlashVideo());
    }

    string Dots(int index)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < UnlockedCount; i++)
            sb.Append(i == index ? $"<color=#{ColorUtility.ToHtmlStringRGB(accentColor)}>●</color>" : "○");
        return sb.ToString();
    }

    // ───────────────────────── 열기·닫기 연출 ─────────────────────────

    void StartAnim(IEnumerator routine)
    {
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(routine);
    }

    IEnumerator OpenRoutine()
    {
        // 창을 띄우기 전의 게임 화면을 찍어 흐리게 만든다 (이번 프레임이 다 그려진 뒤에 찍어야 함)
        yield return new WaitForEndOfFrame();
        CaptureBackdrop();
        canvas.gameObject.SetActive(true);
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.25f)
        {
            float e = 1f - (1f - t) * (1f - t); // 처음엔 빠르게, 끝엔 부드럽게
            group.alpha = e;
            panelRt.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, e);
            yield return null;
        }
        group.alpha = 1f; panelRt.localScale = Vector3.one;
    }

    IEnumerator CloseRoutine()
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.18f)
        {
            group.alpha = 1f - t;
            panelRt.localScale = Vector3.one * Mathf.Lerp(1f, 0.96f, t);
            yield return null;
        }
        canvas.gameObject.SetActive(false);
        stage.SetRendering(false);
        ReleaseBackdrop();
    }

    IEnumerator FlashVideo()
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.2f)
        {
            video.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.15f, 1f, t));
            yield return null;
        }
        video.color = Color.white;
    }

    /// <summary>지금 화면을 찍어 해상도를 여러 번 반으로 줄였다가 다시 키운다 → 부드럽게 번진(흐린) 배경이 된다.
    /// 셰이더 없이 기본 기능(화면 캡처 + Blit)만으로 흐림을 만들어 어떤 렌더 설정에서도 동작한다.</summary>
    void CaptureBackdrop()
    {
        ReleaseBackdrop();
        int w = Screen.width, h = Screen.height;
        var full = RenderTexture.GetTemporary(w, h, 0);
        ScreenCapture.CaptureScreenshotIntoRenderTexture(full);
        RenderTexture cur = full;
        for (int i = 0; i < blurSteps; i++)
        {
            w = Mathf.Max(8, w / 2); h = Mathf.Max(8, h / 2);
            var next = RenderTexture.GetTemporary(w, h, 0);
            next.filterMode = FilterMode.Bilinear;
            Graphics.Blit(cur, next);
            if (cur != full) RenderTexture.ReleaseTemporary(cur);
            cur = next;
        }
        for (int i = 0; i < 2; i++) // 두 번 키워 계단 무늬 없이 부드럽게
        {
            w *= 2; h *= 2;
            var next = RenderTexture.GetTemporary(w, h, 0);
            next.filterMode = FilterMode.Bilinear;
            Graphics.Blit(cur, next);
            RenderTexture.ReleaseTemporary(cur);
            cur = next;
        }
        RenderTexture.ReleaseTemporary(full);
        backdropRt = cur;
        backdrop.texture = backdropRt;
        // 그래픽 API 에 따라 화면 캡처가 위아래 뒤집혀 들어오므로 맞춰 준다
        backdrop.uvRect = SystemInfo.graphicsUVStartsAtTop ? new Rect(0f, 1f, 1f, -1f) : new Rect(0f, 0f, 1f, 1f);
        backdrop.color = Color.Lerp(Color.white, Color.black, backdropDarkness);
    }

    void ReleaseBackdrop()
    {
        if (backdropRt == null) return;
        if (backdrop != null) backdrop.texture = null;
        RenderTexture.ReleaseTemporary(backdropRt);
        backdropRt = null;
    }

    // ───────────────────────── 화면 구성 ─────────────────────────

    void BuildUI()
    {
        var canvasGo = new GameObject("TutorialDemoWindow (UI)");
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        group = canvasGo.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false; group.interactable = false;

        // 흐린 배경 (화면 전체)
        backdrop = Add<RawImage>(canvasGo.transform, "Backdrop");
        Stretch(backdrop.rectTransform);

        const float pad = 30f;
        float panelW = videoSize.x + pad * 2f;
        float panelH = pad + 86f + videoSize.y + 8f + 6f + 22f + 52f + 44f + 30f + 56f + pad;

        // 창 묶음(열고 닫을 때 같이 커지고 작아짐) = 그림자 → 창 순서로 그린다
        var windowGo = new GameObject("Window", typeof(RectTransform));
        windowGo.transform.SetParent(canvasGo.transform, false);
        var windowRt = (RectTransform)windowGo.transform;
        Center(windowRt, new Vector2(panelW, panelH), Vector2.zero);
        var shadow = Add<Image>(windowRt, "Shadow");
        Center(shadow.rectTransform, new Vector2(panelW + 24f, panelH + 24f), new Vector2(0f, -10f));
        shadow.sprite = roundedSprite; shadow.type = Image.Type.Sliced; shadow.color = new Color(0f, 0f, 0f, 0.45f);
        // 얇은 테두리: 창보다 살짝 큰 강조색 판을 창 뒤에 깐다
        // (Outline 효과는 창 전체를 강조색으로 여러 번 겹쳐 그려 반투명한 창이 푸르게 물들어서 쓰지 않음)
        var border = Add<Image>(windowRt, "Border");
        Center(border.rectTransform, new Vector2(panelW + 4f, panelH + 4f), Vector2.zero);
        border.sprite = roundedSprite; border.type = Image.Type.Sliced;
        border.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.45f);
        var panel = Add<Image>(windowRt, "Panel");
        Center(panel.rectTransform, new Vector2(panelW, panelH), Vector2.zero);
        panel.sprite = roundedSprite; panel.type = Image.Type.Sliced; panel.color = panelColor;
        // 안쪽 내용(글씨·영상)은 묶음 아래에 창보다 나중에 추가 → 창 위에 그려짐. 묶음과 창은 크기·위치가 같아 배치 기준도 같다
        panelRt = windowRt;

        float y = -pad;
        // 머리글: 작은 배지 + 큰 제목, 오른쪽에 페이지 점
        badgeText = Text(panelRt, "Badge", new Vector2(pad, y), new Vector2(400f, 26f), 20f, accentColor, TextAlignmentOptions.TopLeft);
        badgeText.characterSpacing = 6f;
        titleText = Text(panelRt, "Title", new Vector2(pad, y - 26f), new Vector2(videoSize.x - 260f, 52f), 40f, titleColor, TextAlignmentOptions.TopLeft);
        pageText = Text(panelRt, "Pages", new Vector2(pad + videoSize.x - 400f - 64f, y - 34f), new Vector2(400f, 40f), 26f, tipColor, TextAlignmentOptions.TopRight);
        y -= 86f;

        // 영상: 둥근 틀 안에 (Mask 로 모서리를 둥글게 자름)
        var frame = Add<Image>(panelRt, "VideoFrame");
        TopLeft(frame.rectTransform, new Vector2(pad, y), videoSize);
        frame.sprite = roundedSprite; frame.type = Image.Type.Sliced; frame.color = stage.backgroundColor; // 영상 뒤 불투명 바탕
        frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        video = Add<RawImage>(frame.transform, "Video");
        Stretch(video.rectTransform);
        y -= videoSize.y + 8f;

        // 진행 막대
        var barBack = Add<Image>(panelRt, "ProgressBack");
        TopLeft(barBack.rectTransform, new Vector2(pad, y), new Vector2(videoSize.x, 6f));
        barBack.color = new Color(1f, 1f, 1f, 0.12f);
        progressFill = Add<Image>(barBack.transform, "ProgressFill");
        Stretch(progressFill.rectTransform);
        progressFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
        progressFill.type = Image.Type.Filled; progressFill.fillMethod = Image.FillMethod.Horizontal; progressFill.fillAmount = 0f;
        progressFill.color = accentColor;
        y -= 6f + 22f;

        instructionText = Text(panelRt, "Instruction", new Vector2(pad, y), new Vector2(videoSize.x, 52f * 2f), 32f, instructionColor, TextAlignmentOptions.TopLeft);
        instructionText.lineSpacing = 4f;
        y -= 52f + 44f;
        tipText = Text(panelRt, "Tip", new Vector2(pad, y), new Vector2(videoSize.x, 30f), 22f, tipColor, TextAlignmentOptions.TopLeft);
        y -= 30f + 22f;

        // 아래 줄: 다시 보는 방법 안내 (닫은 뒤에는 홀로그램 앞에서 O 를 그려야 다시 열림)
        replayText = Text(panelRt, "ReplayHint", new Vector2(pad, y + 2f), new Vector2(videoSize.x, 44f), 20f, tipColor, TextAlignmentOptions.Center);
        replayText.text = "다시 보기: 홀로그램 앞에서 <color=#FFE36A><b>O</b></color> 그리기";

        // 오른쪽 위 X: 누르면 닫기
        closeButton = Button(panelRt, "CloseButton", new Vector2(panelW - 18f - 52f, -18f), new Vector2(52f, 52f));
        closeIcon = Cross(closeButton.rectTransform, 24f, 4f);

        // 창 양옆 화살표: 누르면 이전·다음 시범 (영상 높이 가운데)
        float arrowTop = -pad - 86f - videoSize.y * 0.5f + 50f;
        prevButton = Button(panelRt, "PrevButton", new Vector2(-24f - 64f, arrowTop), new Vector2(64f, 100f));
        prevIcon = Text(prevButton.rectTransform, "Arrow", Vector2.zero, new Vector2(64f, 100f), 40f, Color.white, TextAlignmentOptions.Center);
        ((TextMeshProUGUI)prevIcon).text = "◀";
        nextButton = Button(panelRt, "NextButton", new Vector2(panelW + 24f, arrowTop), new Vector2(64f, 100f));
        nextIcon = Text(nextButton.rectTransform, "Arrow", Vector2.zero, new Vector2(64f, 100f), 40f, Color.white, TextAlignmentOptions.Center);
        ((TextMeshProUGUI)nextIcon).text = "▶";
    }

    Image Button(RectTransform parent, string name, Vector2 topLeft, Vector2 size)
    {
        var bg = Add<Image>(parent, name);
        TopLeft(bg.rectTransform, topLeft, size);
        bg.sprite = roundedSprite; bg.type = Image.Type.Sliced;
        bg.color = new Color(1f, 1f, 1f, 0.1f);
        return bg;
    }

    /// <summary>X 모양: 막대 두 개를 엇갈려 그린다 (글꼴에 없는 기호라도 보이게).</summary>
    static Graphic Cross(RectTransform parent, float size, float thick)
    {
        var a = Add<Image>(parent, "X");
        Center(a.rectTransform, new Vector2(size * 1.3f, thick), Vector2.zero);
        a.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var b = Add<Image>(a.rectTransform, "X2");
        Center(b.rectTransform, new Vector2(size * 1.3f, thick), Vector2.zero);
        b.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        return a;
    }

    TextMeshProUGUI Text(RectTransform parent, string name, Vector2 topLeft, Vector2 size, float fontSize, Color color, TextAlignmentOptions align)
    {
        var t = Add<TextMeshProUGUI>(parent, name);
        TopLeft(t.rectTransform, topLeft, size);
        if (font != null) t.font = font;
        t.fontSize = fontSize; t.color = color; t.alignment = align;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    static T Add<T>(Transform parent, string name) where T : Graphic
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var g = go.AddComponent<T>();
        g.raycastTarget = false; // 클릭을 받지 않음 (그리기 입력을 방해하지 않게)
        return g;
    }

    static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
    static void Center(RectTransform rt, Vector2 size, Vector2 offset) { rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = size; rt.anchoredPosition = offset; }
    static void TopLeft(RectTransform rt, Vector2 topLeft, Vector2 size) { rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f); rt.anchoredPosition = topLeft; rt.sizeDelta = size; }
}
