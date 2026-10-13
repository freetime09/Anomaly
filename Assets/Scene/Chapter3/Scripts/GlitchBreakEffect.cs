using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "배경 그래픽이 깨지는" 연출. 세기(Intensity, 0~1)에 비례해 세 가지가 함께 강해진다.
///  1) 화면 줄무늬: 화면 위에 가로 줄(흰/검/청록/자홍)이 지지직 깜빡인다 (UI 오버레이).
///  2) 배경 조각: 카메라에 보이는 배경 곳곳에 색 블록이 생겼다 사라진다 (월드, 플레이어보다 뒤).
///  3) 카메라 흔들림.
/// 그리고 Flash() 로 화면을 하얗게 번쩍여, 가장 밝은 순간에 장면을 바꿀 수 있다 (순간이동 은폐용).
/// 모든 그림은 코드로 만들기 때문에 씬에 이 컴포넌트 하나만 두면 된다.
/// </summary>
[DisallowMultipleComponent]
public class GlitchBreakEffect : MonoBehaviour
{
    [Header("공통")]
    [Tooltip("현재 깨짐 세기 (0 = 없음, 1 = 최대). 플레이 중 Inspector 에서 바꿔 미리볼 수 있다")]
    [Range(0f, 1f)] public float intensity = 0f;
    [Tooltip("줄무늬·조각 모양을 새로 섞는 간격(초). 작을수록 더 빠르게 지지직거림")]
    [Min(0.01f)] public float refreshInterval = 0.05f;

    [Header("1) 화면 줄무늬")]
    [Tooltip("세기 1일 때 동시에 보이는 최대 줄 수")]
    [Min(0)] public int maxBars = 16;
    [Tooltip("줄 두께 범위 (픽셀, 1920x1080 기준)")]
    public Vector2 barHeight = new Vector2(4f, 40f);

    [Header("2) 배경 조각")]
    [Tooltip("세기 1일 때 동시에 보이는 최대 조각 수")]
    [Min(0)] public int maxFragments = 40;
    [Tooltip("조각 가로 길이 범위 (유닛)")]
    public Vector2 fragmentWidth = new Vector2(0.4f, 4f);
    [Tooltip("조각 세로 길이 범위 (유닛)")]
    public Vector2 fragmentHeight = new Vector2(0.15f, 1.2f);
    [Tooltip("조각 그리기 순서. 바닥(0)보다 위, 플레이어보다 아래로 둔다")]
    public int fragmentSortingOrder = 3;

    [Header("색")]
    [Tooltip("줄무늬·조각에 무작위로 쓰이는 색들 (깨진 화면 느낌의 원색 위주)")]
    public Color[] palette =
    {
        Color.white,
        Color.black,
        new Color(0.2f, 1f, 1f),   // 청록
        new Color(1f, 0.25f, 0.85f), // 자홍
        new Color(0.6f, 0.6f, 0.65f) // 회색
    };

    [Header("3) 카메라 흔들림")]
    [Tooltip("세기 1일 때 흔들림 크기 (유닛)")]
    [Min(0f)] public float maxShake = 0.25f;

    [Header("번쩍임 (Flash)")]
    [Tooltip("번쩍일 때 화면을 덮는 색")]
    public Color flashColor = Color.white;
    [Tooltip("화면이 하얗게 차오르는 시간(초)")]
    [Min(0f)] public float flashInTime = 0.12f;
    [Tooltip("완전히 하얀 상태로 멈춰 있는 시간(초). 이 동안 장면이 바뀐다")]
    [Min(0f)] public float flashHoldTime = 0.15f;
    [Tooltip("하얀 화면이 걷히는 시간(초)")]
    [Min(0f)] public float flashOutTime = 0.7f;

    Canvas canvas;
    RectTransform[] bars;
    Image[] barImages;
    Image flashImage;
    SpriteRenderer[] fragments;
    Sprite whiteSprite;
    Texture2D whiteTex;
    CameraFollow camFollow;
    Camera view;
    float refreshTimer;
    float shakeTimer;
    Coroutine rampCo;

    /// <summary>현재 세기.</summary>
    public float Intensity => intensity;

    void Awake()
    {
        whiteTex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
        whiteTex.SetPixel(0, 0, Color.white);
        whiteTex.Apply();
        whiteSprite = Sprite.Create(whiteTex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        camFollow = FindAnyObjectByType<CameraFollow>();
        view = camFollow != null ? camFollow.GetComponent<Camera>() : Camera.main;
        BuildCanvas();
        BuildFragments();
    }

    void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
        if (fragments != null && fragments.Length > 0 && fragments[0] != null) Destroy(fragments[0].transform.parent.gameObject);
        if (whiteSprite != null) Destroy(whiteSprite);
        if (whiteTex != null) Destroy(whiteTex);
    }

    /// <summary>세기를 즉시 바꾼다.</summary>
    public void SetIntensity(float value)
    {
        if (rampCo != null) { StopCoroutine(rampCo); rampCo = null; }
        intensity = Mathf.Clamp01(value);
        if (intensity <= 0f) HideAll();
    }

    /// <summary>세기를 duration 초 동안 target 까지 서서히 바꾼다.</summary>
    public void RampTo(float target, float duration)
    {
        if (rampCo != null) StopCoroutine(rampCo);
        rampCo = StartCoroutine(RampRoutine(Mathf.Clamp01(target), duration));
    }

