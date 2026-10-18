using UnityEngine;

/// <summary>
/// 직교(2D) 카메라의 자식으로 두고, 화면(뷰포트) 구석에 HUD를 고정한다.
///  - viewportAnchor: (0,0)=왼쪽아래, (1,1)=오른쪽위, (0.5,0.5)=중앙.
///  - worldOffset: 그 구석에서 안쪽으로 밀어줄 여유(월드 유닛).
///  - depth: 카메라 앞쪽 거리(정렬은 sortingOrder로, 이건 프러스텀 안에 두기 위함).
/// 카메라 오쏘 사이즈/화면 비율이 바뀌어도 매 프레임 다시 계산해 구석에 붙어있는다.
/// 카메라와 함께 움직이므로(부모가 카메라) 카메라 추종 순서와 무관하게 흔들리지 않는다.
/// </summary>
[DefaultExecutionOrder(1000)] // 카메라 추종(CameraFollow) 이후에 배치되도록 늦게 실행
public class CameraHudAnchor : MonoBehaviour
{
    [Tooltip("붙일 카메라(비우면 Camera.main). 이 오브젝트는 이 카메라의 자식이어야 한다.")]
    public Camera cam;
    [Tooltip("화면 앵커 (0,0)=왼쪽아래, (1,1)=오른쪽위")]
    public Vector2 viewportAnchor = Vector2.zero;
    [Tooltip("앵커에서 화면 안쪽으로 밀어줄 여유(월드 유닛). HUD가 화면 밖으로 안 나가게.")]
    public Vector2 worldOffset = new Vector2(2.4f, 1.9f);
    [Tooltip("카메라 앞쪽 거리(프러스텀 안). 실제 위/아래 정렬은 sortingOrder가 결정.")]
    public float depth = 5f;
    [Tooltip("0보다 크면, 카메라 오쏘 사이즈가 이 값일 때의 크기를 기준으로 HUD 크기·여유를 같이 키운다 (카메라를 멀리 뺀 씬에서도 HUD가 화면에서 같은 크기). 0이면 끔")]
    public float referenceOrthoSize = 0f;

    Vector3 baseScale;

    void Awake() { baseScale = transform.localScale; }

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || !cam.orthographic) return;
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        float k = referenceOrthoSize > 0f ? halfH / referenceOrthoSize : 1f;
        if (referenceOrthoSize > 0f) transform.localScale = baseScale * k;
        float x = Mathf.Lerp(-halfW, halfW, viewportAnchor.x) + worldOffset.x * k;
        float y = Mathf.Lerp(-halfH, halfH, viewportAnchor.y) + worldOffset.y * k;
        transform.localPosition = new Vector3(x, y, depth); // 카메라 로컬 = 화면 기준
        transform.localRotation = Quaternion.identity;
    }
}
