using UnityEngine;

// Opt-in monument art and effects; the Checkpoint still owns progression and its original sound.
public sealed class CheckpointNeon : MonoBehaviour
{
    public SpriteRenderer monument;
    public Sprite inactiveSprite, activeSprite;
    public Material glowMaterial, groundMaterial, particleMaterial;
    public Color neonColor = new Color(.15f, 1.2f, .84f, 1f);

    SpriteRenderer glyphGlow, groundGlow;
    ParticleSystem sparks;
    MaterialPropertyBlock glyphProperties, groundProperties;
    Camera gameCamera;
    bool activated;
    float litAt;
    static Sprite quad;
    static readonly int Tint = Shader.PropertyToID("_Tint");

    public void SetActivated(bool on)
    {
        bool ignite = on && !activated;
        activated = on;
        if (monument != null) monument.sprite = on ? activeSprite : inactiveSprite;
        if (!on)
        {
            if (glyphGlow != null) glyphGlow.enabled = groundGlow.enabled = false;
            if (sparks != null) sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return;
        }

        if (glyphGlow == null) BuildEffects();
        if (ignite && sparks != null)
        {
            litAt = Time.time;
            sparks.Play();
            sparks.Emit(34);
        }
    }

    void BuildEffects()
    {
        if (monument == null || activeSprite == null || glowMaterial == null) return;
        gameCamera = Camera.main;
        if (quad == null)
            quad = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                Vector2.one * .5f, Texture2D.whiteTexture.width, 0, SpriteMeshType.FullRect);

        var bounds = monument.bounds;
        var center = transform.InverseTransformPoint(bounds.center);
        var localSize = new Vector2(bounds.size.x / Mathf.Abs(transform.lossyScale.x), bounds.size.y / Mathf.Abs(transform.lossyScale.y));
        const float pad = .28f;
        glyphGlow = NewGlow("MintLetterGlow", glowMaterial, center, localSize + Vector2.one * pad * 2, monument.sortingOrder + 1);
        glyphProperties = new MaterialPropertyBlock();
        var rect = activeSprite.rect;
        var texture = activeSprite.texture;
        glyphProperties.SetTexture("_GlowTex", texture);
        glyphProperties.SetVector("_Frame", new Vector4(rect.xMin / texture.width, rect.yMin / texture.height, rect.xMax / texture.width, rect.yMax / texture.height));
        glyphProperties.SetVector("_Margin", new Vector4(pad / localSize.x, pad / localSize.y, 0, 0));

        groundGlow = NewGlow("MintGroundLight", groundMaterial, new Vector3(center.x, transform.InverseTransformPoint(bounds.min).y + .035f, center.z),
            new Vector2(localSize.x * 1.6f, .3f), monument.sortingOrder + 1);
        groundProperties = new MaterialPropertyBlock();
        BuildSparks(center);
    }

    SpriteRenderer NewGlow(string name, Material material, Vector3 position, Vector2 size, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = position;
        go.transform.localScale = new Vector3(size.x, size.y, 1);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = quad;
        renderer.sharedMaterial = material;
        renderer.sortingLayerID = monument.sortingLayerID;
        renderer.sortingOrder = order;
        renderer.enabled = false;
        return renderer;
    }

    void BuildSparks(Vector3 center)
    {
        var go = new GameObject("NeonSparks");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = center + Vector3.up * .08f;
        sparks = go.AddComponent<ParticleSystem>();
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = sparks.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 2;
        main.maxParticles = 64;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.65f, 1.5f);
        main.startSpeed = 0;
        main.startSize = new ParticleSystem.MinMaxCurve(.08f, .19f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(.18f, 1, .7f), new Color(.7f, 1, .95f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.cullingMode = ParticleSystemCullingMode.Pause;
        var emission = sparks.emission;
        emission.rateOverTime = 1.6f;
        var shape = sparks.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(.4f, 1, .01f);
        var velocity = sparks.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-.65f, .65f);
        velocity.y = new ParticleSystem.MinMaxCurve(.3f, 1.1f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var noise = sparks.noise;
        noise.enabled = true;
        noise.strength = .1f;
        noise.frequency = .7f;
        noise.scrollSpeed = .25f;
        var color = sparks.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(.28f, 1, .8f), 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .08f), new GradientAlphaKey(.75f, .5f), new GradientAlphaKey(0, 1) });
        color.color = gradient;
        var size = sparks.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .4f), new Keyframe(.16f, 1), new Keyframe(1, 0)));
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = particleMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingLayerID = monument.sortingLayerID;
        renderer.sortingOrder = monument.sortingOrder + 3;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    void LateUpdate()
    {
        if (!activated || glyphGlow == null) return;
        var bounds = monument.bounds;
        var view = gameCamera.WorldToViewportPoint(bounds.center);
        float marginY = (bounds.extents.y + .4f) / (2 * gameCamera.orthographicSize);
        float marginX = (bounds.extents.x + .4f) / (2 * gameCamera.orthographicSize * gameCamera.aspect);
        bool visible = monument.enabled && view.z > 0 && view.x >= -marginX && view.x <= 1 + marginX && view.y >= -marginY && view.y <= 1 + marginY;
        glyphGlow.enabled = groundGlow.enabled = visible;
        var emission = sparks.emission;
        emission.enabled = visible;
        if (!visible) return;
        float age = Time.time - litAt;
        float ignition = Mathf.SmoothStep(0, 1, age / .16f) * (1 + .7f * Mathf.Exp(-age * 3.8f));
        float breath = .94f + .045f * Mathf.Sin(Time.time * 1.7f + transform.position.x) + .02f * Mathf.Sin(Time.time * 3.3f);
        var tint = neonColor;
        tint.a = ignition * breath;
        glyphProperties.SetColor(Tint, tint);
        glyphGlow.SetPropertyBlock(glyphProperties);
        tint.a *= .19f;
        groundProperties.SetColor(Tint, tint);
        groundGlow.SetPropertyBlock(groundProperties);
    }

    void OnDisable()
    {
        if (glyphGlow != null) glyphGlow.enabled = groundGlow.enabled = false;
        if (sparks != null) sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
