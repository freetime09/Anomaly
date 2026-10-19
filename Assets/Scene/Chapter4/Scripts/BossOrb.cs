using UnityEngine;

/// <summary>
/// 보스(신)가 쏘는 느린 탄. BossFight 가 런타임에 만든다.
///  - 날아오는 동안 플레이어에 닿으면 피해 (판정은 BossFight 가 함).
///  - 나뭇가지 ㄱ(공격)에 맞으면 보스 쪽으로 되돌아가고, 보스에 닿으면 금이 하나 간다.
/// </summary>
public class BossOrb : MonoBehaviour, IDamageable
{
    public BossFight fight;
    public Vector2 velocity;
    public bool reflected;
    [Tooltip("튜토리얼: 메크로 앞에 멈춰서 되받아칠 때까지 기다림")]
    public bool held;
    public float radius = 0.45f;

    public void TakeHit(int damage, Vector2 hitDir)
    {
        if (reflected || fight == null) return;
        reflected = true;
        fight.OnOrbReflected(this);
    }

    void Update()
    {
        if (fight == null) return;
        if (held)
        {
            transform.position = Vector2.MoveTowards(transform.position, fight.OrbHoldPoint, 6f * Time.deltaTime);
            fight.TickOrb(this);
            return;
        }
        if (reflected) velocity = (fight.BossPosition - (Vector2)transform.position).normalized * fight.reflectSpeed; // 보스를 따라감
        transform.position += (Vector3)(velocity * Time.deltaTime);
        fight.TickOrb(this);
    }
}
