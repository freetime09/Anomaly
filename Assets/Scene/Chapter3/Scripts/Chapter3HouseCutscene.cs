using System.Collections;
using UnityEngine;

/// <summary>
/// 챕터3 파트 C 컷신: 메크로의 집 안.
///  집에 들어와 가족을 부름 → 두리번 → 카메라가 '자신과 관련된 것이 사라진 자리'들을 차례로 비춤 (가족사진·식탁·방)
///  → 자신만 '삭제'된 가짜 마을임을 깨달음 → 분노 (화면 지지직 + 흔들림) → 문 쪽으로 몸을 돌려 마을을 되돌리기로 결심.
///
/// 기획의 컷신 3개(둘러보기 / 깨달음 / 돌아서기)를 Timeline 하나에 담았다.
/// 동작 함수는 '무엇을'(대사·초점 대상)을 매개변수로 받는 범용 함수라서, 새 장면을 넣을 때 코드를 고칠 필요 없이
/// Timeline 에 신호를 추가하고 Signal Receiver 에서 대상(DialogueData·Transform)만 지정하면 된다.
/// 컷신을 줄이고 싶으면 해당 신호를 지우면 된다.
/// </summary>
public class Chapter3HouseCutscene : Chapter3CutsceneBase
{
    [Header("집 안")]
    [Tooltip("집 안에서의 카메라 배경색 (벽 바깥으로 보이는 부분)")]
    public Color interiorBackground = new Color(0.16f, 0.12f, 0.1f);

    [Header("두리번")]
    [Tooltip("좌우를 번갈아 보는 횟수")]
    [Min(0)] public int lookCount = 2;
    [Tooltip("한 번 고개를 돌리고 다음까지의 간격(초)")]
    [Min(0.05f)] public float lookInterval = 0.5f;

    [Header("분노 연출")]
    [Tooltip("배경 깨짐 연출 컴포넌트 (비우면 자동으로 찾음). 분노하는 순간 화면이 잠깐 지지직거린다")]
    public GlitchBreakEffect glitch;
    public HouseScreenGlitch screenGlitch;
    [Tooltip("분노 순간 깨짐 세기 (0~1)")]
    [Range(0f, 1f)] public float angerGlitch = 0.45f;
    [Tooltip("깨짐이 다시 0으로 잦아드는 시간(초)")]
    [Min(0.05f)] public float angerFadeTime = 1.2f;
    [Tooltip("분노 순간 화면 흔들림 크기 (유닛)")]
    [Min(0f)] public float angerShake = 0.35f;

    [Header("돌아서기")]
    [Tooltip("마지막에 바라볼 방향 (-1 = 왼쪽 = 들어온 문 쪽, +1 = 오른쪽)")]
    public int turnDirection = -1;

    [Header("끝")]
    [Tooltip("컷신이 끝나면 켤 오브젝트 (예: 4챕터로 나가는 문). 컷신 동안 O 프롬프트가 뜨지 않게 꺼 둔다")]
    public GameObject activateOnEnd;

    protected override void Awake()
    {
        base.Awake();
        if (glitch == null) glitch = FindAnyObjectByType<GlitchBreakEffect>();
        if (screenGlitch == null) screenGlitch = GetComponent<HouseScreenGlitch>();
    }

    // ───────── Timeline 신호가 부르는 동작들 ─────────

    /// <summary>[신호] 컷신 시작: 조작 잠금, 집 안 배경색.</summary>
    public void BeginCutscene()
    {
        StopHouseGlitch();
        LockControl();
        SetBackground(interiorBackground);
        player.Face(1);
    }

    /// <summary>[신호] 대사를 재생하고 끝날 때까지 Timeline 을 멈춘다. (Signal Receiver 에서 대사 에셋 지정)</summary>
    public void PlayDialogue(DialogueData data) => PlayDialogueHolding(data);

    /// <summary>[신호] 카메라가 target 을 비춘다. (Signal Receiver 에서 대상 지정)</summary>
    public void FocusOn(Transform target)
    {
        if (target == null) return;
        FocusCameraOn(target.position);
    }

    /// <summary>[신호] 메크로가 target 의 x 위치까지 걸어간다. (Signal Receiver 에서 목표 지점 지정)
    /// 물건을 비출 때 메크로도 화면 안에 있어야 말풍선이 보이므로, 초점을 옮기기 전에 먼저 걸어가게 한다.</summary>
    public void WalkTo(Transform target)
    {
        if (target == null) return;
        player.WalkToX(target.position.x);
    }

    /// <summary>[신호] 좌우를 두리번거린 뒤 집 안쪽(오른쪽)을 본다.</summary>
    public void LookAround()
    {
        StartCoroutine(LookAroundRoutine(lookCount, lookInterval, 1));
    }

    /// <summary>[신호] 분노: 화면이 잠깐 지지직거리고 흔들린다.</summary>
    public void AngerBurst()
    {
        if (cam != null) cam.Shake(0.5f, angerShake);
        if (glitch != null) glitch.SetIntensity(0f);
        if (screenGlitch != null) screenGlitch.PulseOnce(angerGlitch * 0.75f);
    }

    public void StopHouseGlitch()
    {
        if (screenGlitch != null) screenGlitch.StopEffect();
        if (glitch != null) glitch.SetIntensity(0f);
    }

    protected override void OnDisable()
    {
        StopHouseGlitch();
        base.OnDisable();
    }

    /// <summary>[신호] 문 쪽으로 몸을 돌린다 (마을을 되돌리러 나서는 순간).</summary>
    public void TurnAround()
    {
        player.Face(turnDirection);
    }

    /// <summary>[신호] 컷신 끝: 카메라를 메크로에게 돌려주고 조작을 푼다.</summary>
    public void EndCutscene()
    {
        ReleaseCamera();
        UnlockControl();
        if (activateOnEnd != null) activateOnEnd.SetActive(true);
        if (glitch != null) glitch.SetIntensity(0f);
        if (screenGlitch != null) screenGlitch.BeginRepeating();
    }
}
