using UnityEngine;
[DefaultExecutionOrder(1500)]
public sealed class CameraBackdropPlacement:MonoBehaviour
{
 public Camera target;
 public bool screenCover;
 public float worldLeft=-6,worldWidth=76,worldHeight=14.61f;
 public float horizontalRepeats;
 public float horizonOffset=-.1375f;
 public bool followViewWidth;
 public bool lockHorizon;
 bool horizonReady;
 float fixedBaseY;
 public Vector2 visibleX=new Vector2(-80,120);
 SpriteRenderer image;
 Sprite source;
 Material extension;
 static Sprite quad;
 void Awake(){
 image=GetComponent<SpriteRenderer>();source=image.sprite;
 if(!screenCover){
 var template=Resources.Load<Material>("SceneryRepeatExtend");
 if(template!=null&&source!=null){
 extension=new Material(template);
 extension.SetTexture("_SourceTex",source.texture);
 var rt=RenderTexture.GetTemporary(32,16,0);var old=RenderTexture.active;var pixels=new Texture2D(32,16,TextureFormat.RGBA32,false);
 try{Rect sample=source.rect;Graphics.Blit(source.texture,rt,new Vector2(sample.width/source.texture.width,sample.height*.25f/source.texture.height),new Vector2(sample.x/source.texture.width,sample.y/source.texture.height));RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,32,16),0,0);pixels.Apply();Color sum=Color.clear;float total=0;foreach(var pixel in pixels.GetPixels()){sum+=pixel*pixel.a;total+=pixel.a;}var baseColor=total>.01f?sum/total:new Color(.13f,.15f,.04f);baseColor.a=1;extension.SetColor("_BaseColor",baseColor);}
 finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Destroy(pixels);}
 Rect r=source.rect;
 extension.SetVector("_Rect",new Vector4((r.xMin+.5f)/source.texture.width,(r.yMin+.5f)/source.texture.height,(r.xMax-.5f)/source.texture.width,(r.yMax-.5f)/source.texture.height));
 if(quad==null)quad=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,Texture2D.whiteTexture.width,Texture2D.whiteTexture.height),new Vector2(.5f,.5f),Texture2D.whiteTexture.width,0,SpriteMeshType.FullRect);
 image.sprite=quad;image.sharedMaterial=extension;image.drawMode=SpriteDrawMode.Simple;
 }
 }
 }
 void LateUpdate(){
 if(target==null)target=Camera.main;
 if(target==null||image==null||source==null)return;
 var c=target.transform.position;
 image.enabled=c.x>=visibleX.x&&c.x<=visibleX.y;
 if(!image.enabled)return;
 var s=source.bounds.size;float h=target.orthographicSize*2,w=h*target.aspect;
 if(screenCover){
 image.drawMode=SpriteDrawMode.Simple;
 float scale=Mathf.Max(w/s.x,h/s.y)*1.04f;
 transform.localScale=new Vector3(scale,scale,1);
 transform.position=new Vector3(c.x,c.y,transform.position.z);
 }else if(extension!=null){
 image.drawMode=SpriteDrawMode.Simple;
  if(!horizonReady){fixedBaseY=c.y+h*horizonOffset;horizonReady=true;}
  float baseY=lockHorizon?fixedBaseY:c.y+h*horizonOffset;
  float low=Mathf.Min(c.y-h*.5f-1,baseY);
  float placementWidth=followViewWidth?w*1.12f:worldWidth;
  float left=followViewWidth?c.x-placementWidth*.5f:worldLeft;
 float extra=baseY-low,total=worldHeight+extra;
 float tileWidth=s.x*worldHeight/s.y;
 extension.SetVector("_Layout",new Vector4(horizontalRepeats>0?horizontalRepeats:placementWidth/tileWidth,worldHeight,extra,source.rect.height));
 transform.localScale=new Vector3(placementWidth,total,1);
 transform.position=new Vector3(left+placementWidth*.5f,low+total*.5f,transform.position.z);
 }
 }
 void OnDestroy(){if(extension!=null)Destroy(extension);}
}