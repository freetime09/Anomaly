using System.Collections;
using UnityEngine;

/// <summary>
/// NPC 가 걸어서 떠나며 서서히 사라진다. (예: 메크로를 모른다며 친구들이 가버림)
/// DialogueTrigger 의 On Dialogue Finished 이벤트에 WalkAway() 를 연결해 쓴다.
/// 물리 없이 Transform 만 옮기므로 지형과 부딪히지 않는다 (평지 배경용 NPC 전제).
/// </summary>
public class NpcWalkAway : MonoBehaviour
{
    [Tooltip("떠나기 전 잠깐 멈춰 있는 시간(초). 대화가 끝나자마자 바로 움직이면 어색하다")]
    [Min(0f)] public float delay = 0.4f;
    [Tooltip("걸어갈 거리 (유닛). 양수 = 오른쪽, 음수 = 왼쪽")]
    public float distance = 8f;
    [Tooltip("걷는 속도 (유닛/초)")]
    [Min(0.1f)] public float speed = 2.5f;
    [Tooltip("걷는 동안 서서히 투명해지는 시간(초). 걷기 마지막 구간에 맞춰 사라진다")]
    [Min(0f)] public float fadeTime = 1f;
    [Tooltip("다 사라지면 오브젝트를 끈다 (다시 말을 걸 수 없게)")]
    public bool deactivateAtEnd = true;

    bool walking;

    /// <summary>떠나기 시작한다. 여러 번 불러도 한 번만 동작.</summary>
    public void WalkAway()
    {
        if (walking || !isActiveAndEnabled) return;
        walking = true;
        StartCoroutine(WalkRoutine());
    }

    IEnumerator WalkRoutine()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        var renderers = GetComponentsInChildren<SpriteRenderer>();
        var baseAlpha = new float[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) baseAlpha[i] = renderers[i].color.a;

        Vector3 start = transform.position;
        Vector3 end = start + Vector3.right * distance;
        float duration = Mathf.Abs(distance) / speed;
        float fadeStart = Mathf.Max(0f, duration - fadeTime);

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(start, end, t / duration);
            float k = fadeTime > 0f ? 1f - Mathf.Clamp01((t - fadeStart) / fadeTime) : 1f;
            for (int i = 0; i < renderers.Length; i++)
            {
                var c = renderers[i].color;
                c.a = baseAlpha[i] * k;
                renderers[i].color = c;
            }
            yield return null;
        }
        transform.position = end;
        if (deactivateAtEnd) gameObject.SetActive(false);
    }

    /// <summary>선택 시 도착 지점을 Scene 뷰에 표시.</summary>
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
        Vector3 end = transform.position + Vector3.right * distance;
        Gizmos.DrawLine(transform.position, end);
        Gizmos.DrawWireCube(end, new Vector3(0.6f, 1.2f, 0f));
    }
}
