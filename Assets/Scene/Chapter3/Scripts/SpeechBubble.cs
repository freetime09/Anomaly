using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 캐릭터 머리 위에 뜨는 말풍선 (월드 공간).
///  - DialogueSpeaker 가 Awake 에서 코드로 만든다 → 프리팹/캔버스 세팅 없이 어느 캐릭터에든 붙는다.
///  - 구성: 검은 테두리 + 흰 배경 사각형 + 아래쪽 꼬리(마름모) + TextMeshPro 글자.
///  - 대사 길이에 맞춰 배경 크기를 먼저 계산한 뒤 글자를 한 글자씩 보여준다(타자기 효과).
///    전체 문장으로 크기를 먼저 정하기 때문에 타이핑 도중 말풍선 크기가 출렁이지 않는다.
///  - 캐릭터의 자식이 아니라 따로 두고 LateUpdate 에서 위치만 따라간다.
///    (플레이어처럼 비균일 스케일·좌우 반전이 있는 오브젝트의 자식이면 말풍선이 찌그러지기 때문)
/// </summary>
public class SpeechBubble : MonoBehaviour
{
    DialogueSpeaker owner;
    TextMeshPro text;
    SpriteRenderer border, background, tailBorder, tail;
    float alpha;            // 현재 투명도 (0 = 숨김, 1 = 완전 표시)
    Vector2 centerLift;     // 앵커(꼬리 끝)에서 말풍선 중심까지 — 대사마다 다시 계산
    Coroutine fadeCo;
    AudioSource voice;

    // <glitch>...</glitch> 로 감싼 글자 표시 (글자 순서 기준). 해당 글자만 떨리고 색이 번쩍인다
    bool[] glitchMask;
    bool hasGlitch;
    Vector3[] glitchOffsets;     // 글자별 현재 떨림 (일정 간격마다 새로 뽑음)
    Color32[] glitchColors;      // 글자별 현재 색
    bool[] glitchHidden;         // 글자별 잠깐 사라짐 여부
    float glitchTimer;
    const float GlitchRefresh = 0.05f; // 떨림을 새로 뽑는 간격(초). 매 프레임 바꾸면 너무 정신없음
    const string GlitchOpen = "<glitch>", GlitchClose = "</glitch>";

    static Sprite whiteSprite; // 모든 말풍선이 같이 쓰는 1x1 흰 스프라이트

    /// <summary>지금 화면에 보이는 중인가?</summary>
    public bool IsShowing => gameObject.activeSelf && alpha > 0f;

    /// <summary>마지막으로 출력한 대사의 실제 글자 수 (태그 제외). 자동 유지 시간 계산에 쓴다.</summary>
    public int LastCharacterCount { get; private set; }

    /// <summary>한 글자씩 출력하는 중인가? (Say 코루틴이 아직 끝나지 않음)</summary>
    public bool IsTyping { get; private set; }

    bool completeRequested; // CompleteTyping() 요청 → 다음 프레임에 남은 글자를 한 번에 표시

    /// <summary>speaker 의 설정으로 말풍선 오브젝트를 만든다.</summary>
    public static SpeechBubble Create(DialogueSpeaker speaker)
    {
        var go = new GameObject($"SpeechBubble ({speaker.speakerId})");
        var bubble = go.AddComponent<SpeechBubble>();
        bubble.Build(speaker);
        go.SetActive(false); // 말할 때까지 숨김
        return bubble;
    }

