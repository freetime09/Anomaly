using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SignalWire 인스펙터에 '자동 경로 만들기' 버튼을 붙인다.
/// 시작점(첫 번째 꺾이는 점)에서 대상(문·발판·404 벽)까지, 블록 안쪽(표면에서 routeInset 만큼)을 따라
/// 직각으로 꺾이는 경로를 찾아 꺾이는 점(자식)을 새로 놓는다. Ctrl+Z 로 되돌릴 수 있다.
/// </summary>
[CustomEditor(typeof(SignalWire)), CanEditMultipleObjects]
public class SignalWireEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        if (GUILayout.Button("자동 경로 만들기", GUILayout.Height(26)))
        {
            foreach (var t in targets)
            {
                var wire = (SignalWire)t;
                string err = SignalWireRouter.Route(wire);
                if (err != null) Debug.LogWarning("[자동 경로] " + wire.name + " (" + (wire.transform.parent ? wire.transform.parent.name : "") + "): " + err, wire);
            }
        }
    }
}

public static class SignalWireRouter
{
    const float Step = 0.05f;       // 격자 간격
    const float TurnCost = 1.5f;    // 꺾일 때 추가 비용 (units) — 클수록 덜 꺾임
    const float AirCost = 40f;      // 블록 밖(허공)을 지날 때 1unit 당 비용 — 꼭 필요할 때만 건넘
    const float DepthCost = 4f;     // 원하는 깊이(routeInset)에서 벗어날 때 비용
    const int MaxCells = 1500000;

