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

    [Header("상호작용 영역 테두리")]
    public bool showOutline = true;
    [Tooltip("테두리 위·아래 y (캡쳐 픽셀). 좌우는 windowPixelX")]
    public Vector2 outlinePixelY = new Vector2(385, 700);
    [Tooltip("선 두께 (캡쳐 픽셀)")]
    public float outlineWidth = 1.5f;
    public Color outlineColor = new Color(1f, 1f, 1f, 0.85f);

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

    [Header("O 할 때 Project 창 지지직")]
    [Tooltip("O 뒤 Project 창만 잠깐 지지직거리는 시간 (초)")]
    public float windowGlitchTime = 0.25f;
    [Tooltip("지지직 줄무늬 수 (Project 창 안)")]
    public int windowGlitchBars = 5;

    [Header("검은 배경으로")]
    [Tooltip("2번째 O 뒤 아무 일 없이 기다리는 시간 (초). 그 뒤 지지직·사라짐")]
    public float blackDelay = 0.5f;
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
    GameObject outline;
    readonly List<TMPro.TextMeshPro> texts = new List<TMPro.TextMeshPro>();
    readonly List<SpriteRenderer> backs = new List<SpriteRenderer>();
    float glitchTimer;
    Transform player;

    void Awake()
    {
        var fr = FindAnyObjectByType<FallRespawn>();
        if (fr != null) player = fr.transform;
        CreatePrompt();
        if (showOutline) CreateOutline();
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
            step = 1;
            StartCoroutine(OpenFolder());
        }
        else if (step == 1 && !opening)
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

    bool opening;

    /// <summary>1번째 O: Project 창 부분만 잠깐 지지직거리며 폴더를 연 캡쳐로 바뀐다. 나머지 화면은 그대로.</summary>
    IEnumerator OpenFolder()
    {
        opening = true;
        CutsceneLock.Lock(this);
        ShowPrompt(false);
        Sprite after = openedCapture != null ? openedCapture : background.sprite;
        yield return WindowGlitch(background.sprite, after, true);
        background.sprite = after;
        ShowCovers();
        StartCoroutine(GlitchNames());
        CutsceneLock.Unlock(this);
        opening = false;
    }

    /// <summary>Project 창 영역만 잠깐 지지직. swapPiece 면 창 부분을 잘라 덮어 before ↔ after 가 번갈아 보이며 옆으로 튐
    /// (덮개 글자가 없을 때만 — 있으면 줄무늬만).</summary>
    IEnumerator WindowGlitch(Sprite before, Sprite after, bool swapPiece)
    {
        // Project 창 영역 (캡쳐 픽셀)
        float px0 = Mathf.Min(windowPixelX.x, windowPixelX.y), px1 = Mathf.Max(windowPixelX.x, windowPixelX.y);
        float py0 = Mathf.Min(outlinePixelY.x, outlinePixelY.y), py1 = Mathf.Max(outlinePixelY.x, outlinePixelY.y);
        Vector2 wa = PixelToWorld(new Vector2(px0, py0)), wb = PixelToWorld(new Vector2(px1, py1));
        Vector2 wMin = Vector2.Min(wa, wb), wMax = Vector2.Max(wa, wb);

        var root = new GameObject("OpenGlitch").transform;
        root.SetParent(background.transform, false);
        // 창 조각: 옛/새 캡쳐에서 창 부분만 잘라 그 자리에 덮음
        SpriteRenderer piece = null;
        Vector3 pieceLocal = Vector3.zero;
        Sprite beforePiece = null, afterPiece = null;
        if (swapPiece)
        {
            piece = new GameObject("WindowPiece").AddComponent<SpriteRenderer>();
            piece.transform.SetParent(root, false);
            pieceLocal = background.transform.InverseTransformPoint((wMin + wMax) * 0.5f);
            piece.transform.localPosition = pieceLocal;
            piece.sortingLayerID = background.sortingLayerID;
            piece.sortingOrder = background.sortingOrder + 1;
            beforePiece = Crop(before, px0, py0, px1, py1); afterPiece = Crop(after, px0, py0, px1, py1);
        }

        var tex = Texture2D.whiteTexture;
        var white = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        var bars = new List<SpriteRenderer>();
        for (int i = 0; i < windowGlitchBars; i++)
        {
            var sr = new GameObject("Bar").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(root, true);
            sr.sprite = white;
            sr.sortingLayerID = background.sortingLayerID;
            sr.sortingOrder = background.sortingOrder + 4; // 덮개 글자·테두리 위
            bars.Add(sr);
        }
        Vector2 size = wMax - wMin;

        float t = 0f, tick = 0f;
        while (t < windowGlitchTime)
        {
            t += Time.deltaTime;
            tick -= Time.deltaTime;
            if (tick <= 0f)
            {
                tick = 0.05f;
                float a = t / Mathf.Max(0.01f, windowGlitchTime);
                // 갈수록 새 화면이 많이 보임. 창 조각이 옆으로 살짝 튐
                if (piece != null)
                {
                    piece.sprite = Random.value < a ? afterPiece : beforePiece;
                    piece.transform.localPosition = pieceLocal + background.transform.InverseTransformVector(new Vector3(Random.Range(-0.04f, 0.04f) * size.x, 0f, 0f));
                }
                for (int i = 0; i < bars.Count; i++)
                {
                    var sr = bars[i];
                    sr.enabled = Random.value < 0.5f;
                    if (!sr.enabled) continue;
                    float h = size.y * Random.Range(0.01f, 0.06f);
                    float w = size.x * Random.Range(0.3f, 1f);
                    sr.transform.position = new Vector3(Random.Range(wMin.x + w * 0.5f, wMax.x - w * 0.5f), Random.Range(wMin.y + h * 0.5f, wMax.y - h * 0.5f), 0f);
                    sr.transform.localScale = Vector3.one;
                    sr.transform.localScale = new Vector3(w / sr.transform.lossyScale.x, h / sr.transform.lossyScale.y, 1f);
                    Color c = i % 4 == 0 ? new Color(1f, 0.15f, 0.25f) : i % 4 == 1 ? new Color(0.2f, 0.9f, 1f) : i % 4 == 2 ? Color.white : Color.black;
                    c.a = Random.Range(0.2f, 0.45f);
                    sr.color = c;
                }
            }
            yield return null;
        }
        Destroy(root.gameObject);
    }

    IEnumerator ToBlack()
    {
        CutsceneLock.Lock(this);
        if (blackDelay > 0f) yield return new WaitForSeconds(blackDelay);
        // Project 창만 잠깐 지지직 (덮개 글자가 있어 조각은 안 덮고 줄무늬만) + ???Scene 깨짐
        StartCoroutine(GlitchNames());
        if (background != null) yield return WindowGlitch(background.sprite, background.sprite, false);

        // 캡쳐와 ???Scene 이 검게 사라짐
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / Mathf.Max(0.01f, fadeTime));
            var dim = new Color(k, k, k, 1f);
            if (background != null) background.color = dim;
            for (int n = 0; n < backs.Count; n++) backs[n].color = covers[n].backColor * dim;
            if (outlineLine != null) outlineLine.startColor = outlineLine.endColor = outlineColor * dim;
            foreach (var tx in texts) tx.alpha = k;
            yield return null;
        }
        if (background != null) background.gameObject.SetActive(false);
        if (nameRoot != null) nameRoot.SetActive(false);
        if (outline != null) outline.SetActive(false);
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

    /// <summary>캡쳐에서 픽셀 사각형(왼쪽 위 기준)만 잘라낸 스프라이트 (가운데 피벗, 같은 ppu).</summary>
    static Sprite Crop(Sprite src, float x0, float y0, float x1, float y1)
    {
        Rect tr = src.textureRect;
        var r = new Rect(tr.x + x0, tr.y + (src.rect.height - y1), x1 - x0, y1 - y0);
        return Sprite.Create(src.texture, r, new Vector2(0.5f, 0.5f), src.pixelsPerUnit);
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

    LineRenderer outlineLine;

    /// <summary>상호작용 영역(Project 창 앞)을 얇은 흰 선으로 두른다.</summary>
    void CreateOutline()
    {
        if (background == null || background.sprite == null) return;
        outline = new GameObject("InteractOutline");
        outline.transform.SetParent(background.transform, false);
        var lr = outline.AddComponent<LineRenderer>();
        Vector2 a = PixelToWorld(new Vector2(windowPixelX.x, outlinePixelY.x));
        Vector2 b = PixelToWorld(new Vector2(windowPixelX.y, outlinePixelY.y));
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = 4;
        lr.SetPositions(new Vector3[] { new Vector3(a.x, a.y), new Vector3(b.x, a.y), new Vector3(b.x, b.y), new Vector3(a.x, b.y) });
        lr.widthMultiplier = outlineWidth / background.sprite.pixelsPerUnit;
        lr.numCornerVertices = 0;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = outlineColor;
        lr.sortingLayerID = background.sortingLayerID;
        lr.sortingOrder = background.sortingOrder + 3;
        outlineLine = lr;
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
