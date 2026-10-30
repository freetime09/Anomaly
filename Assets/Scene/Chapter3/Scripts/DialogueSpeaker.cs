using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 말할 수 있는 캐릭터(플레이어·NPC)에 붙이는 컴포넌트.
///  - Speaker Id 로 대사(DialogueData)의 화자와 연결된다. 예: 대사의 speakerId 가 "Mecro" 면 Speaker Id 가 "Mecro" 인 캐릭터 위에 말풍선이 뜬다.
///  - 말풍선 모양(폰트·크기·색)과 위치를 캐릭터마다 따로 조절할 수 있다.
///  - 씬에 있는 화자들을 Id 로 찾을 수 있게 전역 목록에 등록한다.
/// </summary>
[DisallowMultipleComponent]
public class DialogueSpeaker : MonoBehaviour
{
    // Id → 화자. 활성화된 화자만 들어 있다.
    static readonly Dictionary<string, DialogueSpeaker> registry = new Dictionary<string, DialogueSpeaker>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { registry.Clear(); }

    [Header("식별")]
    [Tooltip("대사 데이터(DialogueData)의 speakerId 와 같은 값을 넣는다. 씬 안에서 겹치면 안 됨 (예: Mecro, Villager_A)")]
    public string speakerId = "NPC";

    [Header("말풍선 위치")]
    [Tooltip("이 오브젝트 위치 기준, 말풍선 꼬리 끝이 올 지점 (월드 유닛). 보통 머리 바로 위")]
    public Vector2 bubbleOffset = new Vector2(0f, 1.6f);

    [Header("말풍선 모양")]
    [Tooltip("대사 폰트. 프로젝트 공통 대사 폰트는 Galmuri11 SDF (Assets/TextMesh Pro/Fonts). " +
             "컴포넌트를 새로 붙이면 자동으로 들어간다. 다른 폰트를 쓸 때는 한글이 들어 있는 폰트여야 함")]
    public TMP_FontAsset font;
    [Tooltip("글자 크기 (TextMeshPro 월드 기준. 4 ≈ 한 줄 높이 0.4유닛 정도)")]
    public float fontSize = 4f;
    [Tooltip("말풍선 글자 영역의 최대 폭 (유닛). 넘으면 줄바꿈")]
    public float maxWidth = 6f;
    [Tooltip("글자와 말풍선 가장자리 사이 여백 (유닛)")]
    public float padding = 0.25f;
    [Tooltip("테두리 두께 (유닛). 0이면 테두리 없음")]
    public float borderWidth = 0.06f;
    [Tooltip("아래 꼬리(마름모) 한 변의 길이 (유닛)")]
    public float tailSize = 0.3f;
    [Tooltip("꼬리를 아래 대신 오른쪽 변 가운데에 붙임 (말풍선 오른쪽에 있는 화자를 가리킬 때). 이때 Bubble Offset 은 꼬리 끝 = 말풍선 오른쪽 끝")]
    public bool tailRight;
    [Tooltip("말풍선 배경색")]
    public Color backgroundColor = Color.white;
    [Tooltip("말풍선 테두리색")]
    public Color borderColor = Color.black;
    [Tooltip("글자색")]
    public Color textColor = Color.black;
    [Tooltip("말풍선이 나타나고 사라지는 시간(초)")]
    [Min(0f)] public float fadeTime = 0.15f;
    [Tooltip("그리기 순서. 캐릭터·맵보다 위에 보이도록 크게 (테두리=이 값, 배경=+1, 글자=+2)")]
    public int sortingOrder = 200;

    [Header("이름 깨짐 연출 (<glitch>...</glitch>)")]
    [Tooltip("대사에서 <glitch>메크로</glitch> 처럼 감싼 글자가 떨리는 크기 (유닛). '삭제된 존재'라는 느낌을 준다")]
    [Min(0f)] public float glitchJitter = 0.06f;
    [Tooltip("깨진 글자에 무작위로 번쩍이는 색들")]
    public Color[] glitchColors =
    {
        new Color(1f, 0.15f, 0.3f),  // 빨강
        new Color(0.1f, 0.85f, 0.9f), // 청록
        new Color(0.15f, 0.15f, 0.15f) // 거의 검정
    };
    [Tooltip("깨진 글자가 잠깐 사라지는 확률 (프레임마다). 0이면 사라지지 않음")]
    [Range(0f, 1f)] public float glitchDropChance = 0.15f;

    [Header("말소리 (선택)")]
    [Tooltip("글자가 찍히는 동안 반복 재생되는 소리. 비우면 소리 없음")]
    public AudioClip voiceClip;
    [Tooltip("말소리 볼륨")]
    [Range(0f, 1f)] public float voiceVolume = 0.5f;
    [Tooltip("말소리 높낮이. 캐릭터마다 다르게 주면 목소리 구분이 된다")]
    [Range(0.5f, 2f)] public float voicePitch = 1f;

    /// <summary>이 캐릭터의 말풍선.</summary>
    public SpeechBubble Bubble { get; private set; }

    /// <summary>말풍선 꼬리 끝이 와야 하는 월드 위치.</summary>
    public Vector3 BubbleAnchor => transform.position + (Vector3)bubbleOffset;

    /// <summary>모든 대사가 같이 쓰는 기본 폰트(TMP 폰트 에셋) 경로. 원본 TTF 도 같은 폴더의 Galmuri11.ttf.</summary>
    public const string DefaultFontPath = "Assets/TextMesh Pro/Fonts/Galmuri11 SDF.asset";

#if UNITY_EDITOR
    // 에디터에서 컴포넌트를 처음 붙이거나 Reset 할 때 기본 대사 폰트를 자동으로 넣는다
    // → 앞으로 만드는 화자도 따로 지정하지 않아도 Galmuri11 로 말한다.
    void Reset()
    {
        font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultFontPath);
    }
#endif

    void Awake()
    {
        if (font == null) Debug.LogWarning($"[DialogueSpeaker] {name}: Font 가 비어 있음 → 한글이 안 보일 수 있음 ({DefaultFontPath} 지정 권장)");
        Bubble = SpeechBubble.Create(this);
    }

    void OnEnable()
    {
        if (registry.TryGetValue(speakerId, out var other) && other != null && other != this)
            Debug.LogWarning($"[DialogueSpeaker] Speaker Id '{speakerId}' 가 중복됨: {other.name}, {name}. 나중에 켜진 쪽으로 덮어씀");
        registry[speakerId] = this;
    }

    void OnDisable()
    {
        if (registry.TryGetValue(speakerId, out var cur) && cur == this) registry.Remove(speakerId);
        if (Bubble != null) Bubble.HideImmediate();
    }

    void OnDestroy()
    {
        if (Bubble != null) Destroy(Bubble.gameObject);
    }

    /// <summary>Id 로 씬의 화자를 찾는다. 없으면 null.</summary>
    public static DialogueSpeaker Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        registry.TryGetValue(id, out var s);
        return s;
    }

    /// <summary>선택 시 Scene 뷰에 말풍선 꼬리 위치를 표시해 bubbleOffset 조절을 돕는다.</summary>
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Vector3 a = BubbleAnchor;
        Gizmos.DrawLine(transform.position, a);
        Gizmos.DrawWireCube(a + Vector3.up * 0.4f, new Vector3(1.6f, 0.8f, 0f));
    }
}
