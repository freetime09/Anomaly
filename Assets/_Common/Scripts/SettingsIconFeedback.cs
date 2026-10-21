using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Animates only the icon, leaving the settings button's hit area stationary.
/// Unscaled time keeps feedback responsive while the pause menu is open.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class SettingsIconFeedback : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public RectTransform icon;
    public Image iconImage;
    [Range(0f, 360f)] public float hoverDegrees = 180f;
    [Range(1f, 1.3f)] public float hoverScale = 1.10f;
    [Range(0.7f, 1f)] public float pressedScale = 0.94f;
    [Range(0f, 1f)] public float pressedOpacity = 0.65f;
    [Min(0.01f)] public float rotationSmoothTime = 0.14f;
    [Min(0.01f)] public float scaleSmoothTime = 0.09f;
    [Min(0.01f)] public float pressSmoothTime = 0.045f;

    Button button;
    Vector3 restScale;
    Quaternion restRotation;
    Color restColor;
    bool hovered, pressed;
    float pressUntil;
    float angle, angleVelocity;
    float scale = 1f, scaleVelocity;
    float opacity = 1f, opacityVelocity;

    void Awake()
    {
        button = GetComponent<Button>();
        if (icon == null) return;
        if (iconImage == null) iconImage = icon.GetComponent<Image>();
        restScale = icon.localScale;
        restRotation = icon.localRotation;
        if (iconImage != null) restColor = iconImage.color;
    }

    void Update()
    {
        if (icon == null) return;
        bool interactive = button != null && button.IsInteractable();
        bool over = interactive && hovered;
        bool down = interactive && (pressed || Time.unscaledTime < pressUntil);
        float dt = Time.unscaledDeltaTime;
        // A scalar angle avoids the ambiguous shortest-path direction at exactly 180 degrees.
        angle = Mathf.SmoothDamp(angle, over || (interactive && pressed) ? hoverDegrees : 0f,
            ref angleVelocity, rotationSmoothTime, Mathf.Infinity, dt);
        scale = Mathf.SmoothDamp(scale, down ? pressedScale : over ? hoverScale : 1f,
            ref scaleVelocity, down ? pressSmoothTime : scaleSmoothTime, Mathf.Infinity, dt);
        opacity = Mathf.SmoothDamp(opacity, down ? pressedOpacity : 1f,
            ref opacityVelocity, down ? pressSmoothTime : scaleSmoothTime, Mathf.Infinity, dt);
        icon.localRotation = restRotation * Quaternion.Euler(0f, 0f, -angle);
        icon.localScale = restScale * scale;
        if (iconImage != null)
        {
            Color color = restColor;
            color.a *= opacity;
            iconImage.color = color;
        }
    }

    public void OnPointerEnter(PointerEventData eventData) { hovered = true; }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; }
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !button.IsInteractable()) return;
        pressed = true;
        // Even a very quick click gets a visible, smoothly released press.
        pressUntil = Time.unscaledTime + 0.10f;
    }
    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) pressed = false;
    }

    void OnApplicationFocus(bool focused) { if (!focused) ResetFeedback(); }
    void OnDisable() { ResetFeedback(); }

    void ResetFeedback()
    {
        hovered = pressed = false;
        pressUntil = angle = angleVelocity = scaleVelocity = opacityVelocity = 0f;
        scale = opacity = 1f;
        if (icon == null) return;
        icon.localRotation = restRotation;
        icon.localScale = restScale;
        if (iconImage != null) iconImage.color = restColor;
    }
}
