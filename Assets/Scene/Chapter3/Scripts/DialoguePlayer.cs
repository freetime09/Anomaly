using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// DialogueData(대사 묶음)를 순서대로 재생하는 진행자.
///  - 대사 한 줄마다: 화자(DialogueSpeaker)를 Id 로 찾아 → 그 머리 위 말풍선에 한 글자씩 출력 → 잠깐 유지 → 숨김.
///  - 버튼 입력 없이 시간에 따라 자동으로 넘어간다 (걸으면서도 대화가 자연스럽게 흘러가도록).
///  - 조작이 잠긴 대화(NPC·컷신)에서는 마우스 왼쪽 클릭으로 문장 단위 스킵 가능 (출력 완성 → 다음 문장).
///  - 필요하면 대화 동안 조작을 잠근다 (CutsceneLock).
///  - 씬에 직접 두지 않아도 Ensure() 로 자동 생성된다. 직접 두면 Inspector 값으로 속도를 조절할 수 있다.
/// </summary>
[DisallowMultipleComponent]
public class DialoguePlayer : MonoBehaviour
{
    public static DialoguePlayer Instance { get; private set; }

    [Header("출력 속도")]
    [Tooltip("초당 출력되는 글자 수 (타자기 효과 속도)")]
    [Min(1f)] public float charsPerSecond = 18f;

    [Header("자동 유지 시간 (대사의 Hold Time 이 0일 때)")]
    [Tooltip("다 출력된 뒤 기본으로 유지하는 시간(초)")]
    [Min(0f)] public float autoHoldBase = 0.8f;
    [Tooltip("글자 하나당 추가로 유지하는 시간(초). 긴 대사일수록 오래 보이게")]
    [Min(0f)] public float autoHoldPerChar = 0.05f;

    [Header("줄 사이")]
    [Tooltip("말풍선이 사라지고 다음 대사가 뜨기까지의 간격(초)")]
    [Min(0f)] public float gapBetweenLines = 0.2f;

    [Header("클릭 스킵")]
    [Tooltip("켜면 대화 중 마우스 왼쪽 클릭으로 문장을 넘긴다: 출력 중이면 그 문장을 한 번에 다 보여 주고, 다 보인 상태면 다음 문장으로 넘어간다")]
    public bool allowClickSkip = true;
    [Tooltip("켜면 조작이 잠긴 대화(NPC 대화·컷신 대사)에서만 클릭 스킵이 된다. " +
             "걸으면서 하는 독백 중에는 왼쪽 클릭이 제스처 그리기라서, 끄면 그리기 시작할 때마다 대사가 넘어가 버린다")]
    public bool skipOnlyWhileLocked = true;

    // 재생을 기다리는 대화 한 건
    struct Pending
    {
        public DialogueData data;
        public bool lockControl;
        public Action onFinished;
    }

    Coroutine running;
    DialogueSpeaker currentSpeaker;
    bool lockedControl;
    bool holding;      // 문장이 다 출력되고 유지 시간을 기다리는 중
    bool skipHold;     // 유지 시간을 건너뛰라는 클릭이 들어옴
    readonly System.Collections.Generic.Queue<Pending> queue = new System.Collections.Generic.Queue<Pending>();

