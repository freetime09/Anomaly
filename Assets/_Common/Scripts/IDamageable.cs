using UnityEngine;

/// <summary>
/// 공격(나뭇가지 휘두르기) 히트박스에 맞으면 반응하는 대상이 구현하는 인터페이스.
/// PlayerController 가 앞쪽 히트박스 안에서 이 인터페이스를 찾아 TakeHit 을 호출한다.
/// </summary>
public interface IDamageable
{
    /// <summary>피격 처리. damage=피해량, hitDir=맞은 방향(플레이어가 바라보는 방향, ±x).</summary>
    void TakeHit(int damage, Vector2 hitDir);
}
