using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 시범 창에서 재생할 '짧은 동영상' 한 편의 데이터.
///  - 작은 무대(발판·벽·레버 등)와, 손가락이 그릴 모양 + 그 결과 메크로가 하는 행동을 순서대로 적는다.
///  - TutorialDemoStage 가 이 데이터대로 무대를 만들고, 실제 게임과 같은 규칙(속도·점프 높이 등)으로 움직여 보여 준다.
///  - Inspector 에서 문장·순서·발판 위치를 고치면 코드 수정 없이 시범이 바뀐다.
/// </summary>
[CreateAssetMenu(menuName = "Anomaly/Tutorial/Demo Clip", fileName = "Demo_")]
public class TutorialDemoClip : ScriptableObject
{
    /// <summary>손가락이 그리는 모양. 그리면 실제 게임과 같은 명령이 실행된다.</summary>
    public enum Shape
    {
        [Tooltip("아무것도 그리지 않고 기다리기만")] None,
        Right, Left, Up,
        [Tooltip("ㄴ (오른쪽 슬라이드)")] SlideRight,
        [Tooltip("ㄱ (오른쪽 공격)")] AttackRight,
        N, Nine, S, O,
        [Tooltip("⋉ (나뭇가지로 순간이동)")] X
    }

    public enum PropType
    {
        [Tooltip("밟을 수 있는 땅·벽·천장")] Block,
        [Tooltip("ㄱ 공격으로 부서지는 상자")] Breakable,
        [Tooltip("O 로 당기는 레버 (당기면 Door 가 열림)")] Lever,
        [Tooltip("레버를 당기면 사라지는 문")] Door
    }

    [Serializable]
    public class Prop
    {
        public PropType type = PropType.Block;
        [Tooltip("무대 좌표의 사각형 (x, y = 왼쪽 아래 / 단위: 칸). 땅 윗면은 보통 y = 0")]
        public Rect rect = new Rect(-6f, -2f, 12f, 2f);
        [Tooltip("Breakable 일 때 부서지기까지 맞아야 하는 횟수 (실제 게임의 Max Hp 와 맞춘다)")]
        [Min(1)] public int hitsToBreak = 1;
    }

    [Serializable]
    public class Step
    {
        [Tooltip("손가락이 그릴 모양")]
        public Shape shape = Shape.Right;
        [Tooltip("좌우 이동일 때 움직일 거리(칸). 그리는 선의 길이도 이에 맞춰 길어진다")]
        [Min(0.5f)] public float moveDistance = 3f;
        [Tooltip("이 단계를 시작하기 전에 기다리는 시간(초)")]
        [Min(0f)] public float waitBefore = 0.35f;
        [Tooltip("그리는 데 걸리는 시간(초)")]
        [Min(0.1f)] public float drawTime = 0.55f;
        [Tooltip("켜면 메크로 동작이 끝날 때까지 기다린 뒤 다음 단계로. 끄면 바로 다음 획을 그린다 (공중 이동·연속 긋기 시범)")]
        public bool waitUntilIdle = true;
    }

    [Header("안내 문장")]
    [Tooltip("창 제목 (예: 이동)")]
    public string title = "이동";
    [Tooltip("명령형 안내 한 줄 (예: 오른쪽으로 길게 그으세요)")]
    [TextArea(1, 3)] public string instruction = "오른쪽으로 그어서 이동하세요.";
    [Tooltip("작은 글씨 보충 설명. 비우면 숨김")]
    [TextArea(1, 3)] public string tip = "";

    [Header("무대")]
    [Tooltip("메크로가 서 있는 시작 위치 (x, 발 높이 y)")]
    public Vector2 puppetStart = new Vector2(-3f, 0f);
    [Tooltip("메크로가 나뭇가지를 들고 시작하는가")]
    public bool startWithBranch = true;
    [Tooltip("손에 들고 시작하지 않을 때, 처음부터 땅에 꽂혀 있는 나뭇가지의 밑동 위치 (x, 땅 높이 y)")]
    public Vector2 branchStart = new Vector2(2f, 0f);
    [Tooltip("화면 가운데가 비출 무대 좌표")]
    public Vector2 viewCenter = new Vector2(0f, 1.6f);
    [Tooltip("화면에 보이는 무대의 세로 높이(칸)")]
    [Min(2f)] public float viewHeight = 6f;
    [Tooltip("손가락이 그림을 그리는 자리 (화면 가운데 기준 오프셋, 칸)")]
    public Vector2 drawArea = new Vector2(1.6f, 1.6f);
    [Tooltip("무대의 발판·벽·레버 등")]
    public List<Prop> props = new List<Prop>();

    [Header("순서")]
    public List<Step> steps = new List<Step>();
    [Tooltip("마지막 단계가 끝난 뒤 처음부터 다시 재생하기까지 기다리는 시간(초)")]
    [Min(0f)] public float endPause = 1.2f;
}
