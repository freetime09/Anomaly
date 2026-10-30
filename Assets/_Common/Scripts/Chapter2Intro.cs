using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 챕터2 시작 연출: 1챕터에서 넘어오자마자 플레이어가 공중에서 검은 방으로 떨어진다.
///  1) 시작 순간 화면 지지직(글리치) → 2) 화면이 까맣게 되고 하얀 속도선이 위로 스쳐 지나가며 낙하 (중간중간 글리치)
///  3) 착지 순간 화면이 밝아지며 충격(흔들림 + 글리치), 잠깐 조작 잠금 → 4) 메크로 말풍선 대사 (챕터 3과 같은 방식).
/// 플레이어의 씬 배치 위치가 착지 지점(= 기본 부활 지점)이고, 시작할 때 그 위 fallHeight 만큼 올려서 떨어뜨린다.
/// 연출 동안은 제스처 입력을 잠근다.
/// </summary>
[DefaultExecutionOrder(50)] // FallRespawn.Start(부활 지점 저장)·CameraFollow.Start 이후에 플레이어를 들어올림
public class Chapter2Intro : MonoBehaviour
{
    [Header("대상 (비우면 자동)")]
    public PlayerController player;

    [Header("낙하")]
    [Tooltip("착지 지점(플레이어 배치 위치)에서 얼마나 위에서 떨어질지")]
    public float fallHeight = 40f;
    [Tooltip("낙하 최고 속도 (낮을수록 길게 떨어짐)")]
    public float maxFallSpeed = 16f;
    [Tooltip("낙하 중 카메라 따라가기 시간 (빨리 떨어져서 화면 밖으로 안 나가게)")]
    public float fallCameraSmooth = 0.05f;

    [Header("어둠 (낙하 중 화면 전체를 검게, 플레이어만 보임)")]
    public Color darkColor = Color.black;
    [Tooltip("떨어지기 시작해서 완전히 검어지는 시간")]
    public float darkFadeInTime = 0.5f;
    [Tooltip("바닥까지 이 거리(units)부터 어둠이 걷히기 시작 → 흰 윤곽 바닥이 다가오는 게 보임")]
    public float revealDistance = 12f;
    [Tooltip("착지 순간 남은 어둠이 사라지는 시간")]
    public float darkFadeOutTime = 0.2f;
    [Tooltip("낙하 중 카메라 세로 오프셋 (작을수록 화면 아래쪽이 더 보임 → 바닥이 일찍 보임). 착지 후 원래 값으로 돌아감")]
    public float fallCameraOffsetY = 0f;
    [Tooltip("어둠 그리기 순서 (블럭 0 < 어둠 < 속도선 < 플레이어 10)")]
    public int darkSortingOrder = 5;

    [Header("속도선 (위로 스쳐 지나가는 하얀 선)")]
    public int speedLineCount = 24;
    public Color speedLineColor = new Color(1f, 1f, 1f, 0.6f);
    [Tooltip("선 길이 (최소~최대 units)")]
    public Vector2 speedLineLength = new Vector2(1.5f, 5f);
    [Tooltip("선 두께 (최소~최대 units)")]
    public Vector2 speedLineWidth = new Vector2(0.03f, 0.08f);
    [Tooltip("선 자체가 위로 더 빨리 가는 속도 (최소~최대, 원근감)")]
    public Vector2 speedLineExtraSpeed = new Vector2(0f, 10f);

    [Header("글리치")]
    [Tooltip("시작 순간 글리치 길이")]
    public float startGlitchTime = 0.6f;
    [Tooltip("낙하 중 글리치 간격 (최소~최대 초)")]
    public Vector2 fallGlitchInterval = new Vector2(0.4f, 1.1f);
    [Tooltip("낙하 중 글리치 한 번 길이")]
    public float fallGlitchTime = 0.15f;
    [Tooltip("착지 순간 글리치 길이")]
    public float landGlitchTime = 0.35f;
    [Tooltip("동시에 보이는 가로 줄 수 (최대)")]
    public int glitchBars = 14;

    [Header("착지")]
    public float landShakeTime = 0.4f;
    public float landShakeMagnitude = 0.7f;
    [Tooltip("착지 후 조작을 막아두는 시간 (충격 여운)")]
    public float landLockTime = 0.5f;

