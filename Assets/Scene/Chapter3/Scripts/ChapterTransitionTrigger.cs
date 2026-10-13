using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 플레이어가 닿으면 화면을 검게 덮고 다음 챕터 씬을 불러온 뒤, 새 씬에서 다시 화면을 밝힌다.
///  - Chapter2 추격전 끝(ChaseEnd)에 두어 Chapter3 로 넘어가는 데 쓴다.
///  - 페이드용 화면은 씬이 바뀌어도 사라지지 않게(DontDestroyOnLoad) 만들어, 검은 화면 → 다음 씬 → 밝아짐이 끊기지 않는다.
///    Chapter3 는 검은 공간에서 달리는 장면으로 시작하므로 검게 이어지는 전환이 자연스럽다.
///  - 넘어가는 동안 조작을 잠그고, 새 씬이 시작된 뒤 잠금을 푼다.
///    (새 씬의 컷신이 자기 잠금을 먼저 걸기 때문에 조작이 풀리는 틈은 생기지 않는다)
/// 다음 씬은 File → Build Profiles 의 씬 목록에 들어 있어야 불러올 수 있다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ChapterTransitionTrigger : MonoBehaviour
{
    [Header("다음 씬")]
    [Tooltip("불러올 씬 이름 (Build Profiles 씬 목록에 있는 이름, 예: Chapter3)")]
    public string nextSceneName = "Chapter3";

    [Header("페이드")]
    [Tooltip("화면이 검게 덮이는 시간(초)")]
    [Min(0f)] public float fadeOutTime = 1f;
    [Tooltip("완전히 검은 상태로 기다리는 시간(초). 씬 로딩도 이 사이에 끝난다")]
    [Min(0f)] public float holdTime = 0.3f;
    [Tooltip("새 씬에서 화면이 밝아지는 시간(초)")]
    [Min(0f)] public float fadeInTime = 0.8f;
    [Tooltip("페이드 색")]
    public Color fadeColor = Color.black;

    bool started;

    void Reset() { GetComponent<Collider2D>().isTrigger = true; }

    void Awake() { GetComponent<Collider2D>().isTrigger = true; }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() != null) StartTransition();
    }

    /// <summary>다음 챕터로 넘어가기 시작한다 (UnityEvent 에서 직접 불러도 됨). 한 번만 동작.</summary>
    public void StartTransition()
    {
        if (started) return;
        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogWarning($"[ChapterTransition] '{nextSceneName}' 씬이 Build Profiles 씬 목록에 없어 불러올 수 없음");
            return;
        }
        started = true;

        // 씬이 바뀌어도 살아남는 진행자 오브젝트가 페이드·로딩·잠금 해제를 맡는다
        var runner = new GameObject("ChapterTransition (Fader)").AddComponent<FadeRunner>();
        DontDestroyOnLoad(runner.gameObject);
        runner.Run(this);
    }

    /// <summary>씬 전환 내내 살아 있는 페이드 진행자.</summary>
    class FadeRunner : MonoBehaviour
    {
        public void Run(ChapterTransitionTrigger t)
        {
            // 원래 트리거는 씬과 함께 사라지므로 설정값을 미리 복사해 둔다
            StartCoroutine(Routine(t.nextSceneName, t.fadeOutTime, t.holdTime, t.fadeInTime, t.fadeColor));
        }

        IEnumerator Routine(string scene, float outTime, float hold, float inTime, Color color)
        {
            CutsceneLock.Lock(this);
            var group = BuildOverlay(color);

            yield return Fade(group, 0f, 1f, outTime);

            var load = SceneManager.LoadSceneAsync(scene);
            while (!load.isDone) yield return null;
            yield return null; // 새 씬의 Start(컷신 잠금 등)가 한 번 돈 뒤에

            if (hold > 0f) yield return new WaitForSeconds(hold);
            yield return Fade(group, 1f, 0f, inTime);

            CutsceneLock.Unlock(this);
            Destroy(gameObject);
        }

        static IEnumerator Fade(CanvasGroup g, float from, float to, float time)
        {
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                g.alpha = Mathf.Lerp(from, to, t / time);
                yield return null;
            }
            g.alpha = to;
        }

        CanvasGroup BuildOverlay(Color color)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // 다른 모든 화면 효과보다 위
            var group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            var img = new GameObject("Fade", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(transform, false);
            img.color = color;
            img.raycastTarget = false;
            img.rectTransform.anchorMin = Vector2.zero;
            img.rectTransform.anchorMax = Vector2.one;
            img.rectTransform.offsetMin = img.rectTransform.offsetMax = Vector2.zero;
            return group;
        }

        void OnDestroy() { CutsceneLock.Unlock(this); } // 중간에 끊겨도 잠금이 남지 않게
    }
}
