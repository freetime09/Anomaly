using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// DialogueData(대사 묶음)를 순서대로 재생하는 진행자.
///  - 대사 한 줄마다: 화자(DialogueSpeaker)를 Id 로 찾아 → 그 머리 위 말풍선에 한 글자씩 출력 → 잠깐 유지 → 숨김.
///  - 버튼 입력 없이 시간에 따라 자동으로 넘어간다 (걸으면서도 대화가 자연스럽게 흘러가도록).
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
            yield return new WaitForSeconds(hold);

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
