using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class BossEyePresentation : MonoBehaviour
{
    public Sprite[] introFrames, shootFrames;
    public Sprite whiteSprite, pupilSprite;
    public Material distortionMaterial, auraMaterial;
    /// <summary>밖에서 강제로 주는 눈 지지직 세기 (0~1). 등장·죽는 연출 때 BossFight 가 올렸다 내림</summary>
    [NonSerialized] public float forceGlitch;
    float waveStrength=.018f;
    /// <summary>지금 그려지는 눈 그림 (지지직 잔상이 같은 그림을 쓰게)</summary>
    public SpriteRenderer Art { get { return art; } }
    /// <summary>화면 파동 한 번 (세기 = 일그러짐 정도)</summary>
    public void Pulse(float strength){pulse=0;waveStrength=strength;}
    /// <summary>등장 애니 중 가장 높은 그림 윗끝까지의 높이 (월드, 눈 중심 기준). 카메라가 다 담도록</summary>
    public float IntroTopOffset()
    {
        float best=0;
        foreach(var f in introFrames){if(f==null)continue;float s=whiteSprite.bounds.size.x/f.bounds.size.x*1.075f;best=Mathf.Max(best,f.bounds.max.y*s);}
        return best*body.transform.lossyScale.y;
    }
    SpriteRenderer body, art, pupil;
    Transform player, visual;
    Material noise, pupilNoise;
    ParticleSystem aura;

    Volume volume;
    VolumeProfile profile;
    Vignette vignette;
    FilmGrain grain;
    Action<float,float> shake;
    float clock, nextGlitch, glitchLeft, auraLeft, pulse=-1f;
    bool combat, hurt, ended;
    float hitLeft;
    Camera waveCamera;
    public int CurrentFrame { get; private set; }
    public bool IsShoot { get { return hurt; } }
    public Vector2 PupilOffset { get { return pupil != null ? (Vector2)pupil.transform.localPosition : Vector2.zero; } }

    public void Initialize(SpriteRenderer source, Transform target, Action<float,float> cameraShake)
    {
        body=source; player=target; shake=cameraShake;
        body.forceRenderingOff=true;
        visual=new GameObject("EyeArtwork").transform; visual.SetParent(body.transform,false);
        art=visual.gameObject.AddComponent<SpriteRenderer>();art.sortingOrder=body.sortingOrder;art.enabled=false;art.color=Color.clear;
        var go=new GameObject("TrackingPupil");go.transform.SetParent(visual,false);
        pupil=go.AddComponent<SpriteRenderer>();pupil.sortingOrder=body.sortingOrder+1;pupil.sprite=pupilSprite;pupil.enabled=false;
        if(distortionMaterial!=null){noise=new Material(distortionMaterial);art.sharedMaterial=noise;pupilNoise=new Material(distortionMaterial);pupil.sharedMaterial=pupilNoise;
            pupilNoise.SetTexture("_MaskTex",whiteSprite.texture);pupilNoise.SetFloat("_UseMask",1f);}
        art.sprite=introFrames.Length>0?introFrames[0]:whiteSprite;
        waveCamera=Camera.main;
        var vg=new GameObject("BossAtmosphere");vg.transform.SetParent(transform,false);
        volume=vg.AddComponent<Volume>();volume.isGlobal=true;volume.priority=70;
        profile=ScriptableObject.CreateInstance<VolumeProfile>();volume.sharedProfile=profile;
        vignette=profile.Add<Vignette>(true);vignette.intensity.value=.27f;vignette.smoothness.value=.6f;vignette.color.value=new Color(.015f,.012f,.025f);
        grain=profile.Add<FilmGrain>(true);grain.intensity.value=0f;grain.response.value=.7f;
        profile.Add<Bloom>(true).intensity.value=0f;
        profile.Add<ChromaticAberration>(true).intensity.value=0f;
        profile.Add<PaniniProjection>(true).distance.value=0f;
        BuildAura();
        nextGlitch=UnityEngine.Random.Range(4f,7f);
    }
    void BuildAura()
    {
        var go=new GameObject("RestrainedBlackAura");go.transform.SetParent(body.transform.parent,false);
        aura=go.AddComponent<ParticleSystem>();aura.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var m=aura.main;m.loop=true;m.startLifetime=new ParticleSystem.MinMaxCurve(.6f,1.25f);m.startSpeed=new ParticleSystem.MinMaxCurve(.35f,.8f);m.startSize=new ParticleSystem.MinMaxCurve(.3f,.75f);m.startColor=new Color(.025f,.012f,.04f,.3f);m.maxParticles=70;m.simulationSpace=ParticleSystemSimulationSpace.World;
        var e=aura.emission;e.rateOverTime=0;
        var sh=aura.shape;sh.shapeType=ParticleSystemShapeType.Circle;sh.radius=3.6f;sh.radiusThickness=.12f;sh.scale=new Vector3(1.5f,.65f,1);
        var vel=aura.velocityOverLifetime;vel.enabled=true;vel.y=.4f;
        var co=aura.colorOverLifetime;co.enabled=true;var grad=new Gradient();grad.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.7f,.18f),new GradientAlphaKey(0,1)});co.color=grad;
        var sz=aura.sizeOverLifetime;sz.enabled=true;sz.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.5f),new Keyframe(1,1.8f)));
        var r=go.GetComponent<ParticleSystemRenderer>();r.sharedMaterial=auraMaterial;r.sortingOrder=body.sortingOrder-2;
        aura.Play();
    }
    public IEnumerator PlayIntro()
    {
        combat=false;hurt=false;body.color=Color.white;pupil.enabled=false;art.enabled=true;art.color=Color.white;
        for(int i=0;i<introFrames.Length;i++)
        {
            CurrentFrame=i;art.sprite=introFrames[i];
            visual.localScale=Vector3.one*(whiteSprite.bounds.size.x/art.sprite.bounds.size.x*1.075f);
            if(i==10){pulse=0;waveStrength=.018f;shake(.38f,.16f);var ep=new ParticleSystem.EmitParams();ep.startColor=new Color(1f,1f,1f,.65f);ep.startSize=.15f;ep.startLifetime=.8f;aura.Emit(ep,22);}
            if(i==23){shake(.55f,.32f);glitchLeft=.16f;vignette.intensity.value=.38f;}
            float wait=i<10?.12f:i<21?.10f:.14f;
            if(i==11)wait=.45f;if(i==22)wait=.35f;
            yield return new WaitForSeconds(wait);
        }
        yield return new WaitForSeconds(.4f);
    }
    public void EnterCombat()
    {
        combat=true;hurt=false;ended=false;art.enabled=true;art.sprite=whiteSprite;visual.localScale=Vector3.one;pupil.enabled=true;pupil.transform.localPosition=new Vector3(0,-.2f,0);
        glitchLeft=0;nextGlitch=UnityEngine.Random.Range(4f,7f);vignette.intensity.value=.27f;
    }
    public void SetHurt(bool value)
    {
        hurt=value;glitchLeft=value?.45f:.12f;pupil.enabled=!value&&combat;
        if(value){shake(.4f,.22f);pulse=0;}
        else {art.sprite=whiteSprite;visual.localScale=Vector3.one;}
    }
    public void HitResponse(){hitLeft=.32f;shake(.2f,.25f);}
    public void AttackAura(float duration=1.3f){if(!ended)auraLeft=Mathf.Max(auraLeft,duration);}
    public void Finish(){ResetWave();ended=true;hurt=false;pupil.enabled=false;volume.weight=0;var e=aura.emission;e.rateOverTime=0;}
    public void Tick(bool defeated)
    {
        if(body==null||PauseMenu.IsPaused)return;
        float dt=Time.deltaTime;float fxDt=Time.unscaledDeltaTime;clock+=fxDt;
        art.color=body.color;pupil.color=body.color;
        if(combat&&!hurt)visual.localScale=Vector3.one;
        if(defeated){pupil.enabled=false;ended=true;}
        if(!ended)
        {
            var drift=new Vector3(Mathf.Sin(clock*.83f)*.018f+Mathf.Sin(clock*1.91f)*.007f,Mathf.Sin(clock*1.12f)*.023f,0);
            if(hurt)drift+=(Vector3)UnityEngine.Random.insideUnitCircle*.024f;
            visual.localPosition=drift;
            visual.localRotation=Quaternion.Euler(0,0,Mathf.Sin(clock*.63f)*.65f);
            if(combat&&!hurt&&player!=null)
            {
                Vector2 dir=(player.position-body.transform.position);dir.Normalize();
                // Restricted ellipse leaves ample space for the entire 87x79 pupil inside the 231x133 white.
                Vector2 want=new Vector2(dir.x*.19f,dir.y*.065f)+new Vector2(Mathf.Sin(clock*19.1f)*.004f,Mathf.Sin(clock*23.7f)*.003f);
                Vector2 ellipse=new Vector2(want.x/.20f,want.y/.075f);if(ellipse.sqrMagnitude>1)want/=ellipse.magnitude;
                want.y-=.18f;
                pupil.transform.localPosition=Vector3.Lerp(pupil.transform.localPosition,want,1-Mathf.Exp(-dt*7));
            }
            if(hurt&&shootFrames.Length>0){CurrentFrame=(int)(clock*11)%shootFrames.Length;art.sprite=shootFrames[CurrentFrame];visual.localScale=Vector3.one*(whiteSprite.pixelsPerUnit/art.sprite.pixelsPerUnit);}
            if(combat&&(nextGlitch-=fxDt)<=0){glitchLeft=.18f;nextGlitch=UnityEngine.Random.Range(4f,7f);}
        }
        glitchLeft=Mathf.Max(0,glitchLeft-fxDt);
        float amount=glitchLeft>0?(hurt?.7f:.45f):0;
        if(hurt&&Mathf.Repeat(clock,.8f)<.12f)amount=.6f;
        amount=Mathf.Max(amount,forceGlitch);
        if(noise!=null){noise.SetFloat("_Amount",amount);noise.SetFloat("_Phase",Mathf.Floor(clock*24));}
        if(pupilNoise!=null){pupilNoise.SetFloat("_Amount",amount);pupilNoise.SetFloat("_Phase",Mathf.Floor(clock*24));
            Vector2 shift=(Vector2)pupil.transform.localPosition*whiteSprite.pixelsPerUnit+whiteSprite.pivot-pupilSprite.rect.position-pupilSprite.pivot;
            pupilNoise.SetVector("_MaskUVOffset",new Vector4(shift.x/whiteSprite.texture.width,shift.y/whiteSprite.texture.height,0,0));}
        // Grain remains restrained; distortion is confined to the boss material.
        grain.intensity.value=0f;
        SetFrameBounds(noise,art.sprite);SetFrameBounds(pupilNoise,pupilSprite);
        if(hitLeft>0){hitLeft=Mathf.Max(0,hitLeft-fxDt);float k=hitLeft/.32f;float punch=Mathf.Sin((1-k)*Mathf.PI)*k;visual.localScale=Vector3.Scale(visual.localScale,new Vector3(1+.23f*punch,1-.2f*punch,1));
            Color hit=Color.Lerp(Color.white,Color.red,Mathf.Clamp01(k*2));hit.a=body.color.a;art.color=hit;pupil.color=hit;}
        Color tint=hitLeft>0?Color.Lerp(Color.white,Color.red,Mathf.Clamp01(hitLeft/.16f)):Color.white;
        if(noise!=null)noise.SetColor("_HitTint",tint);if(pupilNoise!=null)pupilNoise.SetColor("_HitTint",tint);
        auraLeft=Mathf.Max(0,auraLeft-dt);var emission=aura.emission;emission.rateOverTime=!ended&&auraLeft>0?20:0;
        if(pulse>=0 && waveCamera!=null)
        {
            pulse+=fxDt;
            if(pulse<.9f){BossScreenWaveFeature.Target=waveCamera;BossScreenWaveFeature.Strength=waveStrength;
                Vector3 center=waveCamera.WorldToViewportPoint(body.transform.position);
                Shader.SetGlobalVector("_BossWaveCenter",new Vector4(center.x,center.y,0,0));
                Shader.SetGlobalVector("_BossWaveParams",new Vector4(pulse/.9f,waveStrength,waveCamera.aspect,0));}
            else {pulse=-1;ResetWave();}
        }
    }
    static void SetFrameBounds(Material m,Sprite s)
    {
        if(m==null||s==null)return;
        Rect r=s.textureRect;Vector2 size=new Vector2(s.texture.width,s.texture.height);
        m.SetVector("_SpriteRect",new Vector4(r.xMin/size.x,r.yMin/size.y,r.xMax/size.x,r.yMax/size.y));
    }
    void ResetWave(){if(BossScreenWaveFeature.Target==waveCamera){BossScreenWaveFeature.Strength=0;BossScreenWaveFeature.Target=null;Shader.SetGlobalVector("_BossWaveParams",Vector4.zero);}}
    void OnDestroy()
    {
        if(noise!=null)Destroy(noise);if(pupilNoise!=null)Destroy(pupilNoise);ResetWave();
        if(profile!=null){foreach(var c in profile.components)if(c!=null)Destroy(c);Destroy(profile);}
    }
}
