using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어가 kill-Y 아래로 떨어지면 화면을 검게 페이드한 뒤 시작 위치로 되살린다.
/// 페이드용 캔버스/이미지는 런타임에 자체 생성하므로 씬 세팅이 필요 없다(패키지로 옮겨도 그대로 동작).
/// </summary>
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(Rigidbody2D))]
public class FallRespawn : MonoBehaviour
{
    [Header("죽음 판정")]
    [Tooltip("이 레이어의 (트리거) 콜라이더에 닿으면 죽고 리스폰. 예: DeathZone 레이어.")]
    public LayerMask deathLayers;

    [Header("죽음 애니메이션")]
    [Tooltip("죽는 순간 죽음 애니(메크로Death)를 보여준 뒤 페이드를 시작하기까지의 시간(초). 클립 길이 ≈ 0.5")]
    public float deathAnimTime = 0.55f;

    [Header("페이드")]
    [Tooltip("검게 어두워지는 시간(초)")]
    public float fadeOutTime = 0.3f;
    [Tooltip("완전히 검은 상태로 멈춰 있는 시간(초)")]
    public float blackHoldTime = 0.15f;
    [Tooltip("다시 밝아지는 시간(초)")]
    public float fadeInTime = 0.3f;
    [Tooltip("페이드 색 (기본 검정)")]
    public Color fadeColor = Color.black;

    [Header("소리")]
    [Tooltip("부활할 때 (다시 밝아지기 시작할 때) 소리")]
    public AudioClip respawnClip;
    [Range(0f, 1f)] public float respawnVolume = 1f;

    [Header("배경음")]
    [Tooltip("죽으면 배경음(카메라의 AudioSource)이 꺼졌다가 부활하며 멈춘 곳부터 다시 커짐")]
    public bool fadeBgmOnDeath = true;
    [Tooltip("배경음이 꺼지는 시간 (초). 다시 켜질 때는 화면이 밝아지는 시간과 같이")]
    public float bgmFadeTime = 0.4f;

    PlayerController player;
    Rigidbody2D rb;
    GestureRecognizer input;
    CameraFollow cam;

    Vector3 currentSpawnPos;         // 현재 부활 지점 (시작 위치 또는 마지막으로 밟은 체크포인트)
    Quaternion spawnRot;             // 부활 시 자세 (시작 자세로 세움)
    int currentOrder = int.MinValue; // 마지막으로 활성화된 체크포인트 순서 (시작 지점 = -무한대)
    bool respawning;

    CanvasGroup fadeGroup; // 알파로 페이드

    AudioSource bgm;          // 끈 배경음
    float bgmVolume = -1f;    // 원래 음량
    bool bgmPaused;           // 여기서 일시정지했는가 (다시 켤 때만 UnPause)
    Coroutine bgmFade;

    readonly PuzzleSnapshot puzzles = new PuzzleSnapshot(); // 죽으면 되돌릴 퍼즐 상태 (시작/체크포인트 때 저장)

    /// <summary>죽는 순간 (백신 벽 등이 멈추는 데 사용)</summary>
    public event System.Action Died;
    /// <summary>부활 지점으로 옮겨진 순간 (화면이 검은 동안). 인자 = 부활 위치</summary>
    public event System.Action<Vector3> Respawned;

    void Awake()
    {
        player = GetComponent<PlayerController>();
        rb = GetComponent<Rigidbody2D>();
        input = GetComponent<GestureRecognizer>();
        cam = FindObjectOfType<CameraFollow>();
        BuildFadeOverlay();
    }

    void Start()
    {
        // 시작 위치를 기본 리스폰 지점으로 저장 (체크포인트를 밟기 전까지 여기로 부활)
        currentSpawnPos = transform.position;
        spawnRot = transform.rotation;
        StartCoroutine(SaveInitialPuzzles());
    }

    // 다른 오브젝트들의 Start 까지 끝난 뒤 시작 상태를 저장
    IEnumerator SaveInitialPuzzles()
    {
        yield return null;
        if (currentOrder == int.MinValue) puzzles.Save();
    }