    void Build(DialogueSpeaker speaker)
    {
        owner = speaker;
        int order = speaker.sortingOrder;

        // 그리는 순서: 테두리(order) < 배경·꼬리(order+1) < 글자(order+2)
        border     = CreateRect("Border", order);
        tailBorder = CreateRect("TailBorder", order);
        background = CreateRect("Background", order + 1);
        tail       = CreateRect("Tail", order + 1);
        tailBorder.transform.localRotation = Quaternion.Euler(0f, 0f, 45f); // 마름모 → 아래 꼭짓점이 꼬리 끝
        tail.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(transform, false);
        text = textGo.AddComponent<TextMeshPro>();
        if (speaker.font != null) text.font = speaker.font;
        text.fontSize = speaker.fontSize;
        text.fontStyle |= FontStyles.Bold;
        text.color = speaker.textColor;
        text.alignment = TextAlignmentOptions.Center;
        // TMP 기본 규칙은 한글을 글자 단위로 끊어("왔/니?") 어색하므로, 자동 줄바꿈은 끄고
        // WrapByWords 에서 띄어쓰기 기준으로 직접 줄을 나눈다.
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.sortingOrder = order + 2;

        if (speaker.voiceClip != null)
        {
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
        }
        ApplyAlpha(0f);
    }

    SpriteRenderer CreateRect(string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = WhiteSprite();
        sr.sortingOrder = order;
        return sr;
    }

    static Sprite WhiteSprite()
    {
        if (whiteSprite != null) return whiteSprite;
        var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        // pixelsPerUnit = 1 → 스프라이트 크기 1x1 유닛. localScale 이 곧 월드 크기가 된다.
        whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return whiteSprite;
    }

    /// <summary>대사를 한 글자씩 출력한다. 다 출력되면 코루틴이 끝난다 (말풍선은 Hide 전까지 유지).</summary>
    public IEnumerator Say(string message, float charsPerSecond)
    {
        gameObject.SetActive(true);
        message = ParseGlitchTags(message);
        Layout(message);
        LateUpdate(); // 첫 프레임부터 올바른 자리에 보이도록 위치 즉시 갱신
        FadeTo(1f);

        text.maxVisibleCharacters = 0;
        text.ForceMeshUpdate();
        int total = text.textInfo.characterCount;
        LastCharacterCount = total;
        float shown = 0f;
        int visible = 0;
        IsTyping = true;
        completeRequested = false;
        while (visible < total)
        {
            if (completeRequested)
            {
                // 스킵: 남은 글자를 한 번에 보여 준다. 목소리는 글자마다 내면 소리가 겹치므로 생략
                visible = total;
                text.maxVisibleCharacters = visible;
                break;
            }
            shown += Mathf.Max(1f, charsPerSecond) * Time.deltaTime;
            int next = Mathf.Min(total, Mathf.FloorToInt(shown));
            for (int i = visible; i < next; i++) PlayVoice(i);
            visible = next;
            text.maxVisibleCharacters = visible;
            yield return null;
        }
        IsTyping = false;
        completeRequested = false;
    }

    /// <summary>출력 중인 대사를 즉시 끝까지 보여 준다 (클릭 스킵용). 출력 중이 아니면 아무 일도 안 한다.</summary>
    public void CompleteTyping()
    {
        if (IsTyping) completeRequested = true;
    }

    /// <summary>말풍선을 서서히 숨긴다.</summary>
    public void Hide() { if (gameObject.activeSelf) FadeTo(0f); }

    /// <summary>말풍선을 즉시 숨긴다 (대화 강제 종료 등).</summary>
    public void HideImmediate()
    {
        if (fadeCo != null) { StopCoroutine(fadeCo); fadeCo = null; }
        IsTyping = false; // Say 가 도중에 끊겨도(대화 강제 종료) 출력 중 상태가 남지 않게
        completeRequested = false;
        ApplyAlpha(0f);
        gameObject.SetActive(false);
    }

