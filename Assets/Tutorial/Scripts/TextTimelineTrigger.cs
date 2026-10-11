using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// 특정 오브젝트(트리거)를 지나면, 미리 배치해둔 여러 텍스트 오브젝트와
/// 제스처 라인들이 각자 정해진 시각(startTime)에 맞춰 나타났다 사라집니다.
///
/// 텍스트 트랙과 라인 트랙은 서로 독립적으로 동시에 진행되며(멀티트랙 타임라인),
/// 같은 트랙 안의 항목들도 시간이 겹치면 동시에 표시될 수 있습니다.
///
/// - 텍스트: 각 항목마다 "이미 배치해둔" 텍스트 오브젝트를 하나씩 연결 (좌표 이동 없음, 그 자리에서 페이드).
/// - 라인: 각 항목마다 Scene 뷰에서 그려둔 LineRenderer를 연결. Catmull-Rom으로 부드럽게 이어 그리고,
///         코어+글로우 이중 라인을 자동 생성. 라인별로 반복 재생(loop)도 개별 설정 가능.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TimelineTrigger : MonoBehaviour
{
    [System.Serializable]
    public class TimelineTextEntry
    {
        [Tooltip("트리거가 발동된 시점(0초) 기준, 이 텍스트가 나타나는 시각(초)")]
        public float startTime = 0f;
        [Tooltip("Scene 뷰에 미리 배치해둔 텍스트 오브젝트 (항목마다 다른 오브젝트를 연결하세요)")]
        public TextMeshPro textObject;
        [TextArea]
        public string message = "";
        public float fadeInDuration = 0.3f;
        public float holdDuration = 1.5f;
        public float fadeOutDuration = 0.5f;

        [System.NonSerialized] public Coroutine routineCoroutine;
    }

    [System.Serializable]
    public class TimelineLineEntry
    {
        [Tooltip("트리거가 발동된 시점(0초) 기준, 이 라인이 그려지기 시작하는 시각(초)")]
        public float startTime = 0f;

        [Header("참조")]
        [Tooltip("Scene 뷰에서 모양(점들)을 미리 찍어둔 LineRenderer")]
        public LineRenderer lineRenderer;

        [Header("외형")]
        public Color color = Color.white;
        public float lineWidth = 0.12f;
        public float glowWidthMultiplier = 2.8f;
        [Range(0f, 1f)]
        public float glowAlpha = 0.35f;
        public int smoothSegments = 16;

        [Header("타이밍")]
        public float fadeInDuration = 0.25f;
        public float fadeOutDuration = 0.5f;
        public float drawDuration = 1.0f;
        public float holdDuration = 0.6f;

        [Header("반복 재생")]
        public bool loopWhileInside = false;
        public float loopInterval = 1.0f;

        [System.NonSerialized] public LineRenderer glowLine;
        [System.NonSerialized] public List<Vector3> smoothPoints;
        [System.NonSerialized] public float[] segmentLengths;
        [System.NonSerialized] public float totalLength;
        [System.NonSerialized] public Coroutine routineCoroutine;
    }

    [System.Serializable]
    public class TimelineHideEntry
    {
        [Tooltip("트리거가 발동된 시점(0초) 기준, 이 텍스트들이 사라지기 시작하는 시각(초)")]
        public float startTime = 0f;
        [Tooltip("이 시각에 페이드 아웃될 텍스트들 (다른 트리거가 이미 보여준 텍스트도 참조 가능)")]
        public TextMeshPro[] textsToHide;
        public float fadeOutDuration = 0.4f;

        [Header("전부 다 사라진 직후 자동 실행 (선택, 시간 계산 없이 완료 시점에 바로 연결됨)")]
        [Tooltip("텍스트가 다 사라지면 활성화할 오브젝트 (문 열기, 이펙트 등 아무 오브젝트나 가능)")]
        public GameObject objectToActivateAfterHide;
        [Tooltip("텍스트가 다 사라지면 새로 등장할 텍스트 (0초 = 다 사라진 시점)")]
        public TimelineTextEntry[] newTextsAfterHide;
        [Tooltip("텍스트가 다 사라지면 새로 그려질 라인 (0초 = 다 사라진 시점)")]
        public TimelineLineEntry[] newLinesAfterHide;

        [System.NonSerialized] public bool isSetUp;
    }

    [System.Serializable]
    public class TimelineEventEntry
    {
        [Tooltip("외부 스크립트가 TriggerEvent(\"이 이름\")으로 호출하면 발동됩니다. 예: BranchThrown")]
        public string eventId = "BranchThrown";

        [Header("이 이벤트가 발생하면 사라질 텍스트")]
        public TextMeshPro[] textsToHide;
        public float textFadeOutDuration = 0.4f;

        [Header("이 이벤트가 발생하면 사라질 라인 (이미 그려진 LineRenderer)")]
        public LineRenderer[] linesToHide;
        public float lineFadeOutDuration = 0.4f;

        [Header("이 이벤트가 발생하면 새로 등장할 텍스트 (0초 = 이벤트 발생 시점)")]
        public TimelineTextEntry[] newTexts;

        [Header("이 이벤트가 발생하면 새로 그려질 라인 (0초 = 이벤트 발생 시점)")]
        public TimelineLineEntry[] newLines;

        [Tooltip("체크하면 여러 번 다시 발동될 수 있음 (기본은 한 번만)")]
        public bool repeatable = false;

        [System.NonSerialized] public bool hasFired;
        [System.NonSerialized] public bool isSetUp;
    }

    [System.Serializable]
    public class TimelineCheckpointEntry
    {
        [Tooltip("TimelineCheckpointRelay가 이 이름으로 찾아서 발동시킵니다. 서로 겹치지 않는 이름으로 지어주세요")]
        public string id = "Checkpoint1";

        [Header("이 체크포인트에서 사라질 텍스트")]
        public TextMeshPro[] textsToHide;
        public float hideFadeOutDuration = 0.4f;

        [Header("이 체크포인트에서 사라질 라인 (이미 그려진 LineRenderer)")]
        [Tooltip("Line Timeline이나 다른 체크포인트가 이미 그려둔 라인을 여기서 페이드 아웃시킬 수 있습니다")]
        public LineRenderer[] linesToHide;
        public float lineHideFadeOutDuration = 0.4f;

        [Header("이 체크포인트에서 새로 등장할 텍스트 (0초 = 체크포인트 발동 시점)")]
        public TimelineTextEntry[] newTexts;

        [Header("이 체크포인트에서 새로 그려질 라인 (0초 = 체크포인트 발동 시점)")]
        public TimelineLineEntry[] newLines;

        [Tooltip("체크하면 여러 번 다시 발동될 수 있음 (기본은 한 번만)")]
        public bool repeatable = false;

        [System.NonSerialized] public bool hasFired;
        [System.NonSerialized] public bool isSetUp;
    }

    [Header("텍스트 트랙 (여러 오브젝트, 각자 정해진 시각에 등장)")]
    [SerializeField] private TimelineTextEntry[] textTimeline;

    [Header("텍스트 숨김 트랙 (정해진 시각에 지정 텍스트들을 페이드 아웃)")]
    [SerializeField] private TimelineHideEntry[] hideTimeline;

    [Header("제스처 라인 트랙 (여러 라인, 각자 정해진 시각에 그려짐)")]
    [SerializeField] private TimelineLineEntry[] lineTimeline;

    [Header("체크포인트 (별도 오브젝트에 플레이어가 들어가면 발동 — TimelineCheckpointRelay와 짝을 이룹니다)")]
    [SerializeField] private TimelineCheckpointEntry[] checkpoints;

    [Header("외부 이벤트 숨김 (다른 스크립트가 TriggerEvent()를 호출하면 발동, 예: 나뭇가지 던지기)")]
    [SerializeField] private TimelineEventEntry[] eventHides;

    [Header("옵션")]
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private string playerTag = "Player";
    [Tooltip("체크하면 재생 도중 플레이어가 트리거를 벗어나도 끝까지 재생합니다")]
    [SerializeField] private bool playToEndEvenIfPlayerLeaves = true;

    [Header("사운드 (선택, 각 텍스트/라인이 나타날 때마다 재생)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip revealSound;

    private bool hasTriggered = false;
    private bool playerInside = false;
    private Coroutine textMasterRoutine;
    private Coroutine lineMasterRoutine;
    private Coroutine hideMasterRoutine;

    /// <summary>코어 LineRenderer → 자동 생성된 글로우 LineRenderer 매핑. 라인을 나중에 참조로만 숨길 때 필요.</summary>
    private readonly Dictionary<LineRenderer, LineRenderer> glowLookup = new Dictionary<LineRenderer, LineRenderer>();

    /// <summary>코어 LineRenderer → 그 라인을 담당하는 TimelineLineEntry 매핑. 반복 재생 코루틴을 멈추기 위해 필요.</summary>
    private readonly Dictionary<LineRenderer, TimelineLineEntry> entryLookup = new Dictionary<LineRenderer, TimelineLineEntry>();

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    /// <summary>텍스트 배열의 알파를 0으로 초기화하고 시간순 정렬. textTimeline과 각종 하위 배열에서 공용으로 사용.</summary>
    private void PrepareTextArray(TimelineTextEntry[] arr)
    {
        if (arr == null) return;
        foreach (var e in arr)
        {
            if (e != null && e.textObject != null) e.textObject.alpha = 0f;
        }
        System.Array.Sort(arr, (a, b) => a.startTime.CompareTo(b.startTime));
    }

    /// <summary>라인 배열을 세팅(곡선/글로우 생성)하고 시간순 정렬. lineTimeline과 각종 하위 배열에서 공용으로 사용.</summary>
    private void PrepareLineArray(TimelineLineEntry[] arr)
    {
        if (arr == null) return;
        foreach (var e in arr)
        {
            if (e != null && e.lineRenderer != null) SetupEntry(e);
        }
        System.Array.Sort(arr, (a, b) => a.startTime.CompareTo(b.startTime));
    }

    private void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        PrepareTextArray(textTimeline);
        PrepareLineArray(lineTimeline);

        if (hideTimeline != null)
        {
            System.Array.Sort(hideTimeline, (a, b) => a.startTime.CompareTo(b.startTime));
            foreach (var e in hideTimeline)
            {
                if (e == null) continue;
                PrepareTextArray(e.newTextsAfterHide);
                PrepareLineArray(e.newLinesAfterHide);
                if (e.objectToActivateAfterHide != null) e.objectToActivateAfterHide.SetActive(false);
            }
        }

        if (checkpoints != null)
        {
            foreach (var cp in checkpoints)
            {
                if (cp == null) continue;
                PrepareCheckpoint(cp);
            }
        }

        if (eventHides != null)
        {
            foreach (var e in eventHides)
            {
                if (e == null) continue;
                PrepareEventEntry(e);
            }
        }
    }

    /// <summary>이벤트 항목 안의 새 텍스트/라인을 미리 준비합니다 (체크포인트와 동일한 방식).</summary>
    private void PrepareEventEntry(TimelineEventEntry e)
    {
        if (e.isSetUp) return;
        e.isSetUp = true;

        PrepareTextArray(e.newTexts);
        PrepareLineArray(e.newLines);
    }

    /// <summary>체크포인트 안의 텍스트 알파를 0으로, 라인은 곡선/글로우 세팅을 미리 해둡니다.</summary>
    private void PrepareCheckpoint(TimelineCheckpointEntry cp)
    {
        if (cp.isSetUp) return;
        cp.isSetUp = true;

        PrepareTextArray(cp.newTexts);
        PrepareLineArray(cp.newLines);
    }

    /// <summary>
    /// TimelineCheckpointRelay가 호출합니다. id가 일치하는 체크포인트를 찾아
    /// 지정된 텍스트를 숨기고, 새 텍스트/라인 타임라인을 0초부터 재생합니다.
    /// </summary>
    public void TriggerCheckpoint(string checkpointId)
    {
        if (checkpoints == null) return;

        foreach (var cp in checkpoints)
        {
            if (cp == null || cp.id != checkpointId) continue;
            if (cp.hasFired && !cp.repeatable) return;

            cp.hasFired = true;
            PrepareCheckpoint(cp); // 혹시 Awake 이전에 호출되는 경우를 대비한 안전장치

            if (cp.textsToHide != null)
            {
                foreach (var t in cp.textsToHide)
                {
                    if (t != null) StartCoroutine(FadeText(t, t.alpha, 0f, cp.hideFadeOutDuration));
                }
            }

            if (cp.linesToHide != null)
            {
                foreach (var lr in cp.linesToHide)
                {
                    if (lr != null) StartCoroutine(FadeOutRawLine(lr, cp.lineHideFadeOutDuration));
                }
            }

            if (cp.newTexts != null) StartCoroutine(PlayTextEntries(cp.newTexts));
            if (cp.newLines != null) StartCoroutine(PlayLineEntries(cp.newLines));

            return;
        }
    }

    /// <summary>
    /// 다른 스크립트(예: 나뭇가지 던지기)에서 이 함수를 직접 호출하면,
    /// eventId가 일치하는 항목에 등록된 텍스트/라인을 페이드 아웃합니다.
    /// 예: GetComponent<TimelineTrigger>().TriggerEvent("BranchThrown");
    /// </summary>
    public void TriggerEvent(string eventId)
    {
        if (eventHides == null) return;

        foreach (var e in eventHides)
        {
            if (e == null || e.eventId != eventId) continue;
            if (e.hasFired && !e.repeatable) return;

            e.hasFired = true;
            PrepareEventEntry(e); // Awake 이전에 호출되는 경우를 대비한 안전장치

            if (e.textsToHide != null)
            {
                foreach (var t in e.textsToHide)
                {
                    if (t != null) StartCoroutine(FadeText(t, t.alpha, 0f, e.textFadeOutDuration));
                }
            }

            if (e.linesToHide != null)
            {
                foreach (var lr in e.linesToHide)
                {
                    if (lr != null) StartCoroutine(FadeOutRawLine(lr, e.lineFadeOutDuration));
                }
            }

            if (e.newTexts != null) StartCoroutine(PlayTextEntries(e.newTexts));
            if (e.newLines != null) StartCoroutine(PlayLineEntries(e.newLines));

            return;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (triggerOnce && hasTriggered) return;

        hasTriggered = true;
        playerInside = true;

        textMasterRoutine = StartCoroutine(PlayTextTimeline());
        lineMasterRoutine = StartCoroutine(PlayLineTimeline());
        hideMasterRoutine = StartCoroutine(PlayHideTimeline());
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        playerInside = false;

        if (!playToEndEvenIfPlayerLeaves)
        {
            StopAllAndHide();
        }
    }

    private void StopAllAndHide()
    {
        if (textMasterRoutine != null) StopCoroutine(textMasterRoutine);
        if (lineMasterRoutine != null) StopCoroutine(lineMasterRoutine);
        if (hideMasterRoutine != null) StopCoroutine(hideMasterRoutine);

        if (textTimeline != null)
        {
            foreach (var e in textTimeline)
            {
                if (e == null) continue;
                if (e.routineCoroutine != null) StopCoroutine(e.routineCoroutine);
                if (e.textObject != null) e.textObject.alpha = 0f;
            }
        }

        if (hideTimeline != null)
        {
            foreach (var e in hideTimeline)
            {
                if (e == null) continue;

                if (e.textsToHide != null)
                {
                    foreach (var t in e.textsToHide)
                    {
                        if (t != null) t.alpha = 0f;
                    }
                }

                if (e.newTextsAfterHide != null)
                {
                    foreach (var te in e.newTextsAfterHide)
                    {
                        if (te != null && te.textObject != null) te.textObject.alpha = 0f;
                    }
                }

                if (e.newLinesAfterHide != null)
                {
                    foreach (var le in e.newLinesAfterHide)
                    {
                        if (le == null || le.lineRenderer == null) continue;
                        le.lineRenderer.positionCount = 0;
                        if (le.glowLine != null) le.glowLine.positionCount = 0;
                    }
                }

                if (e.objectToActivateAfterHide != null) e.objectToActivateAfterHide.SetActive(false);
            }
        }

        if (lineTimeline != null)
        {
            foreach (var e in lineTimeline)
            {
                if (e == null || e.lineRenderer == null) continue;
                if (e.routineCoroutine != null) StopCoroutine(e.routineCoroutine);
                e.lineRenderer.positionCount = 0;
                if (e.glowLine != null) e.glowLine.positionCount = 0;
            }
        }
    }

    // ── 텍스트 트랙 ──────────────────────────────────────────────

    private IEnumerator PlayTextTimeline()
    {
        yield return PlayTextEntries(textTimeline);
    }

    /// <summary>배열 하나를 받아 시간순으로 재생. textTimeline과 체크포인트의 newTexts 둘 다 이 함수를 씁니다.</summary>
    private IEnumerator PlayTextEntries(TimelineTextEntry[] entries)
    {
        if (entries == null) yield break;

        float elapsed = 0f;
        foreach (var entry in entries)
        {
            if (entry == null || entry.textObject == null) continue;

            float wait = entry.startTime - elapsed;
            if (wait > 0f)
            {
                yield return new WaitForSeconds(wait);
                elapsed += wait;
            }

            // 다음 항목 스케줄을 막지 않도록, 실제 재생은 백그라운드로 실행
            entry.routineCoroutine = StartCoroutine(PlayTextEntry(entry));
        }
    }

    private IEnumerator PlayTextEntry(TimelineTextEntry entry)
    {
        entry.textObject.text = entry.message;
        PlaySound();

        yield return FadeText(entry.textObject, entry.textObject.alpha, 1f, entry.fadeInDuration);
        yield return new WaitForSeconds(entry.holdDuration);
        yield return FadeText(entry.textObject, entry.textObject.alpha, 0f, entry.fadeOutDuration);
    }

    private IEnumerator FadeText(TextMeshPro text, float from, float to, float duration)
    {
        float elapsed = 0f;
        text.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            text.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        text.alpha = to;
    }

    // ── 텍스트 숨김 트랙 ──────────────────────────────────────────

    private IEnumerator PlayHideTimeline()
    {
        if (hideTimeline == null) yield break;

        float elapsed = 0f;
        foreach (var entry in hideTimeline)
        {
            if (entry == null || entry.textsToHide == null) continue;

            float wait = entry.startTime - elapsed;
            if (wait > 0f)
            {
                yield return new WaitForSeconds(wait);
                elapsed += wait;
            }

            // 사라지는 동안 다음 hideTimeline 항목 스케줄을 막지 않도록 백그라운드로 실행
            StartCoroutine(PlayHideEntry(entry));
        }
    }

    /// <summary>지정된 텍스트들을 페이드 아웃하고, 전부 다 사라진 직후 후속 콘텐츠를 자동 실행합니다.</summary>
    private IEnumerator PlayHideEntry(TimelineHideEntry entry)
    {
        var fades = new List<Coroutine>();
        foreach (var text in entry.textsToHide)
        {
            if (text != null) fades.Add(StartCoroutine(FadeText(text, text.alpha, 0f, entry.fadeOutDuration)));
        }

        // 지정된 텍스트가 전부 다 사라질 때까지 대기 (시간 계산 없이 실제 완료 시점에 맞춰짐)
        foreach (var co in fades) yield return co;

        if (entry.objectToActivateAfterHide != null)
            entry.objectToActivateAfterHide.SetActive(true);

        if (entry.newTextsAfterHide != null)
            StartCoroutine(PlayTextEntries(entry.newTextsAfterHide));

        if (entry.newLinesAfterHide != null)
            StartCoroutine(PlayLineEntries(entry.newLinesAfterHide));
    }

    // ── 라인 트랙 ────────────────────────────────────────────────

    private IEnumerator PlayLineTimeline()
    {
        yield return PlayLineEntries(lineTimeline);
    }

    /// <summary>배열 하나를 받아 시간순으로 재생. lineTimeline과 체크포인트의 newLines 둘 다 이 함수를 씁니다.</summary>
    private IEnumerator PlayLineEntries(TimelineLineEntry[] entries)
    {
        if (entries == null) yield break;

        float elapsed = 0f;
        foreach (var entry in entries)
        {
            if (entry == null || entry.lineRenderer == null) continue;

            float wait = entry.startTime - elapsed;
            if (wait > 0f)
            {
                yield return new WaitForSeconds(wait);
                elapsed += wait;
            }

            entry.routineCoroutine = StartCoroutine(EntryShowRoutine(entry));
        }
    }

    /// <summary>이 라인 하나를 그리고, loopWhileInside면 지웠다가 대기 후 다시 그리기를 반복.</summary>
    private IEnumerator EntryShowRoutine(TimelineLineEntry entry)
    {
        do
        {
            yield return StartCoroutine(DrawLineRoutine(entry));

            if (entry.loopWhileInside && playerInside)
            {
                yield return StartCoroutine(FadeLine(entry, GetLineAlpha(entry), 0f, entry.fadeOutDuration));
                entry.lineRenderer.positionCount = 0;
                if (entry.glowLine != null) entry.glowLine.positionCount = 0;
                yield return new WaitForSeconds(entry.loopInterval);
            }
        }
        while (entry.loopWhileInside && playerInside);
    }

    private void SetupEntry(TimelineLineEntry entry)
    {
        var line = entry.lineRenderer;

        Vector3[] anchors = new Vector3[line.positionCount];
        line.GetPositions(anchors);

        // Scene 뷰에서 점을 찍을 때 카메라 깊이가 Z값으로 같이 찍히는 경우가 있어서,
        // 게임 카메라 기준으로 항상 보이도록 Z를 0으로 강제 평탄화합니다.
        for (int i = 0; i < anchors.Length; i++)
        {
            anchors[i] = new Vector3(anchors[i].x, anchors[i].y, 0f);
        }

        entry.smoothPoints = GenerateSmoothPath(anchors, entry.smoothSegments);
        PrecomputeSegmentLengths(entry);

        Material mat = line.sharedMaterial != null
            ? line.sharedMaterial
            : new Material(Shader.Find("Sprites/Default"));

        line.material = mat;
        line.useWorldSpace = true;
        line.numCapVertices = 6;
        line.numCornerVertices = 6;
        line.startWidth = entry.lineWidth;
        line.endWidth = entry.lineWidth;
        line.textureMode = LineTextureMode.Stretch;
        line.loop = false; // "도형 닫기" 옵션이라 반복 재생과 무관 — 혼동 방지용으로 강제로 끔

        var glowGO = new GameObject(line.gameObject.name + "_Glow (Auto)");
        glowGO.transform.SetParent(line.transform.parent, false);
        entry.glowLine = glowGO.AddComponent<LineRenderer>();
        entry.glowLine.material = mat;
        entry.glowLine.useWorldSpace = true;
        entry.glowLine.numCapVertices = 6;
        entry.glowLine.numCornerVertices = 6;
        entry.glowLine.startWidth = entry.lineWidth * entry.glowWidthMultiplier;
        entry.glowLine.endWidth = entry.lineWidth * entry.glowWidthMultiplier;
        entry.glowLine.textureMode = LineTextureMode.Stretch;
        entry.glowLine.sortingOrder = line.sortingOrder - 1;

        line.positionCount = 0;
        entry.glowLine.positionCount = 0;
        SetLineAlpha(entry, 0f);

        glowLookup[line] = entry.glowLine;
        entryLookup[line] = entry;
    }

    private void PrecomputeSegmentLengths(TimelineLineEntry entry)
    {
        if (entry.smoothPoints == null || entry.smoothPoints.Count < 2) return;

        entry.segmentLengths = new float[entry.smoothPoints.Count - 1];
        entry.totalLength = 0f;
        for (int i = 0; i < entry.smoothPoints.Count - 1; i++)
        {
            entry.segmentLengths[i] = Vector3.Distance(entry.smoothPoints[i], entry.smoothPoints[i + 1]);
            entry.totalLength += entry.segmentLengths[i];
        }
    }

    private IEnumerator DrawLineRoutine(TimelineLineEntry entry)
    {
        if (entry.smoothPoints == null || entry.smoothPoints.Count < 2) yield break;

        SetLinePositions(entry, new List<Vector3> { entry.smoothPoints[0] });
        yield return StartCoroutine(FadeLine(entry, 0f, 1f, entry.fadeInDuration));

        PlaySound();

        float elapsed = 0f;
        var current = new List<Vector3>();

        while (elapsed < entry.drawDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / entry.drawDuration);
            float distanceTravelled = progress * entry.totalLength;

            current.Clear();
            current.Add(entry.smoothPoints[0]);
            float accumulated = 0f;

            for (int i = 0; i < entry.segmentLengths.Length; i++)
            {
                if (accumulated + entry.segmentLengths[i] <= distanceTravelled)
                {
                    current.Add(entry.smoothPoints[i + 1]);
                    accumulated += entry.segmentLengths[i];
                }
                else
                {
                    float segT = entry.segmentLengths[i] <= 0f
                        ? 0f
                        : (distanceTravelled - accumulated) / entry.segmentLengths[i];
                    current.Add(Vector3.Lerp(entry.smoothPoints[i], entry.smoothPoints[i + 1], segT));
                    break;
                }
            }

            SetLinePositions(entry, current);
            yield return null;
        }

        SetLinePositions(entry, entry.smoothPoints);
        yield return new WaitForSeconds(entry.holdDuration);
    }

    private void SetLinePositions(TimelineLineEntry entry, List<Vector3> pts)
    {
        entry.lineRenderer.positionCount = pts.Count;
        entry.lineRenderer.SetPositions(pts.ToArray());
        if (entry.glowLine != null)
        {
            entry.glowLine.positionCount = pts.Count;
            entry.glowLine.SetPositions(pts.ToArray());
        }
    }

    private IEnumerator FadeLine(TimelineLineEntry entry, float from, float to, float duration)
    {
        float elapsed = 0f;
        SetLineAlpha(entry, from);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetLineAlpha(entry, Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }
        SetLineAlpha(entry, to);
    }

    private void SetLineAlpha(TimelineLineEntry entry, float alpha)
    {
        if (entry.lineRenderer == null) return;
        Color core = entry.color; core.a = alpha;
        entry.lineRenderer.startColor = core;
        entry.lineRenderer.endColor = core;

        if (entry.glowLine != null)
        {
            Color glow = entry.color; glow.a = alpha * entry.glowAlpha;
            entry.glowLine.startColor = glow;
            entry.glowLine.endColor = glow;
        }
    }

    private float GetLineAlpha(TimelineLineEntry entry)
    {
        return entry.lineRenderer != null ? entry.lineRenderer.startColor.a : 0f;
    }

    /// <summary>
    /// 특정 엔트리에 속하지 않고 참조로만 들어온 LineRenderer를 페이드 아웃합니다.
    /// (체크포인트의 Lines To Hide처럼, 이미 그려진 라인을 나중에 끌 때 사용)
    /// 현재 색상을 그대로 두고 알파만 0으로 내리며, 짝지어진 글로우 라인이 있으면 함께 처리합니다.
    /// </summary>
    private IEnumerator FadeOutRawLine(LineRenderer line, float duration)
    {
        // 이 라인이 반복 재생(Loop While Inside) 중이었다면, 그 코루틴부터 멈춰야
        // 알파를 내려도 다시 그려지며 되살아나는 일이 없습니다.
        TimelineLineEntry owner;
        if (entryLookup.TryGetValue(line, out owner) && owner.routineCoroutine != null)
        {
            StopCoroutine(owner.routineCoroutine);
            owner.routineCoroutine = null;
        }

        LineRenderer glow;
        glowLookup.TryGetValue(line, out glow);

        Color coreColor = line.startColor;
        float fromCore = coreColor.a;

        Color glowColor = glow != null ? glow.startColor : default;
        float fromGlow = glow != null ? glowColor.a : 0f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            Color c = coreColor; c.a = Mathf.Lerp(fromCore, 0f, t);
            line.startColor = c;
            line.endColor = c;

            if (glow != null)
            {
                Color g = glowColor; g.a = Mathf.Lerp(fromGlow, 0f, t);
                glow.startColor = g;
                glow.endColor = g;
            }

            yield return null;
        }

        Color finalCore = coreColor; finalCore.a = 0f;
        line.startColor = finalCore;
        line.endColor = finalCore;
        line.positionCount = 0;

        if (glow != null)
        {
            Color finalGlow = glowColor; finalGlow.a = 0f;
            glow.startColor = finalGlow;
            glow.endColor = finalGlow;
            glow.positionCount = 0;
        }
    }

    private void PlaySound()
    {
        if (audioSource != null && revealSound != null)
        {
            audioSource.PlayOneShot(revealSound);
        }
    }

    // ── Catmull-Rom 스플라인 (곡선 스무딩) ──────────────────────────────

    private static List<Vector3> GenerateSmoothPath(Vector3[] anchors, int segmentsPerCurve)
    {
        var result = new List<Vector3>();
        if (anchors == null || anchors.Length < 2) return result;
        if (anchors.Length == 2 || segmentsPerCurve < 1)
        {
            result.Add(anchors[0]);
            result.Add(anchors[anchors.Length - 1]);
            return result;
        }

        var pts = new List<Vector3> { anchors[0] };
        pts.AddRange(anchors);
        pts.Add(anchors[anchors.Length - 1]);

        for (int i = 0; i < pts.Count - 3; i++)
        {
            Vector3 p0 = pts[i];
            Vector3 p1 = pts[i + 1];
            Vector3 p2 = pts[i + 2];
            Vector3 p3 = pts[i + 3];

            bool isLast = i == pts.Count - 4;
            int steps = isLast ? segmentsPerCurve + 1 : segmentsPerCurve;

            for (int s = 0; s < steps; s++)
            {
                float t = s / (float)segmentsPerCurve;
                result.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }

        return result;
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }
}