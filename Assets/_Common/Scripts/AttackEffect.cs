using UnityEngine;

/// <summary>
/// 공격(나뭇가지 휘두르기) 타격 이펙트. Animator 로 Attack 클립을 '한 번' 재생하고 끝나면 스스로 사라진다.
/// 속도·타이밍은 Attack.anim(및 Animation 창의 Samples/키프레임)이 그대로 결정한다 → 창에서 조절하면 인게임 반영.
/// 플레이어 그림과 무관한 별도 오브젝트라, 공격해도 플레이어는 안 바뀌고 이 이펙트만 앞에서 재생된다.
/// (클립 loop 는 꺼져 있어야 한 번만 재생됨 — 셋업에서 off 로 맞춤)
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
public class AttackEffect : MonoBehaviour
{
    SpriteRenderer sr;
    Animator anim;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        if (anim == null) { Destroy(gameObject); return; }
        var st = anim.GetCurrentAnimatorStateInfo(0);
        if (st.normalizedTime >= 1f) Destroy(gameObject); // 한 번 재생이 끝나면 소멸
    }

    /// <summary>플레이어가 바라보는 방향으로 이펙트를 좌우 반전한다.</summary>
    public void SetFacing(int facing) { if (sr == null) sr = GetComponent<SpriteRenderer>(); sr.flipX = facing < 0; }
}
