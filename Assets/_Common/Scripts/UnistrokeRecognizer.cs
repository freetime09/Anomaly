using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// $1 Unistroke Recognizer — 그린 궤적을 저장된 템플릿(정답 모양)과 통째로 비교해
/// 가장 닮은 것과 닮은 정도(0~1 점수)를 돌려준다.
/// §, ★, Z 처럼 방향 규칙으로는 잡기 어려운 "한 붓 그리기" 모양을 인식할 때 사용.
/// 참고: Wobbrock et al., "$1 Unistroke Recognizer" (UIST 2007).
///
/// 원본 $1과 다른 점 (방향이 중요한 게임이라):
///  - 그린 모양을 아무 방향으로나 돌려 맞추지 않는다. 기울기 ±AngleRange 안에서만 보정 → 옆으로 누운 9는 9가 아님.
///    단 RotationFree 에 있는 모양(O)은 어디서 시작해도 되도록 예전처럼 회전 무관하게 비교.
///  - 가로·세로를 같은 비율로 키운다(비율 유지) → 길쭉한 모양이 뭉개지지 않음.
/// 템플릿 저장 형식이 바뀌었으므로 FormatVersion 보다 낮은 파일의 템플릿은 쓰지 않는다(다시 녹화 필요).
/// 예외: legacy 표시된 템플릿(예전 §)은 옛날 방식(회전 무관)으로 그대로 비교한다.
/// </summary>
public static class UnistrokeRecognizer
{
    /// <summary>템플릿 JSON 형식 버전. 2 = 회전하지 않고 저장(방향 정보 유지).</summary>
    public const int FormatVersion = 2;

    /// <summary>회전과 무관하게 비교할 모양 이름 (시작점이 어디든 같은 모양인 것).</summary>
    public static readonly HashSet<string> RotationFree = new HashSet<string> { "O(상호작용)" };

    const int NumPoints = 64;          // 모든 궤적을 이 개수로 리샘플
    const float SquareSize = 250f;     // 정규화 시 맞출 정사각형 크기
    const float AngleRange = 20f;      // 기울기 보정 탐색 범위(도) — 이보다 많이 돌아간 모양은 다른 모양으로 봄
    const float FreeAngleRange = 45f;  // RotationFree 모양의 추가 탐색 범위(도)
    const float AnglePrecision = 2f;   // 회전 탐색 정밀도(도)
    static readonly float HalfDiagonal = 0.5f * Mathf.Sqrt(SquareSize * SquareSize + SquareSize * SquareSize);
    static readonly float Phi = 0.5f * (-1f + Mathf.Sqrt(5f)); // 황금비

    /// <summary>정답 모양 하나. points 는 이미 정규화(Normalize)된 64점.
    /// legacy = 옛날 방식(회전 무관, LegacyNormalize)으로 저장된 템플릿 — 예전 녹화를 그대로 쓰는 모양(§)용.</summary>
    public class Template
    {
        public string name;
        public List<Vector2> points;
        public bool legacy;
    }

    /// <summary>옛날 방식 표준형 (원본 $1: 시작점 기준으로 회전 + 정사각형에 맞춰 늘림). legacy 템플릿 비교용.</summary>
    public static List<Vector2> LegacyNormalize(List<Vector2> raw)
    {
        var pts = Resample(raw, NumPoints);
        pts = RotateToZero(pts);
        pts = ScaleToSquare(pts, SquareSize);
        pts = TranslateTo(pts, Vector2.zero);
        return pts;
    }

    /// <summary>원시 궤적을 비교 가능한 표준형(64점, 크기·위치 정규화, 회전은 안 함)으로 변환.
    /// 템플릿 저장과 인식 양쪽에서 똑같이 거쳐야 한다.</summary>
    public static List<Vector2> Normalize(List<Vector2> raw)
    {
        var pts = Resample(raw, NumPoints);
        pts = ScaleTo(pts, SquareSize);
        pts = TranslateTo(pts, Vector2.zero);
        return pts;
    }

    /// <summary>시작점이 왼쪽 정면에 오도록 돌린 형태 (RotationFree 모양 비교용).</summary>
    static List<Vector2> RotateToZero(List<Vector2> pts)
    {
        return RotateBy(pts, -IndicativeAngle(pts));
    }

    /// <summary>궤적을 템플릿들과 비교해 (가장 닮은 이름, 점수 0~1)를 반환.
    /// 그린 방향과 무관하게 인식되도록 거꾸로 그린 궤적도 같이 비교해 더 닮은 쪽을 쓴다
    /// (O를 시계/반시계 어느 쪽으로 그려도, 9를 아래부터 그려도 인식).
    /// 매칭할 게 없으면 (null, 0).</summary>
    public static (string name, float score) Recognize(List<Vector2> raw, List<Template> templates)
    {
        if (templates == null || templates.Count == 0 || raw == null || raw.Count < 2)
            return (null, 0f);

        var reversed = new List<Vector2>(raw);
        reversed.Reverse();
        var candidates = new[] { Normalize(raw), Normalize(reversed) };
        var freeCandidates = new[] { RotateToZero(candidates[0]), RotateToZero(candidates[1]) };
        List<Vector2>[] legacyCandidates = null; // legacy 템플릿이 있을 때만 계산

        float best = float.MaxValue;
        string bestName = null;
        foreach (var t in templates)
        {
            if (t.points == null || t.points.Count == 0) continue;
            if (t.legacy)
            {
                if (legacyCandidates == null) legacyCandidates = new[] { LegacyNormalize(raw), LegacyNormalize(reversed) };
                float lr = FreeAngleRange * Mathf.Deg2Rad;
                foreach (var candidate in legacyCandidates)
                {
                    float d = DistanceAtBestAngle(candidate, t.points, -lr, lr, AnglePrecision * Mathf.Deg2Rad);
                    if (d < best) { best = d; bestName = t.name; }
                }
                continue;
            }
            bool free = RotationFree.Contains(t.name);
            var tp = free ? RotateToZero(t.points) : t.points;
            float range = (free ? FreeAngleRange : AngleRange) * Mathf.Deg2Rad;
            foreach (var candidate in free ? freeCandidates : candidates)
            {
                float d = DistanceAtBestAngle(candidate, tp, -range, range, AnglePrecision * Mathf.Deg2Rad);
                if (d < best) { best = d; bestName = t.name; }
            }
        }
        float score = 1f - best / HalfDiagonal;
        return (bestName, score);
    }

