using UnityEngine;

/// <summary>
/// 캡쳐 그림 속 게임 화면의 톱니바퀴를 진짜 설정 버튼처럼 쓴다. 캡쳐 배경(SpriteRenderer)에 붙인다.
///  - 캡쳐가 보이는 동안: 왼쪽 위 진짜 톱니는 숨기고, 그림 속 톱니를 누르면 설정이 열린다.
///  - 캡쳐가 꺼지거나 어두워지면(검게 사라지는 연출) 진짜 톱니를 다시 보여준다.
///  - 위치는 캡쳐 픽셀 좌표(왼쪽 위 = 0,0). CaptureSkillBar 와 같은 방식.
///  - 진짜 톱니(SettingsIconFeedback)처럼 올리면 돌고 커지고, 누르면 작아진다:
///    그림에서 톱니 모양(회색)만 오려 낸 조각을 돌리고, 원래 톱니 자리는 주변 배경색으로 메운다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class CaptureSettingsButton : MonoBehaviour
{
    [Tooltip("누를 수 있는 자리: x, y = 왼쪽 위, z, w = 너비, 높이 (캡쳐 픽셀). 그림 속 톱니보다 조금 넉넉하게")]
    public Vector4 pixelRect = new Vector4(179f, 750f, 37f, 37f);

    [Header("움직임 (진짜 톱니와 같은 값)")]
    [Range(0f, 360f)] public float hoverDegrees = 180f;
    [Range(1f, 1.3f)] public float hoverScale = 1.10f;
    [Range(0.7f, 1f)] public float pressedScale = 0.94f;
    [Range(0f, 1f)] public float pressedOpacity = 0.65f;
    [Min(0.01f)] public float rotationSmoothTime = 0.14f;
    [Min(0.01f)] public float scaleSmoothTime = 0.09f;
    [Min(0.01f)] public float pressSmoothTime = 0.045f;
    [Tooltip("배경 sortingOrder 에 더할 값 (글리치·테두리보다 위)")]
    public int orderOffset = 4;

    SpriteRenderer bg;
    PauseMenu menu;
    bool shown; // 그림 속 톱니가 버튼으로 동작 중

    // 움직이는 톱니 조각 + 원래 톱니를 가리는 배경 조각 (배경 그림이 바뀌면 다시 만든다)
    SpriteRenderer gear, patch;
    Sprite builtFrom;
    float angle, angleVelocity;
    float scale = 1f, scaleVelocity;
    float opacity = 1f, opacityVelocity;
    float pressUntil;

    void Awake()
    {
        bg = GetComponent<SpriteRenderer>();
    }

    void OnEnable() { PauseMenu.ExtraSettingsArea = Contains; }

    void OnDisable()
    {
        if (PauseMenu.ExtraSettingsArea == (System.Func<Vector2, bool>)Contains) PauseMenu.ExtraSettingsArea = null;
        SetShown(false);
        angle = angleVelocity = scaleVelocity = opacityVelocity = pressUntil = 0f;
        scale = opacity = 1f;
        if (gear != null) gear.gameObject.SetActive(false);
        if (patch != null) patch.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        SetShown(Visible());
        bool over = shown && Contains(Input.mousePosition);
        if (over && !PauseMenu.IsPaused && Input.GetMouseButtonDown(0))
        {
            pressUntil = Time.unscaledTime + 0.10f; // 아주 짧은 클릭도 눌린 게 보이게
            if (menu == null) menu = FindAnyObjectByType<PauseMenu>();
            if (menu != null) menu.Pause();
        }
        Animate(over, over && Input.GetMouseButton(0));
    }

    void Animate(bool over, bool pressed)
    {
        if (bg.sprite != builtFrom) Build();
        if (gear == null) return;
        bool down = pressed || Time.unscaledTime < pressUntil;
        float dt = Time.unscaledDeltaTime; // 설정이 열려 시간이 멈춰도 움직임
        angle = Mathf.SmoothDamp(angle, over ? hoverDegrees : 0f, ref angleVelocity, rotationSmoothTime, Mathf.Infinity, dt);
        scale = Mathf.SmoothDamp(scale, down ? pressedScale : over ? hoverScale : 1f,
            ref scaleVelocity, down ? pressSmoothTime : scaleSmoothTime, Mathf.Infinity, dt);
        opacity = Mathf.SmoothDamp(opacity, down ? pressedOpacity : 1f,
            ref opacityVelocity, down ? pressSmoothTime : scaleSmoothTime, Mathf.Infinity, dt);

        // 가만히 있을 땐 원래 그림 그대로 (조각을 안 띄움)
        bool moving = Mathf.Abs(angle) > 0.05f || Mathf.Abs(scale - 1f) > 0.001f || opacity < 0.999f;
        bool active = moving && bg.enabled && bg.gameObject.activeInHierarchy;
        if (gear.gameObject.activeSelf != active) { gear.gameObject.SetActive(active); patch.gameObject.SetActive(active); }
        if (!active) return;
        gear.transform.localRotation = Quaternion.Euler(0f, 0f, -angle);
        gear.transform.localScale = Vector3.one * scale;
        Color c = bg.color;
        patch.color = c;
        c.a *= opacity;
        gear.color = c;
        gear.sortingLayerID = patch.sortingLayerID = bg.sortingLayerID;
        patch.sortingOrder = bg.sortingOrder + orderOffset;
        gear.sortingOrder = bg.sortingOrder + orderOffset + 1;
    }

    /// <summary>배경 그림에서 톱니 자리를 읽어 톱니 조각(회색 부분만)과 배경 조각(톱니를 주변 색으로 메운 것)을 만든다.</summary>
    void Build()
    {
        builtFrom = bg.sprite;
        Sprite sp = bg.sprite;
        if (sp == null) return;
        int w = Mathf.RoundToInt(pixelRect.z), h = Mathf.RoundToInt(pixelRect.w);
        int x0 = Mathf.RoundToInt(sp.rect.x + pixelRect.x);
        int y0 = Mathf.RoundToInt(sp.rect.y + sp.rect.height - pixelRect.y - h); // 텍스처는 아래가 0
        Color32[] src = ReadRegion(sp.texture, x0, y0, w, h);
        if (src == null) return;

        // 톱니 = 회색(채도가 낮음) 또는 아주 어두운 테두리, 배경 = 갈색
        var isGear = new bool[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            Color32 c = src[i];
            int mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), mn = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            isGear[i] = mx - mn < 20 || mx < 41;
        }
        // 가릴 자리는 1픽셀 넓혀서 테두리 찌꺼기까지 덮는다
        var cover = new bool[src.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                for (int dy = -1; dy <= 1 && !cover[y * w + x]; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx >= 0 && xx < w && yy >= 0 && yy < h && isGear[yy * w + xx]) { cover[y * w + x] = true; break; }
                    }

        var gearPx = new Color32[src.Length];
        var patchPx = new Color32[src.Length];
        for (int i = 0; i < src.Length; i++) gearPx[i] = isGear[i] ? src[i] : new Color32(0, 0, 0, 0);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (!cover[i]) { patchPx[i] = src[i]; continue; }
                // 같은 줄 양옆의 가장 가까운 배경색 사이를 이어서 메움
                int l = x, r = x;
                while (l >= 0 && cover[y * w + l]) l--;
                while (r < w && cover[y * w + r]) r++;
                Color a = l >= 0 ? (Color)src[y * w + l] : r < w ? (Color)src[y * w + r] : (Color)src[i];
                Color b = r < w ? (Color)src[y * w + r] : a;
                float t = (r - l) > 0 ? (x - l) / (float)(r - l) : 0.5f;
                Color m = Color.Lerp(a, b, t); m.a = 1f;
                patchPx[i] = m;
            }

        Vector2 center = PixelToLocal(sp, new Vector2(pixelRect.x + w * 0.5f, pixelRect.y + h * 0.5f));
        patch = MakePiece(patch, "SettingsPatch", patchPx, w, h, sp.pixelsPerUnit, center);
        gear = MakePiece(gear, "SettingsGear", gearPx, w, h, sp.pixelsPerUnit, center);
    }

    SpriteRenderer MakePiece(SpriteRenderer sr, string name, Color32[] px, int w, int h, float ppu, Vector2 center)
    {
        if (sr == null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            sr = go.AddComponent<SpriteRenderer>();
            go.SetActive(false);
        }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = bg.sprite.texture.filterMode, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px);
        tex.Apply();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
        sr.transform.localPosition = center;
        return sr;
    }

    /// <summary>읽기 불가 텍스처도 읽을 수 있게 GPU 로 복사해서 일부를 읽어 온다.</summary>
    static Color32[] ReadRegion(Texture2D tex, int x, int y, int w, int h)
    {
        if (tex == null || x < 0 || y < 0 || x + w > tex.width || y + h > tex.height) return null;
        var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(tex, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tmp = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tmp.ReadPixels(new Rect(x, y, w, h), 0, 0);
        tmp.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        var px = tmp.GetPixels32();
        Destroy(tmp);
        return px;
    }

    /// <summary>캡쳐 픽셀 좌표(왼쪽 위 = 0,0) → 배경 로컬 좌표.</summary>
    static Vector2 PixelToLocal(Sprite sp, Vector2 px)
    {
        Rect r = sp.rect;
        return new Vector2(px.x - sp.pivot.x, (r.height - px.y) - sp.pivot.y) / sp.pixelsPerUnit;
    }

    bool Visible()
    {
        if (bg == null || !bg.enabled || !bg.gameObject.activeInHierarchy || bg.sprite == null) return false;
        float dim = Mathf.Max(bg.color.r, bg.color.g, bg.color.b) * bg.color.a; // 검게 사라지는 중엔 안 눌림
        return dim > 0.5f;
    }

    void SetShown(bool on)
    {
        if (shown == on) return;
        shown = on;
        if (menu == null) menu = FindAnyObjectByType<PauseMenu>();
        if (menu != null && menu.settingsButton != null) menu.settingsButton.gameObject.SetActive(!on);
    }

    /// <summary>화면 좌표가 그림 속 톱니 자리 안인가.</summary>
    bool Contains(Vector2 screenPos)
    {
        if (!shown) return false;
        var cam = Camera.main;
        if (cam == null) return false;
        Vector3 w = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
        Vector3 local = transform.InverseTransformPoint(w);
        Sprite sp = bg.sprite;
        float px = local.x * sp.pixelsPerUnit + sp.pivot.x;
        float py = sp.rect.height - (local.y * sp.pixelsPerUnit + sp.pivot.y); // 왼쪽 위 기준
        return px >= pixelRect.x && px <= pixelRect.x + pixelRect.z && py >= pixelRect.y && py <= pixelRect.y + pixelRect.w;
    }
}
