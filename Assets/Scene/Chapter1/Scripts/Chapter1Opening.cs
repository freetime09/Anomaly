using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class Chapter1Opening : MonoBehaviour
{
    public string[] lines = {
        "으윽.. 머리가..", "뭐지..? 무슨 일이 있었던거야",
        "아까 분명 저기에 이상한 눈이 있었는데!!", "안되겠어 마을로 가봐야겠어!!"
    };
    Camera cam;
    CameraFollow follow;
    PlayerController player;
    Animator animator;
    SpriteRenderer sprite;
    Transform art;
    Vector3 restPosition;
    Quaternion restRotation;
    bool animated, simulated, playerEnabled, followEnabled, finished;
    Rigidbody2D body;
    Canvas overlay;
    Material reveal;
    SpeechBubble bubble;
    DialogueSpeaker speaker;
    float previousFade, groundY;
    Vector2 opaqueMin, opaqueMax;

    IEnumerator Start()
    {
        player = FindAnyObjectByType<PlayerController>();
        cam = Camera.main;
        if (player == null || cam == null) yield break;
        follow = cam.GetComponent<CameraFollow>();
        body = player.GetComponent<Rigidbody2D>();
        animator = player.GetComponentInChildren<Animator>();
        sprite = animator.GetComponent<SpriteRenderer>();
        art = sprite.transform;
        restPosition = art.localPosition;
        restRotation = art.localRotation;
        animated = animator.enabled;
        playerEnabled = player.enabled;
        followEnabled = follow != null && follow.enabled;
        simulated = body.simulated;
        CutsceneLock.Lock(this);
        player.ResetMotion();
        player.enabled = false;
        player.Face(1);
        animator.SetFloat("Speed", 0);
        animator.SetFloat("YVel", 0);
        animator.SetFloat("Grounded", 1);
        animator.SetBool("Sliding", false);
        animator.Play("Idle", 0, 0);
        animator.Update(0);
        animator.enabled = false;
        MeasureOpaqueBounds();

        var collider = player.GetComponent<BoxCollider2D>();
        groundY = collider.bounds.min.y;
        foreach (var hit in Physics2D.RaycastAll(new Vector2(collider.bounds.center.x, collider.bounds.min.y + .3f), Vector2.down, 4, player.groundLayer))
        {
            if (hit.collider.isTrigger || hit.collider.attachedRigidbody == body || hit.normal.y < .5f) continue;
            groundY = hit.point.y;
            player.transform.position += Vector3.up * (groundY + .015f - collider.bounds.min.y);
            break;
        }
        body.simulated = false;
        // Facing right rotated counterclockwise puts the face toward the sky.
        var lyingRotation = restRotation * Quaternion.Euler(0, 0, 90);
        art.localRotation = lyingRotation;
        art.position += Vector3.up * (groundY + .015f - VisibleBottom());
        Vector3 lyingPosition = art.localPosition;
        if (follow != null) follow.enabled = false;
        float size = cam.orthographicSize;
        Vector3 initial = player.transform.position + new Vector3(1, 1.4f, -10);
        cam.transform.position = initial;

        overlay = new GameObject("WakingEyes").AddComponent<Canvas>();
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = 2000;
        overlay.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
        var cover = new GameObject("OpeningFade", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        cover.transform.SetParent(overlay.transform, false);
        cover.rectTransform.anchorMin = Vector2.zero;
        cover.rectTransform.anchorMax = Vector2.one;
        cover.rectTransform.offsetMin = cover.rectTransform.offsetMax = Vector2.zero;
        cover.color = Color.black;
        cover.raycastTarget = false;
        var openingFade = overlay.GetComponent<CanvasGroup>();
        bool sharedFade = FrontEndTransition.Busy;
        openingFade.alpha = sharedFade ? 0 : 1;
        float revealStart = Time.unscaledTime;
        while (Time.unscaledTime - revealStart < 1f)
        {
            float t = Time.unscaledTime - revealStart;
            openingFade.alpha = sharedFade ? 0 : 1 - Mathf.SmoothStep(0, 1, t);
            yield return null;
        }
        Destroy(overlay.gameObject);
        Destroy(reveal);

        speaker = player.GetComponent<DialogueSpeaker>();
        if (speaker == null) speaker = player.gameObject.AddComponent<DialogueSpeaker>();
        previousFade = speaker.fadeTime;
        speaker.fadeTime = .35f;
        bubble = SpeechBubble.Create(speaker);
        yield return Say(0, 11, 1.25f);
        yield return new WaitForSecondsRealtime(.65f);
        for (float t = 0; t < 1.1f; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0, 1, t / 1.1f);
            art.localRotation = Quaternion.Slerp(lyingRotation, restRotation, k);
            art.localPosition = Vector3.Lerp(lyingPosition, restPosition, k);
            art.position += Vector3.up * Mathf.Max(0, groundY + .015f - VisibleBottom());
            cam.orthographicSize = Mathf.Lerp(size, size * .86f, k);
            yield return null;
        }
        art.localRotation = restRotation;
        art.localPosition = restPosition;
        if (player.withBranchController != null) animator.runtimeAnimatorController = player.withBranchController;
        animator.enabled = true;
        animator.SetFloat("Speed", 0); animator.SetFloat("YVel", 0); animator.SetFloat("Grounded", 1);
        animator.SetBool("Sliding", false); animator.Play("Idle", 0, 0); animator.Update(0);
        CutsceneHeadTurn.Face(player, -1);
        yield return new WaitForSecondsRealtime(.55f);
        CutsceneHeadTurn.Face(player, 1);
        yield return new WaitForSecondsRealtime(.6f);
        CutsceneHeadTurn.Face(player, -1);
        yield return Say(1, 17, .2f);
        CutsceneHeadTurn.Face(player, 1);
        // The surprise is a visual hop; the frozen Idle pose does not enter Jump/Fall.
        for (float t = 0; t < .55f; t += Time.unscaledDeltaTime)
        {
            art.localPosition = restPosition;
            art.position += Vector3.up * (Mathf.Sin(t / .55f * Mathf.PI) * .42f);
            yield return null;
        }
        art.localPosition = restPosition;
        if (follow != null) follow.Shake(.12f, .035f);
        yield return Say(2, 19, .25f);
        yield return Say(3, 18, .35f);
        for (float t = 0; t < 1.2f; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0, 1, t / 1.2f);
            cam.orthographicSize = Mathf.Lerp(size * .86f, size, k);
            cam.transform.position = Vector3.Lerp(initial, player.transform.position + (follow != null ? follow.offset : new Vector3(0, 1, -10)), k);
            yield return null;
        }
        cam.orthographicSize = size;
        Restore();
    }

    IEnumerator Say(int index, float speed, float extra)
    {
        string text = lines[index];
        yield return bubble.Say(text, speed);
        yield return new WaitForSecondsRealtime(Mathf.Clamp(.8f + text.Length * .055f, 1.25f, 2.4f) + extra);
        bubble.Hide();
        yield return new WaitForSecondsRealtime(.5f);
    }

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

    void Restore()
    {
        if (finished) return;
        finished = true;
        if (art != null) { art.localPosition = restPosition; art.localRotation = restRotation; }
        if (animator != null) animator.enabled = animated;
        if (body != null) { body.simulated = simulated; body.linearVelocity = Vector2.zero; }
        if (player != null) player.enabled = playerEnabled;
        if (follow != null) follow.enabled = followEnabled;
        if (speaker != null) speaker.fadeTime = previousFade;
        if (bubble != null) Destroy(bubble.gameObject);
        if (overlay != null) Destroy(overlay.gameObject);
        if (reveal != null) Destroy(reveal);
        CutsceneLock.Unlock(this);
    }
    void OnDestroy() { Restore(); }
}
