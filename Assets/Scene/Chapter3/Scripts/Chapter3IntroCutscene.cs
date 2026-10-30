using System.Collections;
using UnityEngine;
using UnityEngine.Timeline;

/// <summary>
/// 챕터3 파트 A 컷신: 검은 공간에서 달리던 메크로 → 배경이 깨짐 → 고향 마을 입구로 순간이동 → 두리번 →
/// 추억의 무언가를 보고 고향이라고 확신 → 마을 안으로 몇 걸음 걸어 들어가며 조작이 풀린다.
///
/// [구조] 연출 '순서와 타이밍'은 Timeline(PlayableDirector)이 맡고, 각 '동작'은 이 스크립트의 public 함수가 맡는다.
///  Timeline 의 Signal Track 에 놓인 신호(Signal Emitter)가 정해진 시각에 아래 함수들을 부른다 (Signal Receiver 로 연결).
///  → 타이밍을 바꾸려면 Timeline 창에서 신호를 드래그하면 되고, 연출을 빼려면 신호를 지우면 된다 (코드 수정 불필요).
///  대사 중에는 Timeline 을 일시정지했다가 대사가 끝나면 이어서 재생한다 → 대사 길이가 바뀌어도 타이밍이 꼬이지 않는다.
/// 공통 도우미(잠금·대사 대기·카메라 초점 등)는 Chapter3CutsceneBase 에 있다.
/// </summary>
public class Chapter3IntroCutscene : Chapter3CutsceneBase
{
    [Header("배경 깨짐")]
    [Tooltip("배경 깨짐 연출 컴포넌트 (비우면 자동으로 찾음)")]
    public GlitchBreakEffect glitch;
    public HouseScreenGlitch screenGlitch;
    [Tooltip("깨짐이 0에서 최대까지 차오르는 데 걸리는 시간(초). Timeline 에서 '순간이동' 신호까지의 간격과 맞추면 자연스럽다")]
    [Min(0.1f)] public float glitchRampTime = 5f;
    [Tooltip("순간이동 직전 도달할 깨짐 세기 (0~1)")]
    [Range(0f, 1f)] public float glitchPeak = 1f;

    [Header("검은 공간")]
    [Tooltip("컷신 시작 시 메크로가 서 있을 위치 (검은 공간의 왼쪽 끝)")]
    public Transform blackSpaceStart;
    [Tooltip("검은 공간에서의 카메라 배경색")]
    public Color blackSpaceBackground = new Color(0.07f, 0.07f, 0.07f);
    [Tooltip("자동으로 달릴 때 이동 속도 배율 (1 = 평소 걷기 속도). 정신없이 달리는 느낌을 위해 평소보다 빠르게")]
    [Min(0.1f)] public float runSpeedMultiplier = 1.5f;
    [Tooltip("검은 공간에서만 켤 것들 (2챕터와 같은 배경 꾸미기: 숫자 비·먼지·안개 등). 마을에 도착하면 꺼진다")]
    public Behaviour[] blackSpaceOnly;

    [Header("마을 도착")]
    [Tooltip("순간이동으로 도착할 마을 입구 위치")]
    public Transform villageArrivalPoint;
    [Tooltip("마을에서의 카메라 배경색 (하늘색)")]
    public Color villageBackground = new Color(0.62f, 0.82f, 0.95f);
    [Tooltip("도착 순간 화면 흔들림 크기")]
    [Min(0f)] public float arrivalShake = 0.3f;

    [Header("두리번 (당황)")]
    [Tooltip("좌우를 번갈아 보는 횟수")]
    [Min(0)] public int lookCount = 3;
    [Tooltip("한 번 고개를 돌리고 다음까지의 간격(초)")]
    [Min(0.05f)] public float lookInterval = 0.4f;

    [Header("추억의 무언가")]
    [Tooltip("카메라가 비출 추억의 오브젝트 (임시 오브젝트, 나중에 교체)")]
    public Transform memoryObject;
    [Tooltip("카메라가 비출 지점: 0 = 메크로, 1 = 추억의 오브젝트, 0.5 = 둘의 가운데 (메크로 말풍선과 오브젝트가 함께 보이게)")]
    [Range(0f, 1f)] public float focusBlend = 0.55f;

    [Header("대사 (말풍선)")]
    [Tooltip("도착 후 두리번거린 다음 메크로의 혼잣말")]
    public DialogueData arrivalMonologue;
    [Tooltip("추억의 무언가를 보고 고향임을 확신하는 혼잣말")]
    public DialogueData memoryMonologue;

    [Header("마무리")]
    [Tooltip("컷신 끝에 마을 안쪽(오른쪽)으로 자동으로 걸어 들어가는 거리 (유닛)")]
    [Min(0f)] public float walkInDistance = 3f;

