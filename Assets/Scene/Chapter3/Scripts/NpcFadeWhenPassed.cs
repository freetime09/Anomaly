using System.Collections;
using UnityEngine;

/// <summary>
/// 대화가 끝난 NPC 가 그 자리에 가만히 서 있다가, 메크로가 옆을 지나쳐 가면 제자리에서 서서히 사라진다.
/// (예: 메크로를 모른다던 친구들이, 메크로가 지나가고 나면 원래 없던 것처럼 사라짐)
///  - DialogueTrigger 의 On Dialogue Finished 이벤트에 Arm() 을 연결한다.
///    → 대화 전에 지나가 버려도 사라지지 않고, 대화가 끝난 뒤부터 지켜본다.
///  - '지나쳤다' = 메크로의 x 가 NPC 의 x 를 Pass Margin 만큼 넘어섬 (Pass Direction 방향 기준).
///  - 움직이지 않고 투명도만 줄이므로 물리·지형과 상관없다.
/// </summary>
public class NpcFadeWhenPassed : MonoBehaviour
{
    [Header("지나침 판정")]
    [Tooltip("메크로가 지나가는 방향. 1 = 오른쪽으로 지나감 (마을 진행 방향), -1 = 왼쪽")]
    public int passDirection = 1;
    [Tooltip("NPC 위치에서 이만큼(유닛) 더 지나가야 '지나쳤다'로 본다. 너무 작으면 바로 옆에서 사라져 어색하다")]
    [Min(0f)] public float passMargin = 2.5f;

    [Header("사라짐")]
    [Tooltip("지나친 뒤 사라지기 시작할 때까지 기다리는 시간(초)")]
    [Min(0f)] public float delay = 0.5f;
    [Tooltip("완전히 투명해지는 데 걸리는 시간(초)")]
    [Min(0.01f)] public float fadeTime = 1.2f;
    [Tooltip("다 사라지면 오브젝트를 끈다 (다시 말을 걸 수 없게)")]
    public bool deactivateAtEnd = true;

    bool armed;     // 대화가 끝나 지나치기를 기다리는 중
    bool fading;
    Transform player;

    /// <summary>[이벤트] 대화가 끝났을 때 부른다. 이때부터 메크로가 지나가기를 기다린다.</summary>
    public void Arm()
    {
        if (fading) return;
        armed = true;
        if (player == null)
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }
    }

    void Update()
    {
        if (!armed || fading || player == null) return;
        float passed = (player.position.x - transform.position.x) * Mathf.Sign(passDirection);
        if (passed >= passMargin) StartCoroutine(FadeRoutine());
    }

    IEnumerator FadeRoutine()
    {
        fading = true;
        armed = false;
        if (delay > 0f) yield return new WaitForSeconds(delay);

        var renderers = GetComponentsInChildren<SpriteRenderer>();
        var baseAlpha = new float[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) baseAlpha[i] = renderers[i].color.a;

        for (float t = 0f; t < fadeTime; t += Time.deltaTime)
        {
            SetAlpha(renderers, baseAlpha, 1f - t / fadeTime);
            yield return null;
        }
        SetAlpha(renderers, baseAlpha, 0f);
        if (deactivateAtEnd) gameObject.SetActive(false);
    }

    static void SetAlpha(SpriteRenderer[] renderers, float[] baseAlpha, float k)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            var c = renderers[i].color;
            c.a = baseAlpha[i] * k;
            renderers[i].color = c;
        }
    }

    /// <summary>선택 시 '여기를 넘으면 사라짐' 선을 Scene 뷰에 표시.</summary>
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
        float x = transform.position.x + passMargin * Mathf.Sign(passDirection);
        Gizmos.DrawLine(new Vector3(x, transform.position.y - 1f), new Vector3(x, transform.position.y + 2f));
    }
}
