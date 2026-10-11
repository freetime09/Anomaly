using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 텔레포터. 플레이어가 근처에서 O(상호작용)를 그리면 화면이 페이드아웃되며
/// 연결된(destination) 텔레포터 위치로 순간이동한 뒤 페이드인된다.
/// 페이드/이동은 플레이어의 FallRespawn 을 재사용한다(TeleportTo).
/// 한 쌍이면 서로의 destination 을 가리키게 설정(양방향).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Teleporter : MonoBehaviour, IInteractable
{
    [Header("연결")]
    [Tooltip("이 텔레포터로 상호작용하면 이동할 목적지 텔레포터")]
    public Teleporter destination;
    [Tooltip("목적지에 도착할 때 목적지 텔레포터 기준 위치 보정(units)")]
    public Vector2 exitOffset = Vector2.zero;

    [Header("상호작용")]
    [Tooltip("O 제스처 상호작용이 먹히는 반경(플레이어 기준, units)")]
    public float interactRadius = 1.2f;

    [Header("O 프롬프트")]
    [Tooltip("텔레포터 위로 O를 띄우는 높이(units)")]
    public float promptHeight = 0.9f;
    [Tooltip("프롬프트 O 색상")]
    public Color promptColor = Color.white;

    [Header("사운드")]
    [Tooltip("상호작용으로 텔레포트할 때 소리")]
    public AudioClip teleportClip;
    [Range(0f, 1f)]
    [Tooltip("소리 볼륨")]
    public float sfxVolume = 1f;

    [Header("이벤트")]
    [Tooltip("이 텔레포터로 실제 텔레포트할 때 발생 (예: 튜토리얼에서 스킬 HUD 켜기)")]
    public UnityEvent onTeleport;

    FallRespawn player;
    AudioSource sfxSrc;
    GameObject prompt;
    SpriteRenderer promptSr;

    void Awake()
    {
        player = FindObjectOfType<FallRespawn>();
        sfxSrc = gameObject.AddComponent<AudioSource>();
        sfxSrc.playOnAwake = false;
        CreatePrompt();
    }

    void OnDestroy()
    {
        if (prompt != null) Destroy(prompt);
    }

    // ── IInteractable ──
    public bool PlayerInRange(Vector2 playerPos)
    {
        return Vector2.Distance(playerPos, transform.position) <= interactRadius;
    }

    public void Interact()
    {
        if (destination == null) { Debug.LogWarning("[텔레포터] destination 이 비어있음: " + name); return; }
        if (player == null) player = FindObjectOfType<FallRespawn>();
        if (player == null) return;
        Vector3 dest = destination.transform.position + (Vector3)destination.exitOffset;
        player.TeleportVortex(dest); // 소용돌이 전환으로 이동 (검은 페이드 대체)
        if (teleportClip != null && sfxSrc != null) sfxSrc.PlayOneShot(teleportClip, sfxVolume); // 텔레포트 소리
        onTeleport.Invoke(); // 튜토리얼: 첫 텔레포트 후 스킬 HUD 표시 등
    }

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
        prompt = new GameObject("TeleporterPrompt"); // 부모 없이 (스케일/회전 상속 방지)
        promptSr = prompt.AddComponent<SpriteRenderer>();
        promptSr.sprite = GenerateRingSprite();
        promptSr.color = promptColor;
        promptSr.sortingOrder = 100;
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

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, interactRadius);
        if (destination != null)
        {
            Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.9f);
            Vector3 dest = destination.transform.position + (Vector3)destination.exitOffset;
            Gizmos.DrawLine(transform.position, dest);
            Gizmos.DrawWireCube(dest, Vector3.one * 0.3f);
        }
    }
#endif
}