    static readonly Vector2Int[] Dirs = { new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0), new Vector2Int(0, -1) };

    /// <summary>경로를 만들어 꺾이는 점을 바꾼다. 실패하면 이유를 반환.</summary>
    public static string Route(SignalWire wire)
    {
        // 시작점: 첫 번째 꺾이는 점 (레버·버튼 바닥)
        Transform first = null;
        for (int i = 0; i < wire.transform.childCount; i++)
        {
            var c = wire.transform.GetChild(i);
            if (c.name == "_WireFx") continue;
            first = c; break;
        }
        if (first == null) return "시작점(자식 P0)이 없음";
        Vector2 start = first.position;

        // 대상
        var targetCols = FindTargetColliders(wire, start);
        if (targetCols == null || targetCols.Count == 0) return "대상을 못 찾음 (Route Target 을 넣거나 같은 Signal Id 를 받는 오브젝트가 있어야 함)";
        Bounds tb = targetCols[0].bounds;
        for (int i = 1; i < targetCols.Count; i++) tb.Encapsulate(targetCols[i].bounds);

        // 격자 범위
        Vector2 min = Vector2.Min(start, tb.min) - Vector2.one * wire.routeMargin;
        Vector2 max = Vector2.Max(start, tb.max) + Vector2.one * wire.routeMargin;
        int w = Mathf.CeilToInt((max.x - min.x) / Step) + 1;
        int h = Mathf.CeilToInt((max.y - min.y) / Step) + 1;
        if ((long)w * h > MaxCells) return "범위가 너무 큼 (" + w + "x" + h + "). Route Margin 을 줄이기";
        // 격자가 시작점에 정확히 맞도록 원점 조정
        min = start - new Vector2(Mathf.Round((start.x - min.x) / Step), Mathf.Round((start.y - min.y) / Step)) * Step;

        var ignore = new HashSet<Collider2D>(targetCols);
        foreach (var c in wire.GetComponentsInParent<Collider2D>()) ignore.Add(c);
        bool[] solid = BuildSolid(wire, min, w, h, ignore);
        float[] depth = DepthField(solid, w, h);

        // 목표 칸: 대상 테두리 바로 바깥(routeInset 이내)의 블록 칸. 가능하면 맞닿은 면의 가운데 줄만 (선이 대상 가운데에 닿게)
        float reach = wire.routeInset + Step;
        var goal = new bool[w * h];
        var goalMid = new bool[w * h];
        bool anyGoal = false, anyMid = false;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 p = min + new Vector2(x, y) * Step;
                if (!solid[y * w + x]) continue;
                foreach (var c in targetCols)
                {
                    Bounds b = c.bounds;
                    Vector2 cp = b.ClosestPoint(new Vector3(p.x, p.y, b.center.z));
                    if ((cp - p).sqrMagnitude < 1e-8f) continue; // 대상 안
                    if ((cp - p).magnitude > reach) continue;
                    goal[y * w + x] = true; anyGoal = true;
                    bool above = p.y > b.max.y || p.y < b.min.y, side = p.x > b.max.x || p.x < b.min.x;
                    if ((above && !side && Mathf.Abs(p.x - b.center.x) <= Step * 0.6f) ||
                        (side && !above && Mathf.Abs(p.y - b.center.y) <= Step * 0.6f)) { goalMid[y * w + x] = true; anyMid = true; }
                    break;
                }
            }
        if (!anyGoal) return "대상 옆에 블록이 없음 (대상이 블록에 붙어 있어야 선이 닿음)";
        if (anyMid) goal = goalMid;

        Vector2Int s = new Vector2Int(Mathf.RoundToInt((start.x - min.x) / Step), Mathf.RoundToInt((start.y - min.y) / Step));
        var cells = AStar(s, goal, solid, depth, w, h, wire.routeInset, tb, min);
        if (cells == null) return "경로를 못 찾음 (Route Margin 을 키워 보기)";

        // 꺾이는 점만 남기기
        var pts = new List<Vector2> { start };
        for (int i = 1; i < cells.Count - 1; i++)
        {
            Vector2Int d0 = cells[i] - cells[i - 1], d1 = cells[i + 1] - cells[i];
            if (d0 != d1) pts.Add(min + (Vector2)cells[i] * Step);
        }
        Vector2 last = min + (Vector2)cells[cells.Count - 1] * Step;
        if (cells.Count > 1) pts.Add(last);
        // 마지막: 대상 테두리까지 직선으로
        Vector2 end = ClosestOnTargets(targetCols, last);
        if (!Mathf.Approximately(end.x, last.x) && !Mathf.Approximately(end.y, last.y))
        {
            // 대각선이면 더 가까운 축 하나만 맞춤 (테두리 모서리)
            if (Mathf.Abs(end.x - last.x) < Mathf.Abs(end.y - last.y)) end.x = last.x; else end.y = last.y;
        }
        if ((end - last).sqrMagnitude > 1e-6f) pts.Add(end);

        // 소수점 정리 (0.05 단위)
        for (int i = 1; i < pts.Count; i++)
            pts[i] = new Vector2(Mathf.Round(pts[i].x * 100f) / 100f, Mathf.Round(pts[i].y * 100f) / 100f);
        // 겹친 점 제거
        for (int i = pts.Count - 1; i >= 1; i--)
            if ((pts[i] - pts[i - 1]).sqrMagnitude < 1e-6f) pts.RemoveAt(i);
        // 한 줄 위의 중간점 제거
        for (int i = pts.Count - 2; i >= 1; i--)
        {
            Vector2 a = pts[i - 1], b = pts[i], c = pts[i + 1];
            bool colinear = (Mathf.Approximately(a.x, b.x) && Mathf.Approximately(b.x, c.x)) || (Mathf.Approximately(a.y, b.y) && Mathf.Approximately(b.y, c.y));
            if (colinear || (a - b).sqrMagnitude < 1e-6f) pts.RemoveAt(i);
        }

        ApplyPoints(wire, pts);
        return null;
    }

    static List<Collider2D> FindTargetColliders(SignalWire wire, Vector2 start)
    {
        var list = new List<Collider2D>();
        if (wire.routeTarget != null)
        {
            foreach (var c in wire.routeTarget.GetComponentsInChildren<Collider2D>()) if (!c.isTrigger) list.Add(c);
            if (list.Count == 0) list.AddRange(wire.routeTarget.GetComponentsInChildren<Collider2D>());
            return list;
        }
        int id = wire.signalId;
        if (wire.useParentSignal && wire.transform.parent != null)
        {
            var lever = wire.transform.parent.GetComponent<Lever>();
            var button = wire.transform.parent.GetComponent<PressButton>();
            if (lever != null) id = lever.signalId; else if (button != null) id = button.signalId;
        }
        SignalReceiver best = null; float bestD = float.MaxValue;
        foreach (var r in Object.FindObjectsByType<SignalReceiver>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (r.gameObject.scene != wire.gameObject.scene || !r.signalIds.Contains(id)) continue;
            var cols = r.GetComponentsInChildren<Collider2D>();
            if (cols.Length == 0) continue;
            float d = float.MaxValue;
            foreach (var c in cols) d = Mathf.Min(d, ((Vector2)c.bounds.ClosestPoint(start) - start).magnitude);
            if (d < bestD) { bestD = d; best = r; }
        }
        if (best == null) return list;
        foreach (var c in best.GetComponentsInChildren<Collider2D>()) if (!c.isTrigger) list.Add(c);
        if (list.Count == 0) list.AddRange(best.GetComponentsInChildren<Collider2D>());
        return list;
    }

    /// <summary>각 칸이 블록 안인가. 퍼즐 오브젝트(문·404·버튼·상자·레버)와 움직이는 물체는 블록으로 안 침.</summary>
    static bool[] BuildSolid(SignalWire wire, Vector2 min, int w, int h, HashSet<Collider2D> ignore)
    {
        var solid = new bool[w * h];
        var cols = new List<Collider2D>();
        Physics2D.SyncTransforms();
        Vector2 max = min + new Vector2(w, h) * Step;
        foreach (var c in Physics2D.OverlapAreaAll(min, max, wire.routeLayers))
        {
            if (c.isTrigger || ignore.Contains(c)) continue;
            if (c.attachedRigidbody != null && c.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic) continue;
            if (c.GetComponentInParent<SignalReceiver>() || c.GetComponentInParent<PressButton>() || c.GetComponentInParent<PullBox>() || c.GetComponentInParent<Lever>()) continue;
            cols.Add(c);
        }
        foreach (var c in cols)
        {
            Bounds b = c.bounds;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((b.min.x - min.x) / Step)), x1 = Mathf.Min(w - 1, Mathf.CeilToInt((b.max.x - min.x) / Step));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((b.min.y - min.y) / Step)), y1 = Mathf.Min(h - 1, Mathf.CeilToInt((b.max.y - min.y) / Step));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int i = y * w + x;
                    if (solid[i]) continue;
                    if (c.OverlapPoint(min + new Vector2(x, y) * Step)) solid[i] = true;
                }
        }
        return solid;
    }

    /// <summary>블록 칸마다 가장 가까운 허공까지 거리(units, 체비쇼프). 허공은 0.</summary>
    static float[] DepthField(bool[] solid, int w, int h)
    {
        var dist = new int[w * h];
        var q = new Queue<int>();
        for (int i = 0; i < dist.Length; i++)
        {
            if (!solid[i]) { dist[i] = 0; q.Enqueue(i); }
            else dist[i] = int.MaxValue;
        }
        while (q.Count > 0)
        {
            int i = q.Dequeue();
            int x = i % w, y = i / w;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int j = ny * w + nx;
                    if (dist[j] > dist[i] + 1) { dist[j] = dist[i] + 1; q.Enqueue(j); }
                }
        }
        var d = new float[w * h];
        // 표면에 걸친 칸이 1 이므로 1 을 빼서 '표면에서 거리'로
        for (int i = 0; i < d.Length; i++) d[i] = dist[i] == int.MaxValue ? 999f : Mathf.Max(0, dist[i] - 1) * Step;
        return d;
    }

    static List<Vector2Int> AStar(Vector2Int s, bool[] goal, bool[] solid, float[] depth, int w, int h, float inset, Bounds tb, Vector2 min)
    {
        int n = w * h * 4;
        var g = new float[n];
        var from = new int[n];
        var closed = new bool[n];
        for (int i = 0; i < n; i++) { g[i] = float.MaxValue; from[i] = -1; }
        var heap = new MinHeap();
        Vector2 tc = tb.center;
        System.Func<int, int, float> heur = (x, y) =>
        {
            Vector2 p = min + new Vector2(x, y) * Step;
            Vector2 cp = tb.ClosestPoint(new Vector3(p.x, p.y, tb.center.z));
            return Mathf.Max(0f, Mathf.Abs(cp.x - p.x) + Mathf.Abs(cp.y - p.y) - inset - Step);
        };
        int sc = s.y * w + s.x;
        for (int d = 0; d < 4; d++) { int st = sc * 4 + d; g[st] = 0f; heap.Push(st, heur(s.x, s.y)); }
        int found = -1;
        while (heap.Count > 0)
        {
            int st = heap.Pop();
            if (closed[st]) continue;
            closed[st] = true;
            int cell = st / 4, dir = st % 4;
            if (goal[cell]) { found = st; break; }
            int x = cell % w, y = cell / w;
            for (int nd = 0; nd < 4; nd++)
            {
                if ((nd + 2) % 4 == dir) continue; // 되돌아가기 금지
                int nx = x + Dirs[nd].x, ny = y + Dirs[nd].y;
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                int nc = ny * w + nx;
                // 표면에 너무 가까우면 비싸고, 더 깊은 건 조금만 (얇은 기둥 한가운데로 곧게 지나가도 되게)
                float dd = depth[nc] - inset;
                float step = Step * (solid[nc] ? 1f + (dd < 0f ? -dd * DepthCost : Mathf.Min(dd, 3f) * DepthCost * 0.5f) : AirCost);
                float cost = g[st] + step + (nd != dir && from[st] >= 0 ? TurnCost : 0f);
                int ns = nc * 4 + nd;
                if (cost < g[ns]) { g[ns] = cost; from[ns] = st; heap.Push(ns, cost + heur(nx, ny)); }
            }
        }
        if (found < 0) return null;
        var path = new List<Vector2Int>();
        for (int st = found; st >= 0; st = from[st]) { int c = st / 4; path.Add(new Vector2Int(c % w, c / w)); }
        path.Reverse();
        return path;
    }

    static Vector2 ClosestOnTargets(List<Collider2D> cols, Vector2 p)
    {
        Vector2 best = p; float bd = float.MaxValue;
        foreach (var c in cols)
        {
            Vector2 cp = c.bounds.ClosestPoint(new Vector3(p.x, p.y, c.bounds.center.z));
            float d = (cp - p).sqrMagnitude;
            if (d < bd) { bd = d; best = cp; }
        }
        return best;
    }

    /// <summary>자식 꺾이는 점들을 pts 로 바꾼다. 기존 점은 재사용하고, 모자라면 만들고, 남으면 지운다 (지울 수 없는 프리팹 점은 끝점에 겹쳐 둠).</summary>
    static void ApplyPoints(SignalWire wire, List<Vector2> pts)
    {
        Undo.SetCurrentGroupName("자동 경로");
        int group = Undo.GetCurrentGroup();
        var kids = new List<Transform>();
        for (int i = 0; i < wire.transform.childCount; i++)
        {
            var c = wire.transform.GetChild(i);
            if (c.name != "_WireFx") kids.Add(c);
        }
        for (int i = 0; i < pts.Count; i++)
        {
            Transform t;
            if (i < kids.Count) t = kids[i];
            else
            {
                var go = new GameObject("P" + i);
                Undo.RegisterCreatedObjectUndo(go, "자동 경로");
                Undo.SetTransformParent(go.transform, wire.transform, "자동 경로");
                t = go.transform;
            }
            Undo.RecordObject(t, "자동 경로");
            t.position = new Vector3(pts[i].x, pts[i].y, wire.transform.position.z);
        }
        for (int i = kids.Count - 1; i >= pts.Count; i--)
        {
            var k = kids[i];
            if (PrefabUtility.IsPartOfPrefabInstance(k.gameObject) && !PrefabUtility.IsAddedGameObjectOverride(k.gameObject))
            {
                // 프리팹 원래 점: 지우지 않고 끝점에 겹쳐 둠 (길이 0 이라 안 보임)
                Undo.RecordObject(k, "자동 경로");
                k.position = new Vector3(pts[pts.Count - 1].x, pts[pts.Count - 1].y, wire.transform.position.z);
            }
            else Undo.DestroyObjectImmediate(k.gameObject);
        }
        Undo.CollapseUndoOperations(group);
        EditorUtility.SetDirty(wire);
    }

    /// <summary>간단한 이진 힙 (우선순위 큐).</summary>
    class MinHeap
    {
        readonly List<int> items = new List<int>();
        readonly List<float> keys = new List<float>();
        public int Count => items.Count;
        public void Push(int item, float key)
        {
            items.Add(item); keys.Add(key);
            int i = items.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (keys[p] <= keys[i]) break;
                Swap(i, p); i = p;
            }
        }
        public int Pop()
        {
            int top = items[0];
            int last = items.Count - 1;
            items[0] = items[last]; keys[0] = keys[last];
            items.RemoveAt(last); keys.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, m = i;
                if (l < items.Count && keys[l] < keys[m]) m = l;
                if (r < items.Count && keys[r] < keys[m]) m = r;
                if (m == i) break;
                Swap(i, m); i = m;
            }
            return top;
        }
        void Swap(int a, int b)
        {
            int ti = items[a]; items[a] = items[b]; items[b] = ti;
            float tk = keys[a]; keys[a] = keys[b]; keys[b] = tk;
        }
    }
}
