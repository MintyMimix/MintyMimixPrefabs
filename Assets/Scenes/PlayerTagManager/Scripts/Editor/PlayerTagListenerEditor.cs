using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerTagListener))]
public class PlayerTagListenerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SerializedProperty playerTagManagerProp = serializedObject.FindProperty("playerTagManager");
        EditorGUILayout.PropertyField(playerTagManagerProp);
        PlayerTagManager playerTagManager = (PlayerTagManager)playerTagManagerProp.objectReferenceValue;
        if (playerTagManager == null) { EditorGUILayout.HelpBox("Assign the PlayerTagManager to this object to output a tag's state.", MessageType.Warning); }
        else
        {
            SerializedProperty listenerTypeProp = serializedObject.FindProperty("listenerType");
            EditorGUILayout.PropertyField(listenerTypeProp);
            if (listenerTypeProp.enumValueIndex == (int)ListenerType.Single)
            {
                SerializedProperty tagSingleToMonitorProp = serializedObject.FindProperty("tagSingleToMonitor");
                SerializedProperty tagSingleNamesProp = new SerializedObject(playerTagManager).FindProperty("tagSingleNames");
                string[] tagSingleNames = new string[tagSingleNamesProp.arraySize];
                for (int i = 0; i < tagSingleNames.Length; i++) { tagSingleNames[i] = tagSingleNamesProp.GetArrayElementAtIndex(i).stringValue; }
                if (tagSingleNames.Length == 0) { EditorGUILayout.HelpBox("The PlayerTagManager currently has no tag names.", MessageType.Error); }
                tagSingleToMonitorProp.intValue = EditorGUILayout.Popup("Toggle Tag To Monitor", Mathf.Clamp(tagSingleToMonitorProp.intValue, 0, tagSingleNames.Length - 1), tagSingleNames);

                SerializedProperty tagOutputTypeProp = serializedObject.FindProperty("outputSingleType");
                EditorGUILayout.PropertyField(tagOutputTypeProp);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("outputNameSeparator"));
                if (tagOutputTypeProp.enumValueIndex == (int)SingleOutputType.NameAndTag)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("outputTagSeparator"));
                }
            }
            else if (listenerTypeProp.enumValueIndex == (int)ListenerType.Group)
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
                if (tagGroups.Length == 0) { EditorGUILayout.HelpBox("The PlayerTagManager currently has no group tags.", MessageType.Error); }
                else
                {
                    int selectedGroupValue = Mathf.Clamp(tagGroupToMonitorProp.intValue, 0, tagGroupNames.Length - 1);
                    tagGroupToMonitorProp.intValue = EditorGUILayout.Popup("Tag Group To Monitor", selectedGroupValue, tagGroupNames);

                    SerializedProperty tagOutputTypeProp = serializedObject.FindProperty("outputGroupType");
                    EditorGUILayout.PropertyField(tagOutputTypeProp);
                    if (tagOutputTypeProp.enumValueIndex == (int)GroupOutputType.OutputNamesInState && selectedGroupValue < tagGroups.Length)
                    {
                        SerializedProperty stateToListenForProp = serializedObject.FindProperty("stateToListenFor");
                        int stateSize = tagGroups[selectedGroupValue].size();
                        string[] tagGroupStates = new string[stateSize];
                        for (int j = 0; j < stateSize; j++)
                        {
                            tagGroupStates[j] = tagGroups[selectedGroupValue].GetStateName(j);
                        }
                        if (tagGroupStates.Length == 0) { EditorGUILayout.HelpBox("The currently selected group tag has no defined states.", MessageType.Error); }
                        stateToListenForProp.intValue = EditorGUILayout.Popup("State to Listen For", Mathf.Clamp(stateToListenForProp.intValue, 0, stateSize - 1), tagGroupStates);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("outputStateCount"));
                    }

                    EditorGUILayout.PropertyField(serializedObject.FindProperty("outputNameSeparator"));
                    if (tagOutputTypeProp.enumValueIndex == (int)GroupOutputType.OutputNamesAndTagsInActiveStates || tagOutputTypeProp.enumValueIndex == (int)GroupOutputType.OutputNamesAndTagsInAllStates)
                    {
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("outputTagSeparator"));
                    }
                }
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("optionalOutputTextElement"), new GUIContent("Output Text Element (Optional)", "Assign a TMP_Text to display the output. Leave blank to skip."));
        }

        serializedObject.ApplyModifiedProperties();
    }
}
