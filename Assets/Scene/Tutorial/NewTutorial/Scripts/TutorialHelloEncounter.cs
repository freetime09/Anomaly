using System.Collections;
using TMPro;
using UnityEngine;

// Repeatable encounter. Uses the normal speech bubbles and checkpoint death flow.
public sealed class TutorialHelloEncounter : MonoBehaviour
{
    public PlayerController player;
    public DialogueSpeaker stranger;
    public DialogueSpeaker mecro;
    public SpriteRenderer face;
    public float approachDistance = 3f;
    FallRespawn respawn;
    Coroutine encounter;
    bool busy;
    float readyAt;

    void Start()
    {
        if(player==null)player=FindAnyObjectByType<PlayerController>();
        if(player!=null)respawn=player.GetComponent<FallRespawn>();
        if(respawn!=null)respawn.Respawned+=OnRespawned;
        if(face!=null)face.flipX=true;
    }
    void Update()
    {
        if(busy||Time.time<readyAt||player==null||PauseMenu.IsPaused)return;
        if(TutorialDemoWindow.Instance!=null&&TutorialDemoWindow.Instance.IsOpen)return;
        if(Mathf.Abs(player.transform.position.x-transform.position.x)<=approachDistance&&
            Mathf.Abs(player.transform.position.y-transform.position.y)<3)
            encounter=StartCoroutine(Conversation());
    }
    IEnumerator Conversation()
    {
        busy=true;
        CutsceneLock.Lock(this);
        player.ResetMotion();
        if(face!=null)face.flipX=player.transform.position.x<transform.position.x;
        yield return new WaitForSeconds(.25f);
        yield return stranger.Bubble.Say("?",18);
        yield return new WaitForSeconds(.8f);
        stranger.Bubble.Hide();
        yield return new WaitForSeconds(.25f);
        yield return stranger.Bubble.Say("네가 왜 여깄지?",24);
        yield return new WaitForSeconds(1.25f);
        stranger.Bubble.Hide();
        yield return new WaitForSeconds(.25f);
        var warning=StartCoroutine(stranger.Bubble.Say("여기는 길이 아니다 돌아가도록.",28));
        yield return new WaitForSeconds(.7f);
        var reply=StartCoroutine(mecro.Bubble.Say("넌 누군데!!",26));
        yield return new WaitForSeconds(.18f);
        StopCoroutine(warning);
        stranger.Bubble.Hide();
        yield return reply;
        yield return new WaitForSeconds(.8f);
        mecro.Bubble.Hide();
        CutsceneLock.Unlock(this);
        // If the player went left before touching the first checkpoint, use it.
        if(respawn!=null){
            foreach(var cp in FindObjectsByType<Checkpoint>(FindObjectsSortMode.None))
                if(cp.order==1){respawn.TrySetCheckpoint(cp.order,cp.transform.position+(Vector3)cp.respawnOffset);break;}
            respawn.Die();
        }else busy=false;
        encounter=null;
    }
    void OnRespawned(Vector3 position)
    {
        busy=false;readyAt=Time.time+1;
        if(face!=null)face.flipX=true;
        if(stranger!=null&&stranger.Bubble!=null)stranger.Bubble.HideImmediate();
        if(mecro!=null&&mecro.Bubble!=null)mecro.Bubble.HideImmediate();
    }
    void OnDisable()
    {
        if(encounter!=null)StopCoroutine(encounter);
        CutsceneLock.Unlock(this);
        if(respawn!=null)respawn.Respawned-=OnRespawned;
    }
}
