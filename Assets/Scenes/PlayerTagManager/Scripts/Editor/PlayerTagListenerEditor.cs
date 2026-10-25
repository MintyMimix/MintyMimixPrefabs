using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerTagListener))]
public class PlayerTagListenerEditor : Editor
{

    private bool showStates = true;

    public override void OnInspectorGUI()
    {
        SerializedProperty playerTagManagerProp = serializedObject.FindProperty("playerTagManager");
        EditorGUILayout.PropertyField(playerTagManagerProp);
        PlayerTagManager playerTagManager = (PlayerTagManager)playerTagManagerProp.objectReferenceValue;
        if (playerTagManager == null) { EditorGUILayout.HelpBox("Assign the PlayerTagManager to this object to output a tag's current state.", MessageType.Warning); }
        else
        {
            SerializedProperty tagGroupToMonitorProp = serializedObject.FindProperty("tagGroupToMonitor");
            SerializedProperty tagGroupsProp = new SerializedObject(playerTagManager).FindProperty("tagGroups");
            PlayerTagGroup[] tagGroups = new PlayerTagGroup[tagGroupsProp.arraySize];
            string[] tagGroupNames = new string[tagGroupsProp.arraySize];
            for (int i = 0; i < tagGroups.Length; i++)
            {
                tagGroups[i] = (PlayerTagGroup)tagGroupsProp.GetArrayElementAtIndex(i).objectReferenceValue;
                tagGroupNames[i] = tagGroups[i].tagGroupName;
            }
            if (tagGroups.Length == 0) { EditorGUILayout.HelpBox("The PlayerTagManager currently has no tags.", MessageType.Error); }
            else
            {
                int selectedGroupValue = Mathf.Clamp(tagGroupToMonitorProp.intValue, 0, tagGroupNames.Length - 1);
                tagGroupToMonitorProp.intValue = EditorGUILayout.Popup("Tag Group To Monitor", selectedGroupValue, tagGroupNames);

                SerializedProperty tagOutputTypeProp = serializedObject.FindProperty("outputGroupType");
                EditorGUILayout.PropertyField(tagOutputTypeProp);
                if (tagOutputTypeProp.enumValueIndex == (int)GroupOutputType.OutputNamesInState && selectedGroupValue < tagGroups.Length)
                {
                    showStates = EditorGUILayout.Foldout(showStates, new GUIContent("States to Monitor"), true, EditorStyles.foldoutHeader);
                    if (showStates)
                    {
                        int stateSize = tagGroups[selectedGroupValue].size();
                        string[] tagGroupStates = new string[stateSize];
                        for (int j = 0; j < stateSize; j++)
                        {
                            tagGroupStates[j] = tagGroups[selectedGroupValue].GetStateName(j);
                        }
                        if (tagGroupStates.Length == 0) { EditorGUILayout.HelpBox("The currently selected group tag has no defined states.", MessageType.Error); }
                        else
                        {
                            SerializedProperty statesToListenForProp = serializedObject.FindProperty("statesToListenFor");
                            int indextoRemoveOnClick = -1;
                            for (int j = 0; j < statesToListenForProp.arraySize; j++)
                            {
                                SerializedProperty stateProp = statesToListenForProp.GetArrayElementAtIndex(j);
                                EditorGUILayout.BeginHorizontal();
                                stateProp.intValue = EditorGUILayout.Popup($"State {j}", Mathf.Clamp(stateProp.intValue, 0, stateSize - 1), tagGroupStates);
                                if (GUILayout.Button("-", GUILayout.Width(24))) { indextoRemoveOnClick = j; }
                                EditorGUILayout.EndHorizontal();
                            }
                            if (indextoRemoveOnClick >= 0) { statesToListenForProp.DeleteArrayElementAtIndex(indextoRemoveOnClick); }
                            if (GUILayout.Button("+ Add State"))  { statesToListenForProp.arraySize++; }
                        }
                    }

                    EditorGUILayout.PropertyField(serializedObject.FindProperty("outputStateCount"));
                }

                EditorGUILayout.PropertyField(serializedObject.FindProperty("outputNameSeparator"));
                if (tagOutputTypeProp.enumValueIndex == (int)GroupOutputType.OutputNamesAndTagsInActiveStates || tagOutputTypeProp.enumValueIndex == (int)GroupOutputType.OutputNamesAndTagsInAllStates)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("outputTagSeparator"));
                }
            }
            

            EditorGUILayout.PropertyField(serializedObject.FindProperty("optionalOutputTextElement"), new GUIContent("Output Text Element (Optional)", "Assign a TMP_Text to display the output. Leave blank to skip."));
        }

        serializedObject.ApplyModifiedProperties();
    }
}
