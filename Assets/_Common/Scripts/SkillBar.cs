using System.Collections.Generic;
using UnityEngine;

/// <summary>Shows availability on the animated icon itself, with no oversized rectangle or shared sprite mask.</summary>
public class SkillBar : MonoBehaviour
{
    public PlayerController player;
    // Retained for existing scene serialization.
    public Color overlayColor = new Color(0f, 0f, 0f, 0.6f);
    [Range(0f, 1f)] public float unavailableBrightness = 0.72f;
    [Range(0f, 1f)] public float unavailableOpacity = 0.50f;
    [Min(0.01f)] public float stateFadeTime = 0.10f;

    class Slot
    {
        public PlayerController.SkillId id;
        public SpriteRenderer renderer;
        public Color baseColor;
        public float amount;
    }

    readonly List<Slot> slots = new List<Slot>();
    HudReveal reveal;

    void Awake()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        reveal = GetComponent<HudReveal>();
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            var sr = child.GetComponent<SpriteRenderer>();
            PlayerController.SkillId id;
            if (sr == null || !TryMap(child.name, out id)) continue;
            slots.Add(new Slot
            {
                id = id, renderer = sr, baseColor = sr.color,
                amount = player != null ? Mathf.Clamp01(player.SkillCooldown01(id)) : 0f
            });
        }
    }

    void LateUpdate()
    {
        if (player == null) return;
        // Some chapter scenes add HudReveal to the prefab instance.
        if (reveal == null) reveal = GetComponent<HudReveal>();
        foreach (var s in slots)
        {
            if (s.renderer == null) continue;
            float target = Mathf.Clamp01(player.SkillCooldown01(s.id));
            s.amount = Mathf.MoveTowards(s.amount, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, stateFadeTime));
            float brightness = Mathf.Lerp(1f, unavailableBrightness, s.amount);
            Color c = s.baseColor;
            c.r *= brightness; c.g *= brightness; c.b *= brightness;
            c.a *= Mathf.Lerp(1f, unavailableOpacity, s.amount);
            if (reveal != null && reveal.enabled) c.a *= reveal.GetOpacity(s.renderer.transform);
            s.renderer.color = c;
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
            case 1: id = PlayerController.SkillId.Return; return true;
            case 3: id = PlayerController.SkillId.Plant; return true;
            case 4: id = PlayerController.SkillId.Attack; return true;
            case 5: id = PlayerController.SkillId.Teleport; return true;
            case 6: id = PlayerController.SkillId.Slide; return true;
            case 7: id = PlayerController.SkillId.Interact; return true;
            case 8: id = PlayerController.SkillId.Throw; return true;
            case 9: id = PlayerController.SkillId.Boss; return true;
            default: return false;
        }
    }
}
