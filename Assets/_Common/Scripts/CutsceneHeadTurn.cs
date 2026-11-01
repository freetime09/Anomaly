using UnityEngine;

public static class CutsceneHeadTurn
{
    public static void Face(PlayerController player, int direction)
    {
        if (player == null) return;
        var animator = player.GetComponentInChildren<Animator>();
        var sprite = animator != null ? animator.GetComponent<SpriteRenderer>() : null;
        bool changed = sprite == null || sprite.flipX != (direction < 0);
        player.Face(direction);
        if (!changed) return;
        var clip = Resources.Load<AudioClip>("HeadTurn");
        if (clip == null) return;
        var go = new GameObject("HeadTurnSound");
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false; source.spatialBlend = 0;
        source.volume = .375f; source.clip = clip;
        GameAudioAcoustics.Apply(source); source.Play();
        Object.Destroy(go, clip.length + .8f);
    }
}
