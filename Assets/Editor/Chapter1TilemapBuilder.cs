using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object=UnityEngine.Object;

// Editor authoring only. The saved scene contains ordinary editable tiles and puzzle objects.
public static class Chapter1TilemapBuilder
{
    const string AssetsRoot="Assets/Images/11.10/Chapter1";
    const string MapName="Chapter1_Tilemap_11_10";
    static Material art, ropeArt;
    static Sprite stone,crate,log,tree,buttonOff,buttonOn;
    static Tile[] tiles;
    static Tile down;
    static Chapter1TerrainTile soil;
    static PressButton buttonSource;
    static Lever leverSource;
    static PullBox boxSource;
    static Checkpoint checkpointSource;
    static Transform mapRoot,puzzles,scenery;
    static int ground;

    static readonly List<Vector3Int> positions=new List<Vector3Int>();
    static readonly List<TileBase> painted=new List<TileBase>();
    static readonly Vector2[] profile={new Vector2(-12,0),new Vector2(12,0),new Vector2(28,-3),new Vector2(38,-5),new Vector2(43,-5),new Vector2(44,-1),new Vector2(49,-1),new Vector2(59,-7),new Vector2(72,-10),new Vector2(78,-12),new Vector2(89,-12),new Vector2(90,-7),new Vector2(96,-7),new Vector2(108,-15),new Vector2(116,-16),new Vector2(120,-16),new Vector2(126,-17),new Vector2(132,-18),new Vector2(148,-22),new Vector2(166,-25),new Vector2(181,-28),new Vector2(194,-30),new Vector2(198,-30),new Vector2(212,-31),new Vector2(218,-31),new Vector2(231,-34),new Vector2(248,-38),new Vector2(264,-40),new Vector2(283,-40),new Vector2(291,-43),new Vector2(299,-46),new Vector2(308,-48)};
    static int Height(int x){for(int i=1;i<profile.Length;i++)if(x<=profile[i].x)return Mathf.RoundToInt(Mathf.Lerp(profile[i-1].y,profile[i].y,Mathf.InverseLerp(profile[i-1].x,profile[i].x,x)));return -48;}
    static bool Gap(int x){return (x>=120&&x<126)||(x>=198&&x<212);}

