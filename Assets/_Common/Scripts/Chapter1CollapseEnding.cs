using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class Chapter1CollapseEnding : MonoBehaviour
{
    public ChapterTransitionTrigger transition;
    public AudioClip collapseSound;
    public Transform collapsingGround;
    public float runSeconds = 1.25f, fallSeconds = .8f;
    [Range(1f, 1.3f)] public float collapseGain = 1.15f;

    bool started, running;
    PlayerController player;
    FallRespawn respawn;
    CameraFollow cameraFollow;
    Transform fallFocus, previousFocus;
    Material effectMaterial;
    ParticleSystem grit;
    ParticleSystem dust;
    readonly List<Transform> pieces = new List<Transform>();

    void OnTriggerEnter2D(Collider2D other)
    {
        if (started) return;
        player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;
        started = true;
        StartCoroutine(Play());
    }

    IEnumerator Play()
    {
        CutsceneLock.Lock(this);
        respawn = player.GetComponent<FallRespawn>();
        if (respawn != null) respawn.cinematicFall = true;
        cameraFollow = FindFirstObjectByType<CameraFollow>();
        if (collapsingGround != null)
            foreach (Transform piece in collapsingGround) pieces.Add(piece);
        foreach (var group in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (group == collapsingGround || group.gameObject.scene != gameObject.scene ||
                !group.name.StartsWith("CollapsingGround_NotTilemap", System.StringComparison.Ordinal)) continue;
            foreach (Transform piece in group) pieces.Add(piece);
        }

        player.ResetMotion();
        player.Face(1);
        running = true;
        StartCoroutine(FadeMusic());
        const float shakeLead = .15f;
        float soundLead = Mathf.Min(.55f, Mathf.Max(0f, runSeconds));
        yield return new WaitForSeconds(runSeconds - soundLead);

        var audio = gameObject.AddComponent<AudioSource>();
        audio.spatialBlend = 0;
        if (collapseSound != null) audio.PlayOneShot(collapseSound, collapseGain);
        yield return new WaitForSeconds(Mathf.Max(0f, soundLead - shakeLead));
        if (cameraFollow != null)
        {
            cameraFollow.Shake(1.3f, .22f);
            cameraFollow.SetRumble(this, .06f);
        }

        yield return new WaitForSeconds(Mathf.Min(soundLead, shakeLead));
        running = false;
        player.ResetMotion();

        yield return new WaitForSeconds(.45f);
        var speaker = player.GetComponent<DialogueSpeaker>();
        if (speaker == null) speaker = player.gameObject.AddComponent<DialogueSpeaker>();
        var bubble = speaker.Bubble;
        if (bubble != null) StartCoroutine(bubble.Say("뭐야! 갑자기 길이..!", 20));
        player.Face(-1);
        yield return new WaitForSeconds(.65f);
        player.Face(1);
        yield return new WaitForSeconds(.65f);
        player.Face(-1);
        yield return new WaitForSeconds(.6f);
        player.Face(1);

        if (pieces.Count > 0) yield return BreakGround(bubble);
        yield return new WaitForSeconds(fallSeconds);
        transition.StartTransition();
    }

    IEnumerator BreakGround(SpeechBubble bubble)
    {
        float breakX = player.transform.position.x;
        pieces.Sort((a, b) => Mathf.Abs(a.position.x - breakX).CompareTo(Mathf.Abs(b.position.x - breakX)));
        effectMaterial = new Material(Shader.Find("Sprites/Default"));
        grit = CreateDebris("Loose soil", 1.25f);
        dust = CreateDebris("Dust", .12f);

        var origins = new Vector3[pieces.Count];
        var cracks = new LineRenderer[pieces.Count];
        for (int i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            origins[i] = piece.position;
            // Leave some sections intact so the splits have distinct lengths and spacing.
            if (i % 3 != 2) cracks[i] = CreateCrack(piece, i);
        }

        const float warning = .9f;
        for (float t = 0; t < warning; t += Time.deltaTime)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                float delay = Mathf.Min(.46f, Mathf.Abs(origins[i].x - breakX) * .047f);
                float opening = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(delay, warning, t));
                if (cracks[i] != null && opening > .04f)
                {
                    if (!cracks[i].enabled)
                    {
                        cracks[i].enabled = true;
                        EmitDebris(origins[i] + Vector3.up * .5f, 3, 2);
                    }
                    cracks[i].widthMultiplier = Mathf.Lerp(.015f, .064f, opening);
                }
                float side = Mathf.Sign(origins[i].x - breakX);
                float sag = (.055f + i % 4 * .026f) * opening;
                pieces[i].position = origins[i] + new Vector3(side * .07f * opening, -sag, 0);
                pieces[i].rotation = Quaternion.Euler(0, 0, (i % 2 == 0 ? 1 : -1) * 2.5f * opening);
            }

            if (cameraFollow != null) cameraFollow.SetRumble(this, Mathf.Lerp(.06f, .19f, t / warning));
            yield return null;
        }

        if (bubble != null) bubble.Hide();
        if (cameraFollow != null)
        {
            previousFocus = cameraFollow.target;
            fallFocus = new GameObject("Collapse Camera Focus").transform;
            fallFocus.SetParent(transform);
            fallFocus.position = player.transform.position;
            cameraFollow.target = fallFocus;
            cameraFollow.SetRumble(this, .4f);
        }

        // Give way beneath the player first. The break then races out along both ground groups.
        float elapsed = 0f;
        foreach (var piece in pieces)
        {
            float releaseAt = Mathf.Min(.44f, Mathf.Abs(piece.position.x - breakX) * .038f);
            while (elapsed < releaseAt)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
            foreach (var support in piece.GetComponentsInChildren<Collider2D>()) support.enabled = false;

            var body = piece.GetComponent<Rigidbody2D>();
            if (body == null) body = piece.gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 1.7f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            float side = Mathf.Sign(piece.position.x - breakX);
            body.linearVelocity = new Vector2(side * Random.Range(.25f, .85f), Random.Range(-2.5f, -.5f));
            body.angularVelocity = -side * Random.Range(12f, 52f);
            EmitDebris(piece.position + Vector3.up * .4f, 7, 5);
        }
    }

    LineRenderer CreateCrack(Transform piece, int index)
    {
        var lineObject = new GameObject("Soil fracture");
        lineObject.transform.SetParent(piece, false);
        var line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.sharedMaterial = effectMaterial;
        var surface = piece.GetComponent<SpriteRenderer>();
        if (surface != null)
        {
            line.sortingLayerID = surface.sortingLayerID;
            line.sortingOrder = surface.sortingOrder + 1;
        }
        line.startColor = new Color(.16f, .095f, .065f);
        line.endColor = new Color(.16f, .095f, .065f, .48f);
        line.endWidth = .18f;
        float side = index % 2 == 0 ? -1f : 1f;
        float depth = .55f + (index % 5) * .19f;
        line.positionCount = 5;
        line.SetPositions(new[]
        {
            new Vector3(side * .27f, .49f),
            new Vector3(side * .06f, .22f),
            new Vector3(side * .17f, .05f - depth * .24f),
            new Vector3(-side * .18f, .02f - depth * .58f),
            new Vector3(-side * .08f, -depth)
        });
        line.enabled = false;
        return line;
    }

    ParticleSystem CreateDebris(string label, float gravity)
    {
        var effect = new GameObject(label);
        effect.transform.SetParent(transform, false);
        var system = effect.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 6f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = gravity;
        main.maxParticles = 400;
        var emission = system.emission;
        emission.enabled = false;
        var shape = system.shape;
        shape.enabled = false;
        var fade = system.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.75f, .48f), new GradientAlphaKey(0, 1) });
        fade.color = gradient;
        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = effectMaterial;
        var surface = pieces[0].GetComponent<SpriteRenderer>();
        if (surface != null)
        {
            renderer.sortingLayerID = surface.sortingLayerID;
            renderer.sortingOrder = surface.sortingOrder + 2;
        }
        system.Play();
        return system;
    }

    void EmitDebris(Vector3 origin, int grains, int puffs)
    {
        for (int i = 0; i < grains + puffs; i++)
        {
            bool isDust = i >= grains;
            var particle = new ParticleSystem.EmitParams
            {
                position = origin + new Vector3(Random.Range(-.43f, .43f), Random.Range(-.26f, .02f)),
                velocity = new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(.15f, isDust ? .9f : 1.9f)),
                startLifetime = isDust ? Random.Range(.35f, .85f) : Random.Range(.26f, .61f),
                startSize = isDust ? Random.Range(.09f, .25f) : Random.Range(.036f, .13f),
                startColor = Color.Lerp(
                    isDust ? new Color(.44f, .31f, .24f, .28f) : new Color(.31f, .17f, .16f),
                    isDust ? new Color(.65f, .48f, .34f, .36f) : new Color(.55f, .31f, .22f),
                    Random.value),
                rotation = Random.Range(0f, 180f)
            };
            (isDust ? dust : grit).Emit(particle, 1);
        }
    }

    IEnumerator FadeMusic()
    {
        var music = new List<AudioSource>();
        var volumes = new List<float>();
        foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
        {
            if (!source.isPlaying || !source.loop || source.clip == null || source.clip.length <= 15) continue;
            music.Add(source);
            volumes.Add(source.volume);
        }
        for (float t = 0; t < 1.8f; t += Time.deltaTime)
        {
            for (int i = 0; i < music.Count; i++)
                if (music[i] != null) music[i].volume = volumes[i] * (1 - t / 1.8f);
            yield return null;
        }
        foreach (var source in music)
        {
            if (source == null) continue;
            source.volume = 0;
            source.Stop();
        }
    }

    void Update()
    {
        if (running && player != null && !player.IsMoving)
            player.WalkToX(player.transform.position.x + 15);
    }

    void OnDisable()
    {
        CutsceneLock.Unlock(this);
        if (respawn != null) respawn.cinematicFall = false;
        if (cameraFollow != null)
        {
            cameraFollow.SetRumble(this, 0);
            if (fallFocus != null && cameraFollow.target == fallFocus) cameraFollow.target = previousFocus;
        }
    }

    void OnDestroy()
    {
        if (effectMaterial != null) Destroy(effectMaterial);
    }
}
