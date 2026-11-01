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
    TMP_Text body, quote, number, continueHint;
    Image illustration;
    AudioSource voice, fx;
    bool advance;
    Material illustrationInk, bodyInk, quoteInk;
    float currentSpeed=27, currentNarrationDuration, cutAge;
    RectTransform question;readonly System.Collections.Generic.List<RectTransform> motes=new System.Collections.Generic.List<RectTransform>();
    void Awake()
    {
        voice = gameObject.AddComponent<AudioSource>(); voice.playOnAwake = false; voice.spatialBlend = 0; voice.loop = true; voice.clip = textSound; voice.volume = .16f;
        fx = gameObject.AddComponent<AudioSource>(); fx.playOnAwake = false; fx.spatialBlend = 0;
        Build();
    }
    IEnumerator Start()
    {
        FrontEndTransition.Reveal(1.15f);
        yield return Wait(1.1f);
        for (int i = 0; cuts != null && i < cuts.Length; i++)
        {
            CurrentCut=i;var cut=cuts[i];cutAge=0;currentSpeed=cut.textSpeed>0?cut.textSpeed:charactersPerSecond;currentNarrationDuration=cut.narrationDuration;
            page.alpha=0;body.text=cut.narration;body.maxVisibleCharacters=0;quote.text="";
            number.text=(i+1).ToString("00")+" / "+cuts.Length.ToString("00");
            illustration.gameObject.SetActive(cut.illustration!=null);illustration.sprite=cut.illustration;
            illustration.rectTransform.localScale=Vector3.one;illustration.rectTransform.anchoredPosition=picturePosition+contentOffset;
            if(illustrationInk!=null)illustrationInk.SetFloat("_Seed",i*3.17f);
            ApplyLayout();
            SetInkVisibility(0);
            var entering=StartCoroutine(Fade(page,0,1,cut.fadeIn));
            yield return Wait(cut.imageLead);
            int split=i==0&&cut.narration.Contains("있었답니다.")?cut.narration.IndexOf("있었답니다.",StringComparison.Ordinal)+"있었답니다.".Length:-1;
            yield return Type(body,cut.narration,1,split);
            yield return entering;
            if(!string.IsNullOrEmpty(cut.quote))
            {
                yield return Wait(cut.quoteLead);
                yield return GlitchQuote(cut.quote,cut.quoteDuration,cut.quoteSettle);
            }
            advance=false;
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
        }
        continueHint.gameObject.SetActive(false); ChoiceVisible=true;choice.gameObject.SetActive(true);
        yield return Fade(choice,0,1,.9f);choice.interactable=true;choice.blocksRaycasts=true;
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
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || (Input.GetMouseButtonDown(0) && !PauseMenu.IsPointerOnSettingsButton(Input.mousePosition))) advance=true;
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
    IEnumerator GlitchQuote(string value,float duration,float settle){quote.maxVisibleCharacters=int.MaxValue;voice.pitch=.72f;if(textSound!=null)voice.Play();float tick=0;string symbols="%#^*!01<>";for(float t=0;t<duration;t+=PauseMenu.IsPaused?0:Time.unscaledDeltaTime){if(PauseMenu.IsPaused){yield return null;continue;}tick-=Time.unscaledDeltaTime;if(tick<=0){tick=.09f;var chars=value.ToCharArray();for(int n=0;n<3;n++){int j=Random.Range(0,chars.Length);if(!char.IsWhiteSpace(chars[j]))chars[j]=symbols[Random.Range(0,symbols.Length)];}quote.text=new string(chars);quote.color=Color.Lerp(new Color(.9f,.92f,.95f),Random.value>.5f?new Color(.65f,.88f,.94f):new Color(.94f,.66f,.77f),.65f);quote.rectTransform.anchoredPosition=quotePosition+contentOffset+new Vector2(Random.Range(-2f,2f),Random.Range(-1f,1f));}if(t>.9f)voice.Stop();yield return null;}voice.Stop();quote.text=value;quote.color=new Color(.88f,.9f,.93f);quote.rectTransform.anchoredPosition=quotePosition+contentOffset;yield return Wait(settle);}
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
        quote=Text(page.transform,"EyeSpeech","",quotePosition,new Vector2(1400,80),38,new Color(.8f,.83f,.87f));quote.richText=false;
        illustration=new GameObject("CutIllustration",typeof(RectTransform),typeof(Image)).GetComponent<Image>();illustration.transform.SetParent(page.transform,false);illustration.rectTransform.sizeDelta=pictureSize;illustration.rectTransform.anchoredPosition=picturePosition+contentOffset;illustration.preserveAspect=true;illustration.raycastTarget=false;illustration.gameObject.SetActive(false);
        var inkTemplate=Resources.Load<Material>("StoryInkReveal");
        if(inkTemplate!=null){illustrationInk=new Material(inkTemplate);illustration.material=illustrationInk;}
        bodyInk=new Material(body.fontSharedMaterial);body.fontSharedMaterial=bodyInk;
        quoteInk=new Material(quote.fontSharedMaterial);quote.fontSharedMaterial=quoteInk;
        continueHint=Text(canvasGo.transform,"ContinueHint","클릭 / Space로 이어보기",new Vector2(0,-450),new Vector2(600,40),21,new Color(.38f,.4f,.44f));
        continueHint.gameObject.SetActive(false);
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
        quote.rectTransform.anchoredPosition=quotePosition+contentOffset;quote.rectTransform.sizeDelta=quoteSize;
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
