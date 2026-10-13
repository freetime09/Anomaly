using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 신호 전선(회로선). 발신자(레버·버튼)와 수신자(문·404벽 등)가 연결돼 있다는 걸 보여주는 선.
///  - 경로: 이 오브젝트의 자식들(꺾이는 점) 위치를 순서대로 잇는다. 바닥·벽 블록 안쪽을 따라 배치.
///    orthogonal 이 켜져 있으면 대각선 대신 '가로 먼저 → 세로' 로 자동으로 꺾는다.
///  - signalId 채널이 꺼져 있으면 어두운 선(offColor).
///    켜지면 시작점(발신자)부터 끝점(수신자)까지 밝은 색(onColor)이 차오르고, 그 위로 빛 펄스가 흐른다.
///    꺼지면 밝은 부분이 시작점 쪽으로 빠진다.
/// 에디터에서도 선이 보이므로(ExecuteAlways) 자식 점을 옮기며 경로를 바로 확인할 수 있다.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class SignalWire : MonoBehaviour
{
    [Header("신호 (채널)")]
    [Tooltip("이 채널이 켜지면 전선이 밝아진다. 발신자(레버·버튼)의 signalId 와 같게")]
    public int signalId = 0;

    [Header("경로")]
    [Tooltip("켜면 두 점이 대각선이면 '가로 먼저 → 세로' 로 꺾어서 잇는다(회로처럼 직각)")]
    public bool orthogonal = true;

    [Header("모양")]
    [Tooltip("선 두께(units)")]
    public float width = 0.12f;
    [Tooltip("선에 쓸 머티리얼 (비우면 Sprite-Unlit-Default)")]
    public Material material;
    [Tooltip("정렬 순서. 블록(0)보다 앞, 레버·버튼(5)보다 뒤")]
    public int sortingOrder = 1;

    [Header("색")]
    [Tooltip("꺼져 있을 때 색")]
    public Color offColor = new Color(0.22f, 0.24f, 0.28f, 1f);
    [Tooltip("켜졌을 때 색")]
    public Color onColor = new Color(0.25f, 0.95f, 0.85f, 1f);

    [Header("켜질 때 차오르기")]
    [Tooltip("밝은 색이 선을 따라 차오르는/빠지는 속도(units/초)")]
    public float fillSpeed = 25f;

    [Header("흐르는 빛 (켜져 있을 때)")]
    [Tooltip("빛 펄스 색")]
    public Color pulseColor = Color.white;
    [Tooltip("펄스 하나의 길이(units)")]
    public float pulseLength = 0.5f;
    [Tooltip("펄스 사이 간격(units)")]
    public float pulseSpacing = 2.5f;
    [Tooltip("펄스가 흐르는 속도(units/초)")]
    public float pulseSpeed = 5f;
    [Tooltip("펄스 두께 배율(선 두께 기준)")]
    public float pulseWidthScale = 1.6f;

    LineRenderer baseLine;
    readonly List<Vector3> path = new List<Vector3>();
    readonly List<float> cumLen = new List<float>();
    float totalLen;

    // 런타임 전용(씬에 저장 안 됨)
    Transform fxRoot;
    LineRenderer litLine;
    readonly List<LineRenderer> pulses = new List<LineRenderer>();
    readonly List<Vector3> tmp = new List<Vector3>();
    System.Action<bool> onChange;
    bool isOn;
    float fill;          // 밝게 차오른 길이(0..totalLen)
    float pulsePhase;

    void OnEnable()
    {
        baseLine = GetComponent<LineRenderer>();
        ResolveMaterial();
        SetupLine(baseLine, sortingOrder, width);
        baseLine.startColor = baseLine.endColor = offColor;
        RebuildPath();
        ApplyBase();

        if (!Application.isPlaying) return;
        CreateFx();
        onChange = OnSignal;
        fill = -1f; // 첫 콜백이면 즉시 반영
        SignalBus.Subscribe(signalId, onChange);
    }

    void OnDisable()
    {
        if (Application.isPlaying && onChange != null) SignalBus.Unsubscribe(signalId, onChange);
        if (fxRoot != null) Destroy(fxRoot.gameObject);
        fxRoot = null; litLine = null; pulses.Clear();
    }

    void OnSignal(bool on)
    {
        isOn = on;
        if (fill < 0f) fill = on ? totalLen : 0f; // 시작부터 켜져 있던 채널이면 차오르기 없이 바로
    }

    void Update()
    {
        if (!Application.isPlaying)
        {
            // 에디터: 점을 옮기면 바로 다시 그림
            ResolveMaterial();
            SetupLine(baseLine, sortingOrder, width);
            baseLine.startColor = baseLine.endColor = offColor;
            RebuildPath();
            ApplyBase();
            return;
        }
        if (litLine == null) return;

        float target = isOn ? totalLen : 0f;
        fill = Mathf.MoveTowards(Mathf.Max(fill, 0f), target, fillSpeed * Time.deltaTime);

        Sample(0f, fill, tmp);
        SetPositions(litLine, tmp);

        // 흐르는 빛: 완전히 켜졌을 때만
        bool flowing = isOn && fill >= totalLen - 0.001f;
        pulsePhase = (pulsePhase + pulseSpeed * Time.deltaTime) % Mathf.Max(0.01f, pulseSpacing);
        for (int i = 0; i < pulses.Count; i++)
        {
            float head = pulsePhase + i * pulseSpacing;
            float tail = head - pulseLength;
            if (!flowing || tail >= totalLen) { pulses[i].enabled = false; continue; }
            Sample(Mathf.Max(0f, tail), Mathf.Min(totalLen, head), tmp);
            SetPositions(pulses[i], tmp);
        }
    }

    void ResolveMaterial()
    {
#if UNITY_EDITOR
        if (material == null)
            material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
#endif
    }

    void SetupLine(LineRenderer lr, int order, float w)
    {
        lr.useWorldSpace = true;
        lr.startWidth = lr.endWidth = w;
        lr.numCornerVertices = 0;
        lr.numCapVertices = 0;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.sortingOrder = order;
        if (material != null) lr.sharedMaterial = material;
    }

    void CreateFx()
    {
        var go = new GameObject("_WireFx");
        go.hideFlags = HideFlags.DontSave;
        fxRoot = go.transform;
        fxRoot.SetParent(transform, false);

        litLine = NewLine("Lit", sortingOrder + 1, width, onColor);
        int n = Mathf.CeilToInt((totalLen + pulseLength) / Mathf.Max(0.1f, pulseSpacing)) + 1;
        for (int i = 0; i < n; i++)
            pulses.Add(NewLine("Pulse", sortingOrder + 2, width * pulseWidthScale, pulseColor));
    }

    LineRenderer NewLine(string name, int order, float w, Color c)
    {
        var go = new GameObject(name);
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(fxRoot, false);
        var lr = go.AddComponent<LineRenderer>();
        SetupLine(lr, order, w);
        lr.startColor = lr.endColor = c;
        lr.positionCount = 0;
        return lr;
    }

    /// <summary>자식 점들로 경로(꺾이는 점 목록)와 누적 길이를 만든다.</summary>
    void RebuildPath()
    {
        path.Clear();
        for (int i = 0; i < transform.childCount; i++)
        {
            var c = transform.GetChild(i);
            if (c == fxRoot) continue;
            Vector3 p = c.position; p.z = 0f;
            if (path.Count > 0)
            {
                Vector3 prev = path[path.Count - 1];
                if (orthogonal && !Mathf.Approximately(prev.x, p.x) && !Mathf.Approximately(prev.y, p.y))
                    path.Add(new Vector3(p.x, prev.y, 0f)); // 가로 먼저 → 세로
            }
            path.Add(p);
        }
        cumLen.Clear();
        totalLen = 0f;
        for (int i = 0; i < path.Count; i++)
        {
            if (i > 0) totalLen += Vector3.Distance(path[i - 1], path[i]);
            cumLen.Add(totalLen);
        }
    }

    void ApplyBase()
    {
        baseLine.positionCount = path.Count;
        baseLine.SetPositions(path.ToArray());
    }

    /// <summary>경로에서 길이 d0~d1 구간의 점들(꺾이는 점 포함)을 out 에 담는다.</summary>
    void Sample(float d0, float d1, List<Vector3> outPts)
    {
        outPts.Clear();
        if (path.Count < 2 || d1 <= d0) return;
        outPts.Add(PointAt(d0));
        for (int i = 1; i < path.Count - 1; i++)
            if (cumLen[i] > d0 && cumLen[i] < d1) outPts.Add(path[i]);
        outPts.Add(PointAt(d1));
    }

    Vector3 PointAt(float d)
    {
        for (int i = 1; i < path.Count; i++)
        {
            if (d <= cumLen[i])
            {
                float seg = cumLen[i] - cumLen[i - 1];
                float t = seg > 0f ? (d - cumLen[i - 1]) / seg : 0f;
                return Vector3.Lerp(path[i - 1], path[i], t);
            }
        }
        return path[path.Count - 1];
    }

    static void SetPositions(LineRenderer lr, List<Vector3> pts)
    {
        lr.enabled = pts.Count >= 2;
        lr.positionCount = pts.Count;
        for (int i = 0; i < pts.Count; i++) lr.SetPosition(i, pts[i]);
    }
}
