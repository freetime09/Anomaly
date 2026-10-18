using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캡쳐 그림의 일부(폴더 이름 등)가 가끔 짧게 지지직거리게 한다. 캡쳐 배경(SpriteRenderer)에 붙인다.
///  - 그 자리 그림을 가로 줄로 잘라 좌우로 흔들고, 빨강·청록 잔상을 살짝 어긋나게 겹친다.
///  - 지금 배경에 보이는 캡쳐(sprite)와 같은 항목만 켜진다 → 캡쳐가 바뀌면 그 캡쳐의 자리들이 지지직.
///  - 배경이 어두워지면(검게 사라지는 연출) 같이 어두워진다.
/// 위치는 캡쳐 픽셀 좌표(왼쪽 위 = 0,0)로 적는다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class CaptureGlitchRegions : MonoBehaviour
{
    [System.Serializable]
    public class Region
    {
        [Tooltip("이 캡쳐가 보일 때만 지지직")]
        public Sprite capture;
        [Tooltip("지지직거릴 사각형: x, y = 왼쪽 위, z, w = 너비, 높이 (캡쳐 픽셀)")]
        public Vector4 pixelRect;
    }

    public Region[] regions;

    [Header("지지직")]
    [Tooltip("지지직 사이 간격 (초, 최소~최대)")]
    public Vector2 interval = new Vector2(0.4f, 1.6f);
    [Tooltip("한 번 지지직하는 시간 (초, 최소~최대)")]
    public Vector2 burstTime = new Vector2(0.08f, 0.25f);
    [Tooltip("가로로 잘라 흔들 줄 수")]
    [Min(1)] public int slices = 4;
    [Tooltip("줄이 좌우로 밀리는 최대 거리 (캡쳐 픽셀)")]
    public float maxShift = 4f;
    [Tooltip("빨강·청록 잔상 어긋남 (캡쳐 픽셀)")]
    public float colorSplit = 2f;
    [Range(0f, 1f)] public float splitAlpha = 0.6f;

    class Live
    {
        public Region region;
        public GameObject root;
        public SpriteRenderer[] strips;
        public float[] stripBaseX;
        public SpriteRenderer red, cyan;
        public float timer;     // 다음 지지직까지
        public float burstLeft; // 지지직 남은 시간 (0보다 크면 지지직 중)
    }

    SpriteRenderer bg;
    readonly List<Live> lives = new List<Live>();

    void Awake()
    {
        bg = GetComponent<SpriteRenderer>();
        foreach (var r in regions)
            if (r != null && r.capture != null && r.pixelRect.z > 0f && r.pixelRect.w > 0f) lives.Add(Build(r));
    }

    Live Build(Region r)
    {
        var lv = new Live { region = r };
        lv.root = new GameObject("Glitch_" + r.capture.name);
        lv.root.transform.SetParent(transform, false);
        Texture2D tex = r.capture.texture;
        Rect sr = r.capture.textureRect;               // 캡쳐 스프라이트가 텍스처에서 차지하는 영역
        float ppu = r.capture.pixelsPerUnit;
        // 캡쳐 픽셀(왼쪽 위 기준) → 텍스처 좌표(왼쪽 아래 기준)
        float tx = sr.x + r.pixelRect.x, ty = sr.y + (sr.height - r.pixelRect.y - r.pixelRect.w);
        float w = r.pixelRect.z, h = r.pixelRect.w;
        Vector2 center = PixelToLocal(r.capture, new Vector2(r.pixelRect.x + w * 0.5f, r.pixelRect.y + h * 0.5f));
        lv.root.transform.localPosition = center;

        // 잔상 (전체 영역)
        lv.red = NewPart(lv.root.transform, Sprite.Create(tex, new Rect(tx, ty, w, h), new Vector2(0.5f, 0.5f), ppu), 1);
        lv.cyan = NewPart(lv.root.transform, Sprite.Create(tex, new Rect(tx, ty, w, h), new Vector2(0.5f, 0.5f), ppu), 1);

        // 가로 줄 조각
        int n = Mathf.Max(1, slices);
        lv.strips = new SpriteRenderer[n];
        lv.stripBaseX = new float[n];
        float sh = h / n;
        for (int i = 0; i < n; i++)
        {
            float y0 = Mathf.Round(sh * i), y1 = Mathf.Round(sh * (i + 1));
            if (y1 - y0 < 1f) y1 = y0 + 1f;
            var s = Sprite.Create(tex, new Rect(tx, ty + y0, w, y1 - y0), new Vector2(0.5f, 0.5f), ppu);
            var part = NewPart(lv.root.transform, s, 2);
            part.transform.localPosition = new Vector3(0f, ((y0 + y1) * 0.5f - h * 0.5f) / ppu, 0f);
            lv.strips[i] = part;
        }
        lv.root.SetActive(false);
        lv.timer = Random.Range(interval.x, interval.y);
        return lv;
    }

    SpriteRenderer NewPart(Transform parent, Sprite s, int orderOffset)
    {
        var go = new GameObject("Part");
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = s;
        r.sortingLayerID = bg.sortingLayerID;
        r.sortingOrder = bg.sortingOrder + orderOffset;
        return r;
    }

    void Update()
    {
        foreach (var lv in lives)
        {
            bool showing = bg.enabled && bg.gameObject.activeInHierarchy && bg.sprite == lv.region.capture;
            if (!showing) { if (lv.root.activeSelf) lv.root.SetActive(false); continue; }

            if (lv.burstLeft <= 0f)
            {
                lv.timer -= Time.deltaTime;
                if (lv.timer > 0f) { if (lv.root.activeSelf) lv.root.SetActive(false); continue; }
                lv.burstLeft = Random.Range(burstTime.x, burstTime.y); // 지지직 시작
            }
            lv.burstLeft -= Time.deltaTime;
            if (lv.burstLeft <= 0f)
            {
                lv.timer = Random.Range(interval.x, interval.y); // 지지직 끝
                lv.root.SetActive(false);
                continue;
            }
            if (!lv.root.activeSelf) lv.root.SetActive(true);
            Animate(lv);
        }
    }

    void Animate(Live lv)
    {
        float ppu = lv.region.capture.pixelsPerUnit;
        Color tint = bg.color; // 배경이 어두워지면 같이
        foreach (var s in lv.strips)
        {
            float dx = Random.value < 0.6f ? Random.Range(-maxShift, maxShift) / ppu : 0f;
            var p = s.transform.localPosition; p.x = dx; s.transform.localPosition = p;
            s.color = Random.value < 0.1f ? new Color(tint.r, tint.g, tint.b, 0f) : tint; // 가끔 한 줄이 빠짐
        }
        float split = colorSplit / ppu * Random.Range(0.5f, 1.5f);
        lv.red.transform.localPosition = new Vector3(-split, 0f, 0f);
        lv.cyan.transform.localPosition = new Vector3(split, 0f, 0f);
        lv.red.color = new Color(tint.r, 0f, 0f, splitAlpha);
        lv.cyan.color = new Color(0f, tint.g, tint.b, splitAlpha);
    }

    /// <summary>캡쳐 픽셀 좌표(왼쪽 위 = 0,0) → 배경 로컬 좌표.</summary>
    static Vector2 PixelToLocal(Sprite sp, Vector2 px)
    {
        Rect r = sp.rect;
        return new Vector2(px.x - sp.pivot.x, (r.height - px.y) - sp.pivot.y) / sp.pixelsPerUnit;
    }
}
