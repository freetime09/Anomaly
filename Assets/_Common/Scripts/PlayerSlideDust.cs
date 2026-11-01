using UnityEngine;
public sealed class PlayerSlideDust:MonoBehaviour {
 sealed class Puff{public SpriteRenderer r;public Vector2 velocity;public float age,life,size;}
 readonly Puff[] pool=new Puff[28];readonly RaycastHit2D[] hits=new RaycastHit2D[16];
 PlayerController player;Rigidbody2D body;Collider2D collider;SpriteRenderer art;float timer;bool wasSliding;int next;
 void Awake(){
 player=GetComponent<PlayerController>();body=GetComponent<Rigidbody2D>();collider=GetComponent<Collider2D>();
 var a=GetComponentInChildren<Animator>();if(a!=null)art=a.GetComponent<SpriteRenderer>();var shape=Resources.Load<Sprite>("RunDust");
 for(int i=0;i<pool.Length;i++){var go=new GameObject("SlideFrontDust");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,gameObject.scene);var sr=go.AddComponent<SpriteRenderer>();sr.sprite=shape;sr.enabled=false;pool[i]=new Puff{r=sr};}
 }
 void LateUpdate(){
 float dt=Time.deltaTime;if(dt<=0)return;
 foreach(var puff in pool){if(!puff.r.enabled)continue;puff.age+=dt;float age=puff.age/puff.life;if(age>=1){puff.r.enabled=false;continue;}puff.r.transform.position+=(Vector3)(puff.velocity*dt);puff.velocity*=Mathf.Exp(-3.5f*dt);puff.r.transform.localScale=Vector3.one*puff.size*(1+age*.7f);puff.r.color=new Color(.9f,.93f,.97f,.65f*Mathf.Pow(1-age,.85f));}
 bool sliding=player.IsSliding&&Mathf.Abs(body.linearVelocity.x)>.7f;if(!sliding){timer=0;wasSliding=false;return;}timer-=dt;if(timer>0)return;timer=.09f;
 var b=collider.bounds;int dir=player.SlidingDirection;float edge=dir>0?b.max.x:b.min.x;if(art!=null)edge=dir>0?Mathf.Max(edge,art.bounds.max.x-art.bounds.size.x*.1f):Mathf.Min(edge,art.bounds.min.x+art.bounds.size.x*.1f);float x=edge+dir*.06f;
 int n=Physics2D.RaycastNonAlloc(new Vector2(x,b.min.y+.23f),Vector2.down,hits,.55f);float distance=float.PositiveInfinity;RaycastHit2D ground=default(RaycastHit2D);
 for(int i=0;i<n;i++){var h=hits[i];if(h.collider==null||h.collider.isTrigger||h.collider.transform.IsChildOf(transform)||h.normal.y<.5f)continue;if(h.distance<distance){distance=h.distance;ground=h;}}
 if(ground.collider==null)return;int amount=wasSliding?Random.Range(1,3):3;wasSliding=true;
 for(int i=0;i<amount;i++){var puff=pool[next++%pool.Length];puff.age=0;puff.life=Random.Range(.3f,.44f);puff.size=Random.Range(.21f,.32f);puff.velocity=new Vector2(dir*(Mathf.Abs(body.linearVelocity.x)*.35f+Random.Range(.35f,1.1f)),Random.Range(.18f,.55f));puff.r.transform.position=new Vector3(x+dir*Random.Range(0,.09f),ground.point.y+Random.Range(.055f,.11f),transform.position.z);puff.r.transform.localScale=Vector3.one*puff.size;puff.r.color=new Color(.9f,.93f,.97f,.6f);puff.r.sortingLayerID=art!=null?art.sortingLayerID:0;puff.r.sortingOrder=art!=null?art.sortingOrder+1:11;puff.r.enabled=true;}
 }
 void OnDisable(){foreach(var p in pool)if(p!=null&&p.r!=null)p.r.enabled=false;}
 void OnDestroy(){foreach(var p in pool)if(p!=null&&p.r!=null)Destroy(p.r.gameObject);}
}