    /// <summary>문장 전체 크기를 재서 배경·테두리·꼬리 크기와 위치를 맞춘다.</summary>
    void Layout(string message)
    {
        message = WrapByWords(message);
        text.text = message;
        Vector2 pref = text.GetPreferredValues(message);
        float w = pref.x; // 이미 maxWidth 안으로 줄을 나눴으므로 그대로 사용 (긴 단어 하나는 예외적으로 넘칠 수 있음)
        float h = pref.y;
        text.rectTransform.sizeDelta = new Vector2(w, h);

        float p = owner.padding, b = owner.borderWidth, t = owner.tailSize;
        background.transform.localScale = new Vector3(w + p * 2f, h + p * 2f, 1f);
        border.transform.localScale = new Vector3(w + (p + b) * 2f, h + (p + b) * 2f, 1f);

        float tailTip = t * 0.7071f + b; // 45° 회전한 정사각형의 반대각선 길이
        tail.transform.localScale = new Vector3(t, t, 1f);
        tailBorder.transform.localScale = new Vector3(t + b * 2f, t + b * 2f, 1f);
        if (owner.tailRight)
        {
            // 오른쪽 변 가운데에 꼬리 → 오른쪽 꼭짓점(꼬리 끝)이 앵커에 오도록 말풍선을 왼쪽으로
            float right = w * 0.5f + p;
            tail.transform.localPosition = new Vector3(right, 0f, 0f);
            tailBorder.transform.localPosition = new Vector3(right + b, 0f, 0f);
            centerLift = new Vector2(-(right + tailTip), 0f);
            return;
        }
        float bottom = -(h * 0.5f + p);           // 배경 아래 변의 높이
        tail.transform.localPosition = new Vector3(0f, bottom, 0f);
        tailBorder.transform.localPosition = new Vector3(0f, bottom - b, 0f);

        // 마름모 아래 꼭짓점(꼬리 끝)이 앵커에 오도록 말풍선 중심을 들어 올린다
        centerLift = new Vector2(0f, -bottom + tailTip);
    }

