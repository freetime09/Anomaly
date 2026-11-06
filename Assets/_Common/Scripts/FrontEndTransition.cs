using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class FrontEndTransition : MonoBehaviour
{
    static FrontEndTransition active;
    public static bool Busy { get { return active != null; } }
    CanvasGroup group;
    Image loadingPlayer;
    LoadingRunnerFrames runFrames;
    Material silhouetteMaterial;
    AudioSource loadingFootsteps;
    CanvasGroup loadingGroup;
    float loadingStarted;
    int displayedFrame = -1;
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
        BuildLoadingRunner();
        SetCover(1);
    }
    void BuildLoadingRunner()
    {
        var root = new GameObject("LoadingRunner", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        var rect = (RectTransform)root.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        loadingGroup = root.GetComponent<CanvasGroup>();
        loadingGroup.blocksRaycasts = false;
        // Stationary Chapter2-style floor with a single white upper edge.
        var floor = LoadingImage(root.transform, "BlackGround", Vector2.zero, Vector2.zero, new Color(.025f, .03f, .04f, 1));
        floor.rectTransform.anchorMin = Vector2.zero;
        floor.rectTransform.anchorMax = new Vector2(1, .115f);
        floor.rectTransform.offsetMin = floor.rectTransform.offsetMax = Vector2.zero;
        var edge=LoadingImage(root.transform,"WhiteGroundEdge",Vector2.zero,new Vector2(0,2),Color.white);
        edge.rectTransform.anchorMin=new Vector2(0,.115f);edge.rectTransform.anchorMax=new Vector2(1,.115f);
        edge.rectTransform.sizeDelta=new Vector2(0,2);
        loadingFootsteps=gameObject.AddComponent<AudioSource>();loadingFootsteps.playOnAwake=false;loadingFootsteps.loop=true;loadingFootsteps.spatialBlend=0;loadingFootsteps.volume=0;
        runFrames = Resources.Load<LoadingRunnerFrames>("LoadingRunnerFrames");
        if(runFrames!=null)loadingFootsteps.clip=runFrames.footsteps;
        loadingFootsteps.pitch=.96f;
        loadingPlayer = LoadingImage(root.transform, "PlayerRun", Vector2.zero, new Vector2(112, 112), new Color(1,1,1,.82f));
        loadingPlayer.rectTransform.anchorMin = loadingPlayer.rectTransform.anchorMax = new Vector2(.5f, .115f);
        loadingPlayer.rectTransform.pivot = new Vector2(.5f, 0);
        var silhouetteShader = Resources.Load<Shader>("LoadingRunnerSilhouette");
        if (silhouetteShader != null)
        {
            silhouetteMaterial = new Material(silhouetteShader);
            loadingPlayer.material = silhouetteMaterial;
        }
        loadingPlayer.preserveAspect = true;
        if (runFrames != null && runFrames.frames != null && runFrames.frames.Length > 0)
            loadingPlayer.sprite = runFrames.frames[0];
        else loadingPlayer.enabled = false;
        root.SetActive(false);
    }
    static Image LoadingImage(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.one * .5f;
        image.rectTransform.anchoredPosition = position;
        image.rectTransform.sizeDelta = size;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
    void Update()
    {
        if (loadingGroup == null || !loadingGroup.gameObject.activeSelf) return;
        float age = Time.unscaledTime - loadingStarted;
        // Keep the original run recording quiet, with a gentle difference in tone.
        if(loadingFootsteps!=null)loadingFootsteps.pitch=.96f+Mathf.Sin(age*2.4f)*.02f;
        if (runFrames != null && runFrames.frames != null && runFrames.frames.Length > 0)
        {
            int frame = Mathf.FloorToInt(age * runFrames.framesPerSecond) % runFrames.frames.Length;
            if (frame != displayedFrame) { loadingPlayer.sprite = runFrames.frames[frame]; displayedFrame = frame; }
        }
    }
    void SetCover(float cover)
    {
        group.alpha = cover;
        if(loadingFootsteps!=null)loadingFootsteps.volume=.08f*cover*(loadingGroup!=null?loadingGroup.alpha:0);
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
        yield return Fade(0, 1, 1.05f);
        loadingStarted=Time.unscaledTime;loadingGroup.alpha=0;loadingGroup.gameObject.SetActive(true);
        if(loadingFootsteps.clip!=null)loadingFootsteps.Play();
        var load = SceneManager.LoadSceneAsync(scene); load.allowSceneActivation = false;
        for(float t=0;t<2.1f;t+=Time.unscaledDeltaTime){loadingGroup.alpha=Mathf.SmoothStep(0,1,t/.35f);SetCover(1);yield return null;}
        loadingGroup.alpha=1;
        while (load.progress < .9f) yield return null;
        load.allowSceneActivation = true;
        while (!load.isDone) yield return null;
        // Give the new scene its initial layout before revealing it, then fade everything together.
        yield return null;yield return null;Canvas.ForceUpdateCanvases();
        yield return Fade(1, 0, 1.35f);
        loadingFootsteps.Stop();loadingGroup.gameObject.SetActive(false);
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
    void OnDestroy() { if (silhouetteMaterial != null) Destroy(silhouetteMaterial); CutsceneLock.Unlock(this); if (active == this) active = null; }
}
