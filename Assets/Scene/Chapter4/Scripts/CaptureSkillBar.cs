using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캡쳐 그림 속 게임 화면의 스킬 아이콘을 진짜 스킬 UI처럼 쓴다. 캡쳐 배경(SpriteRenderer)에 붙인다.
///  - SkillBar 와 같은 방식: 쿨타임/사용불가 정도만큼 아이콘 위를 어두운 사각으로 덮고, 쿨타임이 줄면 아래→위로 밝아진다.
///  - 위치는 캡쳐 픽셀 좌표(왼쪽 위 = 0,0)로 적는다. 배경에 보이는 캡쳐가 바뀌어도 같은 자리에 표시
///    (지금 캡쳐들은 게임 화면 부분이 똑같음).
///  - 배경이 꺼지거나 어두워지면(검게 사라지는 연출) 같이 사라진다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class CaptureSkillBar : MonoBehaviour
{
    [System.Serializable]
    public class Icon
    {
        public PlayerController.SkillId skill;
        [Tooltip("아이콘 사각형: x, y = 왼쪽 위, z, w = 너비, 높이 (캡쳐 픽셀)")]
        public Vector4 pixelRect;
    }

    public Icon[] icons;
    [Tooltip("쿨타임을 읽어올 플레이어 (비우면 자동 탐색)")]
    public PlayerController player;
    [Tooltip("쿨타임 중 아이콘을 덮는 오버레이 색")]
    public Color overlayColor = new Color(0f, 0f, 0f, 0.6f);
    [Tooltip("배경 sortingOrder 에 더할 값 (글리치·테두리보다 위)")]
    public int orderOffset = 4;

    class Slot
    {
        public Icon icon;
        public SpriteRenderer overlay;
        public Vector2 center, size; // 배경 로컬 좌표
    }

    SpriteRenderer bg;
    readonly List<Slot> slots = new List<Slot>();
    static Sprite squareSprite;

    void Awake()
    {
        bg = GetComponent<SpriteRenderer>();
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (bg.sprite == null || icons == null) return;
        float ppu = bg.sprite.pixelsPerUnit;
        foreach (var ic in icons)
        {
            if (ic == null || ic.pixelRect.z <= 0f || ic.pixelRect.w <= 0f) continue;
            var go = new GameObject("SkillOverlay_" + ic.skill);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSprite();
            sr.sortingLayerID = bg.sortingLayerID;
            sr.sortingOrder = bg.sortingOrder + orderOffset;
            go.SetActive(false);
            slots.Add(new Slot
            {
                icon = ic,
                overlay = sr,
                center = PixelToLocal(bg.sprite, new Vector2(ic.pixelRect.x + ic.pixelRect.z * 0.5f, ic.pixelRect.y + ic.pixelRect.w * 0.5f)),
                size = new Vector2(ic.pixelRect.z, ic.pixelRect.w) / ppu,
            });
        }
    }

    void LateUpdate()
    {
        if (player == null) return;
        bool visible = bg.enabled && bg.gameObject.activeInHierarchy;
        float dim = Mathf.Max(bg.color.r, bg.color.g, bg.color.b) * bg.color.a; // 배경이 어두워지는 만큼 같이
        foreach (var s in slots)
        {
            float f = visible ? Mathf.Clamp01(player.SkillCooldown01(s.icon.skill)) : 0f; // 1=꽉, 0=사용 가능
            bool show = f > 0.001f && dim > 0.01f;
            if (s.overlay.gameObject.activeSelf != show) s.overlay.gameObject.SetActive(show);
            if (!show) continue;

            // 위에서부터 f 비율만큼 덮기 → 줄어들면 아래로 빠지며 아이콘이 아래→위로 드러남
            float coverH = s.size.y * f;
            s.overlay.transform.localScale = new Vector3(s.size.x, coverH, 1f);
            s.overlay.transform.localPosition = new Vector3(s.center.x, s.center.y + s.size.y * 0.5f - coverH * 0.5f, 0f);
            Color c = overlayColor; c.a *= dim;
            s.overlay.color = c;
        }
    }

    /// <summary>캡쳐 픽셀 좌표(왼쪽 위 = 0,0) → 배경 로컬 좌표.</summary>
    static Vector2 PixelToLocal(Sprite sp, Vector2 px)
    {
        Rect r = sp.rect;
        return new Vector2(px.x - sp.pivot.x, (r.height - px.y) - sp.pivot.y) / sp.pixelsPerUnit;
    }

    /// <summary>1x1(로컬 1유닛) 흰색 사각 스프라이트(공유). 색은 SpriteRenderer.color 로 준다.</summary>
    static Sprite SquareSprite()
    {
        if (squareSprite != null) return squareSprite;
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var cols = new Color32[16];
        for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(cols); tex.Apply();
        squareSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        return squareSprite;
    }
}
