using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 레버(토글 스위치). 두 가지 방법으로 상태가 바뀐다.
///  1) O(상호작용) 제스처 — 플레이어가 interactRadius 안에 있을 때 PlayerController.Interact() 가 토글.
///  2) 돌아오는/날아가는 나뭇가지 — 콜라이더가 아니라 거리로 감지(돌아올 땐 나뭇가지 콜라이더가 꺼지므로).
/// 켜짐/꺼짐에 따라 스프라이트가 바뀌고, onTurnOn/onTurnOff(UnityEvent)로 대상(문·플랫폼 등)을 인스펙터에서 배선한다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Lever : MonoBehaviour, IInteractable
{
    [Header("상태")]
    [Tooltip("현재/시작 상태. 체크면 on 으로 시작")]
    public bool on = false;

    [Header("스프라이트")]
    [Tooltip("표시할 SpriteRenderer (비우면 자식에서 자동 탐색)")]
    public SpriteRenderer sr;
    [Tooltip("꺼짐 상태 스프라이트 (튜토레버1_0)")]
    public Sprite offSprite;
    [Tooltip("켜짐 상태 스프라이트 (튜토레버2_0)")]
    public Sprite onSprite;

    [Header("상호작용")]
    [Tooltip("O 제스처 상호작용이 먹히는 반경(플레이어 기준, units)")]
    public float interactRadius = 1.2f;
    [Tooltip("돌아오는/날아가는 나뭇가지가 이 반경 안에 들어오면 토글(units)")]
    public float branchRadius = 0.9f;
    [Tooltip("나뭇가지로 토글한 뒤 다시 토글되기까지의 쿨다운(초). 통과하며 두 번 토글되는 것 방지")]
    public float branchCooldown = 0.5f;

    [Header("O 프롬프트 (근처에서 뜨는 O)")]
    [Tooltip("레버 중심에서 위로 얼마나 띄울지 (units)")]
    public float promptHeight = 0.7f;
    [Tooltip("프롬프트 O 색상")]
    public Color promptColor = Color.white;

    [Header("신호 (채널)")]
    [Tooltip("이 레버가 켜지면 이 번호 채널로 신호를 보냄. 같은 번호를 '받는' 오브젝트만 반응.")]
    public int signalId = 0;

    [Header("사운드")]
    [Tooltip("레버를 당길 때(on/off 전환) 재생할 소리")]
    public AudioClip toggleClip;
    [Range(0f, 1f)]
    [Tooltip("소리 볼륨")]
    public float sfxVolume = 1f;

    [Header("이벤트 (대상 배선)")]
    public UnityEvent onTurnOn;
    public UnityEvent onTurnOff;

    Branch branch;
    AudioSource sfxSrc;
    float branchLockUntil;

    GameObject prompt;      // 근처에서 뜨는 'O' 표시
    SpriteRenderer promptSr;

    void Awake()
    {
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
        branch = FindObjectOfType<Branch>();
        sfxSrc = gameObject.AddComponent<AudioSource>();
        sfxSrc.playOnAwake = false;
        CreatePrompt();
        ApplyVisual();
    }

    void OnDestroy()
    {
        if (prompt != null) Destroy(prompt); // 부모 없이 만든 프롬프트 정리
    }

    void OnEnable()  { SignalBus.Set(signalId, this, on); }    // 현재 상태를 채널에 보고
    void OnDisable() { SignalBus.Set(signalId, this, false); } // 꺼질 땐 채널에서 빠짐

    void Update()
    {
        // 나뭇가지가 날아가거나 돌아오는 중일 때, 레버 근처를 지나가면 토글(거리 감지)
        if (branch == null) return;
        var st = branch.CurrentState;
        if (st != Branch.State.Flying && st != Branch.State.Returning) return;
        if (Time.time < branchLockUntil) return;
        if (Vector2.Distance(branch.transform.position, transform.position) <= branchRadius)
        {
            branchLockUntil = Time.time + branchCooldown;
            Toggle();
        }
    }

    /// <summary>플레이어가 interactRadius 안에 있는가? (PlayerController 가 O 상호작용 판정에 사용)</summary>
    public bool PlayerInRange(Vector2 playerPos)
    {
        return Vector2.Distance(playerPos, transform.position) <= interactRadius;
    }

    /// <summary>상호작용 가능 표시(O)를 켜고 끈다. 켜질 땐 레버 위에 똑바로 띄운다.
    /// (나뭇가지 회수 프롬프트와 동일한 링. 표시 여부는 PlayerController 가 결정 — 겹치면 더 가까운 것만 켬)</summary>
    public void ShowPrompt(bool show)
    {
        if (prompt == null) return;
        if (show)
        {
            prompt.transform.position = transform.position + Vector3.up * promptHeight;
            prompt.transform.rotation = Quaternion.identity;
        }
        if (prompt.activeSelf != show) prompt.SetActive(show);
    }

    void CreatePrompt()
    {
        prompt = new GameObject("LeverPrompt"); // 부모로 두지 않음 (스케일/회전 상속 방지)
        promptSr = prompt.AddComponent<SpriteRenderer>();
        promptSr.sprite = GenerateRingSprite();
        promptSr.color = promptColor;
        promptSr.sortingOrder = 100; // 항상 위에 보이게
        prompt.SetActive(false);
    }

    Sprite GenerateRingSprite()
    {
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float c = (size - 1) / 2f;
        float outer = size * 0.5f;
        float inner = size * 0.34f;
        var clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                tex.SetPixel(x, y, (d <= outer && d >= inner) ? Color.white : clear);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 128f);
    }

    /// <summary>IInteractable — O 상호작용 시 토글.</summary>
    public void Interact() { Toggle(); }

    /// <summary>on/off 를 뒤집는다.</summary>
    public void Toggle() { SetOn(!on); }

    /// <summary>상태를 지정한다. 바뀔 때만 스프라이트 갱신 + 이벤트 발생.</summary>
    public void SetOn(bool value)
    {
        if (on == value) return;
        on = value;
        ApplyVisual();
        if (toggleClip != null && sfxSrc != null) sfxSrc.PlayOneShot(toggleClip, sfxVolume); // 레버 소리
        SignalBus.Set(signalId, this, on); // 채널 신호 갱신
        if (on) onTurnOn.Invoke();
        else    onTurnOff.Invoke();
    }

    void ApplyVisual()
    {
        if (sr == null) return;
        var s = on ? onSprite : offSprite;
        if (s != null) sr.sprite = s;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.25f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, interactRadius);
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, branchRadius);
    }
#endif
}
