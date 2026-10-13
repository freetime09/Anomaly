using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 퍼즐 오브젝트(레버·버튼·상자·문 등)가 구현. 상태를 저장하고 그대로 되돌린다.
/// </summary>
public interface IPuzzleState
{
    object SaveState();
    void LoadState(object state);
}

/// <summary>
/// 씬의 퍼즐 상태 스냅샷. FallRespawn 이 시작할 때와 체크포인트를 밟을 때 저장하고,
/// 죽어서 부활할 때(화면이 검은 동안) 그 상태로 되돌린다.
/// </summary>
public class PuzzleSnapshot
{
    /// <summary>되돌리는 중 (이때는 소리·이펙트를 내지 않는다)</summary>
    public static bool Restoring { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Restoring = false; }

    readonly List<KeyValuePair<MonoBehaviour, object>> saved = new List<KeyValuePair<MonoBehaviour, object>>();

    public void Save()
    {
        saved.Clear();
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (mb is IPuzzleState ps) saved.Add(new KeyValuePair<MonoBehaviour, object>(mb, ps.SaveState()));
    }

    public void Restore()
    {
        Restoring = true;
        try
        {
            foreach (var kv in saved)
                if (kv.Key != null) ((IPuzzleState)kv.Key).LoadState(kv.Value); // 파괴된 오브젝트는 건너뜀
        }
        finally { Restoring = false; }
    }
}
