using UnityEngine;
public sealed class Chapter1FallSafety : MonoBehaviour {
 PlayerController player;FallRespawn respawn;
 void Start(){player=FindAnyObjectByType<PlayerController>();respawn=player.GetComponent<FallRespawn>();}
 void Update(){if(player==null||respawn==null)return;float floor=-Mathf.Max(0,player.transform.position.x)*.13f-5;if(player.transform.position.y<floor-9)respawn.Die();}
}