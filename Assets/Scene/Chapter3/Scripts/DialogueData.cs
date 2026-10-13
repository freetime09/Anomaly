using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 대화 한 묶음(여러 줄)을 담는 데이터 에셋.
/// Project 창 우클릭 → Create → Anomaly → Chapter3 → Dialogue Data 로 만들고 Inspector 에서 대사를 편집한다.
/// 코드 수정 없이 대사·화자·표시 시간을 바꿀 수 있게 하려고 ScriptableObject 로 분리했다.
/// </summary>
[CreateAssetMenu(menuName = "Anomaly/Chapter3/Dialogue Data", fileName = "NewDialogue")]
public class DialogueData : ScriptableObject
{
    [System.Serializable]
    public class Line
    {
        [Tooltip("말하는 캐릭터의 ID. 씬의 DialogueSpeaker 컴포넌트에 적힌 Speaker Id 와 같아야 그 캐릭터 머리 위에 말풍선이 뜬다 (예: Mecro, Villager_A)")]
        public string speakerId = "Mecro";

        [Tooltip("말풍선에 표시할 대사")]
        [TextArea(2, 4)]
        public string text = "";

        [Tooltip("다 출력된 뒤 말풍선을 유지하는 시간(초). 0이면 글자 수에 맞춰 자동 계산")]
        [Min(0f)]
        public float holdTime = 0f;
    }

    [Tooltip("위에서부터 순서대로 재생되는 대사 목록")]
    public List<Line> lines = new List<Line>();
}
