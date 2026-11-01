using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Uses the scene's actual assets, with gameplay scripts removed before activation.
public sealed class TutorialSceneAssets : MonoBehaviour {
 Tilemap ground;Lever lever;Breakable box;SpriteRenderer door;
 readonly Dictionary<string,TileBase> tiles=new Dictionary<string,TileBase>();
 readonly List<SpriteRenderer> backdrops=new List<SpriteRenderer>();
 readonly List<GameObject> backdropCopies=new List<GameObject>();
 void Awake(){
 ground=FindAnyObjectByType<Tilemap>();lever=FindAnyObjectByType<Lever>(FindObjectsInactive.Include);box=FindAnyObjectByType<Breakable>(FindObjectsInactive.Include);
 foreach(var r in FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
 if(r.name=="Door_404"&&door==null)door=r;
 if(r.name=="Backpicture_0"||r.name=="Background3")backdrops.Add(r);
 }
 if(ground!=null)foreach(var cell in ground.cellBounds.allPositionsWithin){var t=ground.GetTile(cell);if(t!=null&&!tiles.ContainsKey(t.name))tiles.Add(t.name,t);}
 }
 readonly Dictionary<Sprite,Sprite> tiledSprites=new Dictionary<Sprite,Sprite>();
 public Sprite ForTiling(Sprite source){if(source==null)return null;Sprite result;if(tiledSprites.TryGetValue(source,out result))return result;result=Sprite.Create(source.texture,source.rect,new Vector2(source.pivot.x/source.rect.width,source.pivot.y/source.rect.height),source.pixelsPerUnit,0,SpriteMeshType.FullRect,source.border);result.name=source.name; tiledSprites.Add(source,result);return result;}
 void OnDestroy(){foreach(var sprite in tiledSprites.Values)if(sprite!=null)Destroy(sprite);}
 public GameObject VisualCopy(GameObject source,Transform parent){
 if(source==null)return null;
 var staging=new GameObject("InactiveVisualCopy");staging.SetActive(false);staging.transform.SetParent(parent,false);
 var clone=Instantiate(source,staging.transform,false);
 foreach(var c in clone.GetComponentsInChildren<MonoBehaviour>(true))DestroyImmediate(c);
 foreach(var c in clone.GetComponentsInChildren<Collider2D>(true))DestroyImmediate(c);
 foreach(var c in clone.GetComponentsInChildren<Rigidbody2D>(true))DestroyImmediate(c);
 foreach(var c in clone.GetComponentsInChildren<AudioReverbFilter>(true))DestroyImmediate(c);
 foreach(var c in clone.GetComponentsInChildren<AudioSource>(true))DestroyImmediate(c);
 foreach(var c in clone.GetComponentsInChildren<AudioListener>(true))DestroyImmediate(c);
 foreach(var r in clone.GetComponentsInChildren<SpriteRenderer>(true))if(r.drawMode!=SpriteDrawMode.Simple)r.sprite=ForTiling(r.sprite);
 clone.transform.SetParent(parent,false);clone.SetActive(true);Destroy(staging);return clone;
 }
 TileBase Tile(string name){TileBase value;if(tiles.TryGetValue(name,out value))return value;foreach(var t in tiles.Values)return t;return null;}
 public SpriteRenderer Make(TutorialDemoClip.PropType type,Rect rect,Transform parent){
 if(type==TutorialDemoClip.PropType.Block){MakeTiles(rect,parent);return null;}
 GameObject source=type==TutorialDemoClip.PropType.Lever?(lever!=null?lever.gameObject:null):type==TutorialDemoClip.PropType.Breakable?(box!=null?box.gameObject:null):(door!=null?door.gameObject:null);
 var copy=VisualCopy(source,parent);if(copy==null)return null;copy.name="Actual_"+type;
 copy.transform.localRotation=Quaternion.identity;copy.transform.localScale=source.transform.lossyScale;
 var sr=copy.GetComponent<SpriteRenderer>();if(sr==null)sr=copy.GetComponentInChildren<SpriteRenderer>();
 if(type==TutorialDemoClip.PropType.Lever){
 copy.transform.localPosition=new Vector3(rect.center.x,rect.yMin+.3f,0);
 if(sr!=null){sr.sprite=lever.offSprite;sr.sortingOrder=5;}
 }else if(sr!=null){
 copy.transform.localPosition=new Vector3(rect.center.x,rect.center.y,0);
 sr.sprite=ForTiling(sr.sprite);sr.drawMode=SpriteDrawMode.Tiled;sr.size=new Vector2(rect.width/Mathf.Abs(copy.transform.localScale.x),rect.height/Mathf.Abs(copy.transform.localScale.y));sr.sortingOrder=2;if(type==TutorialDemoClip.PropType.Door)FitDoor(sr,rect);
 }
 return sr;
 }
 public int GroundSortingLayer {get{return ground!=null?ground.GetComponent<TilemapRenderer>().sortingLayerID:0;}}
 readonly Dictionary<Sprite,Bounds> opaqueBounds=new Dictionary<Sprite,Bounds>();
 Bounds VisibleBounds(Sprite sprite){
 if(opaqueBounds.TryGetValue(sprite,out var result))return result;
 Rect frame=sprite.rect;int w=Mathf.RoundToInt(frame.width),h=Mathf.RoundToInt(frame.height);
 var rt=RenderTexture.GetTemporary(w,h,0);var old=RenderTexture.active;var pixels=new Texture2D(w,h,TextureFormat.RGBA32,false);
 try{
 var texture=sprite.texture;Graphics.Blit(texture,rt,new Vector2(frame.width/texture.width,frame.height/texture.height),new Vector2(frame.x/texture.width,frame.y/texture.height));RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,w,h),0,0);pixels.Apply();var data=pixels.GetPixels32();int minX=w,minY=h,maxX=-1,maxY=-1;
 for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(data[y*w+x].a>16){minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);}
 result=sprite.bounds;
 if(maxX>=minX&&maxY>=minY){Vector2 low=(new Vector2(minX,minY)-sprite.pivot)/sprite.pixelsPerUnit;Vector2 high=(new Vector2(maxX+1,maxY+1)-sprite.pivot)/sprite.pixelsPerUnit;result=new Bounds((low+high)*.5f,high-low);}
 opaqueBounds.Add(sprite,result);return result;
 }finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Destroy(pixels);}
 }
 public void FitDoor(SpriteRenderer sr,Rect rect){if(sr==null||sr.sprite==null)return;sr.drawMode=SpriteDrawMode.Simple;var b=VisibleBounds(sr.sprite);var scale=new Vector3(rect.width/Mathf.Max(.01f,b.size.x),rect.height/Mathf.Max(.01f,b.size.y),1);sr.transform.localScale=scale;sr.transform.localPosition=new Vector3(rect.center.x-b.center.x*scale.x,rect.yMin-.025f-b.min.y*scale.y,0);}
 void MakeTiles(Rect rect,Transform parent){
 var g=new GameObject("ActualGroundGrid",typeof(Grid));g.transform.SetParent(parent,false);g.transform.localPosition=new Vector3(rect.xMin,rect.yMin,0);
 int w=Mathf.Max(2,Mathf.CeilToInt(rect.width)),h=Mathf.Max(2,Mathf.CeilToInt(rect.height));g.transform.localScale=new Vector3(rect.width/w,rect.height/h,1);
 var go=new GameObject("ActualGroundTiles",typeof(Tilemap),typeof(TilemapRenderer));go.transform.SetParent(g.transform,false);
 var map=go.GetComponent<Tilemap>();var renderer=go.GetComponent<TilemapRenderer>();if(ground!=null){map.color=ground.color;renderer.sharedMaterial=ground.GetComponent<TilemapRenderer>().sharedMaterial;}renderer.sortingOrder=0;renderer.sortingLayerID=GroundSortingLayer;
 for(int y=0;y<h;y++)for(int x=0;x<w;x++){int col=x==0?0:x==w-1?2:1;int id=y==h-1?col:y==0?17+col:9+col;map.SetTile(new Vector3Int(x,y,0),Tile("Tile_"+id));}
 }
 public void SetLever(SpriteRenderer sr,bool on){if(sr!=null&&lever!=null)sr.sprite=on?lever.onSprite:lever.offSprite;}
 public void BuildBackdrop(Transform parent){
 foreach(var source in backdrops){var copy=VisualCopy(source.gameObject,parent);copy.name="ActualBackdrop_"+source.name;backdropCopies.Add(copy);}
 }
 public void FitBackdrop(Vector2 center,float height,float width){
 foreach(var copy in backdropCopies){var sr=copy.GetComponent<SpriteRenderer>();if(sr==null||sr.sprite==null)continue;
 copy.transform.localPosition=new Vector3(center.x,center.y,0);copy.transform.localRotation=Quaternion.identity;
 if(copy.name.Contains("Background3")){sr.drawMode=SpriteDrawMode.Simple;copy.transform.localScale=new Vector3((width+2)/sr.sprite.bounds.size.x,(height+2)/sr.sprite.bounds.size.y,1);sr.sortingOrder=-30;}
 else {float scale=Mathf.Max(width/sr.sprite.bounds.size.x,height/sr.sprite.bounds.size.y)*1.04f;copy.transform.localScale=new Vector3(scale,scale,1);sr.drawMode=SpriteDrawMode.Simple;sr.sortingOrder=-50;}
 }
 }
}
