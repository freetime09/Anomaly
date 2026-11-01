using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// HDR bloom and a fixed pool of real lights; no duplicated attack sprites.
public sealed class BossNeonLighting : MonoBehaviour
{
    public Material spriteMaterial, particleMaterial;
    public VolumeProfile neonProfile;
    public int maxLights=10;
    public float reflectedIntensity=.22f, lightReach=.85f;
    public static BossNeonLighting Current { get; private set; }
    struct Candidate { public BossNeonEmitter emitter; public float distance; }
    readonly List<Candidate> candidates=new List<Candidate>(96);
    static readonly System.Comparison<Candidate> Comparison=Compare;
    Light2D[] lights;
    Vector3[][] paths;
    Vector2[] sizes;
    Transform lightRoot;
    Volume arenaVolume;
    Camera view;
    PlayerController player;
    float nextRefresh;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() { Current=null; }

    public void BeginArena(Transform arena,SpriteRenderer floor)
    {
        Current=this; view=Camera.main; player=FindFirstObjectByType<PlayerController>();
        var volume=new GameObject("BossNeonBloom").AddComponent<Volume>();arenaVolume=volume;
        volume.transform.SetParent(arena,false);volume.isGlobal=true;volume.priority=80;volume.sharedProfile=neonProfile;
        lightRoot=new GameObject("BossReflectionLights").transform;lightRoot.SetParent(arena,false);
        lights=new Light2D[maxLights];paths=new Vector3[maxLights][];sizes=new Vector2[maxLights];
        for(int i=0;i<maxLights;i++)
        {
            var go=new GameObject("AttackBounce"+i);go.transform.SetParent(lightRoot,false);go.SetActive(false);
            var light=go.AddComponent<Light2D>();light.lightType=Light2D.LightType.Freeform;light.blendStyleIndex=0;
            light.falloffIntensity=.65f;light.shapeLightFalloffSize=lightReach;light.shadowIntensity=0;
            light.targetSortingLayers=new[]{SortingLayer.NameToID("Default")};
            lights[i]=light;paths[i]=new Vector3[4];
        }
        var emitter=Emit(floor);if(emitter!=null)emitter.floor=true;
    }

    public static BossNeonEmitter Emit(SpriteRenderer source,bool rectangle=false,bool preserveMaterial=false)
    {
        if(Current==null||source==null)return null;
        if(!preserveMaterial&&Current.spriteMaterial!=null)source.sharedMaterial=Current.spriteMaterial;
        var emitter=source.GetComponent<BossNeonEmitter>();if(emitter==null)emitter=source.gameObject.AddComponent<BossNeonEmitter>();
        emitter.rectangle=rectangle;return emitter;
    }
    public static void Particle(SpriteRenderer source)
    {
        if(Current!=null&&Current.particleMaterial!=null)source.sharedMaterial=Current.particleMaterial;
    }
    void LateUpdate()
    {
        if(lightRoot==null||!lightRoot.gameObject.activeInHierarchy||view==null||Time.realtimeSinceStartup<nextRefresh)return;
        nextRefresh=Time.realtimeSinceStartup+.025f;
        Vector2 focus=player!=null?(Vector2)player.transform.position:(Vector2)view.transform.position;
        float halfHeight=view.orthographicSize+lightReach,halfWidth=view.orthographicSize*view.aspect+lightReach;
        var center=view.transform.position;candidates.Clear();
        foreach(var emitter in BossNeonEmitter.Active)
        {
            if(emitter==null||!emitter.Visible)continue;
            var b=emitter.source.bounds;
            if(b.max.x<center.x-halfWidth||b.min.x>center.x+halfWidth||b.max.y<center.y-halfHeight||b.min.y>center.y+halfHeight)continue;
            var near=new Vector2(Mathf.Clamp(focus.x,b.min.x,b.max.x),Mathf.Clamp(focus.y,b.min.y,b.max.y));
            float score=emitter.floor?-1000:(near-focus).sqrMagnitude+(emitter.Warning?1:0);
            candidates.Add(new Candidate{emitter=emitter,distance=score});
        }
        candidates.Sort(Comparison);
        for(int i=0;i<lights.Length;i++)
        {
            bool used=i<candidates.Count;if(lights[i].gameObject.activeSelf!=used)lights[i].gameObject.SetActive(used);
            if(!used)continue;
            var emitter=candidates[i].emitter;var source=emitter.source;var b=source.localBounds;var scale=source.transform.lossyScale;
            Vector2 size=new Vector2(Mathf.Abs(b.size.x*scale.x),Mathf.Abs(b.size.y*scale.y));Vector3 localCenter=b.center;
            if(emitter.beamStart>0){localCenter.x+=b.size.x*emitter.beamStart*.5f;size.x*=1-emitter.beamStart;}
            var light=lights[i];light.transform.position=source.transform.TransformPoint(localCenter)+Vector3.back*.2f;light.transform.rotation=source.transform.rotation;
            if((size-sizes[i]).sqrMagnitude>.0001f)
            {
                var path=paths[i];path[0]=new Vector3(-size.x*.5f,-size.y*.5f);path[1]=new Vector3(size.x*.5f,-size.y*.5f);
                path[2]=new Vector3(size.x*.5f,size.y*.5f);path[3]=new Vector3(-size.x*.5f,size.y*.5f);
                light.SetShapePath(path);sizes[i]=size;
            }
            var tint=emitter.lightTint.a>0?emitter.lightTint:source.color;
            float peak=Mathf.Max(.001f,Mathf.Max(tint.r,Mathf.Max(tint.g,tint.b)));
            light.color=new Color(tint.r/peak,tint.g/peak,tint.b/peak);
            light.intensity=(emitter.Warning ? .14f : reflectedIntensity)*source.color.a;light.shapeLightFalloffSize=lightReach;
        }
    }
    static int Compare(Candidate a,Candidate b){return a.distance.CompareTo(b.distance);}
    public void EndArena(){if(Current==this)Current=null;if(lightRoot!=null)lightRoot.gameObject.SetActive(false);if(arenaVolume!=null)arenaVolume.weight=0;}
    void OnDisable(){EndArena();}
}
