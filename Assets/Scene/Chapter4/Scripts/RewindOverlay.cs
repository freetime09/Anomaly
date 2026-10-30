using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// 보스전에서 죽었을 때 되감기 화면 (낡은 필름 느낌).
///  - 화면 후처리: 회색 흑백 + 굵은 필름 그레인 + 짙은 비네트 (SetFilm 으로 세기)
///  - 위에 얹는 것: 밝기 깜빡임, 세로 스크래치, 먼지 티, 가로 지직 줄, 되감을 때 미끄러지는 프레임 경계, ◀◀
///  - 암전(SetBlack, 완전 암전은 HoldBlack — 그리는 선은 위로 보임), O 를 그리라는 안내(ShowPrompt: 노란 O 위를 점이 따라 돎)
/// BossFight 가 RewindOverlay.Create() 로 만들고 매 프레임 Tick, 끝나면 FadeOut.
/// </summary>
public sealed class RewindOverlay : MonoBehaviour
{
    CanvasGroup group;
    Image flicker, black, frameLine;
    Image[] scratches, dust, staticLines, arrows;
    RectTransform icon, prompt, ringDot;
    CanvasGroup promptGroup;
    Volume volume;
    VolumeProfile profile;
    float film, fade = 1f, frameLineY, promptTime, staticTick, burstLeft;
    int scratchTick;
    SpriteRenderer worldBlack; // 완전 암전 동안 화면을 덮는 검은 막 (그리는 선(101~)보다 아래)
    Camera blackCam;

    static readonly Color Ink = new Color(0.07f, 0.07f, 0.07f);
    static readonly Color Paper = new Color(0.9f, 0.9f, 0.9f);
    static Sprite triSprite, ringSprite, blobSprite, squareSprite;

