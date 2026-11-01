using UnityEngine;
public sealed class BossOrbVisual : MonoBehaviour
{
    Material material; Sprite enlarged;
    float reveal, hit;
    public void Initialize(SpriteRenderer renderer, Material template)
    {
        if (template == null) return;
        renderer.color=Color.white;
        var original=renderer.sprite;
        enlarged=Sprite.Create(original.texture,original.rect,new Vector2(original.pivot.x/original.rect.width,original.pivot.y/original.rect.height),original.pixelsPerUnit/1.15f,0,SpriteMeshType.FullRect);renderer.sprite=enlarged;
        material = new Material(template);
        renderer.sharedMaterial = material;
        var s = renderer.sprite;
        var r = s.textureRect;
        material.SetVector("_UVRect", new Vector4(r.xMin/s.texture.width, r.yMin/s.texture.height, r.xMax/s.texture.width, r.yMax/s.texture.height));
        material.SetFloat("_Reveal", 0f);
    }
    public void Reflect() { reveal = 1f; hit = .7f; }
    void Update()
    {
        if (material == null || PauseMenu.IsPaused) return;
        reveal = Mathf.MoveTowards(reveal, 1f, Time.deltaTime / .5f);
        hit = Mathf.Max(0f, hit - Time.unscaledDeltaTime);
        material.SetFloat("_Reveal", reveal);
        material.SetFloat("_Glitch", hit / .7f);
        material.SetFloat("_Clock", Time.unscaledTime);
    }
    void OnDestroy() { if (material != null) Destroy(material);if(enlarged!=null)Destroy(enlarged); }
}
