using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 컷신·대화 중 플레이어 조작(제스처 입력)을 잠그고 푸는 전역 유틸.
///  - 여러 연출이 동시에 잠글 수 있으므로 '누가 잠갔는지(owner)'를 모아 두고,
///    마지막 owner 가 풀 때 비로소 조작이 돌아온다. (A 연출이 끝나며 풀어도 B 연출이 아직 잠그고 있으면 유지)
///  - 잠그는 순간 진행 중이던 이동/슬라이드를 멈춰, 컷신 시작 시 캐릭터가 미끄러져 가지 않게 한다.
/// 사용: CutsceneLock.Lock(this); ... CutsceneLock.Unlock(this);
/// </summary>
public static class CutsceneLock
{
    // 현재 조작을 잠그고 있는 주체들 — 하나라도 있으면 잠김
    static readonly HashSet<object> owners = new HashSet<object>();

    // 플레이 시작 시 정적 상태 초기화 (도메인 리로드를 꺼도 이전 플레이의 잠금이 남지 않게)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        owners.Clear();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // 잠긴 채로 씬이 바뀌면(챕터 전환 등) 새 씬의 제스처 입력은 켜진 상태로 시작한다.
    // 그래서 씬이 로드될 때마다 현재 잠금 상태를 새 씬에 다시 적용한다. (sceneLoaded 는 새 씬의 Start 보다 먼저 호출됨)
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsLocked) Apply(true);
    }

    /// <summary>지금 조작이 잠겨 있는가?</summary>
    public static bool IsLocked => owners.Count > 0;

    /// <summary>조작을 잠근다. 같은 owner 가 여러 번 불러도 한 번으로 친다.</summary>
    public static void Lock(object owner)
    {
        if (owner == null || !owners.Add(owner)) return;
        if (owners.Count == 1) Apply(true); // 처음 잠길 때만 실제로 입력을 끈다
    }

    /// <summary>owner 의 잠금을 푼다. 남은 owner 가 없으면 조작이 돌아온다.</summary>
    public static void Unlock(object owner)
    {
        if (owner == null || !owners.Remove(owner)) return;
        if (owners.Count == 0) Apply(false); // 모두 풀렸을 때만 입력을 켠다
    }

    /// <summary>제스처 입력 컴포넌트를 켜고 끈다. 잠글 때는 진행 중인 이동도 멈춘다.</summary>
    static void Apply(bool locked)
    {
        var input = Object.FindAnyObjectByType<GestureRecognizer>();
        if (input != null) input.enabled = !locked;

        if (locked)
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null) player.ResetMotion();
        }
    }
}
