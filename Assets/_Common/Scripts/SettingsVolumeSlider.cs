using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// A quiet, even-width rail. Only a tiny vertical breathing motion remains.
public sealed class SettingsVolumeSlider : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IEndDragHandler
{
    static Sprite rounded;
    Slider slider;
    Image fill;
    bool held;
    static Sprite Rounded()
    {
        if (rounded != null) return rounded;
        const int size = 24;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            var q = new Vector2(Mathf.Abs(x + .5f - 12) - 9, Mathf.Abs(y + .5f - 12) - 9);
            float d = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - 3;
            texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(.5f - d)));
        }
        texture.Apply();
        rounded = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size, 0, SpriteMeshType.FullRect, Vector4.one * 4);
        return rounded;
    }
    public static void Install(Slider target)
    {
        if (target != null && target.GetComponent<SettingsVolumeSlider>() == null) target.gameObject.AddComponent<SettingsVolumeSlider>();
    }
    void Awake()
    {
        slider = GetComponent<Slider>();
        var root = (RectTransform)transform;
        root.sizeDelta = new Vector2(root.sizeDelta.x, 36);
        var background = transform.Find("Background") as RectTransform;
        if (background != null)
        {
            background.anchorMin = new Vector2(0, .5f); background.anchorMax = new Vector2(1, .5f);
            background.sizeDelta = new Vector2(0, 20);
            var image = background.GetComponent<Image>(); image.sprite = Rounded(); image.type = Image.Type.Sliced;
            image.material = null; image.color = new Color(.17f, .17f, .19f, .85f);
        }
        var area = transform.Find("Fill Area") as RectTransform;
        if (area != null)
        {
            area.anchorMin = new Vector2(0, .5f); area.anchorMax = new Vector2(1, .5f);
            area.anchoredPosition = Vector2.zero; area.sizeDelta = new Vector2(-14, 12);
        }
        var handles = transform.Find("Handle Slide Area") as RectTransform;
        if (handles != null) { handles.offsetMin = new Vector2(7, 0); handles.offsetMax = new Vector2(-7, 0); }
        if (slider.fillRect != null)
        {
            fill = slider.fillRect.GetComponent<Image>(); fill.material = null; fill.sprite = Rounded(); fill.type = Image.Type.Sliced;
            fill.color = new Color(.91f, .92f, .94f);
        }
        if (slider.handleRect != null)
        {
            var image = slider.handleRect.GetComponent<Image>(); image.material = null; image.sprite = Rounded(); image.type = Image.Type.Sliced;
            image.color = new Color(.97f, .97f, .98f);
        }
    }
    void LateUpdate()
    {
        if (slider.handleRect != null)
        {
            var h = slider.handleRect; h.anchorMin = new Vector2(h.anchorMin.x, .5f); h.anchorMax = new Vector2(h.anchorMax.x, .5f);
            h.sizeDelta = new Vector2(14, 26);
        }
        if (fill != null)
        {
            float breathe = Mathf.Sin(Time.unscaledTime * 1.6f);
            fill.rectTransform.localScale = new Vector3(1, 1 + breathe * .025f, 1);
            fill.color = new Color(.91f + breathe * .012f, .92f + breathe * .012f, .94f + breathe * .01f);
        }
    }
    public void OnPointerDown(PointerEventData data) { held = data.button == PointerEventData.InputButton.Left && slider.IsInteractable(); }
    public void OnPointerUp(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) Release(); }
    public void OnEndDrag(PointerEventData data) { Release(); }
    void Release() { if (!held) return; held = false; SettingsHoverAudio.PlayVolumeRelease(this); }
    void OnDisable() { held = false; }
}
