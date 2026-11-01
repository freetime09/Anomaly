using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class FrontEndHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    bool over, down;
    Graphic graphic;Color normal,highlight;bool colors;
    public void Configure(Graphic target,Color resting,Color hovered){graphic=target;normal=resting;highlight=hovered;colors=true;graphic.color=normal;}
    public void OnPointerEnter(PointerEventData e) { over = true; SettingsHoverAudio.PlayText(this); }
    public void OnPointerExit(PointerEventData e) { over = down = false; }
    public void OnPointerDown(PointerEventData e) { if(e.button == PointerEventData.InputButton.Left) down = true; }
    public void OnPointerUp(PointerEventData e) { down = false; }
    void Update() { float u=1-Mathf.Exp(-18*Time.unscaledDeltaTime);transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * (down ? .95f : over ? 1.06f : 1),u);if(colors&&graphic!=null)graphic.color=Color.Lerp(graphic.color,over?highlight:normal,u); }
    void OnDisable() { over = down = false; transform.localScale = Vector3.one; }
}
