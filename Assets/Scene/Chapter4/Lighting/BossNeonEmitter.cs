using System.Collections.Generic;
using UnityEngine;

// Marks the original attack renderer, without creating a visual copy.
public sealed class BossNeonEmitter : MonoBehaviour
{
    public static readonly HashSet<BossNeonEmitter> Active = new HashSet<BossNeonEmitter>();
    public SpriteRenderer source;
    public bool rectangle, floor;
    public Color lightTint = Color.clear;
    public float beamStart;
    public float emissionOverride;
    MaterialPropertyBlock properties;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() { Active.Clear(); }
    void Awake() { source=GetComponent<SpriteRenderer>(); properties=new MaterialPropertyBlock(); }
    void OnEnable() { Active.Add(this); }
    void OnDisable() { Active.Remove(this); }
    public bool Warning { get { return rectangle && !floor && source.sortingOrder<=6; } }
    public bool Visible { get { return source!=null && source.enabled && !source.forceRenderingOff && source.sprite!=null && source.color.a>.015f; } }
    void LateUpdate()
    {
        if (!Visible) return;
        var size=source.localBounds.size;
        var scale=transform.lossyScale;
        source.GetPropertyBlock(properties);
        properties.SetFloat("_NeonEmission",emissionOverride>0?emissionOverride:floor?5.1f:Warning?6.3f:4.5f);
        properties.SetFloat("_RectWarning",Warning?1:0);
        properties.SetVector("_WorldSize",new Vector4(Mathf.Abs(size.x*scale.x),Mathf.Abs(size.y*scale.y),0,0));
        source.SetPropertyBlock(properties);
    }
}
