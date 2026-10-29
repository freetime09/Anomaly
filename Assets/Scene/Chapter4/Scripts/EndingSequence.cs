using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보스전 뒤 엔딩: 하얀 빛 → 마을에 서 있는 메크로 (메크로 독백 → 마을 사람 대사) → 화면이 지지직 깨지며 지워짐 → "게임이 삭제되었습니다" 오류창 → 게임 종료.
/// BossFight.onDefeated → Play() 로 연결.
/// </summary>
[DefaultExecutionOrder(1200)] // 카메라 위치를 CameraFollow·CameraClampToSprite·BossFight 뒤에 정하도록
public class EndingSequence : MonoBehaviour
{
    [Header("연결")]
    public BossFight bossFight;
    [Tooltip("엔딩 때 켤 마을 (처음엔 꺼 둠)")]
    public GameObject villageRoot;
    [Tooltip("메크로가 설 자리")]
    public Transform playerSpawn;
    [Tooltip("메크로가 볼 방향 (1 = 오른쪽, -1 = 왼쪽)")]
    public int playerFacing = 1;
    [Tooltip("카메라 자리 (비우면 메크로 위치)")]
    public Transform cameraPoint;
    [Tooltip("마을 카메라 크기 (0 = 보스전 전 크기)")]
    public float cameraSize = 0f;
    [Tooltip("마을 배경색 (투명 = 그대로)")]
    public Color cameraBackground = new Color(0.113f, 0.113f, 0.113f, 1f);
    [Tooltip("카메라가 출발하는 자리 (카메라 자리 기준). 여기서 카메라 자리까지 천천히 옴")]
    public Vector2 cameraStartOffset = new Vector2(-16f, 0f);
    [Tooltip("출발 자리 → 카메라 자리까지 걸리는 시간 (0 = 하얀빛이 걷히는 시간 + 마을 비추는 시간)")]
    public float panTime = 0f;
    [Tooltip("카메라 자리를 지난 뒤 천천히 계속 가는 속도 (units/초). 대사 동안 마을 밖으로 안 나가게")]
    public float driftSpeed = 0.4f;
    [Tooltip("카메라 자리를 지난 뒤 driftSpeed 까지 느려지는 정도 (units/초²)")]
    public float panSlowDown = 1.5f;

    [Header("마을 대사")]
    [Tooltip("마을을 비춘 뒤 메크로 독백")]
    public DialogueData monologue;
    [Tooltip("지지직 직전 마을 사람 대사")]
    public DialogueData villagerLine;
    [Tooltip("마을 사람 대사를 할 엔딩 마을 NPC. DialogueSpeaker 가 없으면 엔딩 때 붙임")]
    public Transform villager;
    public string villagerSpeakerId = "Friend_B";
    public Vector2 villagerBubbleOffset = new Vector2(0f, 2.45f);
    [Tooltip("독백과 마을 사람 대사 사이 (초)")]
    public float betweenLines = 0.6f;
    [Tooltip("마을 사람 대사가 끝나고 지지직 시작까지 (초). 짧을수록 말이 끊기는 느낌")]
    public float afterVillager = 0.1f;

    [Header("시간 (초)")]
    public float startDelay = 0.6f;
    public float whiteInTime = 0.8f;
    public float whiteHold = 0.8f;
    public float whiteOutTime = 1.8f;
    [Tooltip("마을을 조용히 비추는 시간 (대사 전)")]
    public float villageHold = 3.5f;
    [Tooltip("지지직 깨지며 지워지는 시간")]
    public float glitchTime = 3f;
    public float blackHold = 1.2f;
    [Tooltip("오류창이 뜬 뒤 저절로 종료 (0 = 확인 누를 때까지)")]
    public float autoQuitTime = 0f;

    [Header("오류창")]
    public TMP_FontAsset font;
    public string dialogTitle = "An§maly";
    public string dialogMessage = "게임이 삭제되었습니다.";
    [Tooltip("오류창이 뜰 때 소리 (윈도우 오류음)")]
    public AudioClip errorClip;
    [Range(0f, 1f)] public float errorVolume = 1f;

    Camera cam;
    Canvas canvas;
    RectTransform canvasRect;
    Image white;
    readonly List<Image> bars = new List<Image>();
    readonly List<Image> holes = new List<Image>();
    Vector3 camPos, camFrom, camTo;
    float panLeft, panDur, panSpeed;
    bool camHeld;
    float shake;
    bool playing;
    static Sprite square, circle;

