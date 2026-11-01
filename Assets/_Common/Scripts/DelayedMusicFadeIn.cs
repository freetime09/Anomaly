using System.Collections;
using UnityEngine;
[DefaultExecutionOrder(-250)]
[RequireComponent(typeof(AudioSource))]
public sealed class DelayedMusicFadeIn:MonoBehaviour
{
 public float delay=1f,duration=1.5f;
 AudioSource source;float target;
 void Awake(){source=GetComponent<AudioSource>();target=source.volume;source.playOnAwake=false;source.Stop();source.volume=0;}
 IEnumerator Start(){
 for(float t=0;t<delay;t+=Time.deltaTime)yield return null;
 if(source.clip==null)yield break;
 source.Play();
 for(float t=0;t<duration;t+=Time.deltaTime){source.volume=target*Mathf.SmoothStep(0,1,t/Mathf.Max(.01f,duration));yield return null;}
 source.volume=target;
 }
}