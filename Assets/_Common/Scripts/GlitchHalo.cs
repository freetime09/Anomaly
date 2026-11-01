using UnityEngine;
using UnityEngine.UI;
[DefaultExecutionOrder(1500)]
public sealed class GlitchHalo : MonoBehaviour{
 Graphic graphic;SpriteRenderer sprite;RawImage ui;SpriteRenderer world;Material mat;
 static Sprite quad;
 public static void Attach(Graphic g){if(g!=null&&!(g is TMPro.TMP_Text)&&g.GetComponent<GlitchHalo>()==null)g.gameObject.AddComponent<GlitchHalo>();}
 public static void Attach(SpriteRenderer r){if(r!=null&&r.GetComponent<GlitchHalo>()==null)r.gameObject.AddComponent<GlitchHalo>();}
 void Start(){
 graphic=GetComponent<Graphic>();sprite=GetComponent<SpriteRenderer>();
 var template=Resources.Load<Material>("GlitchHalo");
 if(template==null){enabled=false;return;}mat=new Material(template);
 if(graphic!=null){var go=new GameObject("ColoredLightBleed",typeof(RectTransform),typeof(RawImage));go.transform.SetParent(transform,false);ui=go.GetComponent<RawImage>();ui.material=mat;ui.raycastTarget=false;var rt=ui.rectTransform;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;}
 else if(sprite!=null){if(quad==null){var t=new Texture2D(1,1);t.SetPixel(0,0,Color.white);t.Apply();quad=Sprite.Create(t,new Rect(0,0,1,1),new Vector2(.5f,.5f),1);}var g=new GameObject("ColoredLightBleed");g.transform.SetParent(transform,false);world=g.AddComponent<SpriteRenderer>();world.sprite=quad;world.sharedMaterial=mat;}
 }
 void LateUpdate(){if(mat==null)return;Color c=graphic!=null?graphic.color:sprite.color;
 bool visible=(graphic!=null?graphic.enabled:sprite.enabled)&&Mathf.Max(c.r,Mathf.Max(c.g,c.b))>.14f&&c.a>.005f;
 if(ui!=null){ui.enabled=visible;var size=graphic.rectTransform.rect.size;if(size.x>1700&&size.y>800){ui.enabled=false;return;}float mx=22,my=22;ui.rectTransform.offsetMin=new Vector2(-mx,-my);ui.rectTransform.offsetMax=new Vector2(mx,my);mat.SetVector("_Core",new Vector4(mx/(size.x+mx*2),my/(size.y+my*2),1-mx/(size.x+mx*2),1-my/(size.y+my*2)));}
 if(world!=null){world.enabled=visible;world.sortingLayerID=sprite.sortingLayerID;world.sortingOrder=sprite.sortingOrder+1;world.transform.localPosition=sprite.sprite.bounds.center;var size=sprite.sprite.bounds.size;world.transform.localScale=new Vector3(size.x*1.35f,size.y*1.7f,1);mat.SetVector("_Core",new Vector4(.13f,.2f,.87f,.8f));}
 mat.SetColor("_Tint",new Color(c.r*1.5f,c.g*1.5f,c.b*1.5f,c.a));
 }
 void OnDestroy(){if(mat!=null)Destroy(mat);}
}