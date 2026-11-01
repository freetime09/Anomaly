using UnityEngine;
[DefaultExecutionOrder(100)]
public sealed class Chapter1CliffGlide:MonoBehaviour{
 public float edgeX,topY;PlayerController player;Rigidbody2D body;
 void Start(){player=FindAnyObjectByType<PlayerController>();if(player!=null)body=player.GetComponent<Rigidbody2D>();}
 void FixedUpdate(){if(body==null||CutsceneLock.IsLocked)return;var p=body.position;if(p.x>edgeX-.12f&&p.x<edgeX+1.2f&&p.y<topY+1.3f){var v=body.linearVelocity;if(v.y<.5f){v.x=Mathf.Max(v.x,2.1f);v.y=Mathf.Min(v.y,-.6f);body.linearVelocity=v;}}}
}
