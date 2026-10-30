using UnityEngine;
public sealed class BossDragSelection : MonoBehaviour
{
    SpriteRenderer fill;
    LineRenderer border;
    Material material;
    Vector2 origin;
    public void Initialize(SpriteRenderer renderer, Vector2 topLeft)
    {
        fill = renderer;
        origin = topLeft;
        border = new GameObject("SelectionBorder").AddComponent<LineRenderer>();
        border.transform.SetParent(transform, false);
        material = new Material(Shader.Find("Sprites/Default"));
        border.sharedMaterial = material;
        border.useWorldSpace = true;
        border.loop = true;
        border.positionCount = 4;
        border.widthMultiplier = .045f;
        border.numCornerVertices = 0;
        border.sortingOrder = 22;
        border.startColor = border.endColor = new Color(1f, .12f, .12f, .95f);
        SetCorner(origin);
    }
    public void SetCorner(Vector2 corner)
    {
        Vector2 size = new Vector2(Mathf.Max(.025f, Mathf.Abs(corner.x-origin.x)), Mathf.Max(.025f, Mathf.Abs(corner.y-origin.y)));
        fill.transform.position = (origin+corner)*.5f;
        fill.transform.localScale = new Vector3(size.x, size.y, 1f);
        fill.color = new Color(.43f,.025f,.025f,1f);
        border.SetPosition(0, origin);
        border.SetPosition(1, new Vector3(corner.x, origin.y, 0f));
        border.SetPosition(2, corner);
        border.SetPosition(3, new Vector3(origin.x, corner.y, 0f));
    }
    public void Activate() { border.widthMultiplier = .065f; }
    void LateUpdate()
    {
        if (fill == null || border == null) return;
        var c = border.startColor;
        c.a = Mathf.Clamp01(fill.color.a) * .95f;
        border.startColor = border.endColor = c;
    }
    void OnDestroy() { if (material != null) Destroy(material); }
}