    /// <summary>대화가 재생 중이거나 기다리는 대화가 남아 있는가?</summary>
    public bool IsPlaying => running != null || queue.Count > 0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (lockedControl) CutsceneLock.Unlock(this);
    }

    // 클릭 입력은 Update 에서 받는다. 코루틴(Run)은 모든 Update 다음에 실행되므로
    // 이번 프레임의 클릭이 같은 프레임의 Run 에 바로 반영된다.
    void Update()
    {
        if (!allowClickSkip || running == null || currentSpeaker == null) return;
        if (!Input.GetMouseButtonDown(0)) return;
        if (PauseMenu.IsPaused || PauseMenu.IsPointerOnSettingsButton(Input.mousePosition)) return; // 설정 창·톱니 클릭은 제외
        if (skipOnlyWhileLocked && !CutsceneLock.IsLocked) return;
        SkipLine();
    }

    /// <summary>현재 문장을 한 단계 넘긴다: 출력 중이면 '전부 보이기', 다 보인 상태면 '다음 문장'.
    /// 클릭 외의 입력(터치 버튼 등)에서도 부를 수 있게 public 으로 둔다.</summary>
    public void SkipLine()
    {
        if (running == null || currentSpeaker == null) return;
        if (currentSpeaker.Bubble.IsTyping) currentSpeaker.Bubble.CompleteTyping();
        else if (holding) skipHold = true;
    }

    /// <summary>씬에 DialoguePlayer 가 없으면 만들어서 반환한다.</summary>
    public static DialoguePlayer Ensure()
    {
        if (Instance == null) Instance = new GameObject("DialoguePlayer").AddComponent<DialoguePlayer>();
        return Instance;
    }

    /// <summary>대화를 바로 재생한다. 이미 재생 중인 대화가 있으면 끊고 새로 시작한다 (컷신용).
    /// 기다리던 대화들은 이 대화가 끝난 뒤 이어서 재생된다.</summary>
    /// <param name="lockControl">true 면 대화 동안 플레이어 조작을 잠근다</param>
    /// <param name="onFinished">끝까지 재생되면 호출 (Stop 으로 끊기면 호출 안 됨)</param>
    public void Play(DialogueData data, bool lockControl = false, Action onFinished = null)
    {
        if (data == null || data.lines.Count == 0) { Debug.LogWarning("[DialoguePlayer] 재생할 대사가 없음"); return; }
        StopCurrent();
        running = StartCoroutine(Run(data, lockControl, onFinished));
    }

    /// <summary>재생 중인 대화가 없으면 바로 재생하고, 있으면 대기열 끝에 넣어 차례가 오면 재생한다.
    /// 걸어가며 여러 대화 구역을 연달아 지나가도 대사가 건너뛰어지지 않게 하려고 쓴다 (DialogueTrigger 용).</summary>
    public void PlayOrQueue(DialogueData data, bool lockControl = false, Action onFinished = null)
    {
        if (data == null || data.lines.Count == 0) { Debug.LogWarning("[DialoguePlayer] 재생할 대사가 없음"); return; }
        if (IsPlaying) queue.Enqueue(new Pending { data = data, lockControl = lockControl, onFinished = onFinished });
        else Play(data, lockControl, onFinished);
    }

    /// <summary>UnityEvent·Timeline Signal 에서 연결하기 쉬운 버전 (조작 잠금 없음).</summary>
    public void PlayFromEvent(DialogueData data) => Play(data);

    /// <summary>재생 중인 대화와 기다리던 대화를 모두 멈추고 말풍선을 숨긴다.</summary>
    public void Stop()
    {
        queue.Clear();
        StopCurrent();
    }

    /// <summary>지금 재생 중인 대화만 멈춘다 (대기열은 유지).</summary>
    void StopCurrent()
    {
        if (running != null) { StopCoroutine(running); running = null; }
        holding = false;
        skipHold = false;
        if (currentSpeaker != null) { currentSpeaker.Bubble.HideImmediate(); currentSpeaker = null; }
        if (lockedControl) { CutsceneLock.Unlock(this); lockedControl = false; }
    }

    /// <summary>코루틴으로 기다릴 수 있는 버전 — 컷신에서 '대화가 끝날 때까지 대기'할 때 사용.</summary>
    public IEnumerator PlayAndWait(DialogueData data, bool lockControl = false)
    {
        bool done = false;
        Play(data, lockControl, () => done = true);
        while (!done && IsPlaying) yield return null;
    }

    /// <summary>다 출력된 문장을 seconds 동안 유지한다. 그 사이 클릭(skipHold)이 들어오면 바로 끝낸다.
    /// WaitForSeconds 는 중간에 끊을 수 없어서 매 프레임 시간을 세는 방식으로 기다린다.</summary>
    IEnumerator Hold(float seconds)
    {
        holding = true;
        skipHold = false;
        float t = 0f;
        while (t < seconds && !skipHold)
        {
            t += Time.deltaTime;
            yield return null;
        }
        holding = false;
        skipHold = false;
    }

    IEnumerator Run(DialogueData data, bool lockControl, Action onFinished)
    {
        if (lockControl) { CutsceneLock.Lock(this); lockedControl = true; }

        for (int i = 0; i < data.lines.Count; i++)
        {
            var line = data.lines[i];
            var speaker = DialogueSpeaker.Find(line.speakerId);
            if (speaker == null)
            {
                Debug.LogWarning($"[DialoguePlayer] 화자 '{line.speakerId}' 를 씬에서 찾을 수 없어 이 줄을 건너뜀: {line.text}");
                continue;
            }

            currentSpeaker = speaker;
            yield return speaker.Bubble.Say(line.text, charsPerSecond);

            float hold = line.holdTime > 0f ? line.holdTime : autoHoldBase + speaker.Bubble.LastCharacterCount * autoHoldPerChar;
            yield return Hold(hold);

            speaker.Bubble.Hide();
            currentSpeaker = null;
            if (i < data.lines.Count - 1 && gapBetweenLines > 0f) yield return new WaitForSeconds(gapBetweenLines);
        }

        if (lockedControl) { CutsceneLock.Unlock(this); lockedControl = false; }
        running = null;
        onFinished?.Invoke();

        // 기다리던 대화가 있으면 이어서 재생 (onFinished 안에서 새 대화가 시작됐으면 그게 끝난 뒤로 미룸)
        if (running == null && queue.Count > 0)
        {
            var next = queue.Dequeue();
            Play(next.data, next.lockControl, next.onFinished);
        }
    }
}
