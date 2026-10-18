using UnityEngine;
using TMPro;

/// <summary>
/// 지정한 텍스트가 화면에 나타나면(알파가 1에 가까워지면), 지정한 오브젝트를 활성화합니다.
/// 그 텍스트가 어떤 트리거/스크립트에 의해 나타나든 상관없이 독립적으로 알파값만 감시합니다.
///
/// 활성화할 오브젝트는 무엇이든 가능합니다 — Box Collider 2D가 달린 오브젝트를 켜서
/// 새로운 트리거 영역이나 벽/장애물을 나타나게 하는 용도로 특히 유용합니다.
/// </summary>
public class ActivateOnTextShown : MonoBehaviour
{
    [Header("감시할 텍스트")]
    [SerializeField] private TextMeshPro watchedText;
    [Tooltip("텍스트의 알파값이 이 값 이상으로 올라가면 '나타났다'고 판단합니다")]
    [Range(0.9f, 1f)]
    [SerializeField] private float visibleThreshold = 0.99f;

    [Header("나타나면 활성화할 오브젝트 (Box Collider 2D가 있는 오브젝트 등, 무엇이든 가능)")]
    [SerializeField] private GameObject objectToActivate;

    [Header("옵션")]
    [Tooltip("체크하면 한 번만 활성화되고, 텍스트가 다시 사라졌다 나타나도 반복 실행하지 않음")]
    [SerializeField] private bool onlyOnce = true;
    [Tooltip("시작할 때 대상 오브젝트를 자동으로 꺼둘지 여부")]
    [SerializeField] private bool deactivateOnStart = true;

    private bool hasActivated = false;

    private void Awake()
    {
        if (deactivateOnStart && objectToActivate != null)
        {
            objectToActivate.SetActive(false);
        }
    }

    private void Update()
    {
        if (watchedText == null || objectToActivate == null) return;
        if (onlyOnce && hasActivated) return;

        if (watchedText.alpha >= visibleThreshold)
        {
            if (!objectToActivate.activeSelf) objectToActivate.SetActive(true);
            hasActivated = true;
        }
        else if (!onlyOnce)
        {
            // 반복 허용 모드: 텍스트가 다시 사라지면 오브젝트도 다시 꺼서 재사용 가능하게 함
            if (objectToActivate.activeSelf) objectToActivate.SetActive(false);
            hasActivated = false;
        }
    }
}