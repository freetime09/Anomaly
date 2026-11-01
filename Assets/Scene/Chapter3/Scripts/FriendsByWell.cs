using UnityEngine;

[DefaultExecutionOrder(1200)]
public sealed class FriendsByWell : MonoBehaviour
{
    public Transform friendB, friendC;
    public DialogueTrigger conversation;
    public float hopHeight = .08f;
    public float hopDuration = .34f;
    public float hopInterval = 3.6f;
    Transform[] visuals, friends;
    SpriteRenderer[] sprites;
    Vector3[] rests;
    Transform player;
    DialoguePlayer dialogue;
    float clock;
    bool talking;
    public bool IsTalking { get { return talking; } }

    void Start()
    {
        visuals = new Transform[2]; sprites = new SpriteRenderer[2]; rests = new Vector3[2];
        friends = new[] { friendB, friendC };
        for (int i = 0; i < 2; i++)
        {
            sprites[i] = friends[i].GetComponentInChildren<SpriteRenderer>();
            visuals[i] = sprites[i].transform;
            rests[i] = visuals[i].localPosition;
        }
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;
        dialogue = DialoguePlayer.Ensure();
        dialogue.DialogueStarted += OnDialogueStarted;
    }

    void OnDialogueStarted(DialogueData data)
    {
        if (conversation != null && data == conversation.dialogue) talking = true;
    }

    void LateUpdate()
    {
        if (visuals == null || PauseMenu.IsPaused) return;
        clock += Time.deltaTime;

        for (int i = 0; i < 2; i++)
        {
            if (visuals[i] == null) continue;
            float y = 0;
            if (!talking)
            {
                float phase = Mathf.Repeat(clock - 1.4f - i * .55f, hopInterval);
                if (phase < hopDuration) y = Mathf.Sin(phase / hopDuration * Mathf.PI) * hopHeight;
            }
            float current = visuals[i].localPosition.y - rests[i].y;
            if (talking) y = Mathf.MoveTowards(current, 0, Time.deltaTime * .7f);
            visuals[i].localPosition = rests[i] + Vector3.up * y;
            Transform target = talking && player != null ? player : friends[1 - i];
            float dx = target.position.x - friends[i].position.x;
            if (Mathf.Abs(dx) > .08f) sprites[i].flipX = dx < 0;
        }
    }

    void OnDestroy()
    {
        if (dialogue != null) dialogue.DialogueStarted -= OnDialogueStarted;
    }
}
