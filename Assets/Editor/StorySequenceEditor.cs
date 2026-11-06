using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StorySequence))]
public sealed class StorySequenceEditor : Editor
{
    bool layoutOpen, resourcesOpen;
    readonly System.Collections.Generic.HashSet<int> advanced = new System.Collections.Generic.HashSet<int>();

    void OnEnable()
    {
        var cuts=serializedObject.FindProperty("cuts");
        if(cuts!=null&&cuts.arraySize>0)cuts.GetArrayElementAtIndex(0).isExpanded=true;
    }

    void Field(SerializedProperty owner,string name,string label,string tip="")
    {
        var property=owner==null?serializedObject.FindProperty(name):owner.FindPropertyRelative(name);
        EditorGUILayout.PropertyField(property,new GUIContent(label,tip),true);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("스토리 컷씬",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("각 컷의 시간을 초 단위로 조정합니다. 최소 표시 시간은 그림이 나타나기 시작한 순간부터 퇴장을 시작하기 전까지입니다. 대사가 더 길면 읽기가 끝날 때까지 기다립니다. 퇴장 시간과 다음 컷까지 여백은 별도로 더해집니다.",MessageType.Info);
        Field(null,"allowManualAdvance","Space / Enter로 넘기기","키보드 넘기기를 허용합니다. 마우스로 ㅇ 그려 다음 컷 넘기기는 항상 사용할 수 있습니다.");

        var cuts=serializedObject.FindProperty("cuts");
        for(int i=0;i<cuts.arraySize;i++)
        {
            var cut=cuts.GetArrayElementAtIndex(i);
            string narration=cut.FindPropertyRelative("narration").stringValue.Replace('\n',' ');
            if(narration.Length>24)narration=narration.Substring(0,24)+"…";
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            cut.isExpanded=EditorGUILayout.Foldout(cut.isExpanded,(i+1)+". "+cut.FindPropertyRelative("id").stringValue+"  "+narration,true);
            if(cut.isExpanded)
            {
                Field(cut,"illustration","그림");
                Field(cut,"narration","내레이션");
                Field(cut,"minimumDuration","장면 최소 표시(초)","예: 8이면 그림의 등장 시작부터 최소 8초를 보여준 뒤 퇴장합니다. 0이면 글 출력과 읽기 여유 시간만 사용합니다.");
                Field(cut,"narrationDuration","대사 출력 시간(초)","타이핑에 걸리는 시간입니다. 0이면 아래의 글자/초 속도를 사용합니다. 첫 문장 뒤 쉼은 별도로 더해집니다.");
                Field(cut,"imageLead","대사 시작 지연(초)","그림의 등장 시작 후 몇 초 뒤에 대사를 쓰기 시작할지 정합니다.");
                Field(cut,"fadeIn","등장 / 번짐(초)");
                Field(cut,"fadeOut","퇴장 / 번짐(초)");
                Field(cut,"gap","다음 컷까지 여백(초)");
                if(!string.IsNullOrEmpty(cut.FindPropertyRelative("quote").stringValue))
                {
                    Field(cut,"quote","눈의 대사");
                    Field(cut,"quoteLead","눈 대사 전 쉼(초)");
                    Field(cut,"quoteDuration","눈 대사 왜곡(초)");
                    Field(cut,"quoteSettle","왜곡 후 읽기 여유(초)");
                }
                bool open=advanced.Contains(i);
                bool next=EditorGUILayout.Foldout(open,"세부 조정",true);
                if(next)advanced.Add(i);else advanced.Remove(i);
                if(next)
                {
                    Field(cut,"id","컷 ID");
                    Field(cut,"hold","글 완료 후 최소 여유(초)","장면 최소 표시 시간과 이 읽기 여유 시간을 모두 만족한 뒤 퇴장합니다.");
                    Field(cut,"textSpeed","글자 / 초","대사 출력 시간을 0으로 설정했을 때 사용합니다.");
                    if(i==0)Field(cut,"sentencePause","첫 문장 뒤 쉼(초)","있었답니다. 다음 이름 소개 전의 쉼입니다.");
                    Field(cut,"push","느린 확대량");
                    Field(cut,"drift","느린 이동량");
                }
            }
            EditorGUILayout.EndVertical();
        }

        layoutOpen=EditorGUILayout.Foldout(layoutOpen,"그림 · 글 배치 (1920 × 1080 기준)",true);
        if(layoutOpen)
        {
            Field(null,"contentOffset","묶음 전체 위치","그림과 두 대사를 함께 옮깁니다. X는 좌우, Y는 위아래입니다.");
            Field(null,"picturePosition","그림 위치");Field(null,"pictureSize","그림 영역 크기");
            Field(null,"narrationPosition","내레이션 위치");Field(null,"narrationSize","내레이션 영역");
            Field(null,"narrationFontSize","내레이션 글자 크기");
            Field(null,"quotePosition","눈 대사 위치");Field(null,"quoteSize","눈 대사 영역");
        }
        resourcesOpen=EditorGUILayout.Foldout(resourcesOpen,"글꼴 · 소리 · 이동 씬",true);
        if(resourcesOpen)
        {
            Field(null,"font","글꼴");Field(null,"textSound","글 소리");Field(null,"glitchSound","왜곡 소리");
            Field(null,"gameScene","게임 씬");Field(null,"titleScene","타이틀 씬");
            Field(null,"charactersPerSecond","기본 글자 / 초");
        }
        serializedObject.ApplyModifiedProperties();
    }
}
