using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 마우스로 그리는 동안 궤적을 LineRenderer 로 기록해 화면에 "남긴다".
/// - 코어 라인 + 그 뒤에 굵고 옅은 글로우 라인(소프트 헤일로)
/// - 그리는 중 손끝에서 작은 스파크가 반짝인다
/// - 제스처가 인식되면 SetStrokeColor 로 획 전체가 그 색으로 물들고,
///   두께가 잠깐 튀었다 돌아오며(팝) 밝게 번쩍이고, 그 색의 파티클이 터진다.
/// - 손을 떼면 holdTime 유지 후 fadeTime 동안 서서히 사라진다.
/// - 궤적은 화면 좌표로 기록해 매 프레임 월드로 다시 옮긴다 → 그리는 중 카메라가 움직여도 모양이 안 틀어짐.
/// </summary>
[DefaultExecutionOrder(1000)] // CameraFollow.LateUpdate 이후에 궤적을 다시 옮기도록
public class GestureTrail : MonoBehaviour
{
    [Header("선")]
    [Tooltip("코어 선 두께")]
    public float width = 0.12f;
    [Tooltip("기본 색(그리는 중 / 인식 안 된 획). 인식되면 그 모양의 색으로 바뀐다.")]
    public Color color = Color.white;
    [Tooltip("점을 새로 기록하는 최소 이동 거리(월드 단위). 작을수록 촘촘하다.")]
    public float minPointDistance = 0.03f;

    [Header("글로우(헤일로)")]
    [Tooltip("글로우 선 두께 = 코어 두께 × 이 값")]
    public float glowWidthMultiplier = 2.8f;
    [Tooltip("글로우 선의 투명도(0~1). 낮을수록 은은함")]
    [Range(0f, 1f)] public float glowAlpha = 0.35f;

    [Header("사라짐")]
    [Tooltip("손을 뗀 뒤 그대로 유지되는 시간(초)")]
    public float holdTime = 0.5f;
    [Tooltip("서서히 투명해지며 사라지는 시간(초)")]
    public float fadeTime = 0.45f;

    [Header("인식 순간 팝(번쩍)")]
    [Tooltip("팝 때 두께가 늘어나는 배수")]
    public float popScale = 1.9f;
    [Tooltip("팝이 원래 두께로 돌아오는 시간(초)")]
    public float popTime = 0.18f;

    [Header("스파크 파티클")]
    [Tooltip("그리는 동안 손끝에서 초당 튀는 스파크 수")]
    public float drawSparkRate = 25f;
    [Tooltip("인식 순간 터지는 파티클 수")]
    public int burstCount = 24;

    LineRenderer line;      // 코어
    LineRenderer glow;      // 헤일로
    ParticleSystem ps;
    ParticleSystem.EmissionModule psEmission;
    Camera cam;

    readonly List<Vector3> points = new List<Vector3>(); // 화면 좌표 (카메라가 움직여도 화면상 모양 유지)
    Color currentColor;
    bool drawing;
    Coroutine fadeCo;
    Coroutine popCo;

    void Awake()
    {
        cam = Camera.main;

        // 예전에 쓰던 TrailRenderer 가 붙어 있으면 꺼둔다 (중복 렌더 방지)
        var tr = GetComponent<TrailRenderer>();
        if (tr != null) tr.enabled = false;

        var mat = new Material(Shader.Find("Sprites/Default"));

        // 코어 라인 (이 오브젝트)
        line = GetComponent<LineRenderer>();
        if (line == null) line = gameObject.AddComponent<LineRenderer>();
        SetupLine(line, mat, width, 102);

        // 글로우 라인 (자식) — 굵고 옅게 깔아 소프트 헤일로
        var glowGO = new GameObject("Glow");
        glowGO.transform.SetParent(transform, false);
        glow = glowGO.AddComponent<LineRenderer>();
        SetupLine(glow, mat, width * glowWidthMultiplier, 101);

        BuildParticles(mat);

        currentColor = color;
        ApplyColor(color, 1f);
    }

    void SetupLine(LineRenderer lr, Material mat, float w, int order)
    {
        lr.useWorldSpace = true;
        lr.material = mat;
        lr.numCapVertices = 6;
        lr.numCornerVertices = 6;
        lr.startWidth = w;
        lr.endWidth = w;
        lr.sortingOrder = order;
        lr.positionCount = 0;
        lr.textureMode = LineTextureMode.Stretch;
    }

    void BuildParticles(Material mat)
    {
        var go = new GameObject("Sparks");
        go.transform.SetParent(transform, false);
        ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = 0.45f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
        main.gravityModifier = 0.25f;
        main.maxParticles = 400;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = color;

        psEmission = ps.emission;
        psEmission.enabled = true;
        psEmission.rateOverTime = 0f; // 평소엔 0, 그리는 동안만 켠다

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.06f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var psr = ps.GetComponent<ParticleSystemRenderer>();
        psr.material = mat;
        psr.sortingOrder = 103;

        ps.Play();
    }

    void Update()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // 설정 메뉴가 열려 있으면 그리기 막음 (그리던 궤적은 사라지게)
        if (PauseMenu.IsPaused)
        {
            CancelTrail();
            return;
        }

