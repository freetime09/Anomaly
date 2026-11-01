using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public sealed class BossRespawnRecovery : MonoBehaviour {
 SpeechBubble bubble;Coroutine routine;readonly List<int> bag=new List<int>();
 DialogueSpeaker owner;float previousFade;
 public void Play(PlayerController player,string[] lines){
 Cancel();if(lines==null||lines.Length==0)return;
 if(bag.Count==0){for(int i=0;i<lines.Length;i++)bag.Add(i);for(int i=bag.Count-1;i>0;i--){int j=Random.Range(0,i+1),v=bag[i];bag[i]=bag[j];bag[j]=v;}}
 int index=bag[0];bag.RemoveAt(0);routine=StartCoroutine(Say(player,lines[Mathf.Clamp(index,0,lines.Length-1)]));
 }
 IEnumerator Say(PlayerController player,string line){
 owner=player.GetComponent<DialogueSpeaker>();if(owner==null)owner=player.gameObject.AddComponent<DialogueSpeaker>();
 previousFade=owner.fadeTime;owner.fadeTime=.4f; bubble=SpeechBubble.Create(owner);
 yield return bubble.Say(line,25);yield return new WaitForSeconds(1.8f);bubble.Hide();yield return new WaitForSeconds(.5f);
 if(bubble!=null)Destroy(bubble.gameObject);bubble=null;owner.fadeTime=previousFade;owner=null;routine=null;
 }
 public void Cancel(){if(routine!=null)StopCoroutine(routine);routine=null;if(bubble!=null){bubble.Hide();Destroy(bubble.gameObject,.5f);bubble=null;}if(owner!=null){owner.fadeTime=previousFade;owner=null;}}
 void OnDisable(){Cancel();}
}