    public static RewindOverlay Create()
    {
        var go = new GameObject("RewindOverlay", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40; // 보스 체력바(50) 아래 → 차오르는 게 잘 보이게
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var o = go.AddComponent<RewindOverlay>();
        o.Build();
        return o;
    }

    void Build()
    {
        group = GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        // 화면 후처리 (보스 눈의 분위기 볼륨(70)보다 위)
        var vgo = new GameObject("FilmVolume");
        vgo.transform.SetParent(transform, false);
        volume = vgo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 90;
        volume.weight = 0f;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.sharedProfile = profile;
        var ca = profile.Add<ColorAdjustments>(true);
        ca.saturation.value = -100f;
        ca.colorFilter.value = Color.white; // 색 없는 회색 필름
        ca.contrast.value = 28f;
        ca.postExposure.value = -0.15f;
        var grain = profile.Add<FilmGrain>(true);
        grain.type.value = FilmGrainLookup.Large02;
        grain.intensity.value = 1f;
        grain.response.value = 0.3f;
        var vig = profile.Add<Vignette>(true);
        vig.color.value = Color.black;
        vig.intensity.value = 0.5f;
        vig.smoothness.value = 0.45f;
        profile.Add<ChromaticAberration>(true).intensity.value = 0.2f;

        flicker = Full(MakeImage("Flicker", Color.clear));

        // 세로 스크래치
        scratches = new Image[4];
        for (int i = 0; i < scratches.Length; i++)
        {
            var s = MakeImage("Scratch" + i, Color.clear);
            var rt = s.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            scratches[i] = s;
        }

        // 먼지 티
        dust = new Image[14];
        for (int i = 0; i < dust.Length; i++)
        {
            var d = MakeImage("Dust" + i, Color.clear);
            d.sprite = BlobSprite();
            var rt = d.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            d.enabled = false;
            dust[i] = d;
        }

        // 가로 지직 줄 (화면 폭보다 길게, 옆으로 어긋나며 번쩍)
        staticLines = new Image[16];
        for (int i = 0; i < staticLines.Length; i++)
        {
            var l = MakeImage("Static" + i, Color.clear);
            var rt = l.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            l.enabled = false;
            staticLines[i] = l;
        }

        // 되감을 때 화면을 미끄러져 지나가는 프레임 경계 (검은 띠)
        frameLine = MakeImage("FrameLine", Ink);
        var fl = frameLine.rectTransform;
        fl.anchorMin = new Vector2(0f, 1f); fl.anchorMax = new Vector2(1f, 1f); fl.pivot = new Vector2(0.5f, 0.5f);
        fl.offsetMin = fl.offsetMax = Vector2.zero;
        fl.sizeDelta = new Vector2(0f, 26f);
        frameLine.enabled = false;

        // ◀◀ (오른쪽 위)
        var igo = new GameObject("RewindIcon", typeof(RectTransform));
        igo.transform.SetParent(transform, false);
        icon = (RectTransform)igo.transform;
        icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(1f, 1f);
        icon.anchoredPosition = new Vector2(-70f, -60f);
        icon.sizeDelta = new Vector2(110f, 60f);
        arrows = new Image[2];
        for (int i = 0; i < 2; i++)
        {
            var a = MakeImage("Arrow" + i, Paper, icon);
            a.sprite = TriangleSprite();
            var rt = a.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(55f, 60f);
            rt.anchoredPosition = new Vector2(i * 50f, 0f);
            a.enabled = false;
            arrows[i] = a;
        }

        black = Full(MakeImage("Black", Color.clear));

        // 안내: 노란 O, 점이 O 를 따라 돎
        var pgo = new GameObject("Prompt", typeof(RectTransform), typeof(CanvasGroup));
        pgo.transform.SetParent(transform, false);
        prompt = (RectTransform)pgo.transform;
        prompt.anchorMin = prompt.anchorMax = prompt.pivot = new Vector2(0.5f, 0.5f);
        prompt.anchoredPosition = new Vector2(0f, 40f);
        prompt.sizeDelta = new Vector2(180f, 180f);
        promptGroup = pgo.GetComponent<CanvasGroup>();
        promptGroup.alpha = 0f;
        var ring = MakeImage("Ring", new Color(1f, 0.9f, 0.25f), prompt);
        ring.sprite = RingSprite();
        var rrt = ring.rectTransform;
        rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0.5f, 0.5f);
        rrt.sizeDelta = new Vector2(150f, 150f);
        var dot = MakeImage("Dot", Color.white, ring.transform);
        dot.sprite = BlobSprite();
        ringDot = dot.rectTransform;
        ringDot.anchorMin = ringDot.anchorMax = ringDot.pivot = new Vector2(0.5f, 0.5f);
        ringDot.sizeDelta = new Vector2(30f, 30f);
    }

    /// <summary>필름 화면 세기 (0~1).</summary>
    public void SetFilm(float f) { film = Mathf.Clamp01(f); ApplyVolume(); }

    /// <summary>암전 (0 = 없음, 1 = 완전히 검음).</summary>
    public void SetBlack(float a) { black.color = new Color(0f, 0f, 0f, Mathf.Clamp01(a)); }