    // ── 내부 헬퍼 (표준 $1 알고리즘) ──────────────────────────────

    static List<Vector2> Resample(List<Vector2> points, int n)
    {
        var pts = new List<Vector2>(points);
        float interval = PathLength(pts) / (n - 1);
        float acc = 0f;
        var result = new List<Vector2> { pts[0] };
        for (int i = 1; i < pts.Count; i++)
        {
            float d = Vector2.Distance(pts[i - 1], pts[i]);
            if (acc + d >= interval)
            {
                float t = (interval - acc) / d;
                Vector2 q = Vector2.Lerp(pts[i - 1], pts[i], t);
                result.Add(q);
                pts.Insert(i, q); // 다음 반복이 q 부터 이어가도록 삽입
                acc = 0f;
            }
            else acc += d;
        }
        while (result.Count < n) result.Add(pts[pts.Count - 1]); // 반올림 오차 보정
        return result;
    }

    static float PathLength(List<Vector2> points)
    {
        float d = 0f;
        for (int i = 1; i < points.Count; i++) d += Vector2.Distance(points[i - 1], points[i]);
        return d;
    }

    static Vector2 Centroid(List<Vector2> points)
    {
        Vector2 c = Vector2.zero;
        foreach (var p in points) c += p;
        return c / points.Count;
    }

    static float IndicativeAngle(List<Vector2> points)
    {
        Vector2 c = Centroid(points);
        return Mathf.Atan2(c.y - points[0].y, c.x - points[0].x);
    }

    static List<Vector2> RotateBy(List<Vector2> points, float radians)
    {
        Vector2 c = Centroid(points);
        float cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
        var res = new List<Vector2>(points.Count);
        foreach (var p in points)
        {
            float qx = (p.x - c.x) * cos - (p.y - c.y) * sin + c.x;
            float qy = (p.x - c.x) * sin + (p.y - c.y) * cos + c.y;
            res.Add(new Vector2(qx, qy));
        }
        return res;
    }

    static List<Vector2> ScaleTo(List<Vector2> points, float size)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var p in points)
        {
            minX = Mathf.Min(minX, p.x); minY = Mathf.Min(minY, p.y);
            maxX = Mathf.Max(maxX, p.x); maxY = Mathf.Max(maxY, p.y);
        }
        // 긴 쪽을 size에 맞추고 가로·세로 같은 비율로 (길쭉한 모양 비율 유지)
        float big = Mathf.Max(maxX - minX, maxY - minY);
        float k = big > 0f ? size / big : 1f;
        var res = new List<Vector2>(points.Count);
        foreach (var p in points) res.Add(p * k);
        return res;
    }

    // 옛날 방식: 가로·세로를 각각 size에 맞춰 늘림 (legacy 템플릿 비교용)
    static List<Vector2> ScaleToSquare(List<Vector2> points, float size)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var p in points)
        {
            minX = Mathf.Min(minX, p.x); minY = Mathf.Min(minY, p.y);
            maxX = Mathf.Max(maxX, p.x); maxY = Mathf.Max(maxY, p.y);
        }
        float bw = maxX - minX, bh = maxY - minY;
        var res = new List<Vector2>(points.Count);
        foreach (var p in points)
            res.Add(new Vector2(bw > 0 ? p.x * (size / bw) : p.x, bh > 0 ? p.y * (size / bh) : p.y));
        return res;
    }

    static List<Vector2> TranslateTo(List<Vector2> points, Vector2 pt)
    {
        Vector2 c = Centroid(points);
        var res = new List<Vector2>(points.Count);
        foreach (var p in points) res.Add(new Vector2(p.x - c.x + pt.x, p.y - c.y + pt.y));
        return res;
    }

    static float DistanceAtBestAngle(List<Vector2> points, List<Vector2> T, float a, float b, float threshold)
    {
        float x1 = Phi * a + (1 - Phi) * b;
        float f1 = DistanceAtAngle(points, T, x1);
        float x2 = (1 - Phi) * a + Phi * b;
        float f2 = DistanceAtAngle(points, T, x2);
        while (Mathf.Abs(b - a) > threshold)
        {
            if (f1 < f2) { b = x2; x2 = x1; f2 = f1; x1 = Phi * a + (1 - Phi) * b; f1 = DistanceAtAngle(points, T, x1); }
            else { a = x1; x1 = x2; f1 = f2; x2 = (1 - Phi) * a + Phi * b; f2 = DistanceAtAngle(points, T, x2); }
        }
        return Mathf.Min(f1, f2);
    }

    static float DistanceAtAngle(List<Vector2> points, List<Vector2> T, float radians)
    {
        var rotated = RotateBy(points, radians);
        return PathDistance(rotated, T);
    }

    static float PathDistance(List<Vector2> a, List<Vector2> b)
    {
        int n = Mathf.Min(a.Count, b.Count);
        if (n == 0) return float.MaxValue;
        float d = 0f;
        for (int i = 0; i < n; i++) d += Vector2.Distance(a[i], b[i]);
        return d / n;
    }
}
