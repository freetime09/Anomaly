using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 전체 피드백(실패 등). 카메라를 살짝 흔들고 화면을 잠깐 빨갛게 물들였다 되돌린다.
///  - GestureRecognizer 가 제스처 인식 실패 시 FailFeedback() 을 호출한다.
///  - 씬에 직접 놔두지 않아도 Ensure() 로 자동 생성되어 동작한다(놔두면 인스펙터 값으로 튜닝).
/// 빨간 오버레이는 ScreenSpaceOverlay 캔버스로 코드에서 만든다(FallRespawn 페이드와 같은 방식).
/// </summary>
[DisallowMultipleComponent]
public class ScreenFeedback : MonoBehaviour
{
    public static ScreenFeedback Instance { get; private set; }

    [Header("화면 흔들림")]
    [Tooltip("흔들림 지속 시간(초)")]
    public float shakeDuration = 0.18f;
    [Tooltip("흔들림 크기(units). 살짝만 흔들려면 작게")]
    public float shakeMagnitude = 0.15f;

    [Header("빨간 플래시")]
    [Tooltip("플래시 색")]
    public Color flashColor = new Color(1f, 0.15f, 0.15f);
    [Range(0f, 1f)]
    [Tooltip("가장 진할 때 알파(0~1). 살짝만 물들이려면 낮게")]
    public float flashPeakAlpha = 0.28f;
    [Tooltip("빨개지는 데 걸리는 시간(초)")]
    public float flashInTime = 0.05f;
    [Tooltip("다시 사라지는 데 걸리는 시간(초)")]
    public float flashOutTime = 0.22f;

    CameraFollow cam;
    CanvasGroup group;
    Image img;
    Coroutine flashCo;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        cam = FindObjectOfType<CameraFollow>();
        BuildOverlay();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>씬에 ScreenFeedback 이 없으면 자동 생성해서 반환(수동 배치 불필요).</summary>
    public static ScreenFeedback Ensure()
    {
        if (Instance == null)
        {
            var go = new GameObject("ScreenFeedback");
            Instance = go.AddComponent<ScreenFeedback>();
        }
        return Instance;
    }

    /// <summary>실패 피드백 — 카메라 흔들기 + 빨간 플래시.</summary>
    public void FailFeedback()
    {
        if (cam == null) cam = FindObjectOfType<CameraFollow>();
        if (cam != null) cam.Shake(shakeDuration, shakeMagnitude);
        if (flashCo != null) StopCoroutine(flashCo);
        flashCo = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        if (group == null || img == null) yield break;
        img.color = new Color(flashColor.r, flashColor.g, flashColor.b, 1f); // 알파는 CanvasGroup 로 제어

        float t = 0f;
        while (t < flashInTime)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(0f, flashPeakAlpha, flashInTime > 0f ? t / flashInTime : 1f);
            yield return null;
        }
        group.alpha = flashPeakAlpha;

        t = 0f;
        while (t < flashOutTime)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(flashPeakAlpha, 0f, flashOutTime > 0f ? t / flashOutTime : 1f);
            yield return null;
        }
        group.alpha = 0f;
        flashCo = null;
    }

    void BuildOverlay()
    {
        var canvasGO = new GameObject("FailFlashCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // 항상 최상단

        group = canvasGO.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false; // 입력 가로채지 않음

        var imgGO = new GameObject("Flash");
        imgGO.transform.SetParent(canvasGO.transform, false);
        img = imgGO.AddComponent<Image>();
        img.color = flashColor;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
