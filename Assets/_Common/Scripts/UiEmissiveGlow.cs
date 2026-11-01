using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(1800)]
public sealed class UiEmissiveGlow:MonoBehaviour{
 Graphic target;Image halo;Material material,original;TMP_Text text;Color tint;float strength,phase;
 public static UiEmissiveGlow Add(Graphic target,Color tint,float strength=.5f){
 if(target==null)return null;var g=target.GetComponent<UiEmissiveGlow>();if(g==null)g=target.gameObject.AddComponent<UiEmissiveGlow>();g.Configure(target,tint,strength);return g;
 }
 void Configure(Graphic graphic,Color color,float power){
 if(target!=null)return;target=graphic;tint=color;strength=power;phase=Random.Range(0,6.28f);text=graphic as TMP_Text;
 if(text!=null){original=text.fontSharedMaterial;material=new Material(original);var shader=Shader.Find("TextMeshPro/Distance Field");if(shader!=null)material.shader=shader;material.EnableKeyword("GLOW_ON");material.SetFloat("_GlowOffset",0);material.SetFloat("_GlowInner",.055f);material.SetFloat("_GlowOuter",.38f);material.SetFloat("_GlowPower",.65f);text.fontSharedMaterial=material;text.extraPadding=true;text.UpdateMeshPadding();}
 else{var go=new GameObject("SoftEmission",typeof(RectTransform),typeof(Image));go.transform.SetParent(graphic.transform.parent,false);go.transform.SetSiblingIndex(graphic.transform.GetSiblingIndex());halo=go.GetComponent<Image>();halo.raycastTarget=false;var template=Resources.Load<Material>("UiSoftGlow");if(template!=null)material=new Material(template);else material=new Material(Shader.Find("UI/AnomalySoftGlow"));halo.material=material;}
 }
 void LateUpdate(){
 if(target==null||material==null)return;float pulse=.85f+.12f*Mathf.Sin(Time.unscaledTime*1.7f+phase)+.045f*Mathf.Sin(Time.unscaledTime*3.3f+phase);
 if(text!=null){material.SetColor("_GlowColor",new Color(tint.r*1.15f,tint.g*1.15f,tint.b*1.15f,strength*pulse));return;}
 halo.enabled=target.enabled;var rt=halo.rectTransform;var source=target.rectTransform;Vector2 size=source.rect.size;float padding=10+2*Mathf.Sin(Time.unscaledTime*1.4f+phase);
 rt.anchorMin=rt.anchorMax=rt.pivot=Vector2.one*.5f;rt.sizeDelta=size+Vector2.one*padding*2;rt.position=source.TransformPoint(source.rect.center);rt.rotation=source.rotation;rt.localScale=source.localScale;
 halo.color=new Color(tint.r,tint.g,tint.b,strength*pulse);material.SetVector("_CoreSize",size);material.SetVector("_OuterSize",rt.sizeDelta);material.SetFloat("_Radius",padding);material.SetFloat("_Strength",1.2f);var rendered=halo.materialForRendering;if(rendered!=null){rendered.SetVector("_CoreSize",size);rendered.SetVector("_OuterSize",rt.sizeDelta);rendered.SetFloat("_Radius",padding);rendered.SetFloat("_Strength",1.2f);}
 var src=target as Image;if(src!=null){halo.type=src.type==Image.Type.Filled?Image.Type.Filled:Image.Type.Simple;halo.fillMethod=src.fillMethod;halo.fillAmount=src.fillAmount;}
 }
 void OnDisable(){if(halo!=null)halo.enabled=false;}
 void OnDestroy(){if(text!=null&&original!=null)text.fontSharedMaterial=original;if(halo!=null)Destroy(halo.gameObject);if(material!=null)Destroy(material);}
}
