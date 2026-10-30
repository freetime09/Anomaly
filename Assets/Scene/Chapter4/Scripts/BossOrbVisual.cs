using UnityEngine;
public sealed class BossOrbVisual : MonoBehaviour
{
    Material material;
    float reveal, hit;
    public void Initialize(SpriteRenderer renderer, Material template)
    {
        if (template == null) return;
        material = new Material(template);
        renderer.sharedMaterial = material;
        var s = renderer.sprite;
        var r = s.textureRect;
        material.SetVector("_UVRect", new Vector4(r.xMin/s.texture.width, r.yMin/s.texture.height, r.xMax/s.texture.width, r.yMax/s.texture.height));
        material.SetFloat("_Reveal", 0f);
    }
    public void Reflect() { reveal = 1f; hit = .38f; }
    void Update()
    {
        if (material == null || PauseMenu.IsPaused) return;
        reveal = Mathf.MoveTowards(reveal, 1f, Time.deltaTime / .5f);
        hit = Mathf.Max(0f, hit - Time.unscaledDeltaTime);
        material.SetFloat("_Reveal", reveal);
        material.SetFloat("_Glitch", hit / .38f);
        material.SetFloat("_Clock", Time.unscaledTime);
    }
    void OnDestroy() { if (material != null) Destroy(material); }
}
