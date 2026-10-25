using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerTagButton))]
public class PlayerTagButtonEditor : Editor
{
    public override void OnInspectorGUI()
    {
        bool isUI = ((Component)target).transform is RectTransform;
        if (isUI)
        {
            EditorGUILayout.HelpBox("Because this script is on a RectTransform, you must assign this script to a UI event (such as the OnClick() event of a button) and set the event to UdonBehaviour.Interact().", MessageType.Info);
        }   

        SerializedProperty playerTagManagerProp = serializedObject.FindProperty("playerTagManager");
        EditorGUILayout.PropertyField(playerTagManagerProp);
        PlayerTagManager playerTagManager = (PlayerTagManager)playerTagManagerProp.objectReferenceValue;
        if (playerTagManager == null) { EditorGUILayout.HelpBox("Assign the PlayerTagManager to this object.", MessageType.Warning); }
        else
        {
            SerializedProperty tagButtonTypeProp = serializedObject.FindProperty("tagButtonType");
            EditorGUILayout.PropertyField(tagButtonTypeProp,
                new GUIContent("Button Type",
                    "• Next Value: This button will set a tag to the next state in its list. (For example, if a Group tag's states are 'Vanilla, Chocolate, Strawberry', and the current value is Vanilla, the value will become Chocolate, then Strawberry, then Vanilla again.)" +
                    "• Set Value: This button will set a tag to a specific state."
                    ));
            if (tagButtonTypeProp.enumValueIndex == (int)TagButtonType.NextValue || tagButtonTypeProp.enumValueIndex == (int)TagButtonType.SetValue)
            {
                SerializedProperty tagGroupToToggleProp = serializedObject.FindProperty("tagGroupToToggle");
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
                    int selectedGroupValue = Mathf.Clamp(tagGroupToToggleProp.intValue, 0, tagGroupNames.Length - 1);
                    tagGroupToToggleProp.intValue = EditorGUILayout.Popup("Tag To Toggle", selectedGroupValue, tagGroupNames);

                    if (tagButtonTypeProp.enumValueIndex == (int)TagButtonType.SetValue && selectedGroupValue < tagGroups.Length)
                    {
                        SerializedProperty setTagGroupValueProp = serializedObject.FindProperty("setTagGroupValue");
                        int stateSize = tagGroups[selectedGroupValue].size();
                        string[] tagGroupStates = new string[stateSize];
                        for (int j = 0; j < stateSize; j++)
                        {
                            tagGroupStates[j] = tagGroups[selectedGroupValue].GetStateName(j);
                        }
                        if (tagGroupStates.Length == 0) { EditorGUILayout.HelpBox("The currently selected tag has no defined states.", MessageType.Error); }
                        setTagGroupValueProp.intValue = EditorGUILayout.Popup("Set State", Mathf.Clamp(setTagGroupValueProp.intValue, 0, stateSize - 1), tagGroupStates);
                    }
                }
            }

            if (!isUI) 
            {
                EditorGUILayout.HelpBox("You can use $TAG for tag name and $STATE for assigned tag state in the interaction text.", MessageType.None);
                UdonSharpEditor.UdonSharpGUI.DrawInteractSettings((PlayerTagButton)target); 
            }

        }
        serializedObject.ApplyModifiedProperties();
    }
}
