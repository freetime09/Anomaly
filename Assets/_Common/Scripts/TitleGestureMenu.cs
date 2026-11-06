using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-450)]
public sealed class TitleGestureMenu : MonoBehaviour
{
    public TitleController source;
    public TMP_FontAsset font;
    public string gameScene = "Chapter1", tutorialScene = "TutorialNew", storyScene = "Story";
    public AudioClip returnSound,plantSound,teleportSound;
    public bool IsTransitioning { get; private set; }
    readonly List<UnistrokeRecognizer.Template> templates = new List<UnistrokeRecognizer.Template>();
    readonly List<Vector2> points = new List<Vector2>();
    readonly List<RectTransform> floating = new List<RectTransform>();
    readonly List<Vector2> rests = new List<Vector2>();
    CanvasGroup menu, confirm;
    Canvas menuCanvas;
    RectTransform logo;
    bool drawing, confirming;
    AudioSource draw, feedback;
    Vector2 origin;
    float readyAt;
    TMP_Text gestureHint;
    Coroutine hintRoutine;
    TMP_FontAsset symbolFallback;
    static readonly Color Yellow = new Color(.78f, .49f, .015f);
    static readonly Color Blue = new Color(.025f, .30f, .88f);
    static readonly Color Green = new Color(.025f, .52f, .13f);
    static readonly Color Purple = new Color(.53f, .055f, .80f);
    [Serializable] class Item { public string name; public List<Vector2> points; }
    [Serializable] class Store { public int version; public List<Item> items; }
    void Awake()
    {
        symbolFallback=Resources.Load<TMP_FontAsset>("TitleGestureSymbols");
        if(font!=null&&symbolFallback!=null&&!font.fallbackFontAssetTable.Contains(symbolFallback))font.fallbackFontAssetTable.Add(symbolFallback);
        if (source == null) source = GetComponent<TitleController>();
        if (source != null) { source.enabled = false; if (source.hint != null) source.hint.gameObject.SetActive(false); logo = source.logo; }
        draw = gameObject.AddComponent<AudioSource>(); draw.loop = true; draw.playOnAwake = false; draw.spatialBlend = 0;
        feedback = gameObject.AddComponent<AudioSource>(); feedback.playOnAwake = false; feedback.spatialBlend = 0;
        foreach (var file in Resources.LoadAll<TextAsset>("GestureRecordings")) Read(file);
        if (templates.Count == 0 && source != null && source.bundledTemplates != null) Read(source.bundledTemplates);
        Build(); readyAt = Time.unscaledTime + 1.1f;
    }
    void Read(TextAsset file)
    {
        try { var store = JsonUtility.FromJson<Store>(file.text); if (store == null || store.items == null || store.version < UnistrokeRecognizer.FormatVersion) return;
            foreach (var item in store.items) if (item.name == "S" || item.name == "9" || item.name.StartsWith("O") || item.name == "\u22c9" || item.name == "X")
                templates.Add(new UnistrokeRecognizer.Template { name = item.name, points = item.points });
        } catch (Exception e) { Debug.LogWarning("Title gesture data: " + e.Message); }
    }
    IEnumerator Start()
    {
        if (source != null && source.trail != null)
        {
            source.trail.color = Color.white;
            source.trail.SetSortingOrder(1100);
        }
        FrontEndTransition.Reveal(1.15f);
        if (logo != null) { logo.anchorMin = logo.anchorMax = Vector2.one * .5f; logo.anchoredPosition = new Vector2(0, 310); }
        for (float t = 0; t < .9f; t += Time.unscaledDeltaTime) { menu.alpha = Mathf.SmoothStep(0, 1, t / .9f); yield return null; }
        menu.alpha = 1;
    }
    void Update()
    {
        if(menuCanvas!=null)menuCanvas.enabled=!PauseMenu.IsPaused;
        for (int i = 0; i < floating.Count; i++) floating[i].anchoredPosition = rests[i] + Vector2.up * Mathf.Sin(Time.unscaledTime * .65f + i * 1.7f) * 5;
        if (IsTransitioning || FrontEndTransition.Busy || PauseMenu.IsPaused || Time.unscaledTime < readyAt)
        { CancelDraw(); return; }
        if (Input.GetMouseButtonDown(1)) { CancelDraw(); return; }
        if (Input.GetMouseButtonDown(0) && !PauseMenu.IsPointerOnSettingsButton(Input.mousePosition))
        {
            drawing = true; points.Clear(); origin = Input.mousePosition; points.Add(Vector2.zero);
            if (source != null && source.drawLoopClip != null) { draw.clip = source.drawLoopClip; draw.volume = source.drawVolume * .9f; draw.Play(); }
        }
        else if (drawing && Input.GetMouseButton(0))
        { Vector2 p = (Vector2)Input.mousePosition - origin; if (Vector2.Distance(p, points[points.Count - 1]) >= 4) points.Add(p); }
        else if (drawing && Input.GetMouseButtonUp(0)) EndDraw();
        if (confirming && Input.GetKeyDown(KeyCode.Escape)) CancelQuit();
    }
    void CancelDraw() { drawing = false; points.Clear(); if (draw != null) draw.Stop(); }
    void EndDraw()
    {
        drawing = false; draw.Stop();
        if (points.Count < 5) return;
        if (confirming && IsCancelCorner(points)) { Recognized(new Color(.8f,.08f,.06f)); CancelQuit(); return; }
        var match = UnistrokeRecognizer.Recognize(points, templates);
        if (match.name == null || match.score < .78f) { Fail(); return; }
        if (match.name == "\u22c9" || match.name == "X") { Recognized(new Color(.75f,.45f,1),teleportSound); if (confirming) ConfirmQuit(); else SelectAction(3); }
        else if (!confirming && match.name.StartsWith("O")) { Recognized(new Color(1,.9f,.3f)); SelectAction(0); }
        else Fail();
    }
    // Same two-leg attack corner used in the game; only enabled in quit confirmation.
    public static bool IsCancelCorner(List<Vector2> path)
    {
        if (path == null || path.Count < 5) return false;
        var a = path[0]; var b = path[path.Count - 1]; float best = float.MaxValue; int index = -1;
        for (int i = 2; i < path.Count - 2; i++)
        {
            var c = path[i]; var u = c - a; var v = b - c; if (u.magnitude < 22 || v.magnitude < 22) continue;
            bool forward = Mathf.Abs(u.y) < Mathf.Abs(u.x) * .65f && v.y < -Mathf.Abs(v.x) * 1.4f;
            bool reverse = u.y > Mathf.Abs(u.x) * 1.4f && Mathf.Abs(v.y) < Mathf.Abs(v.x) * .65f;
            if (!forward && !reverse) continue;
            float error = 0;
            for (int k = 1; k < path.Count - 1; k++)
            { var start = k <= i ? a : c; var dir = k <= i ? u : v; var delta = path[k] - start; error += Mathf.Abs(dir.x * delta.y - dir.y * delta.x) / dir.magnitude; }
            error /= path.Count; if (error < best) { best = error; index = i; }
        }
        return index >= 0 && best < Mathf.Max(8, (path[index] - a).magnitude * .12f);
    }
    void Recognized(Color color,AudioClip clip=null) { if (source == null) return; if (source.trail != null) source.trail.SetStrokeColor(color);var sound=clip!=null?clip:source.sfxInteract;if(sound!=null)feedback.PlayOneShot(sound,source.recognizeVolume*.7f); }
    void Fail() { if (source != null && source.failClip != null) feedback.PlayOneShot(source.failClip, source.failVolume * .6f); }
    public void SelectAction(int index)
    {
        if (IsTransitioning || FrontEndTransition.Busy || PauseMenu.IsPaused) return;
        CancelDraw();
        if (index == 3) { confirming = true; if(logo!=null)logo.gameObject.SetActive(false); menu.interactable = false; menu.blocksRaycasts = false; confirm.gameObject.SetActive(true);gestureHint.alpha=0; StartCoroutine(ShowConfirm()); return; }
        // 게임 시작 = 스토리부터 (스토리가 끝나면 튜토리얼로 이어짐)
        string scene = index == 0 ? storyScene : index == 1 ? tutorialScene : storyScene;
        IsTransitioning = FrontEndTransition.Load(scene);
    }
    IEnumerator ShowConfirm() { for (float t = 0; t < .25f; t += Time.unscaledDeltaTime) { confirm.alpha = Mathf.SmoothStep(0,1,t/.25f); menu.alpha = 1-confirm.alpha*.88f; yield return null; } confirm.alpha=1; menu.alpha=.12f; }
    public void CancelQuit() { if(hintRoutine!=null){StopCoroutine(hintRoutine);hintRoutine=null;} confirming=false;if(logo!=null)logo.gameObject.SetActive(true); confirm.gameObject.SetActive(false); menu.alpha=1; menu.interactable=menu.blocksRaycasts=true; readyAt=Time.unscaledTime+.2f; }
    public void ConfirmQuit() { if (!confirming || IsTransitioning) return; SettingsHoverAudio.PlayTutorialClick(this); IsTransitioning=true; FrontEndTransition.Quit(); }
    void Build()
    {
        var go=new GameObject("TitleGestureChoices",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);
        var canvas=go.GetComponent<Canvas>();menuCanvas=canvas;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=Camera.main;canvas.planeDistance=5;canvas.sortingOrder=1050;
        var scale=go.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);scale.matchWidthOrHeight=.5f;
        menu=new GameObject("FloatingMenu",typeof(RectTransform),typeof(CanvasGroup)).GetComponent<CanvasGroup>();menu.transform.SetParent(go.transform,false);Stretch((RectTransform)menu.transform);menu.alpha=0;
        Option(menu.transform,"O", "게임 시작",new Vector2(-380,-150),Yellow,0);
        Option(menu.transform,"⋉", "게임 나가기",new Vector2(380,-150),Purple,3);
        confirm=new GameObject("QuitConfirmation",typeof(RectTransform),typeof(CanvasGroup)).GetComponent<CanvasGroup>();confirm.transform.SetParent(go.transform,false);Stretch((RectTransform)confirm.transform);
        var veil=new GameObject("Veil",typeof(RectTransform),typeof(Image)).GetComponent<Image>();veil.transform.SetParent(confirm.transform,false);Stretch(veil.rectTransform);veil.color=new Color(.94f,.94f,.94f,.96f);
        var prompt=Text(confirm.transform,"Question","정말 게임을 나가시겠습니까?",new Vector2(0,80),new Vector2(1200,90),48, new Color(.16f,.16f,.18f));
        Choice(confirm.transform,"QuitYes","예",new Vector2(-210,-90),Green,()=>ShowGestureHint(true));
        Choice(confirm.transform,"QuitNo","아니오",new Vector2(210,-90),new Color(.8f,.075f,.075f),()=>ShowGestureHint(false));
        gestureHint=Text(confirm.transform,"GestureInstruction","",new Vector2(0,-235),new Vector2(850,65),32,new Color(.06f,.06f,.075f));
        gestureHint.alpha=0;
        confirm.gameObject.SetActive(false);
    }
    void Option(Transform parent,string glyph,string label,Vector2 position,Color color,int index)
    {
        var root=new GameObject("Menu_"+index,typeof(RectTransform));root.transform.SetParent(parent,false);var rect=(RectTransform)root.transform;rect.sizeDelta=new Vector2(480,120);rect.anchoredPosition=position;floating.Add(rect);rests.Add(position);
        var labelText=Text(root.transform,"Label",label,Vector2.zero,new Vector2(480,100),48,new Color(.2f,.2f,.23f));labelText.raycastTarget=true;root.AddComponent<FrontEndHover>().Configure(labelText,new Color(.2f,.2f,.23f),color);
        Text(root.transform,"GestureHint",glyph+" 그리기",new Vector2(0,-58),new Vector2(340,30),22,color);
    }
    void Choice(Transform parent,string name,string value,Vector2 position,Color color,UnityEngine.Events.UnityAction action)
    {
        var t=Text(parent,name,value,position,new Vector2(360,85),43,color);t.raycastTarget=true;
        var button=t.gameObject.AddComponent<Button>();button.targetGraphic=t;button.transition=Selectable.Transition.None;button.onClick.AddListener(action);
        t.gameObject.AddComponent<FrontEndHover>().Configure(t,color,Color.Lerp(color,Color.black,.25f));
    }
    void ShowGestureHint(bool yes)
    {
        if(!confirming||IsTransitioning)return;
        SettingsHoverAudio.PlayTutorialClick(this);
        if(hintRoutine!=null)StopCoroutine(hintRoutine);
        hintRoutine=StartCoroutine(GestureHint(yes?"⋉를 그려보세요.":"ㄱ을 그려보세요."));
    }
    IEnumerator GestureHint(string message)
    {
        gestureHint.text=message;
        for(float t=0;t<.22f;t+=Time.unscaledDeltaTime){gestureHint.alpha=Mathf.SmoothStep(0,1,t/.22f);yield return null;}
        gestureHint.alpha=1;yield return new WaitForSecondsRealtime(1.8f);
        for(float t=0;t<.4f;t+=Time.unscaledDeltaTime){gestureHint.alpha=1-Mathf.SmoothStep(0,1,t/.4f);yield return null;}
        gestureHint.alpha=0;hintRoutine=null;
    }
    TMP_Text Text(Transform parent,string name,string value,Vector2 position,Vector2 size,float fontSize,Color color)
    {var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var t=go.GetComponent<TextMeshProUGUI>();if(font!=null)t.font=font;t.text=value;t.fontSize=fontSize;t.color=color;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;t.rectTransform.anchoredPosition=position;t.rectTransform.sizeDelta=size;return t;}
    static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    void OnDisable(){CancelDraw();}
    void OnDestroy(){if(font!=null&&symbolFallback!=null)font.fallbackFontAssetTable.Remove(symbolFallback);}
}