    [Header("소리")]
    [Tooltip("낙하 소리. 크게 나는 부분이 끝나는 순간이 착지에 오도록 시작 시점을 자동으로 맞추고, 착지하면 멈춘다")]
    public AudioClip fallClip;
    [Range(0f, 1f)] public float fallVolume = 1f;
    [Tooltip("착지할 때 낙하 소리가 꺼지는 시간 (초). 0 이면 뚝 끊김")]
    [Min(0f)] public float fallFadeOut = 0.15f;
    [Tooltip("착지하는 순간 '쿵' 소리")]
    public AudioClip landClip;
    [Range(0f, 1f)] public float landVolume = 1f;

    [Header("대사 (챕터 3과 같은 말풍선)")]
    [Tooltip("착지 뒤 메크로 머리 위 말풍선으로 나올 대사")]
    public DialogueData dialogue;

    Rigidbody2D rb;
    GestureRecognizer input;
    CameraFollow cam;
    Camera view;
    Texture2D whiteTex;
    Sprite whiteSprite;
    SpriteRenderer dark;
    SpriteRenderer[] lines;
    float[] lineSpeed;
    float[] lineAlpha;
    float curDark;
    bool linesRecycle;
    Canvas canvas;
    RectTransform[] bars;
    Image[] barImgs;
    bool glitching;
    bool landed;