    [Header("소리")]
    [Tooltip("마을로 넘어가는 '피잉~' 소리. 소리가 치솟다 꺼지는 순간(뒤 여운 제외)에 마을에 도착하도록 시작 시각을 맞춤")]
    public AudioClip riserClip;
    [Range(0f, 1f)] public float riserVolume = 1f;
    [Tooltip("마을 도착 뒤 크게 치솟는 소리가 더 이어지는 시간 (초). 0 이면 치솟던 소리가 꺼지는 순간 도착")]
    [Min(0f)] public float riserTail = 0.15f;
    [Tooltip("마을 배경음악. 검은 공간에서 도망칠 때는 꺼 두고, 마을에 도착하는 순간 처음부터 튼다 (비우면 Main Camera 의 소리)")]
    public AudioSource villageBgm;

    [Header("개발용")]
    [Tooltip("켜면 컷신을 건너뛰고 바로 마을 입구에서 시작 (마을 작업·테스트용). 빌드 전에 꼭 끌 것")]
    public bool skipCutscene = false;

    float originalMoveSpeed;
    bool openingPrepared;
    Canvas openingCanvas;
    UnityEngine.UI.Image openingImage;
    [Min(0.1f)] public float openingFadeTime = 0.5f;
    bool running;   // 자동 달리기 중

    protected override void Awake()
    {
        base.Awake();
        if (glitch == null) glitch = FindAnyObjectByType<GlitchBreakEffect>();
        if (screenGlitch == null) screenGlitch = FindAnyObjectByType<HouseScreenGlitch>();
        if (villageBgm == null && Camera.main != null) villageBgm = Camera.main.GetComponent<AudioSource>();
        if (!skipCutscene)
        {
            BuildOpeningFade();
            if (villageBgm != null) { villageBgm.playOnAwake = false; villageBgm.Stop(); } // 도망치는 동안은 배경음악 없음
        }
    }

    void PlayVillageBgm()
    {
        if (villageBgm != null && !villageBgm.isPlaying) villageBgm.Play();
    }

    void Start()
    {
        if (skipCutscene)
        {
            // 빌드에 켜진 채로 들어가지 않도록 경고로 남겨 둔다
            Debug.LogWarning("[Chapter3Intro] 개발용 Skip Cutscene 이 켜져 있음 → 컷신 없이 마을 입구에서 시작");
            MovePlayer(villageArrivalPoint);
            SetBackground(villageBackground);
            SetBlackSpaceDecor(false);
            PlayVillageBgm();
            return;
        }
        // 첫 신호(0.01초)를 기다리면 그 사이 한 프레임 동안 씬에 배치된 마을 장면이 비친다.
        // 그래서 시작 상태(검은 공간·조작 잠금)를 Start 에서 바로 만들고 Timeline 을 재생한다.
        BeginCutscene();
        StartCoroutine(FadeOpening());
        Play(); // 씬 시작과 동시에 컷신 재생
        if (riserClip != null) StartCoroutine(PlayRiser());
    }

    /// <summary>'피잉~' 소리: Timeline 의 순간이동 신호 시각(+ 하얗게 차오르는 시간)에 소리 끝이 오도록 미리 틀어 둔다.</summary>
    IEnumerator PlayRiser()
    {
        double arrive = -1;
        var timeline = director.playableAsset as TimelineAsset;
        if (timeline != null)
            foreach (var track in timeline.GetOutputTracks())
                foreach (var m in track.GetMarkers())
                {
                    var e = m as SignalEmitter;
                    if (e != null && e.asset != null && e.asset.name.Contains("TeleportToVillage")) arrive = e.time;
                }
        if (timeline != null && timeline.markerTrack != null)
            foreach (var m in timeline.markerTrack.GetMarkers())
            {
                var e = m as SignalEmitter;
                if (e != null && e.asset != null && e.asset.name.Contains("TeleportToVillage")) arrive = e.time;
            }
        if (arrive < 0) yield break;
        if (glitch != null) arrive += glitch.flashInTime;

        // 소리 파일 끝이 아니라 '크게 치솟는 부분'이 끝나는 시각을 도착에 맞춤 (이 소리는 뒤에 4초쯤 사그라드는 여운이 붙어 있음)
        double start = arrive + riserTail - SoundLevels.LoudEndTime(riserClip);
        while (director.time < start) yield return null;
        var src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.clip = riserClip;
        src.volume = riserVolume;
        src.time = Mathf.Clamp((float)(director.time - start), 0f, riserClip.length - 0.01f); // 컷신이 소리보다 짧으면 중간부터
        src.Play();
        // 씬 시작 직후 프레임이 끊기면 Timeline 이 소리보다 늦어짐 → 도착할 때까지 어긋나면 다시 맞춤
        while (director.time < arrive && src.isPlaying)
        {
            float want = (float)(director.time - start);
            if (Mathf.Abs(src.time - want) > 0.15f && want < riserClip.length - 0.01f) src.time = Mathf.Max(0f, want);
            yield return null;
        }
    }

    // ───────── Timeline 신호가 부르는 동작들 (순서대로) ─────────

    /// <summary>[신호] 컷신 시작: 조작 잠금, 메크로를 검은 공간 시작점에 두고 배경을 검게.</summary>
    public void BeginCutscene()
    {
        if (openingPrepared) return;
        openingPrepared = true;
        if (screenGlitch != null) screenGlitch.StopEffect();
        LockControl();
        MovePlayer(blackSpaceStart);
        player.Face(1);
        SetBackground(blackSpaceBackground);
        SetBlackSpaceDecor(true);
        StartAutoRun();
    }