    /// <summary>띄어쓰기 단위로 단어를 이어 붙이다가 maxWidth 를 넘기 직전에 줄을 바꾼다.
    /// 대사에 직접 넣은 줄바꿈(\n)은 그대로 유지한다.</summary>
    string WrapByWords(string message)
    {
        var result = new System.Text.StringBuilder();
        string[] paragraphs = message.Split('\n');
        for (int p = 0; p < paragraphs.Length; p++)
        {
            if (p > 0) result.Append('\n');
            string line = "";
            foreach (string word in paragraphs[p].Split(' '))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && text.GetPreferredValues(candidate).x > owner.maxWidth)
                {
                    result.Append(line).Append('\n'); // 넘치면 지금까지를 한 줄로 확정
                    line = word;
                }
                else line = candidate;
            }
            result.Append(line);
        }
        return result.ToString();
    }

    void LateUpdate()
    {
        if (owner == null) { Destroy(gameObject); return; }
        transform.position = owner.BubbleAnchor + (Vector3)centerLift;
        transform.rotation = Quaternion.identity;
        if (hasGlitch && alpha > 0f) ApplyGlitch();
    }

    // ── 이름 깨짐 연출 ──

    /// <summary>&lt;glitch&gt;...&lt;/glitch&gt; 태그를 지우고, 그 안에 있던 글자 위치를 기록한다.
    /// (태그를 지운 뒤의 글자 순서 = TextMeshPro 의 글자 순서. 줄바꿈 처리도 공백 1칸을 \n 1칸으로 바꾸므로 순서가 유지된다)</summary>
    string ParseGlitchTags(string src)
    {
        var sb = new System.Text.StringBuilder(src.Length);
        var flags = new System.Collections.Generic.List<bool>(src.Length);
        bool inside = false;
        for (int i = 0; i < src.Length;)
        {
            if (string.CompareOrdinal(src, i, GlitchOpen, 0, GlitchOpen.Length) == 0) { inside = true; i += GlitchOpen.Length; continue; }
            if (string.CompareOrdinal(src, i, GlitchClose, 0, GlitchClose.Length) == 0) { inside = false; i += GlitchClose.Length; continue; }
            sb.Append(src[i]);
            flags.Add(inside);
            i++;
        }
        glitchMask = flags.ToArray();
        hasGlitch = flags.Contains(true);
        if (hasGlitch)
        {
            glitchOffsets = new Vector3[glitchMask.Length];
            glitchColors = new Color32[glitchMask.Length];
            glitchHidden = new bool[glitchMask.Length];
            glitchTimer = 0f;
        }
        return sb.ToString();
    }

    /// <summary>깨짐 글자의 꼭짓점을 흔들고 색을 바꾼다.
    /// TextMeshPro 는 글자가 바뀔 때마다 메쉬를 새로 만들기 때문에 매 프레임 다시 적용하고,
    /// 떨림 값 자체는 GlitchRefresh 간격으로만 새로 뽑아 너무 정신없지 않게 한다.</summary>
    void ApplyGlitch()
    {
        glitchTimer -= Time.deltaTime;
        if (glitchTimer <= 0f)
        {
            glitchTimer = GlitchRefresh;
            for (int i = 0; i < glitchMask.Length; i++)
            {
                if (!glitchMask[i]) continue;
                glitchOffsets[i] = Random.insideUnitCircle * owner.glitchJitter;
                var palette = owner.glitchColors;
                glitchColors[i] = palette != null && palette.Length > 0 ? (Color32)palette[Random.Range(0, palette.Length)] : (Color32)owner.textColor;
                glitchHidden[i] = Random.value < owner.glitchDropChance;
            }
        }

        text.ForceMeshUpdate(); // 원래 꼭짓점으로 되돌린 뒤 그 위에 떨림을 얹는다
        var info = text.textInfo;
        int limit = Mathf.Min(info.characterCount, glitchMask.Length, text.maxVisibleCharacters);
        byte a = (byte)(255f * alpha * owner.textColor.a);
        for (int i = 0; i < limit; i++)
        {
            if (!glitchMask[i]) continue;
            var ci = info.characterInfo[i];
            if (!ci.isVisible) continue;
            var mesh = info.meshInfo[ci.materialReferenceIndex];
            Color32 col = glitchColors[i];
            col.a = glitchHidden[i] ? (byte)0 : a;
            for (int k = 0; k < 4; k++)
            {
                mesh.vertices[ci.vertexIndex + k] += glitchOffsets[i];
                mesh.colors32[ci.vertexIndex + k] = col;
            }
        }
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
    }

    void PlayVoice(int charIndex)
    {
        if (voice == null || owner.voiceClip == null) return;
        if (charIndex % Mathf.Max(1, owner.voiceEveryNChars) != 0) return;
        if (char.IsWhiteSpace(text.textInfo.characterInfo[charIndex].character)) return; // 공백엔 소리 없음
        voice.pitch = owner.voicePitch * Random.Range(0.95f, 1.05f); // 살짝 흔들어 기계적인 반복감 완화
        voice.PlayOneShot(owner.voiceClip, owner.voiceVolume);
    }

    void FadeTo(float target)
    {
        if (fadeCo != null) StopCoroutine(fadeCo);
        fadeCo = StartCoroutine(FadeRoutine(target));
    }

    IEnumerator FadeRoutine(float target)
    {
        float from = alpha;
        float dur = Mathf.Max(0.0001f, owner.fadeTime);
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            ApplyAlpha(Mathf.Lerp(from, target, t / dur));
            yield return null;
        }
        ApplyAlpha(target);
        fadeCo = null;
        if (target <= 0f) gameObject.SetActive(false); // 완전히 사라지면 비활성화
    }

    void ApplyAlpha(float a)
    {
        alpha = a;
        SetColor(border, owner.borderColor, a);
        SetColor(tailBorder, owner.borderColor, a);
        SetColor(background, owner.backgroundColor, a);
        SetColor(tail, owner.backgroundColor, a);
        text.alpha = owner.textColor.a * a;
    }

    static void SetColor(SpriteRenderer sr, Color c, float a)
    {
        c.a *= a;
        sr.color = c;
    }
}
