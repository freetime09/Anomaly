using System;
using Random=UnityEngine.Random;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class StorySequence : MonoBehaviour
{
    [Serializable] public sealed class Cut
    {
        public string id;
        [TextArea(2,5)] public string narration;
        [TextArea(1,3)] public string quote;
        public Sprite illustration;
        [Min(0)] public float minimumDuration=8f;
        [Min(0)] public float narrationDuration=0f;
        [Min(0)] public float sentencePause=.85f, quoteSettle=.35f;
        [Min(.1f)] public float fadeIn=.85f, fadeOut=.65f;
        [Min(0)] public float imageLead=.25f, hold=2.5f, gap=.2f;
        [Min(1)] public float textSpeed=27;
        [Min(0)] public float quoteLead=.45f, quoteDuration=2.1f;
        [Range(0,.05f)] public float push=.022f;
        public Vector2 drift=new Vector2(6,0);
    }
    public TMP_FontAsset font;
    public AudioClip textSound, glitchSound;
    public Cut[] cuts;
    public string gameScene = "Chapter1", titleScene = "Title";
    [Tooltip("스토리가 끝나면 넘어갈 씬 (튜토리얼)")]
    public string afterStoryScene = "TutorialNew";
    public float charactersPerSecond = 27;
    public bool allowManualAdvance=true;
    public Vector2 contentOffset=Vector2.zero;
    public Vector2 picturePosition=new Vector2(0,95), pictureSize=new Vector2(870,580);
    public Vector2 narrationPosition=new Vector2(0,-270), narrationSize=new Vector2(1400,140);
    public Vector2 quotePosition=new Vector2(0,-385), quoteSize=new Vector2(1400,80);
    [Min(12)] public float narrationFontSize=42;
    bool layoutDirty;
    public int CurrentCut { get; private set; }
    public bool ChoiceVisible { get; private set; }
    public bool IsTyping { get; private set; }
    public bool Choosing { get; private set; }
    CanvasGroup page, choice;
    Canvas textCanvas;
    TMP_Text body, quote, quoteGhost, number, continueHint;
    RectTransform eyeBubble;
    Image illustration;
    AudioSource voice, fx;
    bool advance, skipRequested, cutActive, cutFinished;
    Coroutine playingCut, enteringCut;
    public bool CanSkipCut { get { return cutActive&&!skipRequested&&!PauseMenu.IsPaused&&!ChoiceVisible&&!FrontEndTransition.Busy; } }
    public bool SkipToNextCut(){if(!CanSkipCut)return false;skipRequested=true;return true;}
    bool holding, quoting, quoteSkip;
    /// <summary>좌클릭: 대사가 나오는 중이면 끝까지 바로 보여 주고, 다 나온 뒤면 다음 장으로.</summary>
    public bool ClickAdvance()
    {
        if(!CanSkipCut)return false;
        if(holding)return SkipToNextCut();
        if(IsTyping){advance=true;return true;}
        if(quoting){quoteSkip=true;return true;}
        return false;
    }
    Material illustrationInk, bodyInk, quoteInk;
    float currentSpeed=27, currentNarrationDuration, cutAge;
    RectTransform question;readonly System.Collections.Generic.List<RectTransform> motes=new System.Collections.Generic.List<RectTransform>();
    void Awake()
    {
        voice = gameObject.AddComponent<AudioSource>(); voice.playOnAwake = false; voice.spatialBlend = 0; voice.loop = true; voice.clip = textSound; voice.volume = .16f;
        fx = gameObject.AddComponent<AudioSource>(); fx.playOnAwake = false; fx.spatialBlend = 0;
        Build();
        gameObject.AddComponent<StorySkipGesture>().sequence=this;
    }
    IEnumerator Start()
    {
        FrontEndTransition.Reveal(1.15f);
        yield return Wait(1.1f);
        for (int i = 0; cuts != null && i < cuts.Length; i++)
        {
            skipRequested=false;cutFinished=false;cutActive=true;
            playingCut=StartCoroutine(PlayCut(i));
            while(!cutFinished&&!skipRequested)yield return null;
            cutActive=false;
            if(skipRequested)
            {
                StopCoroutine(playingCut);if(enteringCut!=null){StopCoroutine(enteringCut);enteringCut=null;}
                voice.Stop();IsTyping=false;advance=false;
                yield return Fade(page,page.alpha,0,.45f);yield return Wait(.12f);
            }
            playingCut=null;
        }
        FrontEndProgress.StoryDone=true;
        // 스토리가 끝나면 묻지 않고 튜토리얼로 이어짐 (선택지 없음)
        continueHint.gameObject.SetActive(false); ChoiceVisible=true;
        while(!(Choosing=FrontEndTransition.Load(afterStoryScene)))yield return null;
        voice.Stop();
    }
    IEnumerator PlayCut(int i)
    {
            holding=false;quoting=false;quoteSkip=false;
            CurrentCut=i;var cut=cuts[i];cutAge=0;currentSpeed=cut.textSpeed>0?cut.textSpeed:charactersPerSecond;currentNarrationDuration=cut.narrationDuration;
            page.alpha=0;body.text=cut.narration;body.maxVisibleCharacters=0;quote.text="";eyeBubble.gameObject.SetActive(false);
            number.text=(i+1).ToString("00")+" / "+cuts.Length.ToString("00");
            illustration.gameObject.SetActive(cut.illustration!=null);illustration.sprite=cut.illustration;
            illustration.rectTransform.localScale=Vector3.one;illustration.rectTransform.anchoredPosition=picturePosition+contentOffset;
            if(illustrationInk!=null)illustrationInk.SetFloat("_Seed",i*3.17f);
            ApplyLayout();
            SetInkVisibility(0);
            enteringCut=StartCoroutine(Fade(page,0,1,cut.fadeIn));
            yield return Wait(cut.imageLead);
            int split=i==0&&cut.narration.Contains("있었답니다.")?cut.narration.IndexOf("있었답니다.",StringComparison.Ordinal)+"있었답니다.".Length:-1;
            yield return Type(body,cut.narration,1,split);
            yield return enteringCut;enteringCut=null;
            if(!string.IsNullOrEmpty(cut.quote))
            {
                yield return Wait(cut.quoteLead);
                yield return GlitchQuote(cut.quote,cut.quoteDuration,cut.quoteSettle);
            }
            advance=false;holding=true;
            float readingHold=0;
            // The clock starts at the beginning of the picture fade, excluding pauses.
            // Both the requested scene duration and the completed-text reading time must pass.
            while(readingHold<cut.hold||cutAge<cut.minimumDuration)
            {
                if(advance&&allowManualAdvance)break;
                if(!PauseMenu.IsPaused)readingHold+=Time.unscaledDeltaTime;
                yield return null;
            }
            yield return Fade(page,1,0,cut.fadeOut);yield return Wait(cut.gap);
        holding=false;cutFinished=true;
    }
    void Update()
    {
        if(layoutDirty){ApplyLayout();layoutDirty=false;}
        if(textCanvas!=null)textCanvas.enabled=!PauseMenu.IsPaused;
        if(ChoiceVisible&&question!=null){question.anchoredPosition=new Vector2(Mathf.Sin(Time.unscaledTime*.47f)*3,70+Mathf.Sin(Time.unscaledTime*.68f)*6);for(int i=0;i<motes.Count;i++){float a=Time.unscaledTime*(.16f+i*.003f)+i*2.39996f;motes[i].anchoredPosition=question.anchoredPosition+new Vector2(Mathf.Cos(a)*(410+i*13),Mathf.Sin(a)*80);}}
        if(!PauseMenu.IsPaused&&!ChoiceVisible&&cuts!=null&&CurrentCut<cuts.Length)
        {
            cutAge+=Time.unscaledDeltaTime;
            if(illustration.gameObject.activeSelf){var cut=cuts[CurrentCut];float motion=1-Mathf.Exp(-cutAge*.12f);
                illustration.rectTransform.localScale=Vector3.one*(1+motion*cut.push);
                illustration.rectTransform.anchoredPosition=picturePosition+contentOffset+cut.drift*motion;}
        }
        if (PauseMenu.IsPaused || ChoiceVisible || FrontEndTransition.Busy || !allowManualAdvance) return;
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) advance=true;
    }
    IEnumerator Type(TMP_Text text,string value,float pitch,int pauseAfter=-1)
    {
        text.text=value;text.maxVisibleCharacters=0;text.ForceMeshUpdate();int total=text.textInfo.characterCount;float typingSpeed=currentNarrationDuration>0?total/currentNarrationDuration:currentSpeed;float visible=0;advance=false;IsTyping=true;
        voice.pitch=pitch;if(textSound!=null)voice.Play();
        while(visible<total)
        {
            if(PauseMenu.IsPaused){if(voice.isPlaying)voice.Pause();yield return null;continue;}
            if(textSound!=null&&!voice.isPlaying)voice.UnPause();
            if(advance){visible=total;advance=false;pauseAfter=-1;}else visible+=Mathf.Min(Time.unscaledDeltaTime,.1f)*typingSpeed;
            if(pauseAfter>0&&visible>=pauseAfter){
                visible=pauseAfter;text.maxVisibleCharacters=pauseAfter;voice.Pause();
                yield return Wait(cuts[CurrentCut].sentencePause);pauseAfter=-1;
                if(textSound!=null)voice.UnPause();
            }
            text.maxVisibleCharacters=Mathf.Min(total,Mathf.FloorToInt(visible));yield return null;
        }
        text.maxVisibleCharacters=int.MaxValue;voice.Stop();IsTyping=false;
    }
    IEnumerator Fade(CanvasGroup target,float from,float to,float duration)
    {
        for(float t=0;t<duration;t+=PauseMenu.IsPaused?0:Time.unscaledDeltaTime){float visibility=Mathf.SmoothStep(from,to,t/duration);target.alpha=visibility;if(target==page)SetInkVisibility(visibility);yield return null;}target.alpha=to;if(target==page)SetInkVisibility(to);
    }
    IEnumerator Wait(float seconds){for(float t=0;t<seconds;t+=PauseMenu.IsPaused?0:Time.unscaledDeltaTime)yield return null;}
    IEnumerator GlitchQuote(string value,float duration,float settle)
    {
        eyeBubble.gameObject.SetActive(true);
        quote.maxVisibleCharacters=int.MaxValue;
        quoting=true;quoteSkip=false;
        voice.pitch=.72f;
        if(textSound!=null)voice.Play();

        float tick=0;
        const string symbols="%#^*!01";
        var normal=new Color(.12f,.10f,.18f);
        for(float t=0;t<duration&&!quoteSkip;t+=PauseMenu.IsPaused?0:Time.unscaledDeltaTime)
        {
            if(PauseMenu.IsPaused){yield return null;continue;}
            tick-=Time.unscaledDeltaTime;
            if(tick<=0)
            {
                tick=.065f;
                float strength=1f-Mathf.SmoothStep(0,1,Mathf.InverseLerp(duration*.42f,duration,t));
                var chars=value.ToCharArray();
                for(int n=0;n<Mathf.CeilToInt(6*strength);n++)
                {
                    int j=Random.Range(0,chars.Length);
                    if(!char.IsWhiteSpace(chars[j]))chars[j]=symbols[Random.Range(0,symbols.Length)];
                }
                quote.text=new string(chars);
                quote.rectTransform.anchoredPosition=new Vector2(Random.Range(-5f,5f)*strength,Random.Range(-2f,2f)*strength);
                quote.color=Color.Lerp(normal,Random.value>.5f?new Color(.43f,.06f,.33f):new Color(.03f,.33f,.39f),strength*.65f);
                quoteGhost.text=quote.text;
                quoteGhost.rectTransform.anchoredPosition=new Vector2(Random.Range(-9f,9f)*strength,Random.Range(-3f,3f)*strength);
                quoteGhost.color=new Color(.94f,.14f,.49f, strength*.35f);
            }
            if(t>.9f&&voice.isPlaying)voice.Stop();
            yield return null;
        }
        voice.Stop();
        quote.text=value;
        quote.color=normal;
        quote.rectTransform.anchoredPosition=Vector2.zero;
        quoteGhost.text="";
        bool skipped=quoteSkip;quoting=false;quoteSkip=false;
        if(!skipped)yield return Wait(Mathf.Max(.65f,settle));
    }

    void BuildEyeBubble(Transform parent)
    {
        eyeBubble=BubblePanel(parent,"EyeSpeechBubble",Vector2.zero,new Vector2(276,94),new Color(.12f,.12f,.2f));
        var tail=BubblePanel(eyeBubble,"Tail",new Vector2(137,0),new Vector2(31,31),new Color(.12f,.12f,.2f));
        tail.localRotation=Quaternion.Euler(0,0,45);
        var fill=new Color(.94f,.94f,.97f);
        var tailFill=BubblePanel(eyeBubble,"TailFill",new Vector2(137,0),new Vector2(24,24),fill);
        tailFill.localRotation=tail.localRotation;
        BubblePanel(eyeBubble,"Paper",Vector2.zero,new Vector2(268,86),fill);
        quoteGhost=Text(eyeBubble,"GlitchEcho","",Vector2.zero,new Vector2(250,84),30,Color.clear);
        quote=Text(eyeBubble,"EyeSpeech","",Vector2.zero,new Vector2(250,84),30,new Color(.12f,.10f,.18f));
        quote.richText=quoteGhost.richText=false;
        eyeBubble.gameObject.SetActive(false);
    }

    RectTransform BubblePanel(Transform parent,string name,Vector2 position,Vector2 size,Color color)
    {
        var panel=new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<Image>();
        panel.transform.SetParent(parent,false);
        panel.color=color;
        panel.raycastTarget=false;
        panel.rectTransform.anchoredPosition=position;
        panel.rectTransform.sizeDelta=size;
        return panel.rectTransform;
    }

    void PlaceEyeBubble()
    {
        var sprite=illustration.sprite;
        if(sprite==null)
        {
            eyeBubble.SetParent(page.transform,false);
            eyeBubble.anchoredPosition=quotePosition+contentOffset;
            return;
        }
        eyeBubble.SetParent(illustration.rectTransform,false);
        var spriteSize=sprite.rect.size;
        float scale=Mathf.Min(pictureSize.x/spriteSize.x,pictureSize.y/spriteSize.y);
        Vector2 size=spriteSize*scale;
        // The left point of the eye sits about 35% across and 73% up the artwork.
        eyeBubble.anchoredPosition=new Vector2(-size.x*.15f-162f,size.y*.235f);
    }
    public void Choose(bool play)
    {
        if(!ChoiceVisible||Choosing||FrontEndTransition.Busy)return;
        SettingsHoverAudio.PlayTutorialClick(this);
        Choosing=FrontEndTransition.Load(play?gameScene:titleScene);
        if(Choosing){choice.interactable=false;voice.Stop();}
    }
    void Build()
    {
        var canvasGo=new GameObject("StoryCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvasGo.transform.SetParent(transform,false);
        var canvas=canvasGo.GetComponent<Canvas>();textCanvas=canvas;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=Camera.main;canvas.planeDistance=5;canvas.sortingOrder=20;
        var scaler=canvasGo.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        page=Group(canvasGo.transform,"CurrentCut");
        number=Text(page.transform,"CutNumber","",new Vector2(0,190),new Vector2(300,45),23,new Color(.45f,.48f,.52f));
        number.gameObject.SetActive(false);
        body=Text(page.transform,"Narration","",new Vector2(0,5),new Vector2(1190,270),44,new Color(.94f,.93f,.89f));body.lineSpacing=12;
        illustration=new GameObject("CutIllustration",typeof(RectTransform),typeof(Image)).GetComponent<Image>();illustration.transform.SetParent(page.transform,false);illustration.rectTransform.sizeDelta=pictureSize;illustration.rectTransform.anchoredPosition=picturePosition+contentOffset;illustration.preserveAspect=true;illustration.raycastTarget=false;illustration.gameObject.SetActive(false);
        BuildEyeBubble(illustration.rectTransform);
        var inkTemplate=Resources.Load<Material>("StoryInkReveal");
        if(inkTemplate!=null){illustrationInk=new Material(inkTemplate);illustration.material=illustrationInk;}
        bodyInk=new Material(body.fontSharedMaterial);body.fontSharedMaterial=bodyInk;
        quoteInk=new Material(quote.fontSharedMaterial);quote.fontSharedMaterial=quoteInk;
        continueHint=Text(canvasGo.transform,"ContinueHint","ㅇ을 그리면 다음 장면",new Vector2(0,-473),new Vector2(600,40),21,new Color(.38f,.4f,.44f,.72f));
        continueHint.gameObject.SetActive(false); // 아래 안내 문구 숨김
        ApplyLayout();
        choice=Group(canvasGo.transform,"BeginGameChoice");choice.alpha=0;choice.interactable=choice.blocksRaycasts=false;
        question=Text(choice.transform,"Question","게임을 바로 시작하시겠습니까?",new Vector2(0,70),new Vector2(1200,110),55,new Color(.94f,.93f,.89f)).rectTransform;
        for(int i=0;i<14;i++){var dot=new GameObject("QuietMote",typeof(RectTransform),typeof(Image)).GetComponent<Image>();dot.transform.SetParent(choice.transform,false);dot.color=new Color(.65f,.74f,.76f,.18f);dot.raycastTarget=false;dot.rectTransform.sizeDelta=Vector2.one*(1.5f+i%3*.4f);motes.Add(dot.rectTransform);}
        Choice(choice.transform,"Yes","예",new Vector2(-190,-110),true);Choice(choice.transform,"No","아니오",new Vector2(190,-110),false);choice.gameObject.SetActive(false);
    }
    void Choice(Transform parent,string name,string label,Vector2 position,bool play)
    {var normal=play?new Color(.37f,.72f,.44f):new Color(.78f,.37f,.38f);var hovered=play?new Color(.52f,1f,.62f):new Color(1f,.5f,.54f);var text=Text(parent,name,label,position,new Vector2(280,90),45,normal);text.raycastTarget=true;var button=text.gameObject.AddComponent<Button>();button.targetGraphic=text;button.transition=Selectable.Transition.None;button.onClick.AddListener(()=>Choose(play));text.gameObject.AddComponent<FrontEndHover>().Configure(text,normal,hovered);}
    CanvasGroup Group(Transform parent,string name){var go=new GameObject(name,typeof(RectTransform),typeof(CanvasGroup));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;return go.GetComponent<CanvasGroup>();}
    TMP_Text Text(Transform parent,string name,string value,Vector2 position,Vector2 size,float fontSize,Color color)
    {var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var t=go.GetComponent<TextMeshProUGUI>();if(font!=null)t.font=font;t.text=value;t.fontSize=fontSize;t.color=color;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;t.rectTransform.anchoredPosition=position;t.rectTransform.sizeDelta=size;return t;}
    void OnValidate(){layoutDirty=true;}
    void ApplyLayout()
    {
        if(illustration==null||body==null||quote==null)return;
        illustration.rectTransform.sizeDelta=pictureSize;
        illustration.rectTransform.anchoredPosition=picturePosition+contentOffset;
        bool hasPicture=cuts!=null&&CurrentCut<cuts.Length&&cuts[CurrentCut].illustration!=null;
        body.rectTransform.anchoredPosition=(hasPicture?narrationPosition:Vector2.zero)+contentOffset;
        body.rectTransform.sizeDelta=hasPicture?narrationSize:new Vector2(1190,270);
        body.fontSize=narrationFontSize;
        body.alignment=TextAlignmentOptions.Top;
        PlaceEyeBubble();
    }
    void SetInkVisibility(float visibility)
    {
        float smudge=1-Mathf.Clamp01(visibility);
        if(illustrationInk!=null){illustrationInk.SetFloat("_Reveal",visibility);illustrationInk.SetFloat("_Smudge",smudge);}
        foreach(var material in new[]{bodyInk,quoteInk})
        {
            if(material==null)continue;
            material.SetFloat("_FaceDilate",smudge*.14f);material.SetFloat("_OutlineWidth",smudge*.12f);
            material.SetFloat("_OutlineSoftness",smudge*.65f);material.SetColor("_OutlineColor",Color.white);
        }
    }
    void OnDisable(){if(voice!=null)voice.Stop();}
    void OnDestroy(){if(illustrationInk!=null)Destroy(illustrationInk);if(bodyInk!=null)Destroy(bodyInk);if(quoteInk!=null)Destroy(quoteInk);}
}
