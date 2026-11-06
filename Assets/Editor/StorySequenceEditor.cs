using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(StorySequence))]
public sealed class StorySequenceEditor : Editor {
 bool advanced;
 public override void OnInspectorGUI(){
 serializedObject.Update();
 EditorGUILayout.HelpBox("장면을 펼쳐 그림, 대사, 표시 시간을 조절하세요. 표시 시간은 등장 시작부터 퇴장 전까지이며, 대사가 끝나기 전에는 넘어가지 않습니다.",MessageType.Info);
 var cuts=serializedObject.FindProperty("cuts");
 for(int i=0;i<cuts.arraySize;i++){
 var c=cuts.GetArrayElementAtIndex(i);var n=c.FindPropertyRelative("narration").stringValue.Replace('\n',' ');
 c.isExpanded=EditorGUILayout.Foldout(c.isExpanded,(i+1)+". "+(n.Length>26?n.Substring(0,26)+"…":n),true);
 if(!c.isExpanded)continue;
 EditorGUI.indentLevel++;
 EditorGUILayout.PropertyField(c.FindPropertyRelative("illustration"),new GUIContent("그림"));
 EditorGUILayout.PropertyField(c.FindPropertyRelative("narration"),new GUIContent("대사"));
 EditorGUILayout.PropertyField(c.FindPropertyRelative("minimumDuration"),new GUIContent("장면 표시 시간(초)"));
 var q=c.FindPropertyRelative("quote");if(!string.IsNullOrEmpty(q.stringValue))EditorGUILayout.PropertyField(q,new GUIContent("눈의 대사"));
 EditorGUI.indentLevel--;
 }
 advanced=EditorGUILayout.Foldout(advanced,"고급 설정 (필요할 때만)",true);
 if(advanced)DrawPropertiesExcluding(serializedObject,"m_Script","cuts");
 serializedObject.ApplyModifiedProperties();
 }
}