    public void Play()
    {
        if (playing) return;
        playing = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        CutsceneLock.Lock(this);
        cam = Camera.main;
        BuildCanvas();
        if (bossFight != null)
        {
            // 보스가 죽기 시작할 때 불림 → 죽는 연출 동안 하얀 빛이 차오르고, 끝나자마자 마을로 (빈틈 없음)
            yield return FadeImage(white, 0f, 1f, Mathf.Max(0.01f, bossFight.DefeatDuration));
        }
        else
        {
            yield return new WaitForSeconds(startDelay);
            yield return FadeImage(white, 0f, 1f, whiteInTime);
        }

        // 하얀 동안 마을로
        if (bossFight != null) bossFight.EndFight();
        if (villageRoot != null) villageRoot.SetActive(true);
        SetupVillager();
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null && playerSpawn != null)
        {
            player.ResetMotion();
            player.transform.position = playerSpawn.position;
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
            player.Face(playerFacing);
        }
        if (cam != null)
        {
            var follow = cam.GetComponent<CameraFollow>();
            if (follow != null) follow.enabled = false;
            var clamp = cam.GetComponent<CameraClampToSprite>();
            if (clamp != null) clamp.enabled = false;
            Vector3 p = cameraPoint != null ? cameraPoint.position : player != null ? player.transform.position : cam.transform.position;
            camTo = new Vector3(p.x, p.y, cam.transform.position.z);
            camFrom = camTo + (Vector3)cameraStartOffset;
            panDur = Mathf.Max(0.01f, panTime > 0f ? panTime : whiteOutTime + villageHold);
            panLeft = 0f;
            camPos = camFrom;
            if (cameraSize > 0f) cam.orthographicSize = cameraSize;
            if (cameraBackground.a > 0f) cam.backgroundColor = cameraBackground;
            cam.transform.position = camPos; // 마을 해·원경이 첫 프레임부터 이 자리를 기준으로 잡히게 바로 옮김
            camHeld = true;
        }
        yield return new WaitForSeconds(whiteHold);
        yield return FadeImage(white, 1f, 0f, whiteOutTime);

        // 조용한 마을 → 메크로 독백 → 마을 사람이 알아봄 (말이 끝나기 전에 지지직)
        yield return new WaitForSeconds(villageHold);
        if (monologue != null) yield return DialoguePlayer.Ensure().PlayAndWait(monologue);
        if (villagerLine != null)
        {
            yield return new WaitForSeconds(betweenLines);
            yield return DialoguePlayer.Ensure().PlayAndWait(villagerLine);
            yield return new WaitForSeconds(afterVillager);
        }

        // 지지직 → 구멍이 늘어나며 지워짐
        for (float t = 0f; t < glitchTime; t += Time.deltaTime)
        {
            float a = t / glitchTime;
            shake = Mathf.Lerp(0.02f, 0.5f, a * a);
            UpdateBars(Mathf.Lerp(0.15f, 1f, a));
            if (Random.value < Mathf.Lerp(0.02f, 0.5f, a)) AddHole(a);
            // 가끔 화면이 통째로 번쩍
            if (Random.value < Mathf.Lerp(0.01f, 0.08f, a)) StartCoroutine(Blink(Random.value < 0.5f ? Color.black : new Color(1f, 0.15f, 0.25f, 0.5f)));
            yield return null;
        }
        shake = 0f;
        UpdateBars(0f);
        white.color = Color.black;
        white.transform.SetAsLastSibling();
        foreach (var h in holes) Destroy(h.gameObject);
        holes.Clear();
        yield return new WaitForSeconds(blackHold);

