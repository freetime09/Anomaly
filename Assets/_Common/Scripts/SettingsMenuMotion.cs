using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public sealed class SettingsMenuMotion : MonoBehaviour
{
 sealed class Entry {public RectTransform rect; public Vector2 rest; public CanvasGroup alpha;}
 readonly List<Entry> entries=new List<Entry>();
 GameObject menu;
 CanvasGroup group;
 Image stencil;
 Material material;
 Coroutine animation;
 public void Prepare(GameObject root){
 if(menu!=null||root==null)return;
 menu=root;group=root.GetComponent<CanvasGroup>();if(group==null)group=root.AddComponent<CanvasGroup>();
 var children=new List<Transform>();foreach(Transform t in root.transform)children.Add(t);
 var go=new GameObject("SettingsDitherMask",typeof(RectTransform),typeof(Image),typeof(Mask));
 go.transform.SetParent(root.transform,false);
 var rt=(RectTransform)go.transform;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
 stencil=go.GetComponent<Image>();stencil.raycastTarget=false;
 var template=Resources.Load<Material>("SettingsDitherMask");if(template!=null){material=new Material(template);stencil.material=material;}
 go.GetComponent<Mask>().showMaskGraphic=false;
 foreach(var child in children)child.SetParent(go.transform,false);
 foreach(var slider in root.GetComponentsInChildren<Slider>(true)){var rect=(RectTransform)slider.transform;var alpha=slider.GetComponent<CanvasGroup>();if(alpha==null)alpha=slider.gameObject.AddComponent<CanvasGroup>();entries.Add(new Entry{rect=rect,rest=rect.anchoredPosition,alpha=alpha});}
 foreach(var text in root.GetComponentsInChildren<TMP_Text>(true)){
 var rect=text.rectTransform;var alpha=text.GetComponent<CanvasGroup>();if(alpha==null)alpha=text.gameObject.AddComponent<CanvasGroup>();
 if(text.GetComponentInParent<Slider>(true)==null)entries.Add(new Entry{rect=rect,rest=rect.anchoredPosition,alpha=alpha});
 var hover=text.GetComponent<SettingsTextHover>();
 if(text.GetComponentInParent<Selectable>(true)!=null){text.raycastTarget=true;if(hover==null)hover=text.gameObject.AddComponent<SettingsTextHover>();hover.Initialize(rect);}
 else {text.raycastTarget=false;if(hover!=null)Destroy(hover);}
 }
 }
 public void Open(GameObject root){
 Prepare(root);if(menu==null)return;
 if(animation!=null)StopCoroutine(animation);
 menu.SetActive(true);SetDissolve(0);animation=StartCoroutine(OpenRoutine());
 }
 IEnumerator OpenRoutine(){
 group.interactable=false;group.blocksRaycasts=true;
 for(float t=0;t<.8f;t+=Time.unscaledDeltaTime){
 group.alpha=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.6f));
 for(int i=0;i<entries.Count;i++){var e=entries[i];float delay=Mathf.Min(i*.025f,.15f);float u=Mathf.Clamp01((t-delay)/(.8f-delay));float ease=1-Mathf.Pow(1-u,3);e.rect.anchoredPosition=e.rest+Vector2.down*24*(1-ease);e.alpha.alpha=Mathf.SmoothStep(0,1,u);}
 yield return null;
 }
 Restore();group.interactable=true;animation=null;
 }
 public void Close(GameObject root,Action finished){
 Prepare(root);if(menu==null){finished();return;}
 if(animation!=null)StopCoroutine(animation);
 animation=StartCoroutine(CloseRoutine(finished));
 }
 IEnumerator CloseRoutine(Action finished){
 group.interactable=false;group.blocksRaycasts=true;float start=group.alpha;
 for(float t=0;t<.5f;t+=Time.unscaledDeltaTime){
 float u=Mathf.SmoothStep(0,1,t/.5f);SetDissolve(u);group.alpha=start*(1-u*.8f);yield return null;
 }
 group.alpha=0;animation=null;
 finished();
 Restore();SetDissolve(0);
 }
 void SetDissolve(float value){
 if(material!=null)material.SetFloat("_Dissolve",value);
 if(stencil!=null){var rendered=stencil.materialForRendering;if(rendered!=null&&rendered.HasProperty("_Dissolve"))rendered.SetFloat("_Dissolve",value);stencil.SetMaterialDirty();}
 }
 void Restore(){group.alpha=1;foreach(var e in entries){if(e.rect!=null)e.rect.anchoredPosition=e.rest;if(e.alpha!=null)e.alpha.alpha=1;}}
 void OnDestroy(){if(material!=null)Destroy(material);}
}