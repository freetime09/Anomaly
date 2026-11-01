using UnityEngine;

public sealed class StoryAtmosphere : MonoBehaviour
{
    public Material background;
    Material owned, dustMaterial;
    GameObject plane, dust;
    void Start()
    {
        var camera = Camera.main; if (camera == null) return;
        plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
        plane.name = "StoryQuietBackdrop"; plane.transform.position = new Vector3(0, 0, 5);
        Destroy(plane.GetComponent<Collider>());
        plane.transform.localScale = new Vector3(camera.orthographicSize * 2 * camera.aspect * 1.1f, camera.orthographicSize * 2 * 1.1f, 1);
        if (background != null) { owned = new Material(background); plane.GetComponent<MeshRenderer>().sharedMaterial = owned; }
        dust = new GameObject("DescendingStoryParticles"); dust.transform.SetParent(transform, false);
        dust.transform.position = new Vector3(0, 1, 0);
        var particles = dust.AddComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main; main.playOnAwake = false; main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(16, 26); main.startSpeed = 0;
        main.startSize = new ParticleSystem.MinMaxCurve(.018f, .045f);
        main.startColor = new Color(.5f, .65f, .75f, .32f); main.maxParticles = 120;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var velocity = particles.velocityOverLifetime; velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-.018f, .018f);
        velocity.y = new ParticleSystem.MinMaxCurve(-.24f, -.1f);
        velocity.z = new ParticleSystem.MinMaxCurve(0, 0);
        var emission = particles.emission; emission.rateOverTime = 6;
        var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(camera.orthographicSize * 2 * camera.aspect * 1.2f, camera.orthographicSize * 2 * 1.5f, .1f);
        var color = particles.colorOverLifetime; color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(.8f, .8f), new GradientAlphaKey(0, 1) });
        color.color = gradient;
        dustMaterial = new Material(Shader.Find("Sprites/Default"));
        var renderer = dust.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = dustMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.velocityScale = .12f; renderer.lengthScale = 1.4f;
        particles.Simulate(18, true, true, false); particles.Play();
    }
    void OnDestroy()
    {
        if (plane != null) Destroy(plane); if (dust != null) Destroy(dust);
        if (owned != null) Destroy(owned); if (dustMaterial != null) Destroy(dustMaterial);
    }
}