    void Start()
    {
        if (player == null) player = FindObjectOfType<PlayerController>();
        if (player == null) { enabled = false; return; }
        rb = player.GetComponent<Rigidbody2D>();
        input = player.GetComponent<GestureRecognizer>();
        cam = FindObjectOfType<CameraFollow>();
        view = cam != null ? cam.GetComponent<Camera>() : Camera.main;
        whiteTex = new Texture2D(1, 1);
        whiteTex.SetPixel(0, 0, Color.white);
        whiteTex.Apply();
        whiteSprite = Sprite.Create(whiteTex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        BuildDarkness();
        BuildSpeedLines();
        BuildCanvas();
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        Vector3 land = player.transform.position;
        if (input != null) input.enabled = false;
        player.ResetMotion();
        Vector3 top = land + Vector3.up * fallHeight;
        player.transform.position = top;
        rb.position = top;
        float oldSmooth = cam != null ? cam.smoothTime : 0f;
        Vector3 oldOffset = cam != null ? cam.offset : Vector3.zero;
        if (cam != null)
        {
            cam.offset = new Vector3(oldOffset.x, fallCameraOffsetY, oldOffset.z);
            cam.SnapToTarget();
            cam.smoothTime = fallCameraSmooth;
        }

        // 1) 넘어오는 순간 글리치
        StartCoroutine(Glitch(startGlitchTime, 1f));
        if (cam != null) cam.Shake(startGlitchTime, 0.3f);

        // 2) 낙하: 속도 제한 + 화면이 검게 + 속도선 + 간간이 글리치, 바닥이 가까워지면 어둠이 걷힘
        float fallStart = Time.time;
        landed = false;
        if (fallClip != null) StartCoroutine(FallSound(land));
        ScatterSpeedLines();
        linesRecycle = true;
        float viewH = view != null ? view.orthographicSize * 2f : 16f;
        float nextGlitch = Time.time + startGlitchTime + Random.Range(fallGlitchInterval.x, fallGlitchInterval.y);
        while (true)
        {
            yield return new WaitForFixedUpdate();
            Vector2 v = rb.linearVelocity;
            if (v.y < -maxFallSpeed) rb.linearVelocity = new Vector2(v.x, -maxFallSpeed);

            // 바닥이 화면에 들어올 즈음부터는 새 속도선을 안 만듦 (남은 선은 위로 빠져나감)
            float dist = player.transform.position.y - land.y;
            if (dist < viewH) linesRecycle = false;

            float fadeIn = darkFadeInTime > 0f ? Mathf.Clamp01((Time.time - fallStart) / darkFadeInTime) : 1f;
            float reveal = Mathf.Clamp01((dist - 1f) / Mathf.Max(0.01f, revealDistance - 1f));
            SetDark(Mathf.Min(fadeIn, reveal));

            if (Time.time >= nextGlitch)
            {
                StartCoroutine(Glitch(fallGlitchTime, 0.5f));
                nextGlitch = Time.time + Random.Range(fallGlitchInterval.x, fallGlitchInterval.y);
            }

            // 착지: 착지 지점 근처까지 내려왔고 더 이상 떨어지지 않음
            if (player.transform.position.y < land.y + 1f && rb.linearVelocity.y > -0.5f) break;
        }
        landed = true;
        if (landClip != null) gameObject.AddComponent<AudioSource>().PlayOneShot(landClip, landVolume);

        // 3) 착지: 화면이 밝아지며 충격(흔들림 + 글리치), 잠깐 조작 잠금
        if (cam != null) { cam.smoothTime = oldSmooth; cam.offset = oldOffset; cam.Shake(landShakeTime, landShakeMagnitude); }
        StartCoroutine(Glitch(landGlitchTime, 1f));
        StartCoroutine(FadeDark(curDark, 0f, darkFadeOutTime));
        HideSpeedLines();
        player.ResetMotion();
        yield return new WaitForSeconds(landLockTime);

        // 4) 조작 가능 → 말풍선 대사
        if (input != null) input.enabled = true;
        if (dialogue != null) yield return DialoguePlayer.Ensure().PlayAndWait(dialogue);
        Cleanup();
    }

    // ── 낙하 소리: 착지까지 남은 시간을 매 스텝 예측해서, 소리의 크게 나는 부분이 끝나는 순간이 착지에 오게 재생 위치를 맞춤 ──
    IEnumerator FallSound(Vector3 land)
    {
        float peak = SoundLevels.LoudEndTime(fallClip);
        var src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.clip = fallClip;
        src.volume = fallVolume;
        float last = fallClip.length - 0.01f;
        while (!landed)
        {
            float want = peak - TimeToLand(land); // 지금 있어야 할 재생 위치
            if (!src.isPlaying) { if (want >= 0f) { src.time = Mathf.Min(want, last); src.Play(); } } // 낙하가 소리보다 길면 기다렸다가, 짧으면 중간부터
            else if (want >= 0f && want < last && Mathf.Abs(src.time - want) > 0.12f) src.time = want; // 예측이 어긋나면 다시 맞춤
            yield return new WaitForFixedUpdate();
        }
        // 착지: 바람 소리는 여기서 끝 (뒤에 남은 여운은 틀지 않음)
        float v0 = src.volume;
        for (float t = 0f; t < fallFadeOut && src.isPlaying; t += Time.deltaTime)
        {
            src.volume = v0 * (1f - t / fallFadeOut);
            yield return null;
        }
        src.Stop();
    }

    /// <summary>지금 속도·중력·최고 속도로 착지 지점까지 걸릴 시간.</summary>
    float TimeToLand(Vector3 land)
    {
        float d = Mathf.Max(0f, player.transform.position.y - land.y);
        float s = Mathf.Max(0f, -rb.linearVelocity.y);
        float g = Mathf.Max(0.01f, -Physics2D.gravity.y * rb.gravityScale);
        float vmax = Mathf.Max(s, maxFallSpeed);
        float t1 = (vmax - s) / g, d1 = s * t1 + 0.5f * g * t1 * t1; // 최고 속도까지 빨라지는 구간
        if (d1 >= d) return (-s + Mathf.Sqrt(s * s + 2f * g * d)) / g;
        return t1 + (d - d1) / vmax;
    }

    // ── 어둠 (카메라 화면을 덮는 검은 판, 플레이어보다 뒤) ──
    void BuildDarkness()
    {
        var go = new GameObject("IntroDarkness");
        dark = go.AddComponent<SpriteRenderer>();
        dark.sprite = whiteSprite;
        dark.sortingOrder = darkSortingOrder;
        go.transform.localScale = new Vector3(200f, 200f, 1f); // 화면보다 충분히 크게 (흔들림 대비)
        SetDark(0f);
    }

    void SetDark(float k)
    {
        curDark = Mathf.Clamp01(k);
        if (dark == null) return;
        dark.color = new Color(darkColor.r, darkColor.g, darkColor.b, darkColor.a * curDark);
    }

    IEnumerator FadeDark(float from, float to, float time)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            SetDark(Mathf.Lerp(from, to, t / time));
            yield return null;
        }
        SetDark(to);
    }

    // ── 속도선 ──
    void BuildSpeedLines()
    {
        lines = new SpriteRenderer[speedLineCount];
        lineSpeed = new float[speedLineCount];
        lineAlpha = new float[speedLineCount];
        var root = new GameObject("IntroSpeedLines").transform;
        for (int i = 0; i < speedLineCount; i++)
        {
            var go = new GameObject("Line");
            go.transform.SetParent(root, false);
            lines[i] = go.AddComponent<SpriteRenderer>();
            lines[i].sprite = whiteSprite;
            lines[i].sortingOrder = darkSortingOrder + 1;
            lines[i].enabled = false;
        }
    }

    void ViewRect(out Vector2 center, out float halfW, out float halfH)
    {
        center = view != null ? (Vector2)view.transform.position : (Vector2)player.transform.position;
        halfH = view != null ? view.orthographicSize : 8f;
        halfW = halfH * (view != null ? view.aspect : 16f / 9f);
    }

    // 낙하 시작: 화면 안과 아래쪽에 고루 뿌림
    void ScatterSpeedLines()
    {
        Vector2 c; float hw, hh;
        ViewRect(out c, out hw, out hh);
        for (int i = 0; i < lines.Length; i++)
            PlaceLine(i, Random.Range(c.y - hh * 3f, c.y + hh), c, hw);
    }

    void PlaceLine(int i, float y, Vector2 c, float hw)
    {
        var l = lines[i];
        float len = Random.Range(speedLineLength.x, speedLineLength.y);
        float w = Random.Range(speedLineWidth.x, speedLineWidth.y);
        l.transform.position = new Vector3(c.x + Random.Range(-hw, hw), y, 0f);
        l.transform.localScale = new Vector3(w, len, 1f);
        lineAlpha[i] = speedLineColor.a * Random.Range(0.4f, 1f);
        l.color = speedLineColor;
        l.enabled = true;
        lineSpeed[i] = Random.Range(speedLineExtraSpeed.x, speedLineExtraSpeed.y);
    }

    void HideSpeedLines()
    {
        linesRecycle = false;
        if (lines == null) return;
        foreach (var l in lines) if (l != null) l.enabled = false;
    }

    void Update()
    {
        if (lines == null) return;
        Vector2 c; float hw, hh;
        ViewRect(out c, out hw, out hh);
        for (int i = 0; i < lines.Length; i++)
        {
            var l = lines[i];
            if (!l.enabled) continue;
            l.transform.position += Vector3.up * lineSpeed[i] * Time.deltaTime;
            var col = speedLineColor;
            col.a = lineAlpha[i] * curDark; // 어둠이 걷히면 선도 같이 사라짐
            l.color = col;
            float bottom = l.transform.position.y - l.transform.localScale.y * 0.5f;
            if (bottom > c.y + hh) // 화면 위로 빠져나감 → 아래에서 다시
            {
                if (linesRecycle) PlaceLine(i, c.y - hh - Random.Range(1f, hh), c, hw);
                else l.enabled = false;
            }
        }
    }

    // ── 글리치 (화면 위에 무작위 가로 줄들이 지직거림) ──
    void BuildCanvas()
    {
        var go = new GameObject("IntroCanvas");
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
    }

    IEnumerator Glitch(float duration, float strength)
    {
        if (glitching) yield break; // 겹치면 앞의 것이 계속
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
                float bw = w * Random.Range(0.3f, 1.2f);
                bars[i].sizeDelta = new Vector2(bw, bh);
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

    void Cleanup()
    {
        StopAllCoroutines();
        if (canvas != null) Destroy(canvas.gameObject);
        if (dark != null) Destroy(dark.gameObject);
        if (lines != null && lines.Length > 0 && lines[0] != null) Destroy(lines[0].transform.parent.gameObject);
        lines = null;
        dark = null;
        enabled = false;
    }

    void OnDestroy()
    {
        if (whiteSprite != null) Destroy(whiteSprite);
        if (whiteTex != null) Destroy(whiteTex);
    }
}
