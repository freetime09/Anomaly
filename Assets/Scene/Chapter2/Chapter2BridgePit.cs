using UnityEngine;
public sealed class Chapter2BridgePit:MonoBehaviour {
 void OnTriggerEnter2D(Collider2D c){var p=c.GetComponentInParent<PlayerController>();if(p!=null){var fall=p.GetComponent<FallRespawn>();if(fall!=null)fall.Die();}}
}