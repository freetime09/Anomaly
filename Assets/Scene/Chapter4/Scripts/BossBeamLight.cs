using UnityEngine;

// Fades the original beam. Bloom spreads light without a second sprite.
public sealed class BossBeamLight : MonoBehaviour
{
    SpriteRenderer source;
    MaterialPropertyBlock properties;
    void Awake(){source=GetComponent<SpriteRenderer>();properties=new MaterialPropertyBlock();}
    public void Configure(Material material,float start,float end)
    {
        source.sharedMaterial=material;source.GetPropertyBlock(properties);
        properties.SetFloat("_Start",start);properties.SetFloat("_End",end);source.SetPropertyBlock(properties);
        var emitter=GetComponent<BossNeonEmitter>();if(emitter!=null)emitter.beamStart=end;
    }
}
