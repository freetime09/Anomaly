using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
public sealed class PlayerRunDust : MonoBehaviour {
 class Puff { public SpriteRenderer r; public Vector3 velocity; public float life,total,size; public Color color; }
 readonly Puff[] pool=new Puff[24];
 readonly RaycastHit2D[] hits=new RaycastHit2D[12];
 readonly Dictionary<Sprite,Color> colors=new Dictionary<Sprite,Color>();
 Collider2D body; SpriteRenderer player; Vector3 previous; float timer; int next; bool stepPending;
 public void FootContact(){stepPending=true;}
 void Awake(){body=GetComponent<Collider2D>();player=GetComponentInChildren<SpriteRenderer>();previous=transform.position;
 var animator=GetComponentInChildren<Animator>(); if(animator!=null && animator.GetComponent<RunDustFootContact>()==null) animator.gameObject.AddComponent<RunDustFootContact>();
 var sprite=Resources.Load<Sprite>("RunDust");
 for(int i=0;i<pool.Length;i++){var g=new GameObject("HeelDust");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(g,gameObject.scene);var r=g.AddComponent<SpriteRenderer>();r.sprite=sprite;r.enabled=false;pool[i]=new Puff{r=r};}}
 void OnEnable(){previous=transform.position;timer=0;}
 void OnDisable(){foreach(var p in pool)if(p!=null&&p.r!=null){p.life=0;p.r.enabled=false;}}
 void OnDestroy(){foreach(var p in pool)if(p!=null&&p.r!=null)Destroy(p.r.gameObject);}
 void LateUpdate(){
 float dt=Time.deltaTime;if(dt<=0)return;
 foreach(var p in pool)if(p.life>0){p.life-=dt;if(p.life<=0){p.r.enabled=false;continue;}p.r.transform.position+=p.velocity*dt;p.velocity*=Mathf.Exp(-3f*dt);float age=1-p.life/p.total;p.r.transform.localScale=Vector3.one*p.size*(1+age*.8f);var c=p.color;c.a*=Mathf.Sin(Mathf.PI*age);p.r.color=c;}
 var delta=transform.position-previous;previous=transform.position;
 float speed=Mathf.Abs(delta.x)/dt; bool emit=stepPending;stepPending=false;
 if(body==null||speed<1.2f||delta.sqrMagnitude>1f||Mathf.Abs(delta.y)/dt>2f||!emit)return;
 var b=body.bounds;float dir=Mathf.Sign(delta.x);
 int n=Physics2D.RaycastNonAlloc(new Vector2(b.center.x,b.min.y+.08f),Vector2.down,hits,.2f);
 RaycastHit2D ground=default;float distance=999;
 for(int i=0;i<n;i++){var h=hits[i];if(h.collider==null||h.collider.isTrigger||h.collider.transform.IsChildOf(transform)||h.normal.y<.5f)continue;if(h.distance<distance){distance=h.distance;ground=h;}}
 if(ground.collider==null)return;
 
 var surface=ground.collider.GetComponent<SpriteRenderer>();if(surface==null)surface=ground.collider.GetComponentInChildren<SpriteRenderer>();
 Sprite sp=surface!=null?surface.sprite:null;Color tint=surface!=null?surface.color:Color.white;
 var map=ground.collider.GetComponent<Tilemap>();if(map==null)map=ground.collider.GetComponentInParent<Tilemap>();
 if(map!=null){var cell=map.WorldToCell(ground.point+Vector2.down*.03f);sp=map.GetSprite(cell);tint=map.color*map.GetColor(cell);}
 Color color=sp!=null?Average(sp)*tint:new Color(.55f,.51f,.43f);
 color=Color.Lerp(color,new Color(.68f,.65f,.57f),.18f);color.a=.48f;
 int count=Random.Range(1,4);
 for(int k=0;k<count;k++){var p=pool[next++%pool.Length];p.total=p.life=Random.Range(.40f,.58f);p.size=Random.Range(.10f,.22f);p.color=color;p.velocity=new Vector3(-dir*Random.Range(.35f,.95f),Random.Range(.12f,.36f),0);p.r.transform.position=new Vector3(b.center.x-dir*(b.extents.x*.7f+k*.045f)+Random.Range(-.025f,.025f),ground.point.y+.055f+Random.Range(0f,.035f),transform.position.z);p.r.transform.localScale=Vector3.one*p.size;p.r.color=new Color(color.r,color.g,color.b,0);p.r.sortingLayerID=player!=null?player.sortingLayerID:0;p.r.sortingOrder=player!=null?player.sortingOrder-1:1;p.r.enabled=true;}
 }
 Color Average(Sprite s){if(colors.TryGetValue(s,out var c))return c;
 var rt=RenderTexture.GetTemporary(8,8,0);var old=RenderTexture.active;var copy=new Texture2D(8,8,TextureFormat.RGBA32,false);
 try{var rect=s.rect;var tex=s.texture;Graphics.Blit(tex,rt,new Vector2(rect.width/tex.width,rect.height/tex.height),new Vector2(rect.x/tex.width,rect.y/tex.height));RenderTexture.active=rt;copy.ReadPixels(new Rect(0,0,8,8),0,0);copy.Apply();Color sum=Color.clear;float weight=0;foreach(var p in copy.GetPixels()){sum+=p*p.a;weight+=p.a;}c=weight>.01f?sum/weight:Color.gray;c.a=1;colors[s]=c;return c;}finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Destroy(copy);}}
}