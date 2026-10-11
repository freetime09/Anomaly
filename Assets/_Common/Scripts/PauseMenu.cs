using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 설정(일시정지) 메뉴 (모든 챕터 공용 — Prefabs/PauseMenu 를 챕터 씬에 하나씩 넣을 것).
///  - 왼쪽 위 톱니 아이콘: 열기/닫기. 열려 있는 동안 시간 정지(Time.timeScale = 0), 소리 일시정지, 그리기 입력 막힘.
///  - 계속하기 / 체크포인트에서 다시 / 소리(전체 음량, 저장됨) / 타이틀로 / 게임 종료
/// 모양(글씨·색·배치)은 프리팹 안의 UI 오브젝트를 직접 고치면 된다.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    /// <summary>메뉴가 열려 있는가 — 입력 스크립트들이 이걸 보고 그리기를 막는다.</summary>
    public static bool IsPaused { get; private set; }

    const string VolumeKey = "MasterVolume";

    [Header("UI 연결")]
    [Tooltip("왼쪽 위 설정(톱니) 아이콘 버튼 — 누르면 메뉴 열기/닫기")]
    public Button settingsButton;
    [Tooltip("열고 닫을 메뉴 루트 (어둡게 덮는 배경 + 버튼들)")]
    public GameObject root;
    public Button continueButton;
    public Button checkpointButton;
    public Slider volumeSlider;
    [Tooltip("음량 % 표시 (없어도 됨)")]
    public TMP_Text volumeValue;
    public Button titleButton;
    public Button quitButton;

    [Header("설정")]
    [Tooltip("'타이틀로'를 누르면 갈 씬 이름 (빌드 설정에 들어 있어야 함)")]
    public string titleScene = "Title";

    float savedTimeScale = 1f;

    void Awake()
    {
        // 저장된 음량을 바로 적용 (메뉴를 안 열어도 반영되도록)
        float vol = PlayerPrefs.GetFloat(VolumeKey, 1f);
        AudioListener.volume = vol;

        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.SetValueWithoutNotify(vol);
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }
        UpdateVolumeText(vol);

        if (settingsButton != null)   settingsButton.onClick.AddListener(Toggle);
        if (continueButton != null)   continueButton.onClick.AddListener(Resume);
        if (checkpointButton != null) checkpointButton.onClick.AddListener(RestartFromCheckpoint);
        if (titleButton != null)      titleButton.onClick.AddListener(GoToTitle);
        if (quitButton != null)       quitButton.onClick.AddListener(QuitGame);

        if (root != null) root.SetActive(false);
        IsPaused = false;

        // 버튼을 누르려면 EventSystem 이 필요 — 씬에 없으면 만든다
        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
    }

    /// <summary>설정 아이콘 위를 누른 건지 — 입력 스크립트가 이 클릭을 제스처로 그리지 않게 확인한다.</summary>
    public static bool IsPointerOnSettingsButton(Vector2 screenPos)
    {
        if (instance == null || instance.settingsButton == null || !instance.settingsButton.gameObject.activeInHierarchy) return false;
        var rt = (RectTransform)instance.settingsButton.transform;
        return RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null); // Overlay 캔버스라 카메라 null
    }

    static PauseMenu instance;

    void OnEnable()  { instance = this; }
    void OnDisable() { if (instance == this) instance = null; }

    /// <summary>설정 아이콘을 누르면 열고, 열려 있을 때 누르면 닫는다.</summary>
    public void Toggle()
    {
        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;
        savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        if (root != null) root.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        Time.timeScale = savedTimeScale;
        AudioListener.pause = false;
        if (root != null) root.SetActive(false);
    }

    /// <summary>마지막 체크포인트(없으면 시작 지점)로 — 떨어져 죽었을 때와 같은 부활 과정을 탄다.</summary>
    public void RestartFromCheckpoint()
    {
        Resume();
        var fr = FindObjectOfType<FallRespawn>();
        if (fr != null) fr.Die();
    }

    public void GoToTitle()
    {
        Resume();
        SceneManager.LoadScene(titleScene);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void SetVolume(float v)
    {
        AudioListener.volume = v;
        PlayerPrefs.SetFloat(VolumeKey, v);
        PlayerPrefs.Save();
        UpdateVolumeText(v);
    }

    void UpdateVolumeText(float v)
    {
        if (volumeValue != null) volumeValue.text = Mathf.RoundToInt(v * 100f) + "%";
    }

    void OnDestroy()
    {
        // 씬이 바뀌며 메뉴가 사라질 때 멈춘 상태가 남지 않게
        if (IsPaused)
        {
            IsPaused = false;
            Time.timeScale = savedTimeScale;
            AudioListener.pause = false;
        }
    }
}
