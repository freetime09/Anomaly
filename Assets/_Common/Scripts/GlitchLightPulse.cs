using UnityEngine;
using UnityEngine.Rendering.Universal;
public sealed class GlitchLightPulse : MonoBehaviour {
 static GlitchLightPulse instance;Light2D left,right;float remaining,duration,power;Color tint;
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetState(){instance=null;}
 public static void Pulse(Color color,float strength=.3f,float seconds=.18f){
 if(instance==null)instance=new GameObject("GlitchLightPulse").AddComponent<GlitchLightPulse>();
 instance.tint=color;instance.power=Mathf.Max(instance.power,Mathf.Clamp01(strength));instance.remaining=Mathf.Max(instance.remaining,seconds);instance.duration=instance.remaining;
 }
 void Awake(){left=Make("GlitchEdgeLightL");right=Make("GlitchEdgeLightR");}
 Light2D Make(string name){var g=new GameObject(name);g.transform.SetParent(transform);var l=g.AddComponent<Light2D>();l.lightType=Light2D.LightType.Point;l.pointLightInnerRadius=0;l.pointLightOuterRadius=12;l.intensity=0;l.falloffIntensity=.7f;
 var f=typeof(Light2D).GetField("m_ApplyToSortingLayers",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);if(f!=null)f.SetValue(l,System.Array.ConvertAll(SortingLayer.layers,x=>x.id));return l;}
 void LateUpdate(){var c=Camera.main;if(c==null)return;if(!PauseMenu.IsPaused)remaining=Mathf.Max(0,remaining-Time.unscaledDeltaTime);float e=remaining/Mathf.Max(.01f,duration);float intensity=PauseMenu.IsPaused?0:power*.22f*e*(.8f+.2f*Mathf.Sin(Time.unscaledTime*63));
 left.color=tint;right.color=Color.Lerp(tint,Color.white,.18f);left.intensity=intensity;right.intensity=intensity*.65f;
 left.pointLightOuterRadius=right.pointLightOuterRadius=c.orthographicSize*2.1f;
 left.transform.position=c.transform.position+new Vector3(-c.orthographicSize*c.aspect*.55f,1,10);right.transform.position=c.transform.position+new Vector3(c.orthographicSize*c.aspect*.55f,-1,10);if(remaining<=0)power=0;}
}