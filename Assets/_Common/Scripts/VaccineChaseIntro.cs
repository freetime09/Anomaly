using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 검은 방(챕터 2) 추격 시작 연출. 추격 시작 트리거(VaccineChaseTrigger)를 처음 밟으면 재생된다.
///  1) 조작 잠금, 뒤에서 이상한 소리 + 화면 흔들림/글리치
///  2) 플레이어가 뒤(lookBackDir 쪽)를 돌아봄 + 자막1
///  3) 카메라가 뒤로 이동 → 하얀 백신 벽이 천천히 밀려오는 게 보임 + 자막2
///  4) 카메라가 플레이어로 돌아오고 플레이어는 앞을 봄
///  5) 조작 풀림 + 추격 시작 (벽.Begin)
/// 한 번 본 뒤에는(부활 후 다시 밟아도) 바로 추격한다.
/// </summary>
public class VaccineChaseIntro : MonoBehaviour
{
    [Header("대상 (비우면 자동)")]
    public PlayerController player;

    [Header("1) 이상한 소리")]
    [Tooltip("뒤에서 나는 소리. 비우면 코드로 만든 임시 소리(웅웅거림 + 지지직)")]
    public AudioClip strangeSound;
    [Range(0f, 1f)] public float soundVolume = 0.8f;
    [Tooltip("소리가 나고 돌아보기까지 시간")]
    public float beforeTurnTime = 0.9f;
    public float startShakeTime = 0.5f;
    public float startShakeMagnitude = 0.15f;
    public float startGlitchTime = 0.35f;

    [Header("2) 돌아보기")]
    [Tooltip("돌아볼 방향 (1 = 오른쪽, -1 = 왼쪽). 연출이 끝나면 반대쪽(진행 방향)을 봄")]
    public int lookBackDir = -1;
    [TextArea] public string subtitle1 = "...무슨 소리지?";
    [Tooltip("돌아본 뒤 카메라가 움직이기까지 시간")]
    public float afterTurnTime = 1.0f;

    [Header("3) 카메라가 뒤를 비춤")]
    [Tooltip("카메라가 지금 위치에서 이만큼 이동해 뒤를 비춤 (월드 유닛, 백신 벽이 보이게)")]
    public Vector2 panOffset = new Vector2(-10f, 0f);
    public float panTime = 0.9f;
    [Tooltip("백신 벽을 비추는 시간")]
    public float lookTime = 1.8f;
    [TextArea] public string subtitle2 = "백신이다... 도망쳐야 해!";
    [Tooltip("벽을 비추는 동안 화면 흔들림 세기")]
    public float lookShakeMagnitude = 0.08f;

    [Header("4) 돌아오기")]
    public float panBackTime = 0.6f;

    [Header("자막")]
    public TMP_FontAsset font;
    public float subtitleSize = 48f;
    public float subtitleFade = 0.3f;
    [Range(0f, 1f)] public float subtitleHeight = 0.42f;

    [Header("글리치")]
    public int glitchBars = 12;

    public bool Played { get; private set; }
    public bool Playing { get; private set; }

    VaccineWall wall;
    GestureRecognizer input;
    CameraFollow cam;
    FallRespawn respawn;
    AudioSource audioSrc;
    AudioClip generatedClip;
    Canvas canvas;
    RectTransform[] bars;
    Image[] barImgs;
    TextMeshProUGUI subText;
    bool glitching;

    void Awake()
    {
        if (player == null) player = FindObjectOfType<PlayerController>();
        if (player != null) input = player.GetComponent<GestureRecognizer>();
        cam = FindObjectOfType<CameraFollow>();
        respawn = FindObjectOfType<FallRespawn>();
        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.spatialBlend = 0f;
    }

    void OnEnable()  { if (respawn != null) respawn.Died += OnPlayerDied; }
    void OnDisable() { if (respawn != null) respawn.Died -= OnPlayerDied; }

