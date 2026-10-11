using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 번호(채널)로 신호를 주고받는 전역 버스.
///  - 보내는 쪽(버튼·레버 등)이 signalId 채널을 켜고 끈다.
///  - 같은 signalId 를 '듣는' 받는 쪽(MovingObject 등)이 반응한다.
/// 한 채널을 여러 발신자가 켤 수 있고(OR 처리), 하나라도 켜져 있으면 그 채널은 ON.
/// </summary>
public static class SignalBus
{
    // 채널별로 '현재 켜고 있는 발신자 집합' — 비어있지 않으면 그 채널 ON
    static readonly Dictionary<int, HashSet<Object>> active = new Dictionary<int, HashSet<Object>>();
    // 채널별 수신 콜백 (멀티캐스트)
    static readonly Dictionary<int, System.Action<bool>> listeners = new Dictionary<int, System.Action<bool>>();

    // 플레이 시작 시 정적 상태 초기화 (도메인 리로드 꺼져 있어도 깨끗이 시작)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { active.Clear(); listeners.Clear(); }

    /// <summary>해당 채널이 현재 ON 인가?</summary>
    public static bool IsOn(int id)
    {
        return active.TryGetValue(id, out var set) && set.Count > 0;
    }

    /// <summary>보내는 쪽이 자기 상태를 보고한다. sender 를 키로 써 같은 채널의 여러 발신자를 OR 처리.
    /// 채널의 ON/OFF 가 실제로 바뀔 때만 수신자에게 알린다.</summary>
    public static void Set(int id, Object sender, bool on)
    {
        if (!active.TryGetValue(id, out var set)) { set = new HashSet<Object>(); active[id] = set; }
        bool wasOn = set.Count > 0;
        if (on) set.Add(sender); else set.Remove(sender);
        bool nowOn = set.Count > 0;
        if (wasOn != nowOn && listeners.TryGetValue(id, out var cb) && cb != null) cb(nowOn);
    }

    /// <summary>수신 등록. 등록 즉시 현재 채널 상태를 콜백으로 한 번 전달(발신자가 먼저 켜져 있어도 반영).</summary>
    public static void Subscribe(int id, System.Action<bool> cb)
    {
        listeners.TryGetValue(id, out var existing);
        listeners[id] = existing + cb;
        cb(IsOn(id));
    }

    public static void Unsubscribe(int id, System.Action<bool> cb)
    {
        if (listeners.TryGetValue(id, out var existing)) listeners[id] = existing - cb;
    }
}
