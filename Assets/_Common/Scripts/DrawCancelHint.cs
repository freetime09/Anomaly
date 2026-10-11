using UnityEngine;

/// <summary>
/// 그리는 동안에만 화면 아래쪽에 "우클릭: 그리기 취소" 안내를 띄운다.
/// 안내 묶음(반투명 검은 띠 + 글자)에 붙이고, CanvasGroup 알파로 부드럽게 나타났다 사라진다.
/// (밝은 배경·어두운 배경 어디서든 읽히도록 검은 띠를 깔아둠)
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class DrawCancelHint : MonoBehaviour
{
    [Tooltip("나타나고 사라지는 속도 (초당 알파 변화)")]
    public float fadeSpeed = 8f;
    [Tooltip("다 보일 때의 투명도")]
    public float maxAlpha = 1f;

    CanvasGroup group;
    GestureRecognizer gesture;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    void Update()
    {
        if (gesture == null) gesture = FindObjectOfType<GestureRecognizer>();
        bool show = gesture != null && gesture.enabled && gesture.IsDrawing && !PauseMenu.IsPaused;
        group.alpha = Mathf.MoveTowards(group.alpha, show ? maxAlpha : 0f, Time.unscaledDeltaTime * fadeSpeed);
    }
}