        // 오류창
        RectTransform okButton, closeButton;
        Image okImage;
        BuildDialog(out okButton, out okImage, out closeButton);
        if (errorClip != null) gameObject.AddComponent<AudioSource>().PlayOneShot(errorClip, errorVolume);
        Color okColor = okImage.color, okHover = Color.Lerp(okColor, Color.white, 0.15f);
        float waited = 0f;
        while (true)
        {
            Vector2 m = Input.mousePosition;
            bool overOk = RectTransformUtility.RectangleContainsScreenPoint(okButton, m, null);
            bool overClose = RectTransformUtility.RectangleContainsScreenPoint(closeButton, m, null);
            okImage.color = overOk ? okHover : okColor;
            if (Input.GetMouseButtonDown(0) && (overOk || overClose)) break;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) break;
            waited += Time.unscaledDeltaTime;
            if (autoQuitTime > 0f && waited >= autoQuitTime) break;
            yield return null;
        }
        Quit();
    }

    void LateUpdate()
    {
        if (!camHeld || cam == null) return;
        // 하얀빛이 걷힐 때부터 왼쪽에서 천천히 들어와 멈추지 않고 계속 감 (panDur 에 카메라 자리를 지남)
        if (white.color.a < 1f)
        {
            panLeft += Time.deltaTime;
            if (panLeft <= panDur)
            {
                camPos = camFrom + (camTo - camFrom) * (panLeft / panDur);
                panSpeed = (camTo - camFrom).magnitude / panDur;
            }
            else
            {
                // 지난 뒤엔 천천히 느려지며 계속 감 (멈추지 않음)
                panSpeed = Mathf.MoveTowards(panSpeed, driftSpeed, panSlowDown * Time.deltaTime);
                camPos += (camTo - camFrom).normalized * panSpeed * Time.deltaTime;
            }
        }
        cam.transform.position = camPos + (Vector3)(Random.insideUnitCircle * shake);
    }

    void OnDestroy()
    {
        if (playing) CutsceneLock.Unlock(this);
    }

    /// <summary>엔딩 마을 NPC 는 스크립트를 빼고 복사해서, 말풍선(DialogueSpeaker)을 여기서 붙인다.</summary>
    void SetupVillager()
    {
        if (villager == null || villagerLine == null || villager.GetComponent<DialogueSpeaker>() != null) return;
        var go = villager.gameObject;
        bool was = go.activeSelf;
        go.SetActive(false); // Awake 전에 화자 값부터 넣으려고 잠깐 끔
        var sp = go.AddComponent<DialogueSpeaker>();
        sp.speakerId = villagerSpeakerId;
        sp.bubbleOffset = villagerBubbleOffset;
        sp.font = font;
        go.SetActive(was);
    }

    static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ───────────── 화면 효과 ─────────────

    IEnumerator FadeImage(Image img, float from, float to, float time)
    {
        var c = img.color;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            c.a = Mathf.Lerp(from, to, t / time);
            img.color = c;
            yield return null;
        }
        c.a = to;
        img.color = c;
    }

    IEnumerator Blink(Color c)
    {
        var img = MakeImage("Blink", canvasRect, c);
        Stretch(img.rectTransform);
        yield return new WaitForSeconds(Random.Range(0.03f, 0.08f));
        Destroy(img.gameObject);
    }

    /// <summary>가로 줄무늬 (빨강·파랑·하양·검정). power 만큼 많고 진하게.</summary>
    void UpdateBars(float power)
    {
        if (bars.Count == 0)
            for (int i = 0; i < 10; i++) bars.Add(MakeImage("GlitchBar", canvasRect, Color.clear));
        Vector2 size = canvasRect.rect.size;
        for (int i = 0; i < bars.Count; i++)
        {
            var b = bars[i];
            if (Random.value > power) { b.enabled = false; continue; }
            if (Random.value > 0.35f && b.enabled) continue; // 한동안 남아 있다 바뀜
            b.enabled = true;
            var rt = b.rectTransform;
            rt.sizeDelta = new Vector2(size.x * Random.Range(0.4f, 1.4f), size.y * Random.Range(0.01f, 0.12f));
            rt.anchoredPosition = new Vector2(Random.Range(-0.3f, 0.3f) * size.x * power, Random.Range(-0.5f, 0.5f) * size.y);
            Color c = i % 4 == 0 ? new Color(1f, 0.15f, 0.25f) : i % 4 == 1 ? new Color(0.2f, 0.9f, 1f) : i % 4 == 2 ? Color.white : Color.black;
            c.a = Random.Range(0.2f, 0.6f) * power;
            b.color = c;
        }
    }

    /// <summary>지워진 자리: 검은 네모가 쌓인다. 갈수록 크게.</summary>
    void AddHole(float a)
    {
        Vector2 size = canvasRect.rect.size;
        var img = MakeImage("Erased", canvasRect, Color.black);
        img.transform.SetSiblingIndex(1); // 하얀 막 위, 줄무늬 아래
        float w = Random.Range(0.05f, 0.25f) * Mathf.Lerp(0.5f, 2f, a);
        float h = Random.Range(0.03f, 0.2f) * Mathf.Lerp(0.5f, 2f, a);
        img.rectTransform.sizeDelta = new Vector2(size.x * w, size.y * h);
        img.rectTransform.anchoredPosition = new Vector2(Random.Range(-0.5f, 0.5f) * size.x, Random.Range(-0.5f, 0.5f) * size.y);
        holes.Add(img);
    }

    // ───────────── UI 만들기 ─────────────

    void BuildCanvas()
    {
        var go = new GameObject("EndingCanvas");
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = (RectTransform)go.transform;

        white = MakeImage("White", canvasRect, new Color(1f, 1f, 1f, 0f));
        Stretch(white.rectTransform);
    }

    /// <summary>윈도우 오류창 흉내.</summary>
    void BuildDialog(out RectTransform okButton, out Image okImage, out RectTransform closeButton)
    {
        var f = font != null ? font : TMP_Settings.defaultFontAsset;
        const float W = 560f, H = 230f, Title = 44f, Footer = 74f;

        var shadow = MakeImage("Shadow", canvasRect, new Color(0f, 0f, 0f, 0.35f));
        shadow.rectTransform.sizeDelta = new Vector2(W + 16f, H + 16f);
        shadow.rectTransform.anchoredPosition = new Vector2(4f, -6f);

        var border = MakeImage("Window", canvasRect, new Color(0.55f, 0.55f, 0.58f));
        border.rectTransform.sizeDelta = new Vector2(W + 2f, H + 2f);
        var win = MakeImage("Body", border.rectTransform, Color.white);
        win.rectTransform.sizeDelta = new Vector2(W, H);
        var root = win.rectTransform;

        // 제목줄
        var title = MakeText("Title", root, f, dialogTitle, 20f, new Color(0.1f, 0.1f, 0.1f), TextAlignmentOptions.Left);
        Place(title.rectTransform, new Vector2(16f - W * 0.5f + 200f, H * 0.5f - Title * 0.5f), new Vector2(400f, Title));

        var close = MakeImage("Close", root, Color.clear);
        Place(close.rectTransform, new Vector2(W * 0.5f - 24f, H * 0.5f - Title * 0.5f), new Vector2(48f, Title));
        MakeCross(close.rectTransform, 14f, 2f, new Color(0.15f, 0.15f, 0.15f));
        closeButton = close.rectTransform;

        // 아이콘 + 문구
        float midY = H * 0.5f - Title - (H - Title - Footer) * 0.5f;
        var icon = MakeImage("Icon", root, new Color(0.85f, 0.12f, 0.12f));
        icon.sprite = Circle();
        Place(icon.rectTransform, new Vector2(-W * 0.5f + 52f, midY), new Vector2(44f, 44f));
        MakeCross(icon.rectTransform, 20f, 4f, Color.white);

        var msg = MakeText("Message", root, f, dialogMessage, 24f, new Color(0.1f, 0.1f, 0.1f), TextAlignmentOptions.Left);
        Place(msg.rectTransform, new Vector2(-W * 0.5f + 92f + 220f, midY), new Vector2(440f, 60f));

        // 아래 회색 띠 + 확인
        var footer = MakeImage("Footer", root, new Color(0.95f, 0.95f, 0.95f));
        Place(footer.rectTransform, new Vector2(0f, -H * 0.5f + Footer * 0.5f), new Vector2(W, Footer));
        var ok = MakeImage("OK", root, new Color(0f, 0.4f, 0.75f));
        Place(ok.rectTransform, new Vector2(W * 0.5f - 90f, -H * 0.5f + Footer * 0.5f), new Vector2(130f, 38f));
        var okText = MakeText("Label", ok.rectTransform, f, "확인", 20f, Color.white, TextAlignmentOptions.Center);
        Stretch(okText.rectTransform);
        okButton = ok.rectTransform;
        okImage = ok;
    }

    void MakeCross(RectTransform parent, float size, float thick, Color c)
    {
        foreach (float ang in new[] { 45f, -45f })
        {
            var l = MakeImage("Line", parent, c);
            l.rectTransform.sizeDelta = new Vector2(size * 1.3f, thick);
            l.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ang);
        }
    }

    static Image MakeImage(string name, RectTransform parent, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = Square();
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    static TextMeshProUGUI MakeText(string name, RectTransform parent, TMP_FontAsset f, string s, float size, Color c, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (f != null) t.font = f;
        t.text = s;
        t.fontSize = size;
        t.color = c;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static Sprite Square()
    {
        if (square != null) return square;
        var tex = new Texture2D(4, 4);
        var px = new Color[16];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        tex.SetPixels(px); tex.Apply();
        square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        return square;
    }

    static Sprite Circle()
    {
        if (circle != null) return circle;
        const int n = 64;
        var tex = new Texture2D(n, n);
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(n * 0.5f - d));
            }
        tex.SetPixels(px); tex.Apply();
        circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        return circle;
    }
}
