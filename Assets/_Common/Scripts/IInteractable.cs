using UnityEngine;

/// <summary>
/// O(상호작용) 제스처로 조작할 수 있는 대상(레버·텔레포터 등)의 공통 인터페이스.
/// PlayerController 가 근처의 IInteractable 중 가장 가까운 것에 O 프롬프트를 띄우고 작동시킨다.
/// 구현체는 모두 MonoBehaviour 라 위치는 transform 으로 얻는다.
/// </summary>
public interface IInteractable
{
    /// <summary>플레이어가 상호작용 가능한 거리 안에 있는가?</summary>
    bool PlayerInRange(Vector2 playerPos);
    /// <summary>상호작용 프롬프트(O)를 켜고 끈다.</summary>
    void ShowPrompt(bool show);
    /// <summary>O 상호작용 실행.</summary>
    void Interact();
}