    /// <summary>연출 시작 (VaccineChaseTrigger가 부름).</summary>
    public void Play(VaccineWall w)
    {
        if (Played || player == null) return;
        Played = true;
        Playing = true;
        wall = w;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        if (canvas == null) BuildCanvas();
        if (input != null) input.enabled = false;
        player.ResetMotion();

        // 1) 이상한 소리 + 흔들림 + 글리치
        var clip = strangeSound != null ? strangeSound : (generatedClip != null ? generatedClip : (generatedClip = GenerateStrangeSound()));
        audioSrc.PlayOneShot(clip, soundVolume);
        if (cam != null) cam.Shake(startShakeTime, startShakeMagnitude);
        StartCoroutine(Glitch(startGlitchTime, 0.6f));
        yield return new WaitForSeconds(beforeTurnTime);

        // 2) 뒤돌아봄 + 자막1
        player.ResetMotion();
        player.Face(lookBackDir < 0 ? -1 : 1);
        StartCoroutine(ShowSubtitle(subtitle1, afterTurnTime + panTime * 0.5f));
        yield return new WaitForSeconds(afterTurnTime);

        // 3) 카메라가 뒤로 → 벽이 천천히 다가옴 + 자막2
        if (wall != null) wall.ShowForIntro();
        Vector3 back = cam != null ? cam.transform.position + (Vector3)panOffset : Vector3.zero;
        if (cam != null)
        {
            cam.enabled = false; // 따라가기 멈추고 직접 움직임
            yield return MoveCamera(cam.transform.position, back, panTime);
        }
        StartCoroutine(ShowSubtitle(subtitle2, lookTime));
        for (float t = 0f; t < lookTime; t += Time.deltaTime)
        {
            if (cam != null) cam.transform.position = back + (Vector3)(Random.insideUnitCircle * lookShakeMagnitude);
            yield return null;
        }

        // 4) 카메라 복귀 + 앞을 봄
        if (cam != null)
        {
            Vector3 home = player.transform.position + cam.offset;
            yield return MoveCamera(back, home, panBackTime);
            cam.enabled = true;
        }
        player.Face(lookBackDir < 0 ? 1 : -1);

        // 5) 조작 풀림 + 추격 시작
        Finish();
        if (wall != null) wall.Begin();
    }

    void Finish()
    {
        if (input != null) input.enabled = true;
        if (cam != null) cam.enabled = true;
        if (subText != null) SetSubAlpha(0f);
        Playing = false;
    }

    /// <summary>연출 중 죽으면(일시정지 메뉴 '체크포인트에서 다시' 등) 연출을 끊고 처음 상태로 — 다시 밟으면 연출부터.</summary>
    void OnPlayerDied()
    {
        if (!Playing) return;
        StopAllCoroutines();
        glitching = false;
        if (bars != null) foreach (var b in bars) b.gameObject.SetActive(false);
        Finish();
        if (wall != null) wall.CancelIntro();
        Played = false;
    }

