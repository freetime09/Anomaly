using UnityEngine;
using UnityEngine.UI;
public sealed class BossDualGauge : MonoBehaviour
{
    class Row {public Material material,background; public float target=1,shown=1,trail=1,hold,flash;}
    Row energy,health;
    RectTransform root;float clock,nextGlitch=5,glitch;
    public float HealthValue {get{return health.target;}}
    public float EnergyValue {get{return energy.target;}}
    public float HealthTrail {get{return health.trail;}}
    public float EnergyTrail {get{return energy.trail;}}
    public void Configure(Sprite frame,Texture green,Texture red,Material template,float width,float top){
        root=GetComponent<RectTransform>();float scale=width/256f;
        root.anchorMin=root.anchorMax=new Vector2(.5f,1);root.pivot=new Vector2(.5f,1);root.sizeDelta=new Vector2(width,27*scale);root.anchoredPosition=new Vector2(0,-top);
        energy=CreateRow("Energy_Green",green,template,new Color(.75f,1f,.22f),scale,7.5f,248f,10f,1f);
        health=CreateRow("Health_Red",red,template,new Color(1f,.65f,.18f),scale,-5.5f,254f,14f,0f);
        var go=new GameObject("OriginalFrame",typeof(RectTransform),typeof(Image));go.transform.SetParent(root,false);
        var rect=(RectTransform)go.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.sizeDelta=Vector2.zero;rect.anchoredPosition=Vector2.zero;
        var img=go.GetComponent<Image>();img.sprite=frame;img.raycastTarget=false;
    }
    Row CreateRow(string name,Texture tex,Material template,Color trail,float scale,float y,float width,float height,float opening){
        var go=new GameObject(name,typeof(RectTransform),typeof(RawImage));go.transform.SetParent(root,false);
        var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(width*scale,height*scale);rect.anchoredPosition=new Vector2(0,y*scale);
        var bg=new GameObject(name+"_Empty",typeof(RectTransform),typeof(RawImage));bg.transform.SetParent(root,false);bg.transform.SetSiblingIndex(go.transform.GetSiblingIndex());var br=(RectTransform)bg.transform;br.anchorMin=br.anchorMax=new Vector2(.5f,.5f);br.sizeDelta=rect.sizeDelta;br.anchoredPosition=rect.anchoredPosition;var bi=bg.GetComponent<RawImage>();bi.texture=tex;bi.color=new Color(0,0,0,.92f);bi.raycastTarget=false;var bm=new Material(template);bm.SetFloat("_Opening",opening);bi.material=bm;
        var image=go.GetComponent<RawImage>();image.texture=tex;image.raycastTarget=false;
        var m=new Material(template);m.SetColor("_TrailTint",trail);m.SetFloat("_Opening",opening);image.material=m;return new Row{material=m,background=bm};
    }
    public void SetValues(float hp,float shield){Set(health,Mathf.Clamp01(hp));Set(energy,Mathf.Clamp01(shield));}
    void Set(Row r,float value){
        if(r==null||Mathf.Approximately(r.target,value))return;
        if(value<r.target){r.trail=Mathf.Max(r.trail,r.shown);r.hold=.32f;r.flash=.24f;glitch=.10f;}
        else {r.shown=r.trail=value;r.hold=0;r.flash=0;}
        r.target=value;
    }
    void TickRow(Row r,float dt){
        r.shown=Mathf.MoveTowards(r.shown,r.target,dt*4.5f);
        if(r.hold>0)r.hold-=dt;else r.trail=Mathf.MoveTowards(r.trail,r.target,dt*.62f);
        r.trail=Mathf.Max(r.shown,r.trail);r.flash=Mathf.Max(0,r.flash-dt);
        r.material.SetFloat("_Fill",r.shown);r.material.SetFloat("_Trail",r.trail);r.material.SetFloat("_Flash",r.flash/.24f);r.material.SetFloat("_Clock",clock);r.material.SetFloat("_Glitch",glitch>0?1:0);
    }
    void Update(){
        if(health==null||PauseMenu.IsPaused)return;
        float dt=Time.unscaledDeltaTime;clock+=dt;
        if((nextGlitch-=dt)<=0){glitch=.08f;nextGlitch=Random.Range(5f,9f);}
        glitch=Mathf.Max(0,glitch-dt);TickRow(health,dt);TickRow(energy,dt);
    }
    void OnDestroy(){if(health!=null){Destroy(health.material);Destroy(health.background);}if(energy!=null){Destroy(energy.material);Destroy(energy.background);}}
}
