using UnityEngine;

// One intact trunk, pivoting at its root when struck by the player's branch.
public sealed class Chapter1FallingLog : MonoBehaviour, IDamageable, IPuzzleState
{
    public float standingAngle=70, fallenAngle=-8, fallDuration=1.15f;
    Rigidbody2D body;
    bool fallen;
    float angularSpeed;
    void Awake(){body=GetComponent<Rigidbody2D>();angularSpeed=Mathf.Abs(standingAngle-fallenAngle)/Mathf.Max(.1f,fallDuration);}
    void FixedUpdate(){if(body!=null&&!PauseMenu.IsPaused)body.MoveRotation(Mathf.MoveTowardsAngle(body.rotation,fallen?fallenAngle:standingAngle,angularSpeed*Time.fixedDeltaTime));}
    public void TakeHit(int damage,Vector2 direction){if(damage>0)fallen=true;}
    struct State {public bool fallen;public float angle;}
    public object SaveState(){return new State{fallen=fallen,angle=body!=null?body.rotation:transform.eulerAngles.z};}
    public void LoadState(object state){var s=(State)state;fallen=s.fallen;if(body==null)body=GetComponent<Rigidbody2D>();body.rotation=s.angle;transform.rotation=Quaternion.Euler(0,0,s.angle);}
}
