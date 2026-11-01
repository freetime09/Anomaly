using System.Collections;
using UnityEngine;

public sealed class HouseAmbienceTransition : MonoBehaviour
{
    public AudioSource birds;
    public HouseScreenGlitch glitch;
    public AudioClip interiorClip;
    [Range(0,1)] public float interiorVolume = .42f;
    [Min(.1f)] public float fadeDuration = 4f;
    bool entered;
    AudioSource interior;
    public void EnterHouse()
    {
        if (glitch != null) glitch.soundEnabled = true;
        if (entered) return;
        entered = true;
        if (birds != null) StartCoroutine(FadeBirds());
        if (interiorClip != null)
        {
            var go = new GameObject("HouseInteriorMusic");go.transform.SetParent(transform,false);
            interior = go.AddComponent<AudioSource>();interior.playOnAwake=false;interior.clip=interiorClip;interior.loop=true;interior.spatialBlend=0;interior.volume=0;
            go.AddComponent<SceneAmbientLoop>();interior.Play();StartCoroutine(FadeInterior());
        }
    }
    IEnumerator FadeInterior()
    {
        for(float t=0;t<fadeDuration;t+=Time.deltaTime){if(interior==null)yield break;interior.volume=interiorVolume*Mathf.SmoothStep(0,1,t/fadeDuration);yield return null;}
        if(interior!=null)interior.volume=interiorVolume;
    }
    IEnumerator FadeBirds()
    {
        float volume = birds.volume;
        for (float age = 0; age < fadeDuration; age += Time.deltaTime)
        {
            if (birds == null) yield break;
            birds.volume = volume * (1 - Mathf.SmoothStep(0, 1, age / fadeDuration));
            yield return null;
        }
        birds.volume = 0;birds.Stop();
    }
}
