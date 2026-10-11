using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 여러 신호 채널을 듣고, 그 중 requiredCount 개 이상이 켜지면 '활성화'되는 수신자 베이스.
///  - signalIds: 들을 채널 번호 목록
///  - requiredCount: 활성화에 필요한 '켜진 채널' 최소 개수
///    · 1        → 아무 채널이나 하나라도 켜지면 (OR)
///    · 목록 수  → 모든 채널이 켜져야 (AND)
///    · 그 사이  → 지정한 개수 이상
/// 활성/비활성 상태가 바뀔 때만 OnSignalStateChanged(bool) 을 호출한다.
/// MovingObject, SignalDisappear 등이 이 클래스를 상속해 반응을 정의한다.
/// </summary>
public abstract class SignalReceiver : MonoBehaviour
{
    [Header("신호 (채널)")]
    [Tooltip("들을 채널 번호들. 이 중 'Required Count' 개 이상이 켜지면 활성화.")]
    public List<int> signalIds = new List<int> { 0 };
    [Tooltip("활성화에 필요한 켜진 채널 최소 개수. 목록 수와 같으면 전부 필요(AND), 1이면 아무거나(OR).")]
    public int requiredCount = 1;

    readonly List<int> distinct = new List<int>();
    System.Action<bool> onChange;
    bool conditionMet;

    /// <summary>현재 조건(requiredCount 충족) 만족 여부.</summary>
    protected bool ConditionMet => conditionMet;

    protected virtual void OnEnable()
    {
        onChange = OnAnyChannelChanged;
        distinct.Clear();
        foreach (var id in signalIds) if (!distinct.Contains(id)) distinct.Add(id);
        conditionMet = false;
        // 각 채널 구독(구독 즉시 현재 상태 콜백 → Evaluate). 같은 델리게이트라 해제도 정확.
        foreach (var id in distinct) SignalBus.Subscribe(id, onChange);
        Evaluate();
    }

    protected virtual void OnDisable()
    {
        if (onChange != null)
            foreach (var id in distinct) SignalBus.Unsubscribe(id, onChange);
    }

    void OnAnyChannelChanged(bool _) { Evaluate(); }

    void Evaluate()
    {
        int on = 0;
        for (int i = 0; i < distinct.Count; i++) if (SignalBus.IsOn(distinct[i])) on++;
        int need = Mathf.Clamp(requiredCount, 1, Mathf.Max(1, distinct.Count));
        bool now = on >= need;
        if (now != conditionMet)
        {
            conditionMet = now;
            OnSignalStateChanged(conditionMet);
        }
    }

    /// <summary>조건 충족(on/off)이 바뀔 때 호출. 구현체가 실제 반응을 정의.</summary>
    protected abstract void OnSignalStateChanged(bool activated);
}