    void OnTriggerEnter2D(Collider2D other) { CheckDeath(other.gameObject); }
    void OnCollisionEnter2D(Collision2D c)   { CheckDeath(c.gameObject); }

    void CheckDeath(GameObject other)
    {
        if ((deathLayers.value & (1 << other.layer)) != 0)
            Die();
    }

    /// <summary>죽음 진입점. 죽는 애니메이션 등은 여기(또는 RespawnRoutine 앞)에 연결하면 된다.</summary>
    public void Die()
    {
        if (respawning) return;
        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        respawning = true;
        if (Died != null) Died();
        if (fadeBgmOnDeath) StartBgmFade(0f, bgmFadeTime); // 배경음 꺼짐

        // 조작만 잠금 — 물리/관성은 그대로 두어 죽는 동안에도 관성대로 계속 움직인다.
        if (input != null) input.enabled = false;

        player.PlayDeath();                          // 죽음 애니(메크로Death) 재생 시작
        if (deathAnimTime > 0f) yield return new WaitForSeconds(deathAnimTime); // 죽는 모습 보여주기

        yield return Fade(0f, 1f, fadeOutTime); // 검게

        // 부활 지점(시작 위치 또는 마지막 체크포인트)으로 복귀 — 여기서 비로소 멈춰서 시작
        transform.position = currentSpawnPos;
        rb.position = currentSpawnPos;      // interpolate 대비 rb 위치도 같이
        transform.rotation = spawnRot;
        player.ResetMotion();               // 부활 순간 속도 0으로
        player.EndDeath();                  // 죽음 상태 해제 → 기본 애니로 복귀
        if (player.branch != null) player.branch.Recover(); // 부활 시 나뭇가지를 손으로 회수(들고 있게)
        if (cam != null) cam.SnapToTarget(); // 카메라도 즉시 플레이어로 스냅
        puzzles.Restore();                  // 퍼즐을 마지막 체크포인트(없으면 시작) 때 상태로
        if (Respawned != null) Respawned(currentSpawnPos);

        if (blackHoldTime > 0f) yield return new WaitForSeconds(blackHoldTime);

        if (respawnClip != null) AudioSource.PlayClipAtPoint(respawnClip, Camera.main != null ? Camera.main.transform.position : transform.position, respawnVolume); // 다시 태어나는 소리
        StartBgmFade(1f, fadeInTime); // 배경음 다시
        yield return Fade(1f, 0f, fadeInTime); // 다시 밝게

        if (input != null) input.enabled = true;
        respawning = false;
    }

    bool teleporting;

    /// <summary>텔레포터가 호출. 화면을 페이드아웃한 뒤 지정 위치로 옮기고 페이드인한다(관성 리셋).</summary>
    public void TeleportTo(Vector3 pos)
    {
        if (respawning || teleporting) return;
        StartCoroutine(TeleportRoutine(pos));
    }

    IEnumerator TeleportRoutine(Vector3 pos)
    {
        teleporting = true;
        if (input != null) input.enabled = false;   // 이동 중 조작 잠금

        yield return Fade(0f, 1f, fadeOutTime);      // 검게

        transform.position = pos;
        rb.position = pos;                           // interpolate 대비
        player.ResetMotion();                        // 도착 순간 속도 0
        if (cam != null) cam.SnapToTarget();         // 카메라 즉시 스냅

        if (blackHoldTime > 0f) yield return new WaitForSeconds(blackHoldTime);

        yield return Fade(1f, 0f, fadeInTime);       // 다시 밝게

        if (input != null) input.enabled = true;
        teleporting = false;
    }

    /// <summary>텔레포터가 호출. 검은 페이드 대신 소용돌이 전환을 쓴다. 절정(peak)에서 위치를 옮기고
    /// 관성/카메라를 리셋한 뒤 소용돌이가 풀리며 도착지가 드러난다. 소용돌이가 없으면 즉시 이동.</summary>
    public void TeleportVortex(Vector3 pos)
    {
        if (respawning || teleporting) return;
        StartCoroutine(TeleportVortexRoutine(pos));
    }

