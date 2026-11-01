using UnityEngine;

public sealed class Chapter1PitReset : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        var player=other.GetComponentInParent<FallRespawn>();
        if(player!=null)player.Die();
    }
}
