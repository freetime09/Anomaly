using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// TutorialTrigger(부모, 콜라이더가 있는 오브젝트)에 붙이는 스크립트.
///
/// - 텍스트: 트리거 진입 시 한 번 페이드 인 되어 유지, 벗어나면 페이드 아웃.
/// - 제스처 라인: 여러 개(GestureLines 배열)를 동시에 보여줄 수 있음 (예: 이동+점프 동시 설명).
///   각 라인마다 두께/색/글로우/곡선 부드러움/그려지는 속도/페이드 인·아웃 시간을 전부 따로 조절합니다.
///   Loop While Inside를 켜면: 다 그려진 뒤 잠깐 지워졌다가, Loop Interval만큼 기다린 후 다시 그리기를
///   플레이어가 트리거 안에 머무는 동안 반복합니다.
///
/// 주의: 각 LineRenderer의 자체 "Loop" 옵션은 도형을 닫는 기능이라 이 반복 재생과 무관합니다.
///       혼동을 막기 위해 스크립트가 시작할 때 자동으로 꺼버립니다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TutorialRevealTrigger : MonoBehaviour
{
    [System.Serializable]
    public class GestureLineEntry
    {
        [Header("참조")]
        [Tooltip("Scene 뷰에서 모양(점들)을 미리 찍어둔 LineRenderer")]
        public LineRenderer lineRenderer;

        [Header("외형 (이 라인만 적용)")]
        public Color color = Color.white;
        public float lineWidth = 0.12f;
        [Tooltip("코어 선 뒤에 깔리는 옅은 글로우 두께 배수")]
        public float glowWidthMultiplier = 2.8f;
        [Range(0f, 1f)]
        public float glowAlpha = 0.35f;
        [Tooltip("코너 점 사이를 몇 개의 점으로 부드럽게 나눌지 (높을수록 매끈함)")]
        public int smoothSegments = 16;

        [Header("타이밍 (이 라인만 적용)")]
        [Tooltip("이 라인이 처음 나타날 때 페이드 인 시간")]
        public float fadeInDuration = 0.25f;
        [Tooltip("이 라인이 사라질 때(트리거 이탈 시, 또는 반복 중 지워질 때) 페이드 아웃 시간")]
        public float fadeOutDuration = 0.5f;
        [Tooltip("이 라인 전체가 그려지는 데 걸리는 시간")]
        public float drawDuration = 1.0f;
        [Tooltip("다 그려진 뒤 화면에 유지되는 시간")]
        public float holdDuration = 0.6f;

        [Header("반복 재생 (이 라인만 적용)")]
        [Tooltip("체크하면 이 라인만 플레이어가 트리거 안에 머무는 동안 '그리기 → 대기 → 다시 그리기'를 반복")]
        public bool loopWhileInside = false;
        [Tooltip("한 번 다 그려진 뒤 지워지고, 다시 그리기 전까지 기다리는 시간(초)")]
        public float loopInterval = 1.0f;

        [System.NonSerialized] public LineRenderer glowLine;
        [System.NonSerialized] public List<Vector3> smoothPoints;
        [System.NonSerialized] public float[] segmentLengths;
        [System.NonSerialized] public float totalLength;
        [System.NonSerialized] public Coroutine routineCoroutine;
    }

    [Header("텍스트 (자식 오브젝트, TextMeshPro 3D)")]
    [SerializeField] private TextMeshPro worldText;
    [TextArea]
    [SerializeField] private string message = "앞으로 길게 드래그하여 이동하세요";
    [SerializeField] private float textFadeInDuration = 0.3f;
    [SerializeField] private float textFadeOutDuration = 0.6f;

    [Header("제스처 라인들 (여러 개 동시 표시 가능, 각각 개별 설정)")]
    [Tooltip("예: 0번=이동 라인, 1번=점프 라인. 각자 색/두께/속도/페이드를 따로 조절합니다")]
    [SerializeField] private GestureLineEntry[] gestureLines;

    [Header("옵션")]
    [SerializeField] private bool triggerOnce = false;
    [SerializeField] private string playerTag = "Player";

    [Header("사운드 (선택)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip revealSound;

    private bool hasTriggered = false;
    private bool playerInside = false;
    private Coroutine textRoutine;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (worldText != null)
        {
            worldText.text = message;
            worldText.alpha = 0f;
        }

        if (gestureLines != null)
        {
            foreach (var entry in gestureLines)
            {
                if (entry != null && entry.lineRenderer != null)
                    SetupEntry(entry);
            }
        }
    }

    private void SetupEntry(GestureLineEntry entry)
    {
        var line = entry.lineRenderer;

        // Scene 뷰에서 찍어둔 코너 점들을 그대로 캐싱
        Vector3[] anchors = new Vector3[line.positionCount];
        line.GetPositions(anchors);

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

        // GestureTrail과 동일하게, 글로우 라인은 자동으로 생성 (직접 안 만들어도 됨)
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
    }

    private void PrecomputeSegmentLengths(GestureLineEntry entry)
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

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (triggerOnce && hasTriggered) return;

        hasTriggered = true;
        playerInside = true;
        PlaySound();

        // 텍스트는 한 번만 페이드 인 되어 유지 (라인 루프와 완전히 별개로 동작)
        if (worldText != null)
        {
            if (textRoutine != null) StopCoroutine(textRoutine);
            textRoutine = StartCoroutine(FadeText(worldText.alpha, 1f, textFadeInDuration));
        }

        // 라인마다 자기만의 루프를 독립적으로 돌린다 (서로 속도/반복 여부가 달라도 상관없음)
        if (gestureLines != null)
        {
            foreach (var entry in gestureLines)
            {
                if (entry == null || entry.lineRenderer == null) continue;
                if (entry.routineCoroutine != null) StopCoroutine(entry.routineCoroutine);
                entry.routineCoroutine = StartCoroutine(EntryShowRoutine(entry));
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        playerInside = false;

        if (worldText != null)
        {
            if (textRoutine != null) StopCoroutine(textRoutine);
            textRoutine = StartCoroutine(FadeText(worldText.alpha, 0f, textFadeOutDuration));
        }

        if (gestureLines != null)
        {
            foreach (var entry in gestureLines)
            {
                if (entry == null || entry.lineRenderer == null) continue;
                if (entry.routineCoroutine != null) StopCoroutine(entry.routineCoroutine);
                entry.routineCoroutine = StartCoroutine(EntryHideRoutine(entry));
            }
        }
    }

    /// <summary>이 라인 하나를 그리고, loopWhileInside면 지웠다가 대기 후 다시 그리기를 반복.</summary>
    private IEnumerator EntryShowRoutine(GestureLineEntry entry)
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

    /// <summary>플레이어가 트리거를 벗어났을 때 이 라인 하나를 페이드 아웃.</summary>
    private IEnumerator EntryHideRoutine(GestureLineEntry entry)
    {
        yield return StartCoroutine(FadeLine(entry, GetLineAlpha(entry), 0f, entry.fadeOutDuration));
        entry.lineRenderer.positionCount = 0;
        if (entry.glowLine != null) entry.glowLine.positionCount = 0;
    }

    private IEnumerator DrawLineRoutine(GestureLineEntry entry)
    {
        if (entry.smoothPoints == null || entry.smoothPoints.Count < 2) yield break;

        SetLinePositions(entry, new List<Vector3> { entry.smoothPoints[0] });
        yield return StartCoroutine(FadeLine(entry, 0f, 1f, entry.fadeInDuration));

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

    private void SetLinePositions(GestureLineEntry entry, List<Vector3> pts)
    {
        entry.lineRenderer.positionCount = pts.Count;
        entry.lineRenderer.SetPositions(pts.ToArray());
        if (entry.glowLine != null)
        {
            entry.glowLine.positionCount = pts.Count;
            entry.glowLine.SetPositions(pts.ToArray());
        }
    }

    private IEnumerator FadeText(float from, float to, float duration)
    {
        if (worldText == null) yield break;
        float elapsed = 0f;
        worldText.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            worldText.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        worldText.alpha = to;
    }

    private IEnumerator FadeLine(GestureLineEntry entry, float from, float to, float duration)
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

    private void SetLineAlpha(GestureLineEntry entry, float alpha)
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

    private float GetLineAlpha(GestureLineEntry entry)
    {
        return entry.lineRenderer != null ? entry.lineRenderer.startColor.a : 0f;
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