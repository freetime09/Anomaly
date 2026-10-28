using UnityEngine;
[DisallowMultipleComponent]
public sealed class BossAttackSigils : MonoBehaviour
{
    public Sprite[] sprites;
    public Material effectMaterial;
    public Material originalColorMaterial;
    bool useOriginalNext;
    [Range(0.1f,1f)] public float opacity=.8f;
    [Range(4,16)] public int maxVisible=8;
    class Item { public SpriteRenderer r; public float age, delay, fadeIn, hold, fadeOut, size, angle, spin; public Vector3 origin, drift; }
    Item[] pool;Transform anchor;SpriteRenderer source;float nextPreparation;int last=-1;
    public int ActiveCount { get {int n=0;if(pool!=null)foreach(var p in pool)if(p.r.enabled)n++;return n;} }
    public void Initialize(Transform boss,SpriteRenderer renderer){
        anchor=boss;source=renderer;if(pool!=null)return;
        useOriginalNext=Random.value<.5f;
        pool=new Item[12];
        for(int i=0;i<pool.Length;i++){var go=new GameObject("BossAttackEffect_"+i);go.transform.SetParent(anchor,false);var r=go.AddComponent<SpriteRenderer>();r.sharedMaterial=effectMaterial;r.sortingLayerID=source.sortingLayerID;r.sortingOrder=source.sortingOrder-3;r.enabled=false;pool[i]=new Item{r=r};}
    }
    public void Prepare(){if(Time.time<nextPreparation)return;nextPreparation=Time.time+.6f;Emit(Random.Range(1,3),false);}
    public void Impact(){Emit(Random.Range(2,4),true);}
    void Emit(int count,bool impact){
        if(pool==null||anchor==null||sprites==null||sprites.Length==0)return;
        float width=source.bounds.size.x,height=source.bounds.size.y;
        for(int n=0;n<count&&ActiveCount<maxVisible;n++){
            Item p=null;foreach(var slot in pool)if(!slot.r.enabled){p=slot;break;}if(p==null)break;
            int index=Random.Range(0,sprites.Length);if(sprites.Length>1&&index==last)index=(index+1)%sprites.Length;last=index;
            var sprite=sprites[index];if(sprite==null)continue;
            bool isOriginal=useOriginalNext&&originalColorMaterial!=null;
            p.r.sharedMaterial=isOriginal?originalColorMaterial:effectMaterial;useOriginalNext=!useOriginalNext;
            p.r.sprite=sprite;p.age=0;p.delay=impact?n*.045f:Random.Range(0,.16f);p.fadeIn=impact?Random.Range(.045f,.12f):Random.Range(.14f,.32f);p.hold=Random.Range(.18f,.4f);p.fadeOut=isOriginal?Random.Range(1f,3f):Random.Range(.55f,1.1f);
            float a=Random.Range(0,Mathf.PI*2);p.origin=new Vector3(Mathf.Cos(a)*width*Random.Range(.34f,.57f),Mathf.Sin(a)*height*Random.Range(.32f,.62f)+height*.08f,0);
            p.drift=p.origin.normalized*Random.Range(.15f,.45f);p.size=height*1.2f*(isOriginal?1.3f:1f)*Random.Range(.20f,impact?.52f:.42f)/Mathf.Max(.01f,sprite.bounds.size.x);
            p.angle=Random.Range(-35f,35f);p.spin=Random.Range(-13f,13f);p.r.color=new Color(1,1,1,0);p.r.transform.localPosition=p.origin;p.r.transform.localScale=Vector3.one*p.size*.18f;p.r.enabled=true;
        }
    }
    void Update(){
        if(pool==null||PauseMenu.IsPaused)return;
        foreach(var p in pool){if(!p.r.enabled)continue;p.age+=Time.deltaTime;float t=p.age-p.delay;if(t<0)continue;
            float end=p.fadeIn+p.hold+p.fadeOut;if(t>=end){p.r.enabled=false;continue;}
            float grow=Mathf.Clamp01(t/(p.fadeIn+.12f));grow=1-Mathf.Pow(1-grow,3);
            float alpha=t<p.fadeIn?Mathf.SmoothStep(0,1,t/p.fadeIn):1-Mathf.SmoothStep(0,1,(t-p.fadeIn-p.hold)/p.fadeOut);
            p.r.color=new Color(1,1,1,alpha*opacity);p.r.transform.localScale=Vector3.one*p.size*Mathf.Lerp(.18f,1,grow);
            p.r.transform.localPosition=p.origin+p.drift*(t/end);p.r.transform.localRotation=Quaternion.Euler(0,0,p.angle+p.spin*t);
        }
    }
    public void Clear(){if(pool!=null)foreach(var p in pool)if(p.r!=null)p.r.enabled=false;nextPreparation=0;}
    void OnDisable(){Clear();}
}
