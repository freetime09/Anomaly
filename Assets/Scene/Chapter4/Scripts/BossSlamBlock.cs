using UnityEngine;
public sealed class BossSlamBlock : MonoBehaviour {
 public static readonly System.Collections.Generic.HashSet<BossSlamBlock> Active = new System.Collections.Generic.HashSet<BossSlamBlock>();
 void OnEnable(){Active.Add(this);}
 void OnDisable(){Active.Remove(this);}
 public BoxCollider2D Shape {get;private set;}
 public void Land(Vector2 size){
 Shape=GetComponent<BoxCollider2D>();Shape.size=size;Shape.offset=Vector2.zero;Shape.enabled=true;
 var rb=gameObject.AddComponent<Rigidbody2D>();rb.gravityScale=3;rb.constraints=RigidbodyConstraints2D.FreezeRotation|RigidbodyConstraints2D.FreezePositionX;rb.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
 }
}
