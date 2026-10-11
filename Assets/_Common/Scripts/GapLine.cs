using UnityEngine;

/// <summary>
/// 낮은 틈을 지나가는 얇은 흰 선을 왼쪽→오른쪽으로 그리듯이 반복해서 보여줌 — "여기 아래로 지나가라"는 암시.
///  - 오브젝트 위치가 선의 시작점(왼쪽 끝), length만큼 오른쪽으로 그어짐.
///  - signalId 채널이 켜져 있을 때만 보임 (-1이면 항상). 예: 발판을 밟아 문이 열렸을 때만.
/// 반복: 그리기(drawTime) → 유지(holdTime) → 사라짐(fadeTime) → 쉬기(restTime)
/// </summary>
public class GapLine : MonoBehaviour
{
    [Tooltip("선 길이 (월드 단위, 오른쪽으로)")]
    public float length = 6f;
    [Tooltip("선 두께 (월드 단위)")]
    public float thickness = 0.05f;
    public Color color = Color.white;
    [Tooltip("이 신호 채널이 켜져 있을 때만 보임. -1이면 항상")]
    public int signalId = -1;
    [Tooltip("왼쪽 끝에서 오른쪽 끝까지 그리는 시간 (초)")]
    public float drawTime = 0.6f;
    [Tooltip("다 그린 뒤 유지 시간 (초)")]
    public float holdTime = 0.3f;
    [Tooltip("사라지는 시간 (초)")]
    public float fadeTime = 0.4f;
    [Tooltip("다시 그리기 전 쉬는 시간 (초)")]
    public float restTime = 0.6f;
    public int sortingOrder = 5;

    static Sprite pixel;
    SpriteRenderer sr;
    float timer;
    bool wasOn;

    void Start()
    {
        var go = new GameObject("Line");
        go.transform.SetParent(transform, false);
        sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetPixel();
        sr.sortingOrder = sortingOrder;
        sr.color = new Color(color.r, color.g, color.b, 0f);
    }

    void Update()
    {
        if (sr == null) return;
        bool on = signalId < 0 || SignalBus.IsOn(signalId);
        if (on && !wasOn) timer = 0f; // 켜지는 순간 처음부터 그리기
        wasOn = on;
        if (!on) { sr.color = new Color(color.r, color.g, color.b, 0f); return; }

        timer += Time.deltaTime;
        float cycle = drawTime + holdTime + fadeTime + restTime;
        if (timer >= cycle) timer -= cycle;

        float t = timer, progress = 1f, a = color.a;
        if (t < drawTime) progress = drawTime > 0f ? t / drawTime : 1f;
        else if ((t -= drawTime) < holdTime) { }
        else if ((t -= holdTime) < fadeTime) a *= 1f - t / fadeTime;
        else a = 0f;

        // 피벗이 왼쪽 끝이라 가로 스케일만 늘리면 왼쪽→오른쪽으로 그어짐
        sr.transform.localScale = new Vector3(length * progress, thickness, 1f);
        sr.color = new Color(color.r, color.g, color.b, a);
    }

    /// <summary>왼쪽 가운데가 피벗인 1×1 유닛 흰 스프라이트 (한 번만 만들어 공유).</summary>
    static Sprite GetPixel()
    {
        if (pixel != null) return pixel;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
        return pixel;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 1f, 1f, 0.6f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.right * length);
    }
}
