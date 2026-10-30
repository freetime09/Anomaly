using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class GameMouseCursor : MonoBehaviour
{
    public Texture2D normalImage;
    public Texture2D pressedImage;
    [Range(0.8f, 1f)] public float pressedBrightness = 0.94f;
    [Range(1f, 2f)] public float cursorScale = 1.2f;
    public Vector2 hotspot = new Vector2(8f, 4f);
    static GameMouseCursor instance;
    Texture2D normalCursor, pressedCursor;
    bool pressed, focused = true;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { instance = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        var prefab = Resources.Load<GameObject>("GameMouseCursor");
        if (prefab != null) Instantiate(prefab);
    }
    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        normalCursor = MakeCursor(normalImage, 1f);
        pressedCursor = MakeCursor(pressedImage, pressedBrightness);
        Apply(false);
    }
    Texture2D MakeCursor(Texture2D source, float brightness)
    {
        if (source == null) return null;
        int w = Mathf.RoundToInt(source.width * cursorScale), h = Mathf.RoundToInt(source.height * cursorScale);
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        texture.name = source.name + "_RuntimeCursor";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        var sourcePixels = source.GetPixels32();
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            pixels[y * w + x] = sourcePixels[Mathf.Min(source.height - 1, y * source.height / h) * source.width + Mathf.Min(source.width - 1, x * source.width / w)];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i].r = (byte)Mathf.RoundToInt(pixels[i].r * brightness);
            pixels[i].g = (byte)Mathf.RoundToInt(pixels[i].g * brightness);
            pixels[i].b = (byte)Mathf.RoundToInt(pixels[i].b * brightness);
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }
    void Update()
    {
        if (!focused) return;
        bool down = false;
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        down = mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
        down = Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);
#endif
        if (down != pressed) Apply(down);
    }
    void Apply(bool down)
    {
        pressed = down;
        var texture = down && pressedCursor != null ? pressedCursor : normalCursor;
        Cursor.SetCursor(texture, texture != null ? new Vector2(Mathf.Round(hotspot.x * cursorScale), Mathf.Round(hotspot.y * cursorScale)) : Vector2.zero, CursorMode.Auto);
    }
    void OnApplicationFocus(bool value) { focused = value; if (instance == this) Apply(false); }
    void OnDestroy()
    {
        if (instance != this) return;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        instance = null;
        if (normalCursor != null) Destroy(normalCursor);
        if (pressedCursor != null) Destroy(pressedCursor);
    }
}
