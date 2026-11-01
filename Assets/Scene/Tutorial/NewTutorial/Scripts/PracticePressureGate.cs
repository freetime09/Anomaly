using UnityEngine;

// Keep the gate safe while the player crosses it, then leave the cleared passage open.
public sealed class PracticePressureGate : MonoBehaviour, IPuzzleState
{
    SignalDisappear gate;
    SpriteRenderer visual;
    Collider2D playerBody;
    Bounds passage;
    bool completed, crossing;
    void Awake()
    {
        gate=GetComponent<SignalDisappear>();visual=GetComponent<SpriteRenderer>();
        var box=GetComponent<BoxCollider2D>();
        var center=transform.TransformPoint(box.offset);
        var scale=transform.lossyScale;
        passage=new Bounds(center,new Vector3(box.size.x*Mathf.Abs(scale.x),box.size.y*Mathf.Abs(scale.y),10));
        var player=FindAnyObjectByType<PlayerController>();if(player!=null)playerBody=player.GetComponent<Collider2D>();
    }
    void Update()
    {
        if(completed||playerBody==null||visual.enabled)return;
        var body=playerBody.bounds;
        if(!crossing&&body.Intersects(passage)){crossing=true;gate.reappearWhenOff=false;}
        if(!crossing)return;
        if(body.min.x>passage.max.x+.04f){completed=true;return;}
        if(body.max.x<passage.min.x-.04f){
            crossing=false;gate.reappearWhenOff=true;
            bool active=false;foreach(int id in gate.signalIds)if(SignalBus.IsOn(id)){active=true;break;}
            if(!active)gate.LoadState(false);
        }
    }
    public object SaveState(){return completed;}
    public void LoadState(object state){completed=(bool)state;crossing=false;gate.reappearWhenOff=!completed;}
}
