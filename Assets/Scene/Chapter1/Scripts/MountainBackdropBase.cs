using UnityEngine;
[DefaultExecutionOrder(1200)]
public sealed class MountainBackdropBase : MonoBehaviour {
 public Sprite square;SpriteRenderer fill;
 void Start(){var g=new GameObject("MountainLowerFill");g.transform.SetParent(transform);fill=g.AddComponent<SpriteRenderer>();fill.sprite=square;fill.color=new Color(127f/255,171f/255,112f/255);fill.sortingOrder=-95;}
 void LateUpdate(){var c=Camera.main;if(c==null||fill==null)return;float h=c.orthographicSize*2;fill.transform.position=new Vector3(c.transform.position.x,c.transform.position.y-.1f*h-h,0);fill.transform.localScale=new Vector3(h*c.aspect*3,h*2,1);}
}