    IEnumerator TeleportVortexRoutine(Vector3 pos)
    {
        teleporting = true;
        if (input != null) input.enabled = false;   // 전환 중 조작 잠금

        var vt = VortexTransition.Instance;
        if (vt != null)
        {
            // 소용돌이가 완전히 감긴 순간(peak)에 순간이동 → 이동 장면이 가려진다
            vt.Play(() =>
            {
                transform.position = pos;
                rb.position = pos;                   // interpolate 대비
                player.ResetMotion();                // 도착 순간 속도 0
                if (cam != null) cam.SnapToTarget(); // 카메라 즉시 스냅
            });
            while (vt.IsPlaying) yield return null;  // 소용돌이가 다 풀릴 때까지 대기
        }
        else
        {
            // 폴백: 소용돌이 컴포넌트가 없으면 그냥 즉시 이동
            transform.position = pos;
            rb.position = pos;
            player.ResetMotion();
            if (cam != null) cam.SnapToTarget();
        }

        if (input != null) input.enabled = true;
        teleporting = false;
    }

    /// <summary>체크포인트가 호출. 순서(order)가 현재보다 뒤일 때만 부활 지점을 이 위치로 갱신(앞으로만).
    /// 갱신되면 true 를 반환 → 체크포인트가 자기 표식을 활성색으로 바꾼다.</summary>
    public bool TrySetCheckpoint(int order, Vector3 pos)
    {
        if (order <= currentOrder) return false;
        currentOrder = order;
        currentSpawnPos = pos;
        puzzles.Save(); // 이 순간의 퍼즐 상태를 부활 때 되돌릴 기준으로
        return true;
    }

    IEnumerator Fade(float from, float to, float dur)
    {
        if (fadeGroup == null) yield break;
        if (dur <= 0f) { fadeGroup.alpha = to; yield break; }
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            fadeGroup.alpha = Mathf.Lerp(from, to, t / dur);
            yield return null;
        }
        fadeGroup.alpha = to;
    }

    void StartBgmFade(float to, float time)
    {
        if (to <= 0f)
        {
            var c = Camera.main;
            bgm = c != null ? c.GetComponent<AudioSource>() : null;
            if (bgm == null || !bgm.isPlaying) { bgm = null; return; }
            if (bgmVolume < 0f) bgmVolume = bgm.volume;
        }
        else if (bgm == null) return; // 꺼 둔 게 없음
        if (bgmFade != null) StopCoroutine(bgmFade);
        bgmFade = StartCoroutine(FadeBgm(to, time));
    }

    /// <summary>배경음을 to(0 = 꺼짐, 1 = 원래 음량)까지 서서히. 0 에 닿으면 일시정지, 다시 켤 땐 멈춘 곳부터.</summary>
    IEnumerator FadeBgm(float to, float time)
    {
        if (to > 0f && bgmPaused) { bgm.UnPause(); bgmPaused = false; }
        float from = bgmVolume > 0f ? bgm.volume / bgmVolume : 0f;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            if (bgm == null) yield break;
            bgm.volume = bgmVolume * Mathf.Lerp(from, to, t / time);
            yield return null;
        }
        if (bgm != null)
        {
            bgm.volume = bgmVolume * to;
            if (to <= 0f) { bgm.Pause(); bgmPaused = true; }
            else bgm = null;
        }
        bgmFade = null;
    }

    /// <summary>페이드용 풀스크린 검은 오버레이(캔버스+이미지)를 코드로 생성한다.</summary>
    void BuildFadeOverlay()
    {
        var canvasGO = new GameObject("FadeCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // 항상 최상단

        fadeGroup = canvasGO.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.interactable = false;
        fadeGroup.blocksRaycasts = false; // 입력을 가로채지 않음

        var imgGO = new GameObject("Black");
        imgGO.transform.SetParent(canvasGO.transform, false);
        var img = imgGO.AddComponent<Image>();
        img.color = fadeColor;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
