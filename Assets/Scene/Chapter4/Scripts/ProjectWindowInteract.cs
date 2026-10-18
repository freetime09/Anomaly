using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 챕터 4: 유니티 에디터 캡쳐 화면 속 Project 창 상호작용.
///  1) Project 창 앞에서 O → 캡쳐를 'Scene 폴더를 연 화면'(openedCapture)으로 바꾸고, 씬 이름 자리들에 ???Scene 을 덮어 띄운다.
///  2) 한 번 더 O → 화면이 흔들리고 캡쳐가 검게 사라진다 → onBlack (보스전 시작은 여기에 연결).
/// 위치는 캡쳐 그림의 픽셀 좌표(왼쪽 위 기준)로 적는다 → 캡쳐를 다시 찍으면 그림만 바꾸고 좌표만 고치면 된다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ProjectWindowInteract : MonoBehaviour, IInteractable
{
    /// <summary>캡쳐 속 글자 하나를 덮는 사각형 + 대신 보여줄 글자.</summary>
    [System.Serializable]
    public class NameCover
    {
        [Tooltip("덮을 사각형: x, y = 왼쪽 위, z, w = 너비, 높이 (캡쳐 픽셀)")]
        public Vector4 pixelRect;
        public string text = "???Scene";
        [Tooltip("덮개 색 (그 자리 배경색과 같게)")]
        public Color backColor = new Color(0.28f, 0.28f, 0.28f, 1f);
    }

    [Header("캡쳐")]
    [Tooltip("캡쳐 그림을 그리는 배경 SpriteRenderer")]
    public SpriteRenderer background;
    [Tooltip("Scene 폴더를 연 상태의 캡쳐 (1번째 O 뒤에 바꿔 끼움)")]
    public Sprite openedCapture;

    [Header("상호작용 (캡쳐 픽셀 좌표, 왼쪽 위 = 0,0)")]
    [Tooltip("Project 창의 왼쪽·오른쪽 끝 x (픽셀). 이 사이에 서면 O")]
    public Vector2 windowPixelX = new Vector2(706, 965);
    [Tooltip("O 프롬프트를 플레이어 위로 띄우는 높이 (units)")]
    public float promptHeight = 2.4f;

    [Header("???Scene (폴더를 연 캡쳐 위에 덮을 글자들)")]
    public NameCover[] covers =
    {
        new NameCover { pixelRect = new Vector4(823, 439, 60, 12), text = "???Scene", backColor = new Color(0.282f, 0.282f, 0.282f, 1f) },          // Project 창 목록
        new NameCover { pixelRect = new Vector4(1001, 72, 140, 15), text = "???Scene (Scene Asset)", backColor = new Color(0.235f, 0.235f, 0.235f, 1f) }, // Inspector 제목
    };
    public Color nameTextColor = new Color(0.82f, 0.82f, 0.82f, 1f);
    [Tooltip("글자 크기 (덮개 높이 기준 배율). 캡쳐 크기를 바꿔도 덮개에 맞춰 같이 커짐")]
    public float nameFontScale = 9.7f;
    [Tooltip("가끔 글자가 깨지는 간격 (초, 최소~최대)")]
    public Vector2 glitchInterval = new Vector2(0.6f, 1.8f);

    [Header("검은 배경으로")]
    [Tooltip("2번째 O 뒤 아무 일 없이 기다리는 시간 (초). 그 뒤 흔들림·사라짐")]
    public float blackDelay = 0.5f;
    [Tooltip("2번째 O 뒤 흔들리는 시간 (초)")]
    public float shakeTime = 0.8f;
    public float shakeMagnitude = 0.25f;
    [Tooltip("캡쳐가 검게 사라지는 시간 (초)")]
    public float fadeTime = 1.5f;
    [Tooltip("검게 사라진 뒤 켤 오브젝트 (보스전 무대 등)")]
    public GameObject[] enableOnBlack;
    [Tooltip("검게 사라진 뒤 끌 오브젝트 (캡쳐 속 발판 등)")]
    public GameObject[] disableOnBlack;
    [Tooltip("검게 사라진 뒤 (보스전 시작은 여기에 연결)")]
    public UnityEvent onBlack;

    int step;          // 0 = 처음, 1 = 폴더 연 뒤, 2 = 끝
    GameObject prompt;
    GameObject nameRoot;
    readonly List<TMPro.TextMeshPro> texts = new List<TMPro.TextMeshPro>();
    readonly List<SpriteRenderer> backs = new List<SpriteRenderer>();
    float glitchTimer;
    Transform player;

    void Awake()
    {
        var fr = FindAnyObjectByType<FallRespawn>();
        if (fr != null) player = fr.transform;
        CreatePrompt();
    }

    void OnDestroy()
    {
        if (prompt != null) Destroy(prompt);
        CutsceneLock.Unlock(this);
    }

    // ── IInteractable ──
    public bool PlayerInRange(Vector2 p)
    {
        if (step >= 2 || background == null) return false;
        float x0 = PixelToWorld(new Vector2(windowPixelX.x, 0)).x, x1 = PixelToWorld(new Vector2(windowPixelX.y, 0)).x;
        return p.x >= Mathf.Min(x0, x1) && p.x <= Mathf.Max(x0, x1);
    }

    public void ShowPrompt(bool show)
    {
        if (prompt == null) return;
        if (show && player != null) prompt.transform.position = player.position + Vector3.up * promptHeight;
        if (prompt.activeSelf != show) prompt.SetActive(show);
    }

    public void Interact()
    {
        if (step == 0)
        {
            if (openedCapture != null) background.sprite = openedCapture;
            ShowCovers();
            step = 1;
        }
        else if (step == 1)
        {
            step = 2;
            ShowPrompt(false);
            StartCoroutine(ToBlack());
        }
    }

    void Update()
    {
        if (prompt != null && prompt.activeSelf && player != null)
            prompt.transform.position = player.position + Vector3.up * promptHeight;

        // ???Scene 가끔 깨짐
        if (nameRoot != null && nameRoot.activeSelf)
        {
            glitchTimer -= Time.deltaTime;
            if (glitchTimer <= 0f)
            {
                glitchTimer = Random.Range(glitchInterval.x, glitchInterval.y);
                StartCoroutine(GlitchNames());
            }
        }
    }

    IEnumerator GlitchNames()
    {
        const string junk = "?#%&@$!";
        for (int i = 0; i < 4; i++)
        {
            for (int n = 0; n < texts.Count; n++)
            {
                var chars = covers[n].text.ToCharArray();
                for (int k = 0; k < chars.Length; k++) if (chars[k] != ' ' && Random.value < 0.35f) chars[k] = junk[Random.Range(0, junk.Length)];
                texts[n].text = new string(chars);
                texts[n].transform.localPosition = new Vector3(Random.Range(-0.04f, 0.04f), 0f, 0f);
            }
            yield return new WaitForSeconds(0.04f);
        }
        for (int n = 0; n < texts.Count; n++)
        {
            texts[n].text = covers[n].text;
            texts[n].transform.localPosition = Vector3.zero;
        }
    }

    IEnumerator ToBlack()
    {
        CutsceneLock.Lock(this);
        if (blackDelay > 0f) yield return new WaitForSeconds(blackDelay);
        var cam = FindAnyObjectByType<CameraFollow>();
        Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
        if (cam != null) cam.Shake(shakeTime, shakeMagnitude);
        float t = 0f;
        while (t < shakeTime)
        {
            t += Time.deltaTime;
            if (background != null) background.color = Random.value < 0.25f ? new Color(0.6f, 0.6f, 0.6f) : Color.white; // 깜빡임
            yield return null;
        }
        if (cam != null) cam.transform.position = camPos; // 고정 카메라라 흔들림 뒤 제자리로

        // 캡쳐와 ???Scene 이 검게 사라짐
        t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / Mathf.Max(0.01f, fadeTime));
            var dim = new Color(k, k, k, 1f);
            if (background != null) background.color = dim;
            for (int n = 0; n < backs.Count; n++) backs[n].color = covers[n].backColor * dim;
            foreach (var tx in texts) tx.alpha = k;
            yield return null;
        }
        if (background != null) background.gameObject.SetActive(false);
        if (nameRoot != null) nameRoot.SetActive(false);
        foreach (var g in disableOnBlack) if (g != null) g.SetActive(false);
        foreach (var g in enableOnBlack) if (g != null) g.SetActive(true);

        CutsceneLock.Unlock(this);
        onBlack.Invoke();
    }

    void ShowCovers()
    {
        if (nameRoot == null)
        {
            nameRoot = new GameObject("HiddenSceneNames");
            nameRoot.transform.SetParent(background.transform, false);
            var tex = Texture2D.whiteTexture;
            var white = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            foreach (var cv in covers)
            {
                Vector2 tl = PixelToWorld(new Vector2(cv.pixelRect.x, cv.pixelRect.y));
                Vector2 br = PixelToWorld(new Vector2(cv.pixelRect.x + cv.pixelRect.z, cv.pixelRect.y + cv.pixelRect.w));
                Vector2 size = new Vector2(Mathf.Abs(br.x - tl.x), Mathf.Abs(tl.y - br.y));
                var root = new GameObject("Cover");
                root.transform.SetParent(nameRoot.transform, false);
                root.transform.position = (tl + br) * 0.5f;

                // 원래 글자를 덮는 사각형
                var back = new GameObject("Back");
                back.transform.SetParent(root.transform, false);
                var bsr = back.AddComponent<SpriteRenderer>();
                bsr.sprite = white;
                bsr.color = cv.backColor;
                bsr.sortingLayerID = background.sortingLayerID;
                bsr.sortingOrder = background.sortingOrder + 1;
                back.transform.localScale = new Vector3(size.x, size.y, 1f);
                backs.Add(bsr);

                var txtGo = new GameObject("Text");
                txtGo.transform.SetParent(root.transform, false);
                var tx = txtGo.AddComponent<TMPro.TextMeshPro>();
                tx.text = cv.text;
                tx.fontSize = size.y * nameFontScale;
                tx.color = nameTextColor;
                tx.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
                tx.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                tx.rectTransform.sizeDelta = size;
                var mr = tx.GetComponent<MeshRenderer>();
                mr.sortingLayerID = background.sortingLayerID;
                mr.sortingOrder = background.sortingOrder + 2;
                texts.Add(tx);
            }
        }
        nameRoot.SetActive(true);
        glitchTimer = Random.Range(glitchInterval.x, glitchInterval.y);
    }

    /// <summary>캡쳐 픽셀 좌표(왼쪽 위 = 0,0) → 월드 좌표.</summary>
    public Vector2 PixelToWorld(Vector2 px)
    {
        var sp = background.sprite;
        Rect r = sp.rect;
        // 스프라이트 로컬: (픽셀 - 피벗) / ppu, y 는 아래가 + 이므로 뒤집음
        Vector2 local = new Vector2(px.x - sp.pivot.x, (r.height - px.y) - sp.pivot.y) / sp.pixelsPerUnit;
        return background.transform.TransformPoint(local);
    }

    void CreatePrompt()
    {
        prompt = new GameObject("ProjectWindowPrompt"); // 부모 없이 (스케일 상속 방지)
        var sr = prompt.AddComponent<SpriteRenderer>();
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float c = (size - 1) / 2f, outer = size * 0.5f, inner = size * 0.34f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                tex.SetPixel(x, y, (d <= outer && d >= inner) ? Color.white : new Color(0f, 0f, 0f, 0f));
            }
        tex.Apply();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 128f);
        sr.sortingOrder = 100;
        prompt.SetActive(false);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (background == null || background.sprite == null) return;
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
        Vector2 a = PixelToWorld(new Vector2(windowPixelX.x, 700)), b = PixelToWorld(new Vector2(windowPixelX.y, 385));
        Gizmos.DrawWireCube((a + b) * 0.5f, new Vector3(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), 0f));
        Gizmos.color = Color.cyan;
        foreach (var cv in covers)
        {
            Vector2 tl = PixelToWorld(new Vector2(cv.pixelRect.x, cv.pixelRect.y));
            Vector2 br = PixelToWorld(new Vector2(cv.pixelRect.x + cv.pixelRect.z, cv.pixelRect.y + cv.pixelRect.w));
            Gizmos.DrawWireCube((tl + br) * 0.5f, new Vector3(Mathf.Abs(br.x - tl.x), Mathf.Abs(tl.y - br.y), 0f));
        }
    }
#endif
}
