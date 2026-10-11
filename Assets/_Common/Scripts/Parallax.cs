using UnityEngine;

/// <summary>
/// 카메라가 움직일 때 배경을 그보다 '적게' 움직여 원근감(시차)을 만든다.
/// 먼 건물처럼 화면에서 조금씩만 흐르게 하는 용도.
///
/// factor 의미 (화면 기준):
///   1 = 화면에 완전히 고정 (무한히 먼 배경, 전혀 안 움직임)
///   0 = 월드에 고정 (전경처럼 카메라 이동만큼 그대로 스크롤)
///   0.85~0.95 = 먼 배경이 화면에서 조금씩만 흐름 ← 건물 배경 추천
///
/// 화면에서의 이동량 = 카메라이동 * (factor - 1). 즉 factor가 1에 가까울수록 덜 움직인다.
/// </summary>
[HelpURL("")]
public class Parallax : MonoBehaviour
{
    [Header("대상 카메라")]
    [Tooltip("따라갈 카메라. 비우면 자동으로 Camera.main 사용")]
    public Transform cam;

    [Header("시차 계수")]
    [Tooltip("1=화면에 고정(가장 멀리), 0=월드 고정(전경). 먼 건물은 0.85~0.95 추천")]
    public Vector2 factor = new Vector2(0.9f, 0.9f);

    Vector3 camStart;  // 기준 카메라 위치
    Vector3 myStart;   // 기준 내 위치
    bool initialized;  // 기준점 캡처 여부

    // 첫 LateUpdate에서 기준점을 잡는다. (이때는 CameraFollow의 시작 스냅이 이미 끝나 있어
    //  시작 순간의 카메라 이동을 시차에 반영하지 않는다 → 시작 렁치 방지)
    void LateUpdate()
    {
        if (cam == null)
        {
            if (Camera.main != null) cam = Camera.main.transform;
            else return;
        }
        if (!initialized)
        {
            camStart = cam.position;
            myStart = transform.position;
            initialized = true;
            return;
        }
        Vector3 d = cam.position - camStart;
        transform.position = new Vector3(
            myStart.x + d.x * factor.x,
            myStart.y + d.y * factor.y,
            myStart.z);
    }
}
