using System.Collections;
using TMPro;
using UnityEngine;
public sealed class TutorialWelcome:MonoBehaviour{
 public TMP_FontAsset font;
 IEnumerator Start(){var go=new GameObject("TutorialWelcome",typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(CanvasGroup));var c=go.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=700;var scale=go.GetComponent<UnityEngine.UI.CanvasScaler>();scale.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);scale.matchWidthOrHeight=.5f;var group=go.GetComponent<CanvasGroup>();group.blocksRaycasts=false;var t=new GameObject("WelcomeText",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();t.transform.SetParent(go.transform,false);t.font=font;t.fontSize=34;t.color=new Color(.17f,.2f,.24f);t.alignment=TextAlignmentOptions.Center;t.text="튜토리얼에 오신 것을 환영합니다.\n마우스로 직선을 그어서 이동하세요.";t.raycastTarget=false;t.rectTransform.anchorMin=t.rectTransform.anchorMax=new Vector2(.5f,.76f);t.rectTransform.sizeDelta=new Vector2(1400,130);for(float age=0;age<6.5f;age+=PauseMenu.IsPaused?0:Time.unscaledDeltaTime){group.alpha=age<.6f?Mathf.SmoothStep(0,1,age/.6f):age>5.3f?1-Mathf.SmoothStep(0,1,(age-5.3f)/1.2f):1;yield return null;}Destroy(go);Destroy(this);}
}
