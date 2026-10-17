using UnityEngine;

/// <summary>
/// 배경을 뿌옇게: 블럭 뒤(백신보다도 뒤)에 은은한 안개 층을 깐다. 카메라에 붙인다.
///  - 안개 무늬는 코드로 만든 이어지는 노이즈 (그림 필요 없음).
///  - 카메라보다 느리게 움직이고(parallax) 천천히 흘러서 멀리 있는 안개처럼 보인다.
/// 배경색 자체는 카메라 Background 색으로 정한다.
/// </summary>
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(1000)] // 카메라 이동(CameraFollow) 뒤에 따라감
public class BackgroundHaze : MonoBehaviour
{
    [Tooltip("안개 색 (알파 = 짙기)")]
    public Color color = new Color(0.11f, 0.11f, 0.13f, 0.5f);
    [Tooltip("안개 무늬 크기 (유닛). 클수록 넓게 뭉친 안개")]
    public float tileSize = 24f;
    [Tooltip("카메라를 따라오는 정도 (0 = 세상에 붙음, 1 = 화면에 붙음). 클수록 멀리 있는 느낌")]
    [Range(0f, 1f)] public float parallax = 0.7f;
    [Tooltip("안개가 흐르는 속도 (유닛/초)")]
    public Vector2 drift = new Vector2(0.4f, 0.1f);
    [Tooltip("그릴 정렬 레이어 / 순서 (블럭과 백신보다 뒤)")]
    public string sortingLayer = "Vaccine";
    public int sortingOrder = -100;

    Camera cam;
    SpriteRenderer sr;
    Texture2D tex;

    void OnEnable()
    {
        cam = GetComponent<Camera>();
        if (sr == null)
        {
            tex = MakeNoise(128, 6, 3);
            var go = new GameObject("BackgroundHaze");
            go.hideFlags = HideFlags.DontSave;
            sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width, 0, SpriteMeshType.FullRect); // 타일 모드는 FullRect 필요
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
        }
        sr.gameObject.SetActive(true);
    }

    void OnDisable()
    {
        if (sr != null) sr.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (sr != null) Destroy(sr.gameObject);
        if (tex != null) Destroy(tex);
    }

    void LateUpdate()
    {
        if (sr == null) return;
        sr.color = color;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = sortingOrder;
        float t = Mathf.Max(1f, tileSize);
        sr.transform.localScale = new Vector3(t, t, 1f); // 무늬 한 장 = tileSize
        float h = cam.orthographicSize * 2f, w = h * cam.aspect;
        sr.size = new Vector2(w / t + 2f, h / t + 2f);    // 화면 + 양옆 한 장씩
        // 카메라보다 (1 - parallax) 만큼 느리게 + 흐름. 무늬 한 장 단위로 되감아 끊김 없이
        Vector3 c = cam.transform.position;
        float ox = Mathf.Repeat(c.x * (1f - parallax) - Time.time * drift.x, t);
        float oy = Mathf.Repeat(c.y * (1f - parallax) - Time.time * drift.y, t);
        sr.transform.position = new Vector3(c.x - ox + t * 0.5f, c.y - oy + t * 0.5f, 0f);
    }

    /// <summary>가장자리가 이어지는 부드러운 노이즈 (흰색, 알파에 무늬).</summary>
    static Texture2D MakeNoise(int size, int cells, int octaves)
    {
        var px = new Color32[size * size];
        var rnd = new System.Random(11);
        var vals = new float[octaves][];
        for (int o = 0; o < octaves; o++)
        {
            int n = cells << o;
            vals[o] = new float[n * n];
            for (int i = 0; i < vals[o].Length; i++) vals[o][i] = (float)rnd.NextDouble();
        }
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float v = 0f, amp = 1f, sum = 0f;
                for (int o = 0; o < octaves; o++)
                {
                    int n = cells << o;
                    float fx = (float)x / size * n, fy = (float)y / size * n;
                    int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
                    float tx = fx - x0, ty = fy - y0;
                    tx = tx * tx * (3f - 2f * tx); ty = ty * ty * (3f - 2f * ty);
                    float a = vals[o][(y0 % n) * n + x0 % n], b = vals[o][(y0 % n) * n + (x0 + 1) % n];
                    float c = vals[o][((y0 + 1) % n) * n + x0 % n], d = vals[o][((y0 + 1) % n) * n + (x0 + 1) % n];
                    v += Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty) * amp;
                    sum += amp; amp *= 0.5f;
                }
                v /= sum;
                // 대비를 조금 키워 뭉친 안개 + 옅은 바탕
                float alpha = Mathf.Clamp01(0.35f + (v - 0.5f) * 1.6f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
