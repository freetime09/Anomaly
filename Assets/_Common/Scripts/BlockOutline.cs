using UnityEngine;

/// <summary>
/// 블럭(스케일로 늘린 단색 스프라이트)에 테두리를 두른다.
/// 블럭보다 thickness만큼 큰 자식 스프라이트를 블럭 뒤(sortingOrder-1)에 깔아서 가장자리만 보이게 한다.
/// 블럭끼리 붙어 있으면 이웃 블럭이 테두리를 덮어서 바깥 윤곽만 남는다.
/// 블럭 크기를 바꿔도 테두리 두께(월드 단위)는 그대로 유지된다.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class BlockOutline : MonoBehaviour
{
    [Tooltip("테두리 색")]
    public Color color = Color.white;
    [Tooltip("테두리 두께 (월드 단위)")]
    public float thickness = 0.08f;

    const string ChildName = "Outline";
    SpriteRenderer sr;
    SpriteRenderer outline;

    void OnEnable()
    {
        sr = GetComponent<SpriteRenderer>();
        var t = transform.Find(ChildName);
        if (t == null)
        {
            var go = new GameObject(ChildName);
            go.transform.SetParent(transform, false);
            t = go.transform;
        }
        outline = t.GetComponent<SpriteRenderer>();
        if (outline == null) outline = t.gameObject.AddComponent<SpriteRenderer>();
        outline.enabled = true;
        Sync();
    }

    void OnDisable()
    {
        if (outline != null) outline.enabled = false;
    }

    void LateUpdate() { Sync(); }
    void OnValidate() { if (isActiveAndEnabled && outline != null) Sync(); }

    void Sync()
    {
        if (sr == null || outline == null) return;
        outline.sprite = sr.sprite;
        outline.color = color;
        outline.sortingLayerID = sr.sortingLayerID;
        outline.sortingOrder = sr.sortingOrder - 1;

        // 부모 스케일로 나눠서 월드 기준으로 thickness*2 만큼만 커지게
        Vector3 s = transform.lossyScale;
        float sx = Mathf.Abs(s.x) < 1e-5f ? 1f : Mathf.Abs(s.x);
        float sy = Mathf.Abs(s.y) < 1e-5f ? 1f : Mathf.Abs(s.y);
        Vector2 size = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
        if (size.x < 1e-5f) size.x = 1f;
        if (size.y < 1e-5f) size.y = 1f;
        var ot = outline.transform;
        ot.localPosition = Vector3.zero;
        ot.localRotation = Quaternion.identity;
        ot.localScale = new Vector3(1f + 2f * thickness / (sx * size.x), 1f + 2f * thickness / (sy * size.y), 1f);
    }
}
