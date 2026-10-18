using UnityEngine;

/// <summary>
/// 카메라가 지정한 스프라이트(캡쳐 배경) 밖을 비추지 않게 위치를 가둔다. 카메라에 붙인다.
/// CameraFollow 가 움직인 뒤에 실행된다. 스프라이트가 꺼지면(검은 화면 뒤) 더 이상 가두지 않는다.
/// </summary>
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(1000)]
public class CameraClampToSprite : MonoBehaviour
{
    public SpriteRenderer area;

    Camera cam;

    void Awake() { cam = GetComponent<Camera>(); }

    void LateUpdate()
    {
        if (area == null || !area.gameObject.activeInHierarchy) return;
        Bounds b = area.bounds;
        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector3 p = transform.position;
        p.x = b.size.x > w * 2f ? Mathf.Clamp(p.x, b.min.x + w, b.max.x - w) : b.center.x;
        p.y = b.size.y > h * 2f ? Mathf.Clamp(p.y, b.min.y + h, b.max.y - h) : b.center.y;
        transform.position = p;
    }
}
