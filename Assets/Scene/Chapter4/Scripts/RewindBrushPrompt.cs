using UnityEngine;
public sealed class RewindBrushPrompt : MonoBehaviour
{
 LineRenderer core,halo; Material ink; AudioSource sound; AudioClip noise;
 public bool Visible {get;private set;}
 public bool Burst {get;private set;}
 float clock,nextBurst,burstLeft; bool soundPaused;
 GestureTrail reference;
 void Awake(){
 reference=Object.FindFirstObjectByType<GestureTrail>();
 ink=new Material(Resources.Load<Material>("BossPlainLine"));
 core=Make("BrushCore",102);halo=Make("BrushHalo",101);
 noise=Resources.Load<AudioClip>("BossSOS");
 sound=gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;sound.spatialBlend=0;sound.ignoreListenerPause=false;sound.volume=.55f;GameAudioAcoustics.Apply(sound);
 }
 LineRenderer Make(string name,int order){
 var go=new GameObject(name);go.transform.SetParent(transform,false);
 var l=go.AddComponent<LineRenderer>();l.sharedMaterial=ink;l.sortingOrder=order;l.numCapVertices=6;l.numCornerVertices=6;l.useWorldSpace=true;return l;
 }
 public void Show(bool on){Visible=on;if(on){clock=0;nextBurst=.65f;}else{core.positionCount=halo.positionCount=0;sound.Stop();soundPaused=false;Burst=false;burstLeft=0;}}
 void LateUpdate(){
 if(!Visible)return;
 if(PauseMenu.IsPaused){core.enabled=halo.enabled=false;if(!soundPaused){sound.Pause();soundPaused=true;}return;}
 if(soundPaused){sound.UnPause();soundPaused=false;}
 core.enabled=halo.enabled=true;
 float dt=Time.unscaledDeltaTime;clock+=dt;
 burstLeft=Mathf.Max(0,burstLeft-dt);
 if(clock>=nextBurst){nextBurst=clock+Mathf.Max(3.8f,noise!=null?noise.length+.9f:3.8f);burstLeft=.24f;sound.pitch=1;if(noise!=null)sound.PlayOneShot(noise);}
 Burst=burstLeft>0;
 var cam=Camera.main;if(cam==null)return;
 float phase=Mathf.Repeat(clock,3.2f),progress=Mathf.Clamp01(phase/1.55f);
 float alpha=phase>2.5f?1-(phase-2.5f)/.7f:1;
 int count=Mathf.Max(2,Mathf.CeilToInt(progress*72)+1);
 core.positionCount=halo.positionCount=count;
 float units=2*cam.orthographicSize/1080f;
 float width=reference!=null?reference.width:.12f;
 Color c=reference!=null?reference.color:Color.white;
 if(reference!=null)reference.ApplyGuideStyle(core,halo,c,alpha);
 else{core.startColor=core.endColor=new Color(c.r,c.g,c.b,alpha);core.widthMultiplier=width;halo.sharedMaterial=ink;halo.widthMultiplier=width*2.8f;halo.startColor=halo.endColor=new Color(c.r,c.g,c.b,alpha*.35f);}
 Vector3 center=cam.ViewportToWorldPoint(new Vector3(.5f,.54f,-cam.transform.position.z));center.z=0;
 for(int i=0;i<count;i++){
 float a=Mathf.PI*.5f+Mathf.Min(i/72f,progress)*Mathf.PI*2;
 float radius=66*(1+.025f*Mathf.Sin(a*3)+.018f*Mathf.Cos(a*5));
 Vector3 p=center+new Vector3(Mathf.Cos(a)*radius*units,Mathf.Sin(a)*radius*units,0);
 if(Burst)p.x+=Mathf.Sin(i*1.7f+Mathf.Floor(clock*28))*.018f;
 core.SetPosition(i,p);halo.SetPosition(i,p);
 }
 }
 void OnDestroy(){if(ink!=null)Destroy(ink);}
}
