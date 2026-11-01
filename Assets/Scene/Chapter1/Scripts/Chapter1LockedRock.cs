using UnityEngine;

// Final combination: a sustained weight AND a lever unbind the rock; the player still has to strike it.
public sealed class Chapter1LockedRock : SignalReceiver, IDamageable, IPuzzleState
{
    public SpriteRenderer artwork;
    bool broken;
    Collider2D solid;
    ParticleSystem dust;
    Material dustMaterial;
    void Awake()
    {
        solid=GetComponent<Collider2D>();
        var go=new GameObject("RockDust");go.transform.SetParent(transform,false);dust=go.AddComponent<ParticleSystem>();dust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=dust.main;main.playOnAwake=false;main.loop=false;main.startLifetime=.5f;main.startSpeed=new ParticleSystem.MinMaxCurve(1.5f,4);main.startSize=new ParticleSystem.MinMaxCurve(.06f,.16f);main.gravityModifier=1;main.maxParticles=48;main.simulationSpace=ParticleSystemSimulationSpace.World;
        var emission=dust.emission;emission.rateOverTime=0;var shape=dust.shape;shape.shapeType=ParticleSystemShapeType.Circle;shape.radius=1.5f;
        dustMaterial=new Material(Shader.Find("Sprites/Default"));dust.GetComponent<ParticleSystemRenderer>().sharedMaterial=dustMaterial;
        Refresh();
    }
    protected override void OnSignalStateChanged(bool on){Refresh();}
    void Refresh(){if(artwork!=null){artwork.enabled=!broken;artwork.color=ConditionMet?Color.white:new Color(.65f,.72f,.67f);}if(solid!=null)solid.enabled=!broken;}
    public void TakeHit(int damage,Vector2 direction)
    {
        if(damage<=0||broken||!ConditionMet)return;
        broken=true;Refresh();if(dust!=null)dust.Emit(new ParticleSystem.EmitParams{startColor=new Color(.5f,.45f,.37f,1)},36);
    }
    public object SaveState(){return broken;}
    public void LoadState(object state){broken=(bool)state;if(solid==null)solid=GetComponent<Collider2D>();Refresh();if(dust!=null)dust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
    void OnDestroy(){if(dustMaterial!=null)Destroy(dustMaterial);}
}
