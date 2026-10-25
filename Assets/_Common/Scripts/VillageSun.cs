using UnityEngine;

/// <summary>Distant sky parallax; daylight is limited to the village.</summary>
[DefaultExecutionOrder(1200)]
public sealed class VillageSun : MonoBehaviour
{
    public Transform visuals;
    public Vector2 viewportAnchor = new Vector2(0.85f, 0.82f);
    public Vector2 villageXRange = new Vector2(-55f, 100f);
    public bool restrictToVillage = true;
    [Range(0f, 0.1f)] public float skyParallax = 0.012f;
    public float approachPerUnit = 0.0003f;
    Camera view;
    PlayerController player;
    Transform sunDisc;
    Vector3 discScale;
    Vector3 skyOrigin;
    Vector3 cameraOrigin;
    bool anchored;

    void Awake()
    {
        view = Camera.main;
        player = FindAnyObjectByType<PlayerController>();
        if (visuals == null) visuals = transform.Find("SunlightVisuals");
        if (visuals != null) sunDisc = visuals.Find("AnimatedSun");
        if (sunDisc != null) discScale = sunDisc.localScale;
    }

    void LateUpdate()
    {
        if (view == null || visuals == null) return;
        bool show = !restrictToVillage || (player != null &&
            player.transform.position.x >= villageXRange.x &&
            player.transform.position.x <= villageXRange.y);
        if (visuals.gameObject.activeSelf != show) visuals.gameObject.SetActive(show);
        if (!show) return;
        // Establish the distant sky once, rather than pinning it to a viewport point each frame.
        if (!anchored)
        {
            cameraOrigin = view.transform.position;
            skyOrigin = view.ViewportToWorldPoint(new Vector3(viewportAnchor.x, viewportAnchor.y, 10f));
            skyOrigin.z = 0f;
            anchored = true;
        }
        Vector3 travel = view.transform.position - cameraOrigin;
        travel.z = 0f;
        Vector3 position = skyOrigin + travel * (1f - skyParallax);
        position.z = 0f;
        transform.position = position;
        if (sunDisc != null)
            sunDisc.localScale = discScale * Mathf.Clamp(1f + travel.x * approachPerUnit, 0.97f, 1.05f);
    }
}