    IEnumerator MoveCamera(Vector3 from, Vector3 to, float time)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / time);
            cam.transform.position = Vector3.Lerp(from, to, k);
            yield return null;
        }
        cam.transform.position = to;
    }

    // ── 자막 ──
    IEnumerator ShowSubtitle(string text, float hold)
    {
        if (string.IsNullOrEmpty(text) || subText == null) yield break;
        subText.text = text;
        for (float t = 0f; t < subtitleFade; t += Time.deltaTime) { SetSubAlpha(t / subtitleFade); yield return null; }
        SetSubAlpha(1f);
        yield return new WaitForSeconds(Mathf.Max(0f, hold - subtitleFade * 2f));
        for (float t = 0f; t < subtitleFade; t += Time.deltaTime) { SetSubAlpha(1f - t / subtitleFade); yield return null; }
        if (subText.text == text) SetSubAlpha(0f); // 그 사이 다음 자막으로 바뀌었으면 건드리지 않음
    }

    void SetSubAlpha(float a) { var c = subText.color; c.a = Mathf.Clamp01(a); subText.color = c; }

    // ── 화면 (글리치 줄 + 자막) ──
    void BuildCanvas()
    {
        var go = new GameObject("ChaseIntroCanvas");
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        bars = new RectTransform[glitchBars];
        barImgs = new Image[glitchBars];
        for (int i = 0; i < glitchBars; i++)
        {
            var b = new GameObject("GlitchBar", typeof(RectTransform), typeof(Image));
            b.transform.SetParent(go.transform, false);
            bars[i] = b.GetComponent<RectTransform>();
            barImgs[i] = b.GetComponent<Image>();
            barImgs[i].raycastTarget = false;
            bars[i].anchorMin = bars[i].anchorMax = new Vector2(0.5f, 0.5f);
            b.SetActive(false);
        }

        var t = new GameObject("Subtitle", typeof(RectTransform));
        t.transform.SetParent(go.transform, false);
        subText = t.AddComponent<TextMeshProUGUI>();
        if (font != null) subText.font = font;
        subText.fontSize = subtitleSize;
        subText.alignment = TextAlignmentOptions.Center;
        subText.color = new Color(1f, 1f, 1f, 0f);
        subText.raycastTarget = false;
        var rt = subText.rectTransform;
        rt.anchorMin = new Vector2(0f, subtitleHeight);
        rt.anchorMax = new Vector2(1f, subtitleHeight);
        rt.sizeDelta = new Vector2(0f, 120f);
        rt.anchoredPosition = Vector2.zero;
    }

    IEnumerator Glitch(float duration, float strength)
    {
        if (glitching) yield break;
        glitching = true;
        var rt = (RectTransform)canvas.transform;
        float end = Time.time + duration;
        while (Time.time < end)
        {
            float w = rt.rect.width, h = rt.rect.height;
            int count = Mathf.RoundToInt(glitchBars * Mathf.Lerp(0.4f, 1f, strength));
            for (int i = 0; i < bars.Length; i++)
            {
                bool on = i < count && Random.value < 0.8f;
                bars[i].gameObject.SetActive(on);
                if (!on) continue;
                float bh = Random.Range(4f, 40f) * (Random.value < 0.15f ? 3f : 1f);
                bars[i].sizeDelta = new Vector2(w * Random.Range(0.3f, 1.2f), bh);
                bars[i].anchoredPosition = new Vector2(Random.Range(-0.4f, 0.4f) * w, Random.Range(-0.5f, 0.5f) * h);
                float r = Random.value;
                Color c = r < 0.45f ? Color.white : r < 0.8f ? Color.black : new Color(0.6f, 0.6f, 0.65f);
                c.a = Random.Range(0.35f, 0.95f) * strength;
                barImgs[i].color = c;
            }
            yield return new WaitForSeconds(Random.Range(0.03f, 0.06f));
        }
        foreach (var b in bars) b.gameObject.SetActive(false);
        glitching = false;
    }

    /// <summary>임시 소리: 낮게 웅웅거리는 소리가 커지며 사이사이 디지털 지지직 (약 2초).</summary>
    static AudioClip GenerateStrangeSound()
    {
        const int rate = 44100;
        const float len = 2.2f;
        int n = (int)(rate * len);
        var data = new float[n];
        var rnd = new System.Random(11);
        float lp = 0f;               // 저역 통과 노이즈 (웅웅)
        int burstLeft = 0; float burstFreq = 0f; float burstAmp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float env = Mathf.Clamp01(t / 0.6f) * Mathf.Clamp01((len - t) / 0.5f);
            // 웅웅: 낮은 사인 두 개 + 걸러진 노이즈, 천천히 떨림
            lp += ((float)rnd.NextDouble() * 2f - 1f - lp) * 0.02f;
            float wob = 1f + 0.3f * Mathf.Sin(t * 7f);
            float rumble = (Mathf.Sin(2f * Mathf.PI * 46f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 69f * t * wob) * 0.25f + lp * 2.5f) * 0.45f;
            // 지지직: 짧은 사각파 조각이 드문드문
            if (burstLeft <= 0 && rnd.NextDouble() < 0.0006 + 0.0012 * t / len)
            {
                burstLeft = (int)(rate * (0.02 + rnd.NextDouble() * 0.06));
                burstFreq = 300f + (float)rnd.NextDouble() * 1500f;
                burstAmp = 0.15f + (float)rnd.NextDouble() * 0.25f;
            }
            float crackle = 0f;
            if (burstLeft > 0)
            {
                burstLeft--;
                crackle = (Mathf.Sin(2f * Mathf.PI * burstFreq * t) > 0f ? 1f : -1f) * burstAmp;
                if (rnd.NextDouble() < 0.3) crackle *= -1f; // 거칠게
            }
            data[i] = Mathf.Clamp((rumble + crackle) * env, -1f, 1f);
        }
        var clip = AudioClip.Create("VaccineApproach_Temp", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void OnDestroy()
    {
        if (generatedClip != null) Destroy(generatedClip);
        if (canvas != null) Destroy(canvas.gameObject);
    }
}
