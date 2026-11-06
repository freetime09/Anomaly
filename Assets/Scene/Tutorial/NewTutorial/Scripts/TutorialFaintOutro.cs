using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 끝 출구 연출: 닿으면 메크로가 어지러워하다 쓰러지고, 눈이 감기듯 화면이 닫힌 뒤 챕터1로 넘어간다.
/// (챕터1은 쓰러져 있다가 깨어나는 Chapter1Opening 으로 시작하므로 그대로 이어짐)
///  - 같은 오브젝트의 ChapterTransitionTrigger 는 triggerOnTouch 를 끄고 이 스크립트가 마지막에 StartTransition 을 부른다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TutorialFaintOutro : MonoBehaviour
{
    [Tooltip("마지막에 부를 씬 전환 (비우면 같은 오브젝트에서 찾음)")]
    public ChapterTransitionTrigger transition;
    [Tooltip("쓰러지기 전 말풍선 (비우면 대사 없음)")]
    public string line = "어..? 머리가 갑자기...";
    [Tooltip("어지러워 휘청거리는 시간 (초)")]
    public float dizzyTime = 1.6f;
    [Tooltip("쓰러지는 시간 (초)")]
    public float fallTime = 0.45f;
    [Tooltip("쓰러진 뒤 눈이 감기기까지 (초)")]
    public float lieTime = 0.6f;
    [Tooltip("쓰러질 때 '쿵' 소리 (비워도 됨)")]
    public AudioClip thudClip;
    [Range(0f, 1f)] public float thudVolume = 1f;

    bool started;
    PlayerController player;
    Rigidbody2D body;
    Animator animator;
    SpriteRenderer sprite;
    Transform art;
    CameraFollow follow;
    Canvas overlay;
    RectTransform lidTop, lidBottom;
    Image tint;
    Vector2 opaqueMin, opaqueMax;

    void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        if (transition == null) transition = GetComponent<ChapterTransitionTrigger>();
        if (transition != null) transition.triggerOnTouch = false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (started) return;
        var p = other.GetComponentInParent<PlayerController>();
        if (p == null) return;
        started = true;
        player = p;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        CutsceneLock.Lock(this);
        body = player.GetComponent<Rigidbody2D>();
        animator = player.GetComponentInChildren<Animator>();
        sprite = animator != null ? animator.GetComponent<SpriteRenderer>() : player.GetComponentInChildren<SpriteRenderer>();
        art = sprite.transform;
        var cam = Camera.main;
        follow = cam != null ? cam.GetComponent<CameraFollow>() : null;

        player.ResetMotion();
        player.enabled = false;
        // 공중이면 땅에 닿을 때까지 기다림
        for (float t = 0; t < 1.5f && body != null && Mathf.Abs(body.linearVelocity.y) > 0.05f; t += Time.deltaTime) yield return null;
        if (body != null) { body.linearVelocity = Vector2.zero; body.simulated = false; }
        if (animator != null)
        {
            animator.SetFloat("Speed", 0); animator.SetFloat("YVel", 0); animator.SetFloat("Grounded", 1);
            animator.SetBool("Sliding", false); animator.Play("Idle", 0, 0); animator.Update(0);
            animator.enabled = false;
        }
        MeasureOpaqueBounds();
        Vector3 restPos = art.localPosition;
        Quaternion restRot = art.localRotation;
        float groundY = VisibleBottom();
        BuildOverlay();

        // 1) 말풍선 + 어지러워 휘청 (화면이 어두워졌다 밝아지고, 눈이 반쯤 감겼다 뜸)
        SpeechBubble bubble = null;
        if (!string.IsNullOrEmpty(line))
        {
            var speaker = player.GetComponent<DialogueSpeaker>();
            if (speaker == null) speaker = player.gameObject.AddComponent<DialogueSpeaker>();
            bubble = SpeechBubble.Create(speaker);
            StartCoroutine(bubble.Say(line, 14f));
        }
        if (follow != null) follow.Shake(dizzyTime, 0.05f);
        for (float t = 0; t < dizzyTime; t += Time.deltaTime)
        {
            float k = t / dizzyTime;
            art.localRotation = restRot * Quaternion.Euler(0, 0, Mathf.Sin(t * 5.5f) * Mathf.Lerp(3f, 11f, k));
            art.localPosition = restPos;
            art.position += Vector3.up * (groundY - VisibleBottom());
            SetLids(Mathf.Abs(Mathf.Sin(t * 2.6f)) * Mathf.Lerp(0.15f, 0.45f, k));
            tint.color = new Color(0, 0, 0, Mathf.Lerp(0f, 0.35f, k) + Mathf.Sin(t * 7f) * 0.05f);
            yield return null;
        }
        if (bubble != null) bubble.Hide();

        // 2) 털썩 쓰러짐 (바라보는 쪽 뒤로 누움, 얼굴이 하늘로)
        Quaternion from = art.localRotation;
        Quaternion lying = restRot * Quaternion.Euler(0, 0, sprite.flipX ? -90f : 90f);
        for (float t = 0; t < fallTime; t += Time.deltaTime)
        {
            float k = t / fallTime;
            art.localRotation = Quaternion.Slerp(from, lying, k * k); // 점점 빨라지며 넘어감
            art.localPosition = restPos;
            art.position += Vector3.up * (groundY - VisibleBottom());
            SetLids(Mathf.Lerp(0.45f, 0.2f, k));
            yield return null;
        }
        art.localRotation = lying;
        art.localPosition = restPos;
        art.position += Vector3.up * (groundY - VisibleBottom());
        if (follow != null) follow.Shake(0.3f, 0.18f);
        if (thudClip != null) AudioSource.PlayClipAtPoint(thudClip, art.position, thudVolume);
        // 살짝 튕김
        Vector3 lyingPos = art.localPosition;
        for (float t = 0; t < 0.22f; t += Time.deltaTime)
        {
            art.localPosition = lyingPos;
            art.position += Vector3.up * (Mathf.Sin(t / 0.22f * Mathf.PI) * 0.12f);
            yield return null;
        }
        art.localPosition = lyingPos;

        // 3) 눈이 두 번 감겼다 뜨다가 완전히 감김
        yield return new WaitForSeconds(lieTime * 0.5f);
        yield return Lids(0.2f, 0.75f, 0.35f);
        yield return Lids(0.75f, 0.4f, 0.3f);
        yield return Lids(0.4f, 0.9f, 0.4f);
        yield return Lids(0.9f, 0.7f, 0.35f);
        yield return Lids(0.7f, 1f, 0.6f);
        tint.color = Color.black;
        yield return new WaitForSeconds(lieTime);

        // 4) 화면이 이미 까마므로 짧게 챕터1로 (챕터1은 쓰러진 채 깨어나며 시작)
        if (transition != null)
        {
            transition.fadeOutTime = 0.05f;
            transition.StartTransition();
        }
        CutsceneLock.Unlock(this);
    }

    IEnumerator Lids(float from, float to, float time)
    {
        for (float t = 0; t < time; t += Time.deltaTime)
        {
            SetLids(Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, t / time)));
            tint.color = new Color(0, 0, 0, Mathf.Lerp(0.35f, 0.6f, to));
            yield return null;
        }
        SetLids(to);
    }

    /// <summary>눈꺼풀 닫힘 정도 (0 = 다 뜸, 1 = 다 감음).</summary>
    void SetLids(float closed)
    {
        closed = Mathf.Clamp01(closed);
        lidTop.anchorMin = new Vector2(-0.1f, 1f - closed * 0.52f);
        lidBottom.anchorMax = new Vector2(1.1f, closed * 0.52f);
    }

    void BuildOverlay()
    {
        overlay = new GameObject("FaintEyes").AddComponent<Canvas>();
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = 2000;
        tint = MakeImage("Tint", Vector2.zero, Vector2.one);
        tint.color = new Color(0, 0, 0, 0);
        lidTop = MakeImage("LidTop", new Vector2(-0.1f, 1f), new Vector2(1.1f, 1.1f)).rectTransform;
        lidBottom = MakeImage("LidBottom", new Vector2(-0.1f, -0.1f), new Vector2(1.1f, 0f)).rectTransform;
    }

    Image MakeImage(string name, Vector2 min, Vector2 max)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(overlay.transform, false);
        img.color = Color.black;
        img.raycastTarget = false;
        img.rectTransform.anchorMin = min;
        img.rectTransform.anchorMax = max;
        img.rectTransform.offsetMin = img.rectTransform.offsetMax = Vector2.zero;
        return img;
    }

    // 스프라이트의 실제 그림 영역 (투명 여백 제외) — 누울 때 바닥에 맞추려고 (Chapter1Opening 과 같은 방식)
    void MeasureOpaqueBounds()
    {
        var s = sprite.sprite;
        var rect = s.rect;
        var previous = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(s.texture.width, s.texture.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(s.texture, rt);
        RenderTexture.active = rt;
        var pixels = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
        pixels.ReadPixels(rect, 0, 0);
        pixels.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        int left = pixels.width, right = 0, bottom = pixels.height, top = 0;
        var colors = pixels.GetPixels32();
        for (int y = 0; y < pixels.height; y++)
        for (int x = 0; x < pixels.width; x++)
            if (colors[y * pixels.width + x].a > 32)
            { left = Mathf.Min(left, x); right = Mathf.Max(right, x + 1); bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y + 1); }
        opaqueMin = (new Vector2(left, bottom) - s.pivot) / s.pixelsPerUnit;
        opaqueMax = (new Vector2(right, top) - s.pivot) / s.pixelsPerUnit;
        Destroy(pixels);
    }

    float VisibleBottom()
    {
        float y = float.PositiveInfinity;
        for (int i = 0; i < 4; i++)
        {
            Vector3 point = new Vector3((i & 1) == 0 ? opaqueMin.x : opaqueMax.x, (i & 2) == 0 ? opaqueMin.y : opaqueMax.y, 0);
            if (sprite.flipX) point.x = -point.x;
            y = Mathf.Min(y, art.TransformPoint(point).y);
        }
        return y;
    }

    void OnDestroy() { CutsceneLock.Unlock(this); }
}
