using UnityEngine;

/// <summary>
/// 스프라이트 외곽선을 빛나게 (ㄱ 나뭇가지 휘두르기로 칠 수 있는 오브젝트 표시용).
/// 같은 스프라이트를 단색 실루엣으로 사방에 살짝 밀어 뒤에 깔아 테두리를 만들고, 밝기를 천천히 깜빡인다.
///  - 대상 스프라이트가 꺼지면(부서진 바위 등) 같이 꺼진다. 쓰러진 통나무(Chapter1FallingLog)도 꺼진다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpriteOutlineGlow : MonoBehaviour
{
    [Tooltip("테두리를 두를 스프라이트 (비우면 이 오브젝트의 SpriteRenderer)")]
    public SpriteRenderer target;
    [Tooltip("테두리 색")]
    public Color color = new Color(1f, 0.86f, 0.35f, 1f);
    [Tooltip("테두리 두께 (월드 단위). 바깥에 두 배 두께로 흐린 빛이 한 겹 더 깔림")]
    public float thickness = 0.06f;
    [Tooltip("깜빡이는 빠르기")]
    public float pulseSpeed = 2.2f;
    [Range(0f, 1f), Tooltip("가장 어두울 때 밝기")]
    public float minAlpha = 0.35f;

    static readonly Vector2[] Dirs = {
        new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1),
        new Vector2(.707f, .707f), new Vector2(-.707f, .707f), new Vector2(.707f, -.707f), new Vector2(-.707f, -.707f) };
    SpriteRenderer[] copies;
    Material silhouette;
    Chapter1FallingLog log;

    void Awake()
    {
        if (target == null) target = GetComponent<SpriteRenderer>();
        log = GetComponentInParent<Chapter1FallingLog>();
        if (target == null) { enabled = false; return; }
        // 텍스처 알파만 쓰고 색은 단색 → 실루엣 (빌드에 셰이더가 빠지지 않게 Resources 머티리얼 우선)
        var template = Resources.Load<Material>("OutlineGlowSilhouette");
        var shader = template != null ? template.shader : Shader.Find("GUI/Text Shader");
        if (shader != null) silhouette = new Material(shader);
        var root = new GameObject("OutlineGlow").transform;
        root.SetParent(target.transform, false);
        copies = new SpriteRenderer[Dirs.Length * 2];
        for (int i = 0; i < copies.Length; i++)
        {
            var sr = new GameObject("Glow" + i).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(root, false);
            if (silhouette != null) sr.sharedMaterial = silhouette;
            copies[i] = sr;
        }
    }

    void LateUpdate()
    {
        if (copies == null) return;
        bool on = target.enabled && target.gameObject.activeInHierarchy && target.sprite != null && (log == null || !log.Fallen);
        float pulse = Mathf.Lerp(minAlpha, 1f, 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed));
        for (int i = 0; i < copies.Length; i++)
        {
            var sr = copies[i];
            if (sr.enabled != on) sr.enabled = on;
            if (!on) continue;
            bool outer = i >= Dirs.Length;
            sr.sprite = target.sprite;
            sr.flipX = target.flipX; sr.flipY = target.flipY;
            sr.sortingLayerID = target.sortingLayerID;
            sr.sortingOrder = target.sortingOrder - (outer ? 2 : 1);
            sr.color = new Color(color.r, color.g, color.b, color.a * pulse * (outer ? 0.35f : 0.9f));
            var t = sr.transform;
            t.position = target.transform.position + (Vector3)(Dirs[i % Dirs.Length] * thickness * (outer ? 2f : 1f));
            t.rotation = target.transform.rotation;
            var ls = target.transform.lossyScale; var ps = t.parent.lossyScale;
            t.localScale = new Vector3(ps.x != 0 ? ls.x / ps.x : 1f, ps.y != 0 ? ls.y / ps.y : 1f, 1f);
        }
    }

    void OnDestroy() { if (silhouette != null) Destroy(silhouette); }
}
