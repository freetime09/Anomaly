using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Distorts only registered HUD pixels, without moving gameplay objects or changing values.
[DefaultExecutionOrder(1500)]
public sealed class BossUiGlitch : MonoBehaviour
{
    class Target
    {
        public Image image;
        public SpriteRenderer sprite;
        public Material original, material;
        public MaterialPropertyBlock properties;
    }
    static BossUiGlitch current;
    public static float Strength { get { return current!=null?current.strength:0; } }
    readonly List<Target> targets=new List<Target>(16);
    BossFight fight;
    Material uiTemplate, spriteTemplate;
    float clock, next=4, remaining, duration=.12f, strength;
    bool running;
    public int TargetCount { get { return targets.Count; } }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState(){current=null;}
    public void Begin(BossFight owner)
    {
        End();current=this;fight=owner;running=true;clock=0;next=3.5f;
        uiTemplate=Resources.Load<Material>("BossHudGlitchUI");spriteTemplate=Resources.Load<Material>("BossHudGlitchSprite");
    }
    public static void Register(Transform root)
    {
        if(current==null||root==null)return;
        foreach(var image in root.GetComponentsInChildren<Image>(true))
        {
            if(current.targets.Exists(t=>t.image==image))continue;
            var material=new Material(current.uiTemplate);
            current.targets.Add(new Target{image=image,original=image.material,material=material});image.material=material;
        }
        foreach(var sprite in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if(!sprite.name.StartsWith("Skill_")||current.targets.Exists(t=>t.sprite==sprite))continue;
            current.targets.Add(new Target{sprite=sprite,original=sprite.sharedMaterial,properties=new MaterialPropertyBlock()});sprite.sharedMaterial=current.spriteTemplate;
        }
    }
    public static void Pulse()
    {
        if(current==null)return;current.remaining=current.duration=.13f;
    }
    void LateUpdate()
    {
        if(!running||PauseMenu.IsPaused)return;
        float dt=Time.unscaledDeltaTime;clock+=dt;remaining=Mathf.Max(0,remaining-dt);
        if(clock>=next){remaining=duration=fight!=null&&fight.VisualPhase>=2?.14f:.09f;next=clock+(fight!=null&&fight.VisualPhase>=2?Random.Range(1.8f,3.2f):Random.Range(4.5f,7.5f));}
        strength=remaining>0?Mathf.Sin(Mathf.Clamp01(remaining/duration)*Mathf.PI)*.9f:0;
        foreach(var target in targets)
        {
            Sprite sprite=target.image!=null?target.image.sprite:target.sprite!=null?target.sprite.sprite:null;
            if(sprite==null)continue;
            Rect rect=sprite.textureRect;float w=sprite.texture.width,h=sprite.texture.height;
            Vector4 frame=new Vector4(rect.xMin/w,rect.yMin/h,rect.xMax/w,rect.yMax/h);
            if(target.image!=null){target.material.SetFloat("_Clock",clock);target.material.SetFloat("_Glitch",strength);target.material.SetVector("_Frame",frame);}
            else if(target.sprite!=null){target.sprite.GetPropertyBlock(target.properties);target.properties.SetFloat("_Clock",clock);target.properties.SetFloat("_Glitch",strength);target.properties.SetVector("_Frame",frame);target.sprite.SetPropertyBlock(target.properties);}
        }
    }
    public void End()
    {
        foreach(var target in targets)
        {
            if(target.image!=null)target.image.material=target.original;
            if(target.sprite!=null){target.sprite.sharedMaterial=target.original;target.sprite.SetPropertyBlock(null);}
            if(target.material!=null)Destroy(target.material);
        }
        targets.Clear();running=false;strength=0;if(current==this)current=null;
    }
    void OnDestroy(){End();}
}