        // 그리는 중 우클릭 → 궤적 취소 (GestureRecognizer 도 같은 프레임에 획을 버림)
        if (drawing && Input.GetMouseButtonDown(1))
        {
            CancelTrail();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (PauseMenu.IsPointerOnSettingsButton(Input.mousePosition)) return; // 설정 아이콘 클릭은 그리기 아님
            if (fadeCo != null) { StopCoroutine(fadeCo); fadeCo = null; }
            if (popCo != null) { StopCoroutine(popCo); popCo = null; }
            points.Clear();
            line.positionCount = 0;
            glow.positionCount = 0;
            currentColor = color;
            ApplyColor(color, 1f);
            SetWidth(width);
            drawing = true;
            AddPoint(Input.mousePosition);

            // 손끝 스파크 켜기 (흰색)
            var main = ps.main; main.startColor = color;
            psEmission.rateOverTime = drawSparkRate;
        }
        else if (drawing && Input.GetMouseButton(0))
        {
            Vector3 sp = Input.mousePosition;
            Vector3 w = ToWorld(sp);
            ps.transform.position = w; // 스파크를 손끝으로
            if (points.Count == 0 ||
                (w - ToWorld(points[points.Count - 1])).sqrMagnitude >= minPointDistance * minPointDistance)
                AddPoint(sp);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            drawing = false;
            psEmission.rateOverTime = 0f; // 손끝 스파크 끄기
            // 인식(SetStrokeColor)은 같은 프레임에 GestureRecognizer 가 호출한다.
            if (points.Count > 0)
                fadeCo = StartCoroutine(HoldThenFade());
        }
    }

    /// <summary>그리던 궤적을 바로 지움.</summary>
    void CancelTrail()
    {
        if (!drawing) return;
        drawing = false;
        psEmission.rateOverTime = 0f;
        points.Clear();
        line.positionCount = 0;
        glow.positionCount = 0;
    }

    Vector3 ToWorld(Vector3 sp)
    {
        sp.z = -cam.transform.position.z;
        Vector3 w = cam.ScreenToWorldPoint(sp);
        w.z = 0f;
        return w;
    }

    void AddPoint(Vector3 sp)
    {
        points.Add(sp);
        int n = points.Count;
        Vector3 w = ToWorld(sp);
        line.positionCount = n;
        line.SetPosition(n - 1, w);
        glow.positionCount = n;
        glow.SetPosition(n - 1, w);
    }

    // 카메라가 이번 프레임에 움직였을 수 있으니, 화면 좌표로 기록한 궤적을 현재 카메라 기준으로 다시 배치
    void LateUpdate()
    {
        if (cam == null || line.positionCount == 0 || line.positionCount != points.Count) return;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 w = ToWorld(points[i]);
            line.SetPosition(i, w);
            glow.SetPosition(i, w);
        }
    }

    /// <summary>제스처가 인식된 순간 호출 — 획 전체에 색을 입히고, 팝(번쩍)과 파티클 버스트를 낸다.</summary>
    public void SetStrokeColor(Color c)
    {
        currentColor = c;
        ApplyColor(c, 1f);

        // 팝: 두께가 튀었다 돌아옴 + 밝기 번쩍
        if (popCo != null) StopCoroutine(popCo);
        popCo = StartCoroutine(Pop());

        // 그 색의 파티클 버스트 (마지막 손끝 위치에서)
        var ep = new ParticleSystem.EmitParams { startColor = c };
        ps.Emit(ep, burstCount);
    }

    void ApplyColor(Color c, float a)
    {
        if (line == null) return;
        Color core = new Color(c.r, c.g, c.b, a);
        line.startColor = core; line.endColor = core;
        Color halo = new Color(c.r, c.g, c.b, a * glowAlpha);
        glow.startColor = halo; glow.endColor = halo;
    }

    void SetWidth(float coreW)
    {
        line.startWidth = coreW; line.endWidth = coreW;
        float g = coreW * glowWidthMultiplier;
        glow.startWidth = g; glow.endWidth = g;
    }

    IEnumerator Pop()
    {
        // 즉시 굵게 + 하얗게 번쩍 → popTime 동안 원래 두께/색으로 복귀
        float t = 0f;
        while (t < popTime)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / popTime);      // 1→0
            SetWidth(width * (1f + (popScale - 1f) * k));
            // 흰색으로 살짝 번쩍였다 제 색으로
            Color flash = Color.Lerp(currentColor, Color.white, 0.6f * k);
            ApplyColor(flash, 1f);
            yield return null;
        }
        SetWidth(width);
        ApplyColor(currentColor, 1f);
        popCo = null;
    }

    IEnumerator HoldThenFade()
    {
        if (holdTime > 0f) yield return new WaitForSeconds(holdTime);
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            ApplyColor(currentColor, Mathf.Lerp(1f, 0f, t / fadeTime));
            yield return null;
        }
        line.positionCount = 0;
        glow.positionCount = 0;
        points.Clear();
        fadeCo = null;
    }
}
