using UnityEngine;

// References the player's original run sprites without duplicating their texture.
public sealed class LoadingRunnerFrames : ScriptableObject
{
    public Sprite[] frames;
    public AudioClip footsteps;
    [Min(1)] public float framesPerSecond = 12f;
}