    public static string Build(bool replaceGenerated=false)
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("플레이 모드를 종료하세요.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(!scene.path.EndsWith("Chapter1.unity"))throw new InvalidOperationException("Chapter1 씬을 먼저 여세요.");
        var previous=GameObject.Find(MapName);if(previous!=null&&!replaceGenerated)throw new InvalidOperationException("기존 편집 맵을 자동으로 덮어쓰지 않습니다.");
        Folder(AssetsRoot+"/Tiles");Folder(AssetsRoot+"/Palettes");Folder(AssetsRoot+"/Prefabs");Folder(AssetsRoot+"/Reference");
        if(previous!=null)Object.DestroyImmediate(previous);
        buttonSource=Object.FindObjectsByType<PressButton>(FindObjectsInactive.Include,FindObjectsSortMode.None).First();
        boxSource=Object.FindObjectsByType<PullBox>(FindObjectsInactive.Include,FindObjectsSortMode.None).First();
        checkpointSource=Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(c=>c.GetComponent<CheckpointNeon>()!=null);
        ground=LayerMask.NameToLayer("Ground");PrepareTiles();
        art=AssetDatabase.LoadAssetAtPath<Material>(AssetsRoot+"/Terrain_Unlit.mat");if(art==null){art=new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));AssetDatabase.CreateAsset(art,AssetsRoot+"/Terrain_Unlit.mat");}
        ropeArt=AssetDatabase.LoadAssetAtPath<Material>(AssetsRoot+"/Rope.mat");if(ropeArt==null){ropeArt=new Material(Shader.Find("Sprites/Default"));AssetDatabase.CreateAsset(ropeArt,AssetsRoot+"/Rope.mat");}
        stone=Sprite("돌멩이");crate=Sprite("상자");log=Sprite("통나무");tree=Sprite("나무");buttonOff=Sprite("나무버튼1");buttonOn=Sprite("나무버튼2");
        mapRoot=new GameObject(MapName,typeof(Grid)).transform;
        puzzles=Node("Puzzles_5개의_서로다른_구간",mapRoot,Vector3.zero).transform;scenery=Node("BackgroundTrees_충돌없음",mapRoot,Vector3.zero).transform;
        var terrainGo=Node("Terrain_Ground_여기를_페인트",mapRoot,Vector3.zero);terrainGo.layer=ground;var terrain=terrainGo.AddComponent<Tilemap>();var tr=terrainGo.AddComponent<TilemapRenderer>();tr.sharedMaterial=art;tr.mode=TilemapRenderer.Mode.Chunk;
        var rb=terrainGo.AddComponent<Rigidbody2D>();rb.bodyType=RigidbodyType2D.Static;var composite=terrainGo.AddComponent<CompositeCollider2D>();composite.geometryType=CompositeCollider2D.GeometryType.Polygons;composite.generationType=CompositeCollider2D.GenerationType.Synchronous;composite.vertexDistance=.025f;
        var tc=terrainGo.AddComponent<TilemapCollider2D>();tc.compositeOperation=Collider2D.CompositeOperation.Merge;
        positions.Clear();painted.Clear();
        for(int x=-12;x<308;x++)
        {
            if(Gap(x))continue;int h=Height(x),next=Height(x+1);bool descends=next==h-1;bool rises=next==h+1;
            for(int y=-78;y<h;y++){positions.Add(new Vector3Int(x,y,0));painted.Add(descends&&y==h-1?down:soil);}
            if(rises){positions.Add(new Vector3Int(x,h,0));painted.Add(tiles[21]);}
        }
        terrain.SetTiles(positions.ToArray(),painted.ToArray());terrain.CompressBounds();terrain.RefreshAllTiles();tc.ProcessTilemapChanges();composite.GenerateGeometry();
        int[] starts={-12,34,64,112,142,192,232,264};int[] ends={34,64,112,142,192,232,264,308};
        string[] names={"01_잠에서_깬_산등성이","02_P1_상자로_바위턱_오르기","03_P2_돌멩이_무게추_발판","04_P3_통나무를_쳐서_쓰러뜨리기","05_전망과_구불구불한_하산로","06_P4_나뭇가지_투척과_순간이동","07_숲속_쉼터","08_P5_봉인을_풀고_바위_깨기"};
        string[] notes={"시작 연출과 완만한 첫 내리막. 이동·점프 자유.","상자를 끌어 바위턱의 발판으로 사용. 세운 나뭇가지도 활용 가능.","돌멩이를 나무 버튼에 올려 매달린 원형 바위를 내리고 발판으로 건넙니다.","나뭇가지 공격으로 단 하나의 통나무를 넘어뜨려 작은 계곡을 건넙니다.","퍼즐 사이 휴식. 산의 높낮이와 숲을 보여주는 연속 하강 구간.","넓은 자연 틈. N으로 반대편에 나뭇가지를 꽂고 ⋉로 이동. 착지 후 S로 회수.","마지막 퍼즐 전 쉬는 숲길과 체크포인트.","상자로 버튼 유지 + 레버를 켜 봉인을 해제한 뒤 나뭇가지로 둥근 바위를 공격합니다."};
        var zones=new Transform[8];for(int i=0;i<8;i++){zones[i]=Node(names[i],puzzles,new Vector3(starts[i],Height(starts[i]))).transform;var note=zones[i].gameObject.AddComponent<Chapter1SectionNote>();note.areaSize=new Vector2(ends[i]-starts[i],16);note.designerNotes=notes[i];}
        Crate(zones[1],"끌어서_발판으로_쓰는_상자",40.2f,-5);
        MakeCounterweight(zones[2]);MakeSingleLog(zones[3]);MakeFinalRock(zones[7]);
        Pit(zones[3],123,-19.2f,5.7f);Pit(zones[5],205,-33.7f,13.5f);
        Checkpoint(zones[0],1,4,Height(4));Checkpoint(zones[1],2,62,Height(62));Checkpoint(zones[3],3,114,Height(114));Checkpoint(zones[6],4,234,Height(234));Checkpoint(zones[7],5,265,Height(265));
        Forest();
        var old=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(t=>t.name=="MountainRoute_Reworked");if(old!=null)old.gameObject.SetActive(false);
        var exit=GameObject.Find("CliffExit_Chapter2");exit.transform.position=new Vector3(315,-53);var ec=exit.GetComponent<BoxCollider2D>();ec.size=new Vector2(10,14);ec.isTrigger=true;
        var glide=Object.FindFirstObjectByType<Chapter1CliffGlide>();if(glide!=null){glide.edgeX=308;glide.topY=-48;}
        var backdrop=Object.FindFirstObjectByType<LayeredParallaxBackground>();if(backdrop!=null)backdrop.visibleCameraX=new Vector2(-100,380);
        var player=Object.FindFirstObjectByType<PlayerController>();float feet=player.transform.position.y-player.GetComponent<Collider2D>().bounds.min.y;player.transform.position=new Vector3(0,feet+.025f,0);Camera.main.transform.position=player.transform.position+new Vector3(1,2,-10);
        MakePalette();MakeDraftSample();PrefabUtility.SaveAsPrefabAsset(mapRoot.gameObject,AssetsRoot+"/Prefabs/Chapter1_EditableMap.prefab");AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Selection.activeGameObject=terrainGo;
        if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(new Vector3(20,-3),Quaternion.identity,25);
        return "320 units / 5 unique puzzles / 1 intact log / "+positions.Count+" tiles";
    }

    static void Crate(Transform parent,string name,float x,float floor)
    {
        var go=Node(name,parent,new Vector3(x,floor+.77f));go.layer=ground;var c=go.AddComponent<BoxCollider2D>();c.size=new Vector2(1.6f,1.5f);c.edgeRadius=.025f;DynamicBox(go,2);
        Graphic("CrateArt",go.transform,crate,go.transform.position,new Vector2(1.6f,1.5f),5);Grab(go,1.5f);
    }
    static void DynamicBox(GameObject go,float mass){var b=go.AddComponent<Rigidbody2D>();b.mass=mass;b.gravityScale=1;b.interpolation=RigidbodyInterpolation2D.Interpolate;b.collisionDetectionMode=CollisionDetectionMode2D.Continuous;b.constraints=RigidbodyConstraints2D.FreezePositionX|RigidbodyConstraints2D.FreezeRotation;}
    static void Grab(GameObject go,float height){var box=go.AddComponent<PullBox>();EditorUtility.CopySerialized(boxSource,box);box.interactRadius=2.5f;box.promptHeight=height*.5f+.6f;box.grabHintHeight=height*.5f+1.35f;}
    static SpriteRenderer RoundStone(GameObject go,float diameter,int order)
    {
        return Graphic("RoundStoneArt",go.transform,stone,go.transform.position,new Vector2(diameter*stone.bounds.size.x/stone.bounds.size.y,diameter),order);
    }
    static PressButton Button(Transform parent,float x,float floor,int channel)
    {
        var go=Node("WoodButton_"+channel,parent,new Vector3(x,floor+.015f));go.layer=ground;var poly=go.AddComponent<PolygonCollider2D>();poly.SetPath(0,new[]{new Vector2(-1.2f,0),new Vector2(-1.05f,.2f),new Vector2(1.05f,.2f),new Vector2(1.2f,0)});
        var sr=Graphic("WoodButtonArt",go.transform,buttonOff,go.transform.position+Vector3.up*.1067f,new Vector2(2.4f,.2133f),6);
        var b=go.AddComponent<PressButton>();EditorUtility.CopySerialized(buttonSource,b);b.sr=sr;b.releasedSprite=buttonOff;b.pressedSprite=buttonOn;b.releasedTopY=.2f;b.pressedTopY=.1467f;b.detectSize=new Vector2(2.25f,.36f);b.detectOffset=new Vector2(0,.27f);b.pressLayers=~0;b.signalId=channel;b.branchRadius=.9f;b.latch=false;b.pressed=false;b.onPressed=new UnityEngine.Events.UnityEvent();b.onReleased=new UnityEngine.Events.UnityEvent();return b;
    }
    static Lever LeverAt(Transform parent,float x,float floor,int channel)
    {
        var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Common/Prefabs/Lever.prefab"));go.name="Lever_"+channel;go.transform.SetParent(parent,true);go.transform.position=new Vector3(x,floor+.9f);var lever=go.GetComponent<Lever>();lever.signalId=channel;lever.on=false;lever.onTurnOn=new UnityEngine.Events.UnityEvent();lever.onTurnOff=new UnityEngine.Events.UnityEvent();foreach(var wire in go.GetComponentsInChildren<SignalWire>(true))Object.DestroyImmediate(wire.gameObject);return lever;
    }
    static void MakeCounterweight(Transform parent)
    {
        Button(parent,80.3f,-12,1200);
        var weight=Node("운반할_둥근돌",parent,new Vector3(77,-11.42f));weight.layer=ground;weight.AddComponent<CircleCollider2D>().radius=.56f;DynamicBox(weight,3);RoundStone(weight,1.2f,5);Grab(weight,1.2f);
        var lift=Node("밧줄에_매달린_원형_돌발판",parent,new Vector3(86.4f,-6.8f));lift.layer=ground;lift.AddComponent<CircleCollider2D>().radius=1.25f;var rb=lift.AddComponent<Rigidbody2D>();rb.bodyType=RigidbodyType2D.Kinematic;rb.interpolation=RigidbodyInterpolation2D.Interpolate;RoundStone(lift,2.6f,3);
        var move=lift.AddComponent<MovingObject>();move.signalIds=new List<int>{1200};move.requiredCount=1;move.direction=MovingObject.Dir.Down;move.distance=4;move.moveSpeed=2.5f;move.returnSpeed=1.7f;move.returnDelay=.5f;
        var anchorTree=Graphic("CounterweightTree",scenery,tree,new Vector3(84.3f,-6.1f),new Vector2(11.8f*tree.bounds.size.x/tree.bounds.size.y,11.8f),-12);
        var rope=Node("CounterweightRope",parent,Vector3.zero);var line=rope.AddComponent<LineRenderer>();line.sharedMaterial=ropeArt;line.sortingOrder=2;line.widthMultiplier=.065f;line.startColor=line.endColor=new Color(.47f,.34f,.18f);line.positionCount=2;line.SetPosition(0,new Vector3(86.4f,-3.5f));line.SetPosition(1,new Vector3(86.4f,-5.5f));var follow=rope.AddComponent<Chapter1LiftRope>();follow.stone=lift.transform;follow.anchor=new Vector3(86.4f,-3.5f);follow.stoneRadius=1.3f;
    }
    static void MakeSingleLog(Transform parent)
    {
        var go=Node("통나무_단_한개_공격해서_넘기기",parent,new Vector3(119.2f,-16.65f));go.layer=ground;
        const float width=7.4f;float height=width*log.bounds.size.y/log.bounds.size.x;
        var sr=Graphic("OneIntactLog",go.transform,log,go.transform.position+Vector3.right*(width*.5f),new Vector2(width,height),4);
        var col=go.AddComponent<BoxCollider2D>();col.size=new Vector2(width,height*.85f);col.offset=new Vector2(width*.5f,0);var rb=go.AddComponent<Rigidbody2D>();rb.bodyType=RigidbodyType2D.Kinematic;rb.interpolation=RigidbodyInterpolation2D.Interpolate;rb.rotation=70;go.transform.rotation=Quaternion.Euler(0,0,70);go.AddComponent<Chapter1FallingLog>();
    }
    static void MakeFinalRock(Transform parent)
    {
        Button(parent,272.4f,-40,1800);Crate(parent,"마지막_봉인용_상자",268.5f,-40);LeverAt(parent,276.5f,-40,1801);
        var go=Node("두_봉인을_풀고_공격할_둥근바위",parent,new Vector3(281,-36.95f));go.layer=ground;go.AddComponent<CircleCollider2D>().radius=3.05f;var sr=RoundStone(go,6.4f,4);var rock=go.AddComponent<Chapter1LockedRock>();rock.artwork=sr;rock.signalIds=new List<int>{1800,1801};rock.requiredCount=2;
    }
    static void Pit(Transform parent,float x,float y,float width){var go=Node("PitReset_낙하복귀",parent,new Vector3(x,y));var c=go.AddComponent<BoxCollider2D>();c.isTrigger=true;c.size=new Vector2(width,1);go.AddComponent<Chapter1PitReset>();}
    static void Checkpoint(Transform parent,int order,float x,float floor)
    {
        var go=Object.Instantiate(checkpointSource.gameObject,parent);go.name="Checkpoint_"+order;go.transform.position=new Vector3(x,floor+1);var cp=go.GetComponent<Checkpoint>();cp.order=order;var neon=go.GetComponent<CheckpointNeon>();neon.monument.sprite=neon.inactiveSprite;foreach(var animator in go.GetComponentsInChildren<Animator>(true))animator.enabled=false;go.transform.position+=Vector3.up*(floor-neon.monument.bounds.min.y);var player=Object.FindFirstObjectByType<PlayerController>();float feet=player.transform.position.y-player.GetComponent<Collider2D>().bounds.min.y;cp.respawnOffset=new Vector2(-1.8f,floor+feet+.03f-go.transform.position.y);
    }
    static void Forest()
    {
        var random=new System.Random(1110);float x=-8;int n=0;
        while(x<305)
        {
            int ix=Mathf.FloorToInt(x);bool clear=(x>38&&x<51)||(x>77&&x<98)||(x>116&&x<129)||(x>196&&x<215)||(x>268&&x<285);
            if(!Gap(ix)&&!clear){float h=4.7f+(float)random.NextDouble()*3.4f;bool far=n%3==0;var sr=Graphic("Tree_"+n,scenery,tree,new Vector3(x,Height(ix)+h*.5f-.12f),new Vector2(h*tree.bounds.size.x/tree.bounds.size.y,h),far?-25:-15);sr.color=far?new Color(.65f,.77f,.68f,.82f):new Color(.86f,.96f,.84f,1);sr.flipX=random.Next(2)==0;n++;}
            x+=4.5f+(float)random.NextDouble()*4.3f;
        }
        for(int i=0;i<17;i++){float px=17+i*16.4f;if(Gap((int)px)||px>268)continue;float d=.35f+(i%3)*.19f;var go=Node("GroundPebble_"+i,scenery,new Vector3(px,Height((int)px)+d*.38f));RoundStone(go,d,1);}
    }

    static void Folder(string path){Directory.CreateDirectory(path);}
    static Sprite Sprite(string name){return AssetDatabase.LoadAllAssetsAtPath("Assets/Images/11.10/"+name+".png").OfType<Sprite>().First();}
    static T Asset<T>(string path) where T:ScriptableObject
    {
        var value=AssetDatabase.LoadAssetAtPath<T>(path);
        if(value==null){value=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(value,path);}
        return value;
    }
    static GameObject Node(string name,Transform parent,Vector3 position)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.position=position;return go;
    }
    static SpriteRenderer Graphic(string name,Transform parent,Sprite sprite,Vector3 position,Vector2 size,int order)
    {
        var go=Node(name,parent,position);var sr=go.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.sharedMaterial=art;sr.sortingOrder=order;
        go.transform.localScale=new Vector3(size.x/sprite.bounds.size.x,size.y/sprite.bounds.size.y,1);return sr;
    }

    static void PrepareTiles()
    {
        string source="Assets/Images/11.10/초안.png";var importer=(TextureImporter)AssetImporter.GetAtPath(source);if(importer.spritePixelsPerUnit!=32||importer.filterMode!=FilterMode.Point||importer.textureCompression!=TextureImporterCompression.Uncompressed){importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.SaveAndReimport();}
        var sprites=AssetDatabase.LoadAllAssetsAtPath(source).OfType<Sprite>().OrderBy(s=>int.Parse(s.name.Substring(s.name.LastIndexOf('_')+1))).ToArray();
        tiles=new Tile[sprites.Length];
        for(int i=0;i<sprites.Length;i++)
        {
            var tile=Asset<Tile>(AssetsRoot+"/Tiles/Draft_"+i.ToString("00")+".asset");tile.sprite=sprites[i];tile.color=Color.white;tile.flags=TileFlags.LockColor;
            tile.colliderType=i>=18&&i!=21&&i!=22&&i!=23&&i!=24?Tile.ColliderType.None:(i==15||i==21?Tile.ColliderType.Sprite:Tile.ColliderType.Grid);tiles[i]=tile;EditorUtility.SetDirty(tile);
        }
        down=Asset<Tile>(AssetsRoot+"/Tiles/DownSlope_45.asset");down.sprite=sprites[21];down.colliderType=Tile.ColliderType.Sprite;down.transform=Matrix4x4.Scale(new Vector3(-1,1,1));down.flags=TileFlags.LockColor;EditorUtility.SetDirty(down);
        soil=Asset<Chapter1TerrainTile>(AssetsRoot+"/Tiles/AutoGrass_Soil.asset");soil.m_DefaultSprite=sprites[8];soil.m_DefaultColliderType=Tile.ColliderType.Grid;soil.m_TilingRules.Clear();
        // Corner rules precede broad edge rules. All unmentioned neighbors are ignored.
        Rule(sprites[0],new Vector3Int(0,1),new Vector3Int(-1,0));Rule(sprites[2],new Vector3Int(0,1),new Vector3Int(1,0));
        Rule(sprites[11],new Vector3Int(0,-1),new Vector3Int(-1,0));Rule(sprites[13],new Vector3Int(0,-1),new Vector3Int(1,0));
        Rule(sprites[1],new Vector3Int(0,1));Rule(sprites[12],new Vector3Int(0,-1));Rule(sprites[7],new Vector3Int(-1,0));Rule(sprites[9],new Vector3Int(1,0));EditorUtility.SetDirty(soil);
    }
    static void Rule(Sprite sprite,params Vector3Int[] empty)
    {
        var rule=new RuleTile.TilingRule();rule.m_Sprites=new[]{sprite};rule.m_ColliderType=Tile.ColliderType.Grid;rule.m_NeighborPositions=empty.ToList();rule.m_Neighbors=empty.Select(p=>RuleTile.TilingRuleOutput.Neighbor.NotThis).ToList();soil.m_TilingRules.Add(rule);
    }
    static void MakePalette()
    {
        string path=AssetsRoot+"/Palettes/Chapter1_11_10_Palette.prefab";
        if(AssetDatabase.LoadAssetAtPath<GameObject>(path)==null)GridPaletteUtility.CreateNewPalette(AssetsRoot+"/Palettes","Chapter1_11_10_Palette",GridLayout.CellLayout.Rectangle,GridPalette.CellSizing.Manual,Vector3.one,GridLayout.CellSwizzle.XYZ);
        var root=PrefabUtility.LoadPrefabContents(path);var tilemap=root.GetComponentInChildren<Tilemap>();tilemap.ClearAllTiles();
        for(int i=0;i<tiles.Length;i++){var r=tiles[i].sprite.rect;tilemap.SetTile(new Vector3Int((int)r.x/32,-(166-(int)r.y)/32,0),tiles[i]);}
        tilemap.SetTile(new Vector3Int(9,0),soil);tilemap.SetTile(new Vector3Int(9,-2),down);
        PrefabUtility.SaveAsPrefabAsset(root,path);PrefabUtility.UnloadPrefabContents(root);
    }
    static void MakeDraftSample()
    {
        var go=new GameObject("초안_편집용_타일맵",typeof(Grid));var child=new GameObject("원본_32px_배치",typeof(Tilemap),typeof(TilemapRenderer));child.transform.SetParent(go.transform,false);var map=child.GetComponent<Tilemap>();
        foreach(var tile in tiles){var r=tile.sprite.rect;map.SetTile(new Vector3Int((int)r.x/32,-(166-(int)r.y)/32,0),tile);}
        PrefabUtility.SaveAsPrefabAsset(go,AssetsRoot+"/Prefabs/초안_편집용_타일맵.prefab");Object.DestroyImmediate(go);
    }

    [MenuItem("Tools/Anomaly/11.10 타일 팔레트 선택")]
    static void SelectPalette(){Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(AssetsRoot+"/Palettes/Chapter1_11_10_Palette.prefab");EditorGUIUtility.PingObject(Selection.activeObject);}
}