    /// <summary>완전히 검은 화면을 유지. 화면 위 UI 검은 막 대신 카메라 앞 검은 막으로 바꿔서
    /// 마우스로 그리는 선은 그 위로 보이게 한다.</summary>
    public void HoldBlack(Camera cam)
    {
        blackCam = cam;
        if (cam != null)
        {
            var go = new GameObject("RewindBlack");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 1f);
            worldBlack = go.AddComponent<SpriteRenderer>();
            worldBlack.sprite = SquareSprite();
            worldBlack.color = Color.black;
            worldBlack.sortingLayerName = "Default";
            worldBlack.sortingOrder = 100;
            FitWorldBlack();
            SetBlack(0f);
        }
        else SetBlack(1f);
    }

    public void ReleaseBlack()
    {
        if (worldBlack != null) Destroy(worldBlack.gameObject);
        worldBlack = null;
        SetBlack(0f);
    }

    void FitWorldBlack()
    {
        if (worldBlack == null || blackCam == null) return;
        float h = blackCam.orthographicSize * 2f + 2f;
        worldBlack.transform.localScale = new Vector3(h * blackCam.aspect + 2f, h, 1f);
    }

    public void ShowPrompt(bool on) { promptOn = on; if (on) promptTime = 0f; }
    bool promptOn;

    void ApplyVolume() { if (volume != null) volume.weight = film * fade; }

    /// <summary>매 프레임. u = 되감기 진행 (0~1), rewinding = 거꾸로 감는 중 (멈춘 화면이면 false).</summary>
    public void Tick(float u, bool rewinding)
    {
        float dt = Time.unscaledDeltaTime;
        var crt = (RectTransform)transform;
        float h = crt.rect.height > 0f ? crt.rect.height : 1080f;
        float w = crt.rect.width > 0f ? crt.rect.width : 1920f;
        float f = film;

        // 밝기 깜빡임 (되감을 땐 더 심하게)
        float fl = Random.Range(0f, rewinding ? 0.22f : 0.1f);
        if (Random.value < (rewinding ? 0.08f : 0.03f)) fl = Random.Range(0.25f, 0.4f);
        flicker.color = new Color(0f, 0f, 0f, fl * f);

        // 스크래치: 몇 프레임마다 다시 뿌림
        if (--scratchTick <= 0)
        {
            scratchTick = Random.Range(2, 6);
            for (int i = 0; i < scratches.Length; i++)
            {
                var s = scratches[i];
                bool on = Random.value < (rewinding ? 0.7f : 0.4f);
                s.enabled = on;
                if (!on) continue;
                var rt = s.rectTransform;
                rt.anchoredPosition = new Vector2(Random.Range(20f, w - 20f), 0f);
                rt.sizeDelta = new Vector2(Random.Range(1.5f, 4f), 0f);
                bool light = Random.value < 0.6f;
                s.color = light ? new Color(Paper.r, Paper.g, Paper.b, Random.Range(0.25f, 0.6f) * f) : new Color(0f, 0f, 0f, Random.Range(0.3f, 0.6f) * f);
            }
        }

        // 먼지 티: 한두 프레임만 보였다 사라짐
        foreach (var d in dust)
        {
            if (d.enabled && Random.value < 0.6f) { d.enabled = false; continue; }
            if (d.enabled || Random.value > (rewinding ? 0.12f : 0.05f)) continue;
            d.enabled = true;
            var rt = d.rectTransform;
            rt.anchoredPosition = new Vector2(Random.Range(-w * 0.45f, w * 0.45f), Random.Range(-h * 0.48f, h * 0.48f));
            float sz = Random.Range(4f, 16f);
            rt.sizeDelta = new Vector2(sz * Random.Range(0.6f, 2.2f), sz);
            rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 180f));
            d.color = Random.value < 0.7f ? new Color(0f, 0f, 0f, 0.75f * f) : new Color(Paper.r, Paper.g, Paper.b, 0.6f * f);
        }

        // 가로 지직: 0.04초마다 줄을 새로 뿌림. 가끔 확 몰려서 터짐 (되감을 땐 자주)
        if (burstLeft > 0f) burstLeft -= dt;
        else if (Random.value < (rewinding ? 0.05f : 0.015f)) burstLeft = Random.Range(0.08f, 0.22f);
        staticTick -= dt;
        if (staticTick <= 0f)
        {
            staticTick = 0.04f;
            float power = (rewinding ? Mathf.Lerp(0.5f, 0.85f, u) : 0.35f) + (burstLeft > 0f ? 0.5f : 0f);
            float bandY = Random.Range(-h * 0.4f, h * 0.4f); // 터질 때는 한 곳에 몰림
            for (int i = 0; i < staticLines.Length; i++)
            {
                var l = staticLines[i];
                bool on = Random.value < power;
                l.enabled = on;
                if (!on) continue;
                var rt = l.rectTransform;
                float y = burstLeft > 0f ? bandY + Random.Range(-90f, 90f) : Random.Range(-h * 0.5f, h * 0.5f);
                rt.anchoredPosition = new Vector2(Random.Range(-60f, 60f) * power, y);
                rt.sizeDelta = new Vector2(w * Random.Range(0.3f, 1.2f), Random.Range(2f, burstLeft > 0f ? 20f : 8f));
                bool light = Random.value < 0.55f;
                float a = Random.Range(0.25f, 0.6f) * Mathf.Min(1f, power + 0.2f);
                l.color = light ? new Color(Paper.r, Paper.g, Paper.b, a * f) : new Color(0f, 0f, 0f, a * 1.3f * f);
            }
        }

        // 프레임 경계가 화면을 미끄러져 지나감 (되감는 중)
        frameLine.enabled = rewinding;
        if (rewinding)
        {
            frameLineY = Mathf.Repeat(frameLineY + dt * h * Mathf.Lerp(2f, 7f, u), h + 60f);
            frameLine.rectTransform.anchoredPosition = new Vector2(0f, -frameLineY + 30f);
        }

        // ◀◀ (되감는 중 오른쪽 위, 숨 쉬듯)
        float iconA = Mathf.Lerp(0.7f, 1f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));
        foreach (var a in arrows)
        {
            a.enabled = rewinding;
            a.color = new Color(Paper.r, Paper.g, Paper.b, iconA);
        }
        FitWorldBlack();

        // 안내: 서서히 나타나 숨 쉬듯, 점이 O 를 따라 돎 (위에서 시작)
        promptTime += dt;
        float target = promptOn ? Mathf.Lerp(0.75f, 1f, 0.5f + 0.5f * Mathf.Sin(promptTime * 3f)) : 0f;
        promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, target, dt * 3f);
        float ang = Mathf.PI * 0.5f + Mathf.Repeat(promptTime * 0.9f, 1f) * Mathf.PI * 2f;
        ringDot.anchoredPosition = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 62f;

        ApplyVolume();
    }

    public void FadeOut(float time) { StartCoroutine(FadeRoutine(time)); }

    IEnumerator FadeRoutine(float time)
    {
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            fade = 1f - t / time;
            group.alpha = fade;
            Tick(1f, false);
            yield return null;
        }
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (profile != null) Destroy(profile);
        if (worldBlack != null) Destroy(worldBlack.gameObject); // 카메라 밑에 붙어 있어서 따로 지움
    }

    /// <summary>1 유닛 흰 네모 (검은 막).</summary>
    static Sprite SquareSprite()
    {
        if (squareSprite != null) return squareSprite;
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var px = new Color32[16];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px);
        tex.Apply();
        squareSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        return squareSprite;
    }

    Image MakeImage(string name, Color c, Transform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent != null ? parent : transform, false);
        var img = go.GetComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    static Image Full(Image img)
    {
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return img;
    }

    /// <summary>왼쪽을 가리키는 삼각형.</summary>
    static Sprite TriangleSprite()
    {
        if (triSprite != null) return triSprite;
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float half = (x + 0.5f) / n * (n * 0.5f);
                bool inside = Mathf.Abs(y + 0.5f - n * 0.5f) <= half;
                px[y * n + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        tex.SetPixels32(px);
        tex.Apply();
        triSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return triSprite;
    }

    /// <summary>O 모양 고리.</summary>
    static Sprite RingSprite()
    {
        if (ringSprite != null) return ringSprite;
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - n * 0.42f) / 6f) * 1.6f;
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
        tex.SetPixels32(px);
        tex.Apply();
        ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return ringSprite;
    }

    /// <summary>부드러운 동그라미 (먼지 티·안내 점).</summary>
    static Sprite BlobSprite()
    {
        if (blobSprite != null) return blobSprite;
        const int n = 16;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 2.5f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        blobSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return blobSprite;
    }
}
