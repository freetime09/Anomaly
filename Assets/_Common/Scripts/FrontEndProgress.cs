using UnityEngine;

/// <summary>
/// 타이틀의 '게임 시작' 잠금용 진행 기록 (PlayerPrefs 저장).
/// 튜토리얼(끝 출구 ChapterTransitionTrigger.marksTutorialDone)과 스토리(StorySequence 마지막 선택지)를 모두 마쳐야 열린다.
/// </summary>
public static class FrontEndProgress
{
    const string TutorialKey = "Progress_TutorialDone", StoryKey = "Progress_StoryDone";

    public static bool TutorialDone
    {
        get { return PlayerPrefs.GetInt(TutorialKey, 0) == 1; }
        set { PlayerPrefs.SetInt(TutorialKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static bool StoryDone
    {
        get { return PlayerPrefs.GetInt(StoryKey, 0) == 1; }
        set { PlayerPrefs.SetInt(StoryKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static bool GameUnlocked { get { return TutorialDone && StoryDone; } }
}
