using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스킬 아이콘(SkillIcon) 위에 쿨타임/사용불가를 '어두운 오버레이'로 표시한다.
///  - 각 자식 Skill_N(SpriteRenderer) 위에 어두운 사각 오버레이를 얹고,
///    PlayerController.SkillCooldown01(id) 값(1=꽉 참/못 씀, 0=사용 가능)만큼 위에서부터 덮는다.
///  - 쿨타임이 줄면 오버레이가 아래로 빠지며 아이콘이 '아래→위'로 밝아진다.
/// 아이콘 이름(Skill_1,3,4,5,6,7,8,9)의 숫자로 어떤 스킬인지 매핑한다.
///   1=S(회수) 3=9(세우기) 4=ㄱ(공격) 5=⋉(텔레포트) 6=ㄴ(슬라이드) 7=O(상호작용) 8=N(던지기) 9=§(보스,잠김)
/// </summary>
public class SkillBar : MonoBehaviour
{
    [Tooltip("쿨타임을 읽어올 플레이어 (비우면 자동 탐색)")]
    public PlayerController player;
    [Tooltip("쿨타임 중 아이콘을 덮는 오버레이 색 (어둡고 반투명 권장)")]
    public Color overlayColor = new Color(0f, 0f, 0f, 0.6f);

    class Slot
    {
        public PlayerController.SkillId id;
        public Transform overlay;   // 아이콘 자식으로 붙는 어두운 사각
        public float w, h;          // 아이콘 로컬 크기
        public Vector2 center;      // 아이콘 스프라이트 바운드 중심(피벗 보정)
    }

    readonly List<Slot> slots = new List<Slot>();
    static Sprite squareSprite;

    void Awake()
    {
        if (player == null) player = FindObjectOfType<PlayerController>();
        BuildSlots();
    }

    void BuildSlots()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            var sr = child.GetComponent<SpriteRenderer>();
            if (sr == null) continue;

            PlayerController.SkillId id;
            if (!TryMap(child.name, out id)) continue;

            // 아이콘 로컬 크기/중심 (스프라이트 바운드 — 피벗이 중앙이 아니어도 정렬)
            Vector2 size = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
            Vector2 center = sr.sprite != null ? (Vector2)sr.sprite.bounds.center : Vector2.zero;

            // 어두운 오버레이(아이콘 자식 → 아이콘 로컬 좌표계에서 정확히 겹침)
            var go = new GameObject("CooldownOverlay");
            go.transform.SetParent(child, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var osr = go.AddComponent<SpriteRenderer>();
            osr.sprite = SquareSprite();
            osr.color = overlayColor;
            osr.sortingLayerID = sr.sortingLayerID;
            osr.sortingOrder = sr.sortingOrder + 1; // 아이콘 바로 위

            slots.Add(new Slot { id = id, overlay = go.transform, w = size.x, h = size.y, center = center });
        }
    }

    void LateUpdate()
    {
        if (player == null) return;
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            float f = Mathf.Clamp01(player.SkillCooldown01(s.id)); // 1=꽉, 0=사용 가능
            bool show = f > 0.001f;
            if (s.overlay.gameObject.activeSelf != show) s.overlay.gameObject.SetActive(show);
            if (!show) continue;

            // 위에서부터 f 비율만큼 덮기 → 줄어들면 아래로 빠지며 아이콘이 아래→위로 드러남
            float coverH = s.h * f;
            s.overlay.localScale = new Vector3(s.w, coverH, 1f);                       // 사각 스프라이트(1x1)를 아이콘 크기로
            s.overlay.localPosition = new Vector3(s.center.x, s.center.y + s.h * 0.5f - coverH * 0.5f, 0f); // 윗변에 고정
        }
    }

    static bool TryMap(string childName, out PlayerController.SkillId id)
    {
        id = PlayerController.SkillId.Interact;
        int us = childName.LastIndexOf('_');
        int n;
        if (us < 0 || !int.TryParse(childName.Substring(us + 1), out n)) return false;
        switch (n)
        {
            case 1: id = PlayerController.SkillId.Return;   return true; // S
            case 3: id = PlayerController.SkillId.Plant;    return true; // 9
            case 4: id = PlayerController.SkillId.Attack;   return true; // ㄱ
            case 5: id = PlayerController.SkillId.Teleport; return true; // ⋉
            case 6: id = PlayerController.SkillId.Slide;    return true; // ㄴ
            case 7: id = PlayerController.SkillId.Interact; return true; // O
            case 8: id = PlayerController.SkillId.Throw;    return true; // N
            case 9: id = PlayerController.SkillId.Boss;     return true; // §
            default: return false;
        }
    }

    /// <summary>1x1(월드 1유닛) 흰색 사각 스프라이트(공유). 오버레이 색은 SpriteRenderer.color 로 준다.</summary>
    static Sprite SquareSprite()
    {
        if (squareSprite != null) return squareSprite;
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var cols = new Color32[16];
        for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(cols); tex.Apply();
        squareSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f); // ppu 4 → 1 unit
        return squareSprite;
    }
}