    void BuildOpeningFade()
    {
        var go = new GameObject("Chapter3OpeningFade", typeof(Canvas));
        go.transform.SetParent(transform, false);
        openingCanvas = go.GetComponent<Canvas>();
        openingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        openingCanvas.sortingOrder = 999;
        var child = new GameObject("Black", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        child.transform.SetParent(go.transform, false);
        openingImage = child.GetComponent<UnityEngine.UI.Image>();
        openingImage.raycastTarget = false;
        openingImage.color = Color.black;
        var rt = openingImage.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    IEnumerator FadeOpening()
    {
        yield return null;
        yield return new WaitForFixedUpdate();
        for (float t = 0f; t < openingFadeTime; t += Time.unscaledDeltaTime)
        {
            if (openingImage == null) yield break;
            openingImage.color = new Color(0f, 0f, 0f, 1f - Mathf.SmoothStep(0f, 1f, t / openingFadeTime));
            yield return null;
        }
        if (openingCanvas != null) Destroy(openingCanvas.gameObject);
    }

    void SetBlackSpaceDecor(bool on)
    {
        if (blackSpaceOnly == null) return;
        foreach (var b in blackSpaceOnly) if (b != null) b.enabled = on;
    }

    /// <summary>[신호] 오른쪽으로 계속 달리기 시작 (평소보다 빠르게).</summary>
    public void StartAutoRun()
    {
        if (running || player == null) return;
        originalMoveSpeed = player.moveSpeed;
        player.moveSpeed = originalMoveSpeed * runSpeedMultiplier;
        running = true;
        player.WalkToX(player.transform.position.x + 50f);
    }

    void Update()
    {
        // 자동 달리기: 남은 이동 거리가 떨어지지 않게 계속 앞쪽 목표를 다시 준다
        if (running && player != null && !player.IsMoving)
            player.WalkToX(player.transform.position.x + 50f);
    }

    /// <summary>[신호] 배경 깨짐이 점점 심해지기 시작.</summary>
    public void StartGlitchRamp()
    {
        if (glitch != null) glitch.SetIntensity(0f);
        if (screenGlitch != null) screenGlitch.BeginRamp(glitchRampTime, glitchPeak * 0.48f);
    }

    /// <summary>[신호] 하얗게 번쩍이는 순간 마을 입구로 순간이동.</summary>
    public void TeleportToVillage()
    {
        StartCoroutine(TeleportRoutine());
    }

    IEnumerator TeleportRoutine()
    {
        if (glitch == null) { ArriveAtVillage(); yield break; }
        yield return glitch.Flash(ArriveAtVillage);
    }

    void ArriveAtVillage()
    {
        StopAutoRun();
        if (screenGlitch != null) screenGlitch.StopEffect();
        if (glitch != null) glitch.SetIntensity(0f);
        MovePlayer(villageArrivalPoint);
        SetBackground(villageBackground);
        SetBlackSpaceDecor(false);
        PlayVillageBgm();
        if (cam != null) cam.Shake(0.3f, arrivalShake);
    }

    /// <summary>[신호] 당황해서 좌우를 두리번거린 뒤 마을 쪽(오른쪽)을 본다.</summary>
    public void LookAround()
    {
        StartCoroutine(LookAroundRoutine(lookCount, lookInterval, 1));
    }

    /// <summary>[신호] 도착 혼잣말. 대사가 끝날 때까지 Timeline 을 멈춘다.</summary>
    public void PlayArrivalMonologue() => PlayDialogueHolding(arrivalMonologue);

    /// <summary>[신호] 카메라가 메크로와 추억의 무언가 사이를 비춘다.</summary>
    public void FocusMemoryObject()
    {
        if (memoryObject == null) return;
        FocusCameraOn(Vector3.Lerp(player.transform.position, memoryObject.position, focusBlend));
    }

    /// <summary>[신호] 추억을 보고 고향이라 확신하는 혼잣말. 대사가 끝날 때까지 Timeline 을 멈춘다.</summary>
    public void PlayMemoryMonologue() => PlayDialogueHolding(memoryMonologue);

    /// <summary>[신호] 마을 안쪽으로 몇 걸음 걸어 들어간다.</summary>
    public void WalkIntoVillage()
    {
        if (walkInDistance > 0f) player.WalkToX(player.transform.position.x + walkInDistance);
    }

    /// <summary>[신호] 컷신 끝: 조작을 돌려준다.</summary>
    public void EndCutscene()
    {
        ReleaseCamera();
        UnlockControl();
    }

    void StopAutoRun()
    {
        if (!running) return;
        running = false;
        if (player == null) return; // 씬 종료 중이면 이미 사라졌을 수 있음
        player.moveSpeed = originalMoveSpeed;
        player.ResetMotion();
    }

    protected override void OnDisable()
    {
        StopAutoRun();
        if (openingCanvas != null) Destroy(openingCanvas.gameObject);
        if (screenGlitch != null) screenGlitch.StopEffect();
        base.OnDisable();
    }
}
