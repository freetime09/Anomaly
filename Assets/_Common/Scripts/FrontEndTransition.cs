using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public sealed class FrontEndTransition : MonoBehaviour
{
    static FrontEndTransition active;
    public static bool Busy { get { return active != null; } }
    CanvasGroup group;
    readonly Image[] loading = new Image[3];
    CanvasGroup loadingGroup;
    float loadingStarted;
    Texture2D dotTexture;
    Sprite dotSprite;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() { active = null; }
    static FrontEndTransition Create()
    {
        var go = new GameObject("FrontEndFadeTransition");
        var runner = go.AddComponent<FrontEndTransition>();
        active = runner; DontDestroyOnLoad(go); runner.Build(); return runner;
    }
    public static void Reveal(float duration = 1.1f)
    {
        if (Busy) return;
        var runner = Create(); runner.StartCoroutine(runner.RevealRoutine(duration));
    }
    public static bool Load(string scene)
    {
        if (Busy || !Application.CanStreamedLevelBeLoaded(scene)) return false;
        var runner = Create(); runner.StartCoroutine(runner.LoadRoutine(scene)); return true;
    }
    public static void Quit()
    {
        if (Busy) return;
        var runner = Create(); runner.StartCoroutine(runner.QuitRoutine());
    }
    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 4000;
        var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        group = gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = true;
        var image = new GameObject("BlackFade", typeof(RectTransform), typeof(Image)).GetComponent<Image>(); image.transform.SetParent(transform, false);
        image.rectTransform.anchorMin = Vector2.zero; image.rectTransform.anchorMax = Vector2.one; image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        image.color = Color.black;
        var dots=new GameObject("LoadingDots",typeof(RectTransform),typeof(CanvasGroup));dots.transform.SetParent(transform,false);
        var rect=(RectTransform)dots.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.11f);rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(160,44);
        loadingGroup=dots.GetComponent<CanvasGroup>();loadingGroup.blocksRaycasts=false;
        dotTexture=new Texture2D(24,24,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear};
        var pixels=new Color[24*24];
        for(int y=0;y<24;y++)for(int x=0;x<24;x++){float radius=new Vector2(x-11.5f,y-11.5f).magnitude;pixels[y*24+x]=new Color(1,1,1,Mathf.Clamp01(11.5f-radius));}
        dotTexture.SetPixels(pixels);dotTexture.Apply(false,true);dotSprite=Sprite.Create(dotTexture,new Rect(0,0,24,24),Vector2.one*.5f);
        for(int i=0;i<loading.Length;i++){
            var dot=new GameObject("Dot"+i,typeof(RectTransform),typeof(Image));dot.transform.SetParent(dots.transform,false);
            var imageDot=dot.GetComponent<Image>();imageDot.sprite=dotSprite;imageDot.raycastTarget=false;imageDot.color=new Color(.3f,.32f,.36f);imageDot.rectTransform.sizeDelta=Vector2.one*9;
            imageDot.rectTransform.anchoredPosition=new Vector2((i-1)*28,0);loading[i]=imageDot;
        }
        dots.SetActive(false);
        SetCover(1);
    }
    void Update(){
        if(loadingGroup==null||!loadingGroup.gameObject.activeSelf)return;
        float phase=(Time.unscaledTime-loadingStarted)/.34f;
        int activeDot=Mathf.FloorToInt(phase)%3;
        float pulse=Mathf.Sin(Mathf.Repeat(phase,1)*Mathf.PI);
        for(int i=0;i<3;i++){
            bool lit=i==activeDot;loading[i].color=lit?Color.white:new Color(.3f,.32f,.36f);
            loading[i].rectTransform.localScale=Vector3.one*(lit?1.22f+.18f*pulse:1);
            loading[i].rectTransform.anchoredPosition=new Vector2((i-1)*28,lit?pulse*3:0);
        }
    }
    void SetCover(float cover)
    {
        group.alpha = cover;
    }
    IEnumerator Fade(float from, float to, float duration)
    {
        for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
        { SetCover(Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, t / Mathf.Max(.01f, duration)))); yield return null; }
        SetCover(to);
    }
    IEnumerator RevealRoutine(float duration)
    {
        yield return null; yield return Fade(1, 0, duration); Destroy(gameObject);
    }
    IEnumerator LoadRoutine(string scene)
    {
        SetCover(0);
        // Let the recognized ink and its click sound finish before hiding them.
        yield return new WaitForSecondsRealtime(.45f);
        CutsceneLock.Lock(this); SceneMusicBridge.Begin();
        var load = SceneManager.LoadSceneAsync(scene); load.allowSceneActivation = false;
        yield return Fade(0, 1, .85f);
        loadingStarted=Time.unscaledTime;loadingGroup.alpha=0;loadingGroup.gameObject.SetActive(true);
        for(float t=0;t<2.4f;t+=Time.unscaledDeltaTime){loadingGroup.alpha=Mathf.SmoothStep(0,1,t/.2f);yield return null;}
        while (load.progress < .9f) yield return null;
        load.allowSceneActivation = true;
        while (!load.isDone) yield return null;
        for(float t=0;t<.3f;t+=Time.unscaledDeltaTime){loadingGroup.alpha=1-Mathf.SmoothStep(0,1,t/.3f);yield return null;}
        loadingGroup.gameObject.SetActive(false);
        yield return null; yield return Fade(1, 0, 1.05f);
        CutsceneLock.Unlock(this); Destroy(gameObject);
    }
    IEnumerator QuitRoutine()
    {
        SetCover(0); var camera = Camera.main; var music = camera != null ? camera.GetComponent<AudioSource>() : null;
        float original = music != null ? music.volume : 0;
        for (float t = 0; t < .65f; t += Time.unscaledDeltaTime)
        { float u = Mathf.SmoothStep(0, 1, t / .65f); SetCover(u); if (music != null) music.volume = original * (1 - u); yield return null; }
        SetCover(1);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    void OnDestroy() { CutsceneLock.Unlock(this); if (active == this) active = null; if(dotSprite!=null)Destroy(dotSprite);if(dotTexture!=null)Destroy(dotTexture); }
}
