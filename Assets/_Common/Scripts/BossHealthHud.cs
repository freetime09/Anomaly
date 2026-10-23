using UnityEngine;
using UnityEngine.UI;

/// <summary>Boss health: synchronized 8 fps frames and a damped impact on the lost heart.</summary>
public sealed class BossHealthHud : MonoBehaviour
{
    const float FramesPerSecond = 8f;
    Image[] icons;
    RectTransform[] slots;
    Sprite[] fullFrames, emptyFrames;
    int current;
    float clock, reveal, impactAge = 10f;
    int lostIndex = -1;

    public void Configure(Sprite[] full, Sprite[] empty, int count, RectTransform settings)
    {
        fullFrames = full;
        emptyFrames = empty;
        current = count;
        var row = (RectTransform)transform;
        row.anchorMin = settings != null ? settings.anchorMin : new Vector2(0f, 1f);
        row.anchorMax = settings != null ? settings.anchorMax : new Vector2(0f, 1f);
        row.pivot = new Vector2(0f, 1f);
        row.anchoredPosition = settings != null
            ? settings.anchoredPosition + new Vector2(-settings.pivot.x * settings.rect.width,
                -settings.pivot.y * settings.rect.height - 18f)
            : new Vector2(28f, -110f);
        row.sizeDelta = new Vector2(count * 80f - 16f, 60f);
        icons = new Image[count];
        slots = new RectTransform[count];
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Heart_" + (i + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(64f, 60f);
            rt.anchoredPosition = new Vector2(32f + i * 80f, -30f);
            slots[i] = rt;
            icons[i] = go.GetComponent<Image>();
            icons[i].raycastTarget = false;
            icons[i].preserveAspect = true;
        }
        Paint();
    }

    public void SetHealth(int value)
    {
        value = Mathf.Clamp(value, 0, icons.Length);
        if (value < current)
        {
            lostIndex = value; // the existing damage path removes exactly one heart
            impactAge = 0f;
        }
        else if (value > current)
        {
            lostIndex = -1;
            impactAge = 10f;
        }
        current = value;
        Paint();
    }

    void OnEnable() { reveal = 0f; }

    void Update()
    {
        if (icons == null || PauseMenu.IsPaused) return;
        float dt = Time.unscaledDeltaTime; // keep the impact readable during hit stop
        clock += dt;
        reveal = Mathf.Min(1f, reveal + dt / 0.45f);
        impactAge += dt;
        Paint();
    }

    void Paint()
    {
        if (icons == null) return;
        float fade = Mathf.SmoothStep(0f, 1f, reveal);
        float t = impactAge;
        float envelope = t < 0.85f ? Mathf.Exp(-4.5f * t) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.85f, t))) : 0f;
        for (int i = 0; i < icons.Length; i++)
        {
            var frames = i < current ? fullFrames : emptyFrames;
            if (frames != null && frames.Length > 0)
                icons[i].sprite = frames[Mathf.FloorToInt(clock * FramesPerSecond) % frames.Length];
            bool lost = i == lostIndex;
            float strength = lost ? 1f : 0.22f;
            float kick = envelope * strength;
            Vector2 offset = new Vector2(
                (Mathf.Sin(t * 74f) + 0.35f * Mathf.Sin(t * 113f)) * 15f,
                Mathf.Sin(t * 91f) * 10f + 8f * Mathf.Cos(t * 25f)) * kick;
            slots[i].anchoredPosition = new Vector2(32f + i * 80f, -30f) + offset;
            slots[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 66f) * 30f * kick);
            float stretch = Mathf.Sin(t * 42f) * 0.15f * kick;
            slots[i].localScale = new Vector3(1f + 0.55f * kick + stretch, 1f + 0.55f * kick - stretch, 1f);
            Color tint = lost ? Color.Lerp(Color.white, new Color(1f, 0.2f, 0.2f), envelope * 0.8f) : Color.white;
            tint.a = fade;
            icons[i].color = tint;
        }
    }
}
