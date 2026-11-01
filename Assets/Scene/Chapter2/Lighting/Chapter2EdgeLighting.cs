using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Emission/bloom use the existing outlines. This pool adds only real lights, never sprite copies.
[ExecuteAlways]
public sealed class Chapter2EdgeLighting : MonoBehaviour
{
    public BlockOutline[] surfaces;
    [Range(1, 16)] public int maxLights = 10;
    [Range(0, 1)] public float reflectedIntensity = .22f;
    [Range(.1f, 2)] public float lightReach = .85f;
    public Color reflectedColor = new Color(.9f, .95f, 1f);

    struct Edge
    {
        public bool horizontal;
        public float fixedAxis, from, to, distance;
    }

    readonly List<Edge> edges = new List<Edge>(128);
    readonly List<Vector2> ranges = new List<Vector2>(16);
    SpriteRenderer[] renderers;
    Bounds[] bounds;
    bool[] active;
    Light2D[] lights;
    Vector3[][] paths;
    float[] lengths;
    Transform lightRoot;
    Camera view;
    PlayerController player;
    float nextRefresh;
    Rect window;
    Vector2 focus;

    void OnEnable()
    {
        if (surfaces == null) return;
        view = Camera.main;
        player = FindFirstObjectByType<PlayerController>();
        renderers = new SpriteRenderer[surfaces.Length];
        bounds = new Bounds[surfaces.Length];
        active = new bool[surfaces.Length];
        for (int i = 0; i < surfaces.Length; i++)
            if (surfaces[i] != null) renderers[i] = surfaces[i].GetComponent<SpriteRenderer>();

        var root = new GameObject("EdgeReflectionLights");
        root.hideFlags = HideFlags.DontSave;
        lightRoot = root.transform;
        lightRoot.SetParent(transform, false);
        lights = new Light2D[maxLights];
        paths = new Vector3[maxLights][];
        lengths = new float[maxLights];
        for (int i = 0; i < maxLights; i++)
        {
            var go = new GameObject("EdgeBounce" + i);
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(lightRoot, false);
            go.SetActive(false);
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Freeform;
            light.blendStyleIndex = 0;
            light.color = reflectedColor;
            light.intensity = reflectedIntensity;
            light.shapeLightFalloffSize = lightReach;
            light.falloffIntensity = .65f;
            light.shadowIntensity = 0;
            light.targetSortingLayers = new[] { SortingLayer.NameToID("Default") };
            lights[i] = light;
            paths[i] = new Vector3[4];
        }
        nextRefresh = 0;
    }

    void LateUpdate()
    {
        if (lights == null || view == null || Time.realtimeSinceStartup < nextRefresh) return;
        nextRefresh = Time.realtimeSinceStartup + .05f;
        float h = view.orthographicSize + lightReach;
        float w = view.orthographicSize * view.aspect + lightReach;
        var position = view.transform.position;
        window = Rect.MinMaxRect(position.x-w, position.y-h, position.x+w, position.y+h);
        focus = player != null ? (Vector2)player.transform.position : (Vector2)position;
        for (int i = 0; i < renderers.Length; i++)
        {
            active[i] = renderers[i] != null && renderers[i].enabled && renderers[i].gameObject.activeInHierarchy;
            if (active[i]) bounds[i] = renderers[i].bounds;
        }

        edges.Clear();
        for (int i = 0; i < bounds.Length; i++)
        {
            if (!active[i]) continue;
            var b = bounds[i];
            float inset = surfaces[i].thickness + .012f;
            Exposed(i, true, b.max.y + inset, b.min.x, b.max.x);
            Exposed(i, true, b.min.y - inset, b.min.x, b.max.x);
            Exposed(i, false, b.min.x - inset, b.min.y, b.max.y);
            Exposed(i, false, b.max.x + inset, b.min.y, b.max.y);
        }
        edges.Sort(Compare);
        for (int i = 0; i < lights.Length; i++)
        {
            bool used = i < edges.Count;
            if (lights[i].gameObject.activeSelf != used) lights[i].gameObject.SetActive(used);
            if (!used) continue;
            var edge = edges[i];
            float length = edge.to - edge.from;
            var light = lights[i];
            light.transform.position = edge.horizontal
                ? new Vector3((edge.from+edge.to)*.5f, edge.fixedAxis, -.2f)
                : new Vector3(edge.fixedAxis, (edge.from+edge.to)*.5f, -.2f);
            if (Mathf.Abs(lengths[i] - length) > .015f || ((paths[i][1]-paths[i][0]).x > .04f) != edge.horizontal)
            {
                float x = edge.horizontal ? length*.5f : .018f;
                float y = edge.horizontal ? .018f : length*.5f;
                var path = paths[i];
                path[0] = new Vector3(-x,-y); path[1] = new Vector3(x,-y);
                path[2] = new Vector3(x,y); path[3] = new Vector3(-x,y);
                light.SetShapePath(path);
                lengths[i] = length;
            }
            light.color = reflectedColor;
            light.intensity = reflectedIntensity;
            light.shapeLightFalloffSize = lightReach;
        }
    }

    void Exposed(int own, bool horizontal, float fixedAxis, float start, float end)
    {
        if (fixedAxis < (horizontal ? window.yMin : window.xMin) || fixedAxis > (horizontal ? window.yMax : window.xMax)) return;
        start = Mathf.Max(start, horizontal ? window.xMin : window.yMin);
        end = Mathf.Min(end, horizontal ? window.xMax : window.yMax);
        if (end-start < .08f) return;
        ranges.Clear();
        ranges.Add(new Vector2(start, end));
        for (int j = 0; j < bounds.Length; j++)
        {
            if (j == own || !active[j]) continue;
            var b = bounds[j];
            if (fixedAxis < (horizontal ? b.min.y : b.min.x) || fixedAxis > (horizontal ? b.max.y : b.max.x)) continue;
            float from = horizontal ? b.min.x : b.min.y, to = horizontal ? b.max.x : b.max.y;
            for (int r = ranges.Count-1; r >= 0; r--)
            {
                var range = ranges[r];
                if (to <= range.x || from >= range.y) continue;
                ranges.RemoveAt(r);
                if (from > range.x) ranges.Add(new Vector2(range.x, from));
                if (to < range.y) ranges.Add(new Vector2(to, range.y));
            }
        }
        foreach (var range in ranges)
        {
            if (range.y-range.x < .08f) continue;
            var nearest = horizontal ? new Vector2(Mathf.Clamp(focus.x,range.x,range.y),fixedAxis) : new Vector2(fixedAxis,Mathf.Clamp(focus.y,range.x,range.y));
            edges.Add(new Edge { horizontal=horizontal, fixedAxis=fixedAxis, from=range.x, to=range.y, distance=(nearest-focus).sqrMagnitude });
        }
    }

    static int Compare(Edge a, Edge b) { return a.distance.CompareTo(b.distance); }

    void OnDisable()
    {
        if (lightRoot != null)
        {
            if (Application.isPlaying) Destroy(lightRoot.gameObject);
            else DestroyImmediate(lightRoot.gameObject);
        }
        lightRoot = null;
        lights = null;
    }
}
