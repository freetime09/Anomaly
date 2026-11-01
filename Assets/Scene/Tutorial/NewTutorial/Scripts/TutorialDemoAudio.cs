using UnityEngine;

// Playback-only audio: never invokes the real player, branch or puzzle objects.
public sealed class TutorialDemoAudio : MonoBehaviour
{
    PlayerController player; GestureRecognizer gestures; Branch branch; Breakable box; Lever lever;
    AudioSource draw, gesture, action, branchLoop, branchHit, feet, slide, impact;
    bool visible; float footTimer; int branchMode=-1; double explosionUntil;
    public bool IsVisible { get { return visible; } }

    AudioSource Make(string name, bool loop=false)
    {
        var go=new GameObject("TutorialAudio_"+name);go.transform.SetParent(transform,false);
        var a=go.AddComponent<AudioSource>();a.playOnAwake=false;a.loop=loop;a.spatialBlend=0;
        a.dopplerLevel=0;a.ignoreListenerPause=false;return a;
    }
    void Awake()
    {
        draw=Make("Draw",true);gesture=Make("Gesture");action=Make("Action");
        branchLoop=Make("BranchLoop",true);branchHit=Make("BranchHit");
        feet=Make("Footsteps");slide=Make("Slide");impact=Make("Impact");
    }
    public void Configure(PlayerController sourcePlayer, Branch sourceBranch)
    {
        player=sourcePlayer;branch=sourceBranch;
        gestures=player!=null?player.GetComponent<GestureRecognizer>():null;
        box=FindAnyObjectByType<Breakable>(FindObjectsInactive.Include);
        lever=FindAnyObjectByType<Lever>(FindObjectsInactive.Include);
    }
    public void SetVisible(bool on) {visible=on;if(!on)ResetSounds();}
    public void ResetSounds()
    {
        foreach(var a in new[]{draw,gesture,action,branchLoop,branchHit,feet,slide,impact})if(a!=null)a.Stop();
        footTimer=0;branchMode=-1;explosionUntil=0;
    }
    void OneShot(AudioSource channel, AudioClip clip, float volume)
    {if(visible&&clip!=null)channel.PlayOneShot(clip,volume);}
    public void BeginDraw()
    {
        if(!visible||gestures==null||gestures.drawLoopClip==null)return;
        draw.clip=gestures.drawLoopClip;draw.volume=gestures.drawVolume*.9f;draw.Play();
    }
    public void CompleteDraw(TutorialDemoClip.Shape shape)
    {
        draw.Stop();if(gestures==null)return;AudioClip clip=null;
        switch(shape)
        {
            case TutorialDemoClip.Shape.N:clip=gestures.sfxThrow;break;
            case TutorialDemoClip.Shape.Nine:clip=gestures.sfxPlant;break;
            case TutorialDemoClip.Shape.S:clip=gestures.sfxReturn;break;
            case TutorialDemoClip.Shape.O:clip=gestures.sfxInteract;break;
            case TutorialDemoClip.Shape.X:clip=gestures.sfxTeleport;break;
            case TutorialDemoClip.Shape.Up:clip=gestures.sfxJump;break;
            case TutorialDemoClip.Shape.SlideRight:clip=gestures.sfxSlide;break;
            case TutorialDemoClip.Shape.AttackRight:clip=gestures.sfxAttack;break;
            case TutorialDemoClip.Shape.Right:case TutorialDemoClip.Shape.Left:clip=gestures.sfxMove;break;
            default:clip=gestures.sfxOther;break;
        }
        OneShot(gesture,clip,gestures.recognizeVolume);
    }
    public void Jump(){if(player!=null)OneShot(action,player.jumpClip,player.sfxVolume);}
    public void Land(){if(player!=null)OneShot(action,player.landClip,player.sfxVolume);}
    public void Attack(){if(player!=null)OneShot(action,player.attackClip,player.sfxVolume);}
    public void Throw(){if(player!=null)OneShot(action,player.throwClip,player.sfxVolume);}
    public void Teleport(){if(player!=null)OneShot(action,player.branchTeleportClip,player.sfxVolume);}
    public void Stick(){if(branch!=null)OneShot(branchHit,branch.stickClip,branch.sfxVolume);}
    public void WallHit(){if(branch!=null)OneShot(branchHit,branch.wallHitClip,branch.sfxVolume);}
    public void Catch(){if(branch!=null)OneShot(branchHit,branch.catchClip,branch.sfxVolume);}
    public void Lever(){if(lever!=null)OneShot(impact,lever.toggleClip,lever.sfxVolume);}
    public void Break()
    {
        if(!visible||box==null||box.breakClip==null)return;
        if(box.breakClip.name=="Explosion"&&AudioSettings.dspTime<explosionUntil)return;
        explosionUntil=AudioSettings.dspTime+box.breakClip.length;
        OneShot(impact,box.breakClip,box.breakVolume);
    }
    public void BeginSlide()
    {
        if(!visible||player==null||player.slideClip==null)return;
        slide.clip=player.slideClip;slide.volume=player.sfxVolume;
        slide.time=Mathf.Clamp(player.slideClipStartTime,0,Mathf.Max(0,slide.clip.length-.01f));slide.Play();
    }
    public void Tick(bool moving, bool sliding, bool flying, bool returning, float dt)
    {
        if(!visible)return;
        if(moving&&!sliding&&player!=null)
        {
            footTimer-=dt;
            if(footTimer<=0){if(player.footstepClip!=null){feet.clip=player.footstepClip;feet.volume=player.sfxVolume;feet.Play();}footTimer=player.footstepInterval;}
        }
        else{footTimer=0;feet.Stop();}
        if(!sliding)slide.Stop();
        int mode=returning?2:flying?1:0;
        if(mode==branchMode)return;branchMode=mode;branchLoop.Stop();
        var clip=branch==null?null:mode==2?branch.returnClip:mode==1?branch.flyingClip:null;
        if(clip!=null){branchLoop.clip=clip;branchLoop.volume=branch.sfxVolume;branchLoop.Play();}
    }
    void OnDisable(){SetVisible(false);}
}
