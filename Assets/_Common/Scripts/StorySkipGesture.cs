using System.Collections.Generic;
using UnityEngine;

// Story-only input: clockwise or anticlockwise closed circles skip exactly one picture.
// A plain left click first shows the whole line, then the next click moves to the next picture.
public sealed class StorySkipGesture : MonoBehaviour
{
    public StorySequence sequence;
    readonly List<Vector2> points=new List<Vector2>();
    bool drawing;
    float downAt;
    GestureTrail trail;
    void Start(){trail=FindFirstObjectByType<GestureTrail>();if(trail==null){var go=new GameObject("StoryGestureBrush");go.transform.SetParent(transform,false);trail=go.AddComponent<GestureTrail>();}trail.color=Color.white;trail.SetSortingOrder(100);}
    void Update()
    {
        if(sequence==null||!sequence.CanSkipCut){drawing=false;points.Clear();return;}
        if(Input.GetMouseButtonDown(1)){drawing=false;points.Clear();return;}
        if(Input.GetMouseButtonDown(0)&&!PauseMenu.IsPointerOnSettingsButton(Input.mousePosition)){points.Clear();points.Add(Input.mousePosition);drawing=true;downAt=Time.unscaledTime;}
        if(drawing&&Input.GetMouseButton(0)){Vector2 at=Input.mousePosition;if((at-points[points.Count-1]).sqrMagnitude>=9)points.Add(at);}
        if(drawing&&Input.GetMouseButtonUp(0)){drawing=false;points.Add(Input.mousePosition);if(IsClick(points))SkipByClick();else SubmitStroke(points);points.Clear();}
    }
    public bool SubmitStroke(IList<Vector2> stroke)
    {
        if(sequence==null||!sequence.CanSkipCut||!IsCircle(stroke))return false;
        if(!sequence.SkipToNextCut())return false;
        if(trail!=null)trail.SetStrokeColor(Color.white);
        SettingsHoverAudio.PlayTutorialClick(this);return true;
    }
    // 거의 안 움직이고 짧게 누른 좌클릭 = 대사 끝까지 보이기 / 다 나왔으면 다음 장
    bool IsClick(IList<Vector2> stroke)
    {
        if(Time.unscaledTime-downAt>.35f)return false;
        float limit=Mathf.Max(12,Screen.height*.015f);
        for(int i=1;i<stroke.Count;i++)if((stroke[i]-stroke[0]).sqrMagnitude>limit*limit)return false;
        return true;
    }
    void SkipByClick(){if(sequence!=null&&sequence.ClickAdvance())SettingsHoverAudio.PlayTutorialClick(this);}
    public static bool IsCircle(IList<Vector2> stroke)
    {
        if(stroke==null||stroke.Count<16)return false;
        Vector2 min=stroke[0],max=min;float length=0;
        for(int i=1;i<stroke.Count;i++){min=Vector2.Min(min,stroke[i]);max=Vector2.Max(max,stroke[i]);length+=Vector2.Distance(stroke[i-1],stroke[i]);}
        Vector2 size=max-min;float span=Mathf.Max(size.x,size.y);
        if(span<Mathf.Max(32,Screen.height*.04f)||Mathf.Min(size.x,size.y)<span*.45f||Vector2.Distance(stroke[0],stroke[stroke.Count-1])>span*.35f)return false;
        if(length<span*2.2f||length>span*4.5f)return false;
        Vector2 center=(min+max)*.5f;float turn=0;
        for(int i=1;i<stroke.Count;i++){var a=stroke[i-1]-center;var b=stroke[i]-center;if(a.sqrMagnitude<span*span*.025f||b.sqrMagnitude<span*span*.025f)return false;turn+=Mathf.Atan2(a.x*b.y-a.y*b.x,Vector2.Dot(a,b));}
        return Mathf.Abs(turn)>5.0f&&Mathf.Abs(turn)<8.2f;
    }
    void OnDisable(){drawing=false;points.Clear();}
}
