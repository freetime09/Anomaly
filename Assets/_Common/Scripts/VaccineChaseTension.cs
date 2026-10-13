using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 백신 추격 중 긴장감 연출. 백신 벽(VaccineWall)과 같은 오브젝트에 붙인다.
///  - 화면 가장자리 붉은 빛(비네트): 벽이 가까울수록 진해지고, 심장 뛰듯 깜빡임이 빨라진다.
///  - 웅웅거리는 소리: 쫓는 동안 계속 나고, 가까울수록 커지고 높아진다.
/// 쫓는 중이 아니면(연출 중·죽음·끝) 둘 다 서서히 꺼진다.
/// 화면 흔들림은 VaccineWall 이 거리에 따라 직접 처리한다.
/// </summary>
[RequireComponent(typeof(VaccineWall))]
public class VaccineChaseTension : MonoBehaviour
{
    [Header("거리")]
    [Tooltip("벽이 이 거리 안으로 들어오면 연출 시작")]
    public float far = 14f;
    [Tooltip("이 거리 이하면 최대")]
    public float near = 2f;
    [Tooltip("켜지고 꺼지는 부드러움 (클수록 빨리 따라감)")]
    public float response = 4f;

    [Header("화면 가장자리 붉은 빛")]
    public Color vignetteColor = new Color(0.85f, 0.05f, 0.05f, 1f);
    [Tooltip("최대 진하기 (0~1)")]
    [Range(0f, 1f)] public float vignetteMax = 0.7f;
    [Tooltip("붉은 빛이 화면 안쪽으로 얼마나 들어오는지 (0~1, 클수록 안쪽까지)")]
    [Range(0f, 1f)] public float vignetteReach = 0.45f;
    [Tooltip("심장 박동 속도 (멀 때 ~ 가까울 때, 초당 박동 수)")]
    public Vector2 heartRate = new Vector2(1.1f, 2.6f);
    [Tooltip("박동할 때 진하기 변화 폭")]
    [Range(0f, 1f)] public float heartAmount = 0.35f;

    [Header("소리")]
    [Tooltip("반복 재생할 소리. 비우면 코드로 만든 임시 웅웅 소리")]
    public AudioClip loopClip;
    [Tooltip("멀 때 ~ 가까울 때 볼륨")]
    public Vector2 volume = new Vector2(0.15f, 0.9f);
    [Tooltip("멀 때 ~ 가까울 때 높낮이")]
    public Vector2 pitch = new Vector2(0.9f, 1.25f);

    VaccineWall wall;
    float level;       // 0~1 현재 긴장도 (부드럽게)
    float active;      // 0~1 쫓는 중인지 (부드럽게)
    float beatPhase;
    Canvas canvas;
    Image vignette;
    Texture2D vigTex;
    AudioSource src;
    AudioClip generated;

    void Awake()
    {
        wall = GetComponent<VaccineWall>();
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = true;
        src.spatialBlend = 0f;
        src.volume = 0f;
        src.clip = loopClip != null ? loopClip : (generated = GenerateLoop());
        BuildVignette();
    }

    void Update()
    {
        bool chasing = wall != null && wall.IsChasing;
        float target = chasing ? Mathf.InverseLerp(far, near, wall.Gap) : 0f;
        float k = 1f - Mathf.Exp(-response * Time.deltaTime);
        level = Mathf.Lerp(level, target, k);
        active = Mathf.Lerp(active, chasing ? 1f : 0f, k);

        // 비네트 + 심장 박동
        beatPhase += Mathf.Lerp(heartRate.x, heartRate.y, level) * Time.deltaTime;
        float f = beatPhase % 1f;
        float beat = Mathf.Exp(-f * 8f) + 0.6f * Mathf.Exp(-Mathf.Max(0f, f - 0.18f) * 10f) * (f > 0.18f ? 1f : 0f); // 쿵-쿵
        float a = vignetteMax * level * (1f - heartAmount + heartAmount * Mathf.Clamp01(beat));
        var c = vignetteColor; c.a = a;
        vignette.color = c;
        vignette.enabled = a > 0.003f;

        // 소리
        float vol = active * Mathf.Lerp(volume.x, volume.y, level);
        src.volume = vol;
        src.pitch = Mathf.Lerp(pitch.x, pitch.y, level);
        if (vol > 0.001f && !src.isPlaying) src.Play();
        else if (vol <= 0.001f && src.isPlaying) src.Stop();
    }

    void BuildVignette()
    {
        var go = new GameObject("ChaseTensionCanvas");
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 800; // 시작 연출 자막(900)보다 아래
        var img = new GameObject("Vignette", typeof(RectTransform), typeof(Image));
        img.transform.SetParent(go.transform, false);
        var rt = img.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        vignette = img.GetComponent<Image>();
        vignette.raycastTarget = false;
        vigTex = MakeVignetteTexture(vignetteReach);
        vignette.sprite = Sprite.Create(vigTex, new Rect(0, 0, vigTex.width, vigTex.height), new Vector2(0.5f, 0.5f));
        vignette.enabled = false;
    }

    /// <summary>가운데는 투명, 가장자리로 갈수록 불투명한 흰색 텍스처 (색은 Image.color 로).</summary>
    static Texture2D MakeVignetteTexture(float reach)
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float inner = Mathf.Lerp(0.95f, 0.3f, reach); // 투명한 안쪽 반경
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                // 가장자리 가까운 정도 (네모난 화면에 맞게 둥근 사각형 거리)
                float d = Mathf.Pow(Mathf.Pow(Mathf.Abs(u), 4f) + Mathf.Pow(Mathf.Abs(v), 4f), 0.25f);
                float t = Mathf.Clamp01((d - inner) / (1f - inner));
                byte a = (byte)(255 * t * t);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    /// <summary>임시 소리: 낮은 웅웅거림 + 떨림, 이음매 없이 반복되는 2초 루프.</summary>
    static AudioClip GenerateLoop()
    {
        const int rate = 44100;
        const float len = 2f;
        int n = (int)(rate * len);
        int fade = (int)(rate * 0.05f);
        var raw = new float[n + fade]; // 이음매용으로 조금 더 만들어 앞부분과 교차 페이드
        var rnd = new System.Random(5);
        float lp = 0f;
        for (int i = 0; i < raw.Length; i++)
        {
            float t = (float)i / rate;
            // 주파수를 0.5Hz 배수로 맞춰 2초 루프가 이어지게
            float tone = Mathf.Sin(2f * Mathf.PI * 41f * t) * 0.5f
                       + Mathf.Sin(2f * Mathf.PI * 61.5f * t) * 0.25f
                       + Mathf.Sin(2f * Mathf.PI * 82f * t) * 0.12f;
            float trem = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 3f * t); // 떨림 (3Hz, 루프에 맞음)
            lp += ((float)rnd.NextDouble() * 2f - 1f - lp) * 0.03f;
            raw[i] = (tone * trem + lp * 1.5f) * 0.5f;
        }
        // 노이즈 이음매: 끝에서 이어지는 부분(raw[n..])을 앞 0.05초와 교차 페이드 → 끝→처음이 자연스럽게 이어짐
        var data = new float[n];
        for (int i = 0; i < n; i++)
            data[i] = i < fade ? Mathf.Lerp(raw[n + i], raw[i], (float)i / fade) : raw[i];
        var clip = AudioClip.Create("VaccineRumble_Temp", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
        if (vigTex != null) Destroy(vigTex);
        if (generated != null) Destroy(generated);
    }
}
