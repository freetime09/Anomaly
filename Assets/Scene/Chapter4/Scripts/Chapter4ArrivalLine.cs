using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
public sealed class Chapter4ArrivalLine:MonoBehaviour{
 SpeechBubble bubble;DialoguePlayer dialogue;bool interrupted,speaking;
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void Register(){SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
 static void Loaded(Scene s,LoadSceneMode m){if(s.name=="LastScene")new GameObject("Chapter4ArrivalLine").AddComponent<Chapter4ArrivalLine>();}
 IEnumerator Start(){yield return null;var p=FindAnyObjectByType<PlayerController>();if(p==null)yield break;dialogue=DialoguePlayer.Ensure();dialogue.DialogueStarted+=NextDialogue;Vector3 origin=p.transform.position;while(Mathf.Abs(p.transform.position.x-origin.x)<3)yield return null;if(dialogue.IsPlaying||CutsceneLock.IsLocked){Destroy(gameObject);yield break;}var sp=p.GetComponent<DialogueSpeaker>();if(sp==null)yield break;speaking=true;bubble=SpeechBubble.Create(sp);yield return bubble.Say("새들이 지저귀는 소리가 끊겼어.",24);yield return Hold(1.6f);if(!interrupted){bubble.Hide();yield return Hold(.5f);yield return bubble.Say("어떻게 된거지..?",22);yield return Hold(1.8f);bubble.Hide();yield return Hold(.5f);}Destroy(gameObject);}
 IEnumerator Hold(float seconds){for(float t=0;t<seconds&&!interrupted;t+=Time.deltaTime)yield return null;}
 void NextDialogue(DialogueData data){if(!speaking)return;interrupted=true;if(bubble!=null){bubble.CompleteTyping();bubble.HideImmediate();}StopAllCoroutines();Destroy(gameObject);}
 void OnDestroy(){if(dialogue!=null)dialogue.DialogueStarted-=NextDialogue;if(bubble!=null)Destroy(bubble.gameObject);}
}
