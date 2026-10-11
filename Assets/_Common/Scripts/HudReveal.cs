using System.Collections;
using UnityEngine;

/// <summary>
/// HUD(예: SkillIcon)를 처음엔 투명하게 숨겨두었다가, Reveal() 호출 시
/// (기본) 소용돌이 텔레포트 전환이 끝난 뒤 서서히(투명→불투명) 나타나게 한다.
/// 자기 자식의 모든 SpriteRenderer 알파를 페이드한다. 한 번 나타나면 계속 유지.
/// 튜토리얼: 텔레포터 onTeleport 이벤트 → HudReveal.Reveal() 로 배선.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(500)] // SkillBar 가 오버레이를 만든 뒤에 캡처하도록 늦게 실행
public class HudReveal : MonoBehaviour
{
    [Tooltip("페이드 인 시간(초)")]
    public float fadeDuration = 0.6f;
    [Tooltip("소용돌이 텔레포트 전환이 끝날 때까지 기다렸다가 나타난다")]
    public bool waitForVortex = true;
    [Tooltip("전환이 끝난 뒤 추가로 기다리는 시간(초)")]
    public float extraDelay = 0f;

    SpriteRenderer[] rends;
    float[] baseAlpha;
    bool revealed;
    Coroutine co;

    void Start()
    {
        // SkillBar(Awake)가 만든 쿨타임 오버레이까지 포함해 수집 → 전부 투명하게 숨김
        rends = GetComponentsInChildren<SpriteRenderer>(true);
        baseAlpha = new float[rends.Length];
        for (int i = 0; i < rends.Length; i++)
        {
            baseAlpha[i] = rends[i].color.a;
            SetAlpha(i, 0f);
        }
    }

    /// <summary>전환이 끝난 뒤 HUD를 서서히 나타나게 한다. (여러 번 불러도 처음 한 번만)</summary>
    public void Reveal()
    {
        if (revealed) return;
        revealed = true;
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(RevealRoutine());
    }

    IEnumerator RevealRoutine()
    {
        if (waitForVortex)
        {
            yield return null; // 전환이 시작되는 프레임을 넘긴 뒤
            while (VortexTransition.Instance != null && VortexTransition.Instance.IsPlaying)
                yield return null; // 이동(소용돌이)이 완전히 끝날 때까지 대기
        }
        if (extraDelay > 0f) yield return new WaitForSecondsRealtime(extraDelay);

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(fadeDuration, 1e-4f));
            for (int i = 0; i < rends.Length; i++) SetAlpha(i, baseAlpha[i] * k);
            yield return null;
        }
        for (int i = 0; i < rends.Length; i++) SetAlpha(i, baseAlpha[i]);
        co = null;
    }

    void SetAlpha(int i, float a)
    {
        if (rends[i] == null) return;
        var c = rends[i].color;
        c.a = a;
        rends[i].color = c;
    }
}
