using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public sealed class Chapter1LiftRope : MonoBehaviour
{
    public Transform stone;
    public Vector3 anchor;
    public float stoneRadius=1.3f;
    LineRenderer line;
    void Awake(){line=GetComponent<LineRenderer>();line.positionCount=2;line.useWorldSpace=true;}
    void LateUpdate(){if(stone==null||line==null)return;line.SetPosition(0,anchor);line.SetPosition(1,stone.position+Vector3.up*stoneRadius);}
}