    IEnumerator RampRoutine(float target, float duration)
    {
        float from = intensity;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            intensity = Mathf.Lerp(from, target, t / duration);
            yield return null;
        }
        intensity = target;
        rampCo = null;
    }

    /// <summary>화면을 하얗게 번쩍인다. 완전히 하얀 순간 onPeak 를 호출한다 (여기서 순간이동 등).</summary>
    public IEnumerator Flash(System.Action onPeak)
    {
        yield return FadeFlash(0f, 1f, flashInTime);
        onPeak?.Invoke();
        if (flashHoldTime > 0f) yield return new WaitForSeconds(flashHoldTime);
        yield return FadeFlash(1f, 0f, flashOutTime);
    }

    IEnumerator FadeFlash(float from, float to, float time)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            SetFlashAlpha(Mathf.Lerp(from, to, t / time));
            yield return null;
        }
        SetFlashAlpha(to);
    }

    void SetFlashAlpha(float a)
    {
        var c = flashColor;
        c.a *= a;
        flashImage.color = c;
        flashImage.enabled = a > 0f;
    }

    void Update()
    {
        if (intensity <= 0f) return;

        // 줄무늬·조각은 refreshInterval 마다 새로 섞는다 (매 프레임 섞으면 너무 정신없음)
        refreshTimer -= Time.deltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = refreshInterval * Random.Range(0.6f, 1.4f);
            ShuffleBars();
            ShuffleFragments();
        }

        // 흔들림은 짧게 자주 다시 걸어 계속 떨리게 한다
        shakeTimer -= Time.deltaTime;
        if (camFollow != null && maxShake > 0f && shakeTimer <= 0f)
        {
            camFollow.Shake(0.12f, maxShake * intensity);
            shakeTimer = 0.1f;
        }
    }

    // ── 1) 화면 줄무늬 ──
    void BuildCanvas()
    {
        var go = new GameObject("GlitchBreakCanvas");
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 950; // HUD 위, 페이드(999) 아래
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        bars = new RectTransform[maxBars];
        barImages = new Image[maxBars];
        for (int i = 0; i < maxBars; i++)
        {
            var b = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            b.transform.SetParent(go.transform, false);
            bars[i] = b.GetComponent<RectTransform>();
            barImages[i] = b.GetComponent<Image>();
            barImages[i].raycastTarget = false;
            bars[i].anchorMin = bars[i].anchorMax = new Vector2(0.5f, 0.5f);
            b.SetActive(false);
        }

        // 번쩍임용 전체 화면 이미지 (줄무늬보다 위)
        var f = new GameObject("Flash", typeof(RectTransform), typeof(Image));
        f.transform.SetParent(go.transform, false);
        flashImage = f.GetComponent<Image>();
        flashImage.raycastTarget = false;
        var rt = flashImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        SetFlashAlpha(0f);
    }

    void ShuffleBars()
    {
        var area = (RectTransform)canvas.transform;
        float w = area.rect.width, h = area.rect.height;
        int count = Mathf.RoundToInt(maxBars * intensity);
        for (int i = 0; i < bars.Length; i++)
        {
            bool on = i < count && Random.value < 0.8f;
            bars[i].gameObject.SetActive(on);
            if (!on) continue;
            float bh = Random.Range(barHeight.x, barHeight.y) * (Random.value < 0.15f ? 3f : 1f); // 가끔 굵은 줄
            bars[i].sizeDelta = new Vector2(w * Random.Range(0.3f, 1.2f), bh);
            bars[i].anchoredPosition = new Vector2(Random.Range(-0.4f, 0.4f) * w, Random.Range(-0.5f, 0.5f) * h);
            Color c = RandomColor();
            c.a = Random.Range(0.35f, 0.95f) * Mathf.Lerp(0.5f, 1f, intensity);
            barImages[i].color = c;
        }
    }

    // ── 2) 배경 조각 ──
    void BuildFragments()
    {
        var root = new GameObject("GlitchBreakFragments").transform;
        fragments = new SpriteRenderer[maxFragments];
        for (int i = 0; i < maxFragments; i++)
        {
            var go = new GameObject("Fragment");
            go.transform.SetParent(root, false);
            fragments[i] = go.AddComponent<SpriteRenderer>();
            fragments[i].sprite = whiteSprite;
            fragments[i].sortingOrder = fragmentSortingOrder;
            fragments[i].enabled = false;
        }
    }

    void ShuffleFragments()
    {
        if (view == null) return;
        Vector2 c = view.transform.position;
        float hh = view.orthographicSize, hw = hh * view.aspect;
        int count = Mathf.RoundToInt(maxFragments * intensity);
        for (int i = 0; i < fragments.Length; i++)
        {
            var f = fragments[i];
            bool on = i < count && Random.value < 0.75f;
            f.enabled = on;
            if (!on) continue;
            f.transform.position = new Vector3(c.x + Random.Range(-hw, hw), c.y + Random.Range(-hh, hh), 0f);
            f.transform.localScale = new Vector3(Random.Range(fragmentWidth.x, fragmentWidth.y), Random.Range(fragmentHeight.x, fragmentHeight.y), 1f);
            Color col = RandomColor();
            col.a = Random.Range(0.4f, 1f);
            f.color = col;
        }
    }

    Color RandomColor()
    {
        return palette != null && palette.Length > 0 ? palette[Random.Range(0, palette.Length)] : Color.white;
    }

    void HideAll()
    {
        if (bars != null) foreach (var b in bars) if (b != null) b.gameObject.SetActive(false);
        if (fragments != null) foreach (var f in fragments) if (f != null) f.enabled = false;
    }
}
