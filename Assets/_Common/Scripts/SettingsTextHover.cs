using UnityEngine;
using UnityEngine.EventSystems;
public sealed class SettingsTextHover:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
{
 RectTransform target;Vector3 normal;bool over;
 public void Initialize(RectTransform rect){target=rect;normal=rect.localScale;}
 public void OnPointerEnter(PointerEventData data){if(!over)SettingsHoverAudio.PlayText(this);over=true;}
 public void OnPointerExit(PointerEventData data){over=false;}
 void Update(){if(target==null)return;var desired=normal*(over?1.065f:1f);target.localScale=Vector3.Lerp(target.localScale,desired,1-Mathf.Exp(-Time.unscaledDeltaTime*18));}
 void OnDisable(){over=false;if(target!=null)target.localScale=normal;}
}