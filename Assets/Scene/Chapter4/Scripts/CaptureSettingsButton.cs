using UnityEngine;

/// <summary>
/// 캡쳐 그림 속 게임 화면의 톱니바퀴를 진짜 설정 버튼처럼 쓴다. 캡쳐 배경(SpriteRenderer)에 붙인다.
///  - 캡쳐가 보이는 동안: 왼쪽 위 진짜 톱니는 숨기고, 그림 속 톱니를 누르면 설정이 열린다.
///  - 캡쳐가 꺼지거나 어두워지면(검게 사라지는 연출) 진짜 톱니를 다시 보여준다.
///  - 위치는 캡쳐 픽셀 좌표(왼쪽 위 = 0,0). CaptureSkillBar 와 같은 방식.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class CaptureSettingsButton : MonoBehaviour
{
    [Tooltip("누를 수 있는 자리: x, y = 왼쪽 위, z, w = 너비, 높이 (캡쳐 픽셀). 그림 속 톱니보다 조금 넉넉하게")]
    public Vector4 pixelRect = new Vector4(179f, 750f, 37f, 37f);

    SpriteRenderer bg;
    PauseMenu menu;
    bool shown; // 그림 속 톱니가 버튼으로 동작 중

    void Awake()
    {
        bg = GetComponent<SpriteRenderer>();
    }

    void OnEnable() { PauseMenu.ExtraSettingsArea = Contains; }

    void OnDisable()
    {
        if (PauseMenu.ExtraSettingsArea == (System.Func<Vector2, bool>)Contains) PauseMenu.ExtraSettingsArea = null;
        SetShown(false);
    }

    void LateUpdate()
    {
        SetShown(Visible());
        if (shown && !PauseMenu.IsPaused && Input.GetMouseButtonDown(0) && Contains(Input.mousePosition))
        {
            if (menu == null) menu = FindAnyObjectByType<PauseMenu>();
            if (menu != null) menu.Pause();
        }
    }

    bool Visible()
    {
        if (bg == null || !bg.enabled || !bg.gameObject.activeInHierarchy || bg.sprite == null) return false;
        float dim = Mathf.Max(bg.color.r, bg.color.g, bg.color.b) * bg.color.a; // 검게 사라지는 중엔 안 눌림
        return dim > 0.5f;
    }

    void SetShown(bool on)
    {
        if (shown == on) return;
        shown = on;
        if (menu == null) menu = FindAnyObjectByType<PauseMenu>();
        if (menu != null && menu.settingsButton != null) menu.settingsButton.gameObject.SetActive(!on);
    }

    /// <summary>화면 좌표가 그림 속 톱니 자리 안인가.</summary>
    bool Contains(Vector2 screenPos)
    {
        if (!shown) return false;
        var cam = Camera.main;
        if (cam == null) return false;
        Vector3 w = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
        Vector3 local = transform.InverseTransformPoint(w);
        Sprite sp = bg.sprite;
        float px = local.x * sp.pixelsPerUnit + sp.pivot.x;
        float py = sp.rect.height - (local.y * sp.pixelsPerUnit + sp.pivot.y); // 왼쪽 위 기준
        return px >= pixelRect.x && px <= pixelRect.x + pixelRect.z && py >= pixelRect.y && py <= pixelRect.y + pixelRect.w;
    }
}
