using UnityEngine;

public sealed class Chapter1SectionNote : MonoBehaviour
{
    [TextArea(2,5)] public string designerNotes;
    public Vector2 areaSize=new Vector2(32,16);
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color=new Color(.4f,1,.7f,.8f);
        Gizmos.DrawWireCube(transform.position+new Vector3(areaSize.x*.5f,2),areaSize);
        UnityEditor.Handles.Label(transform.position+Vector3.up*7,name+"\n"+designerNotes);
    }
#endif
}
