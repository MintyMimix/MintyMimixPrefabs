
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerTagManager))]
public class PlayerTagManagerEditor : Editor
{
    private bool showSingleTags = true;
    public override void OnInspectorGUI()
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty("tagGroups"),
            new GUIContent("Group-Type Tags", "Group-type Tags are tags with multiple mutually exclusive states." +
            "\nFor example, the 'Allow Headpats' group tag could have the states 'Headpats OK', 'Ask First', and 'No Headpats'." +
            "\n\nMake as many PlayerTagGroup objects as you'd like group tags and configure their settings." +
            "\n\nClick and drag all of those PlayerTagGroup objects into this array.")
            );

        showSingleTags = EditorGUILayout.Foldout(showSingleTags, 
            new GUIContent("Single-Type Tags", "Single-type Tags are tags that have two states: ON and OFF." +
            "\nFor example, if you wanted to have a 'Livestreaming Now!' label, you'd want to use a Single-type Tag." +
            "\n\nFor every tag you want to have available, use the arrays below to set the name, tag color, and whether or not the tag should float above a player's head when ON for each." +
            "\n\nMake sure that the arrays all match in length, or else you will encounter errors!"),
            true,
            EditorStyles.foldoutHeader
        );

        if (showSingleTags)
        {
            SerializedProperty tagSingleNamesProp = serializedObject.FindProperty("tagSingleNames");
            SerializedProperty tagSingleColorsProp = serializedObject.FindProperty("tagSingleColors");
            SerializedProperty tagSingleDisplayAboveHeadProp = serializedObject.FindProperty("tagSingleDisplayAboveHead");

            if (tagSingleNamesProp.arraySize != tagSingleColorsProp.arraySize
                || tagSingleNamesProp.arraySize != tagSingleDisplayAboveHeadProp.arraySize
                || tagSingleColorsProp.arraySize != tagSingleDisplayAboveHeadProp.arraySize)
            {
                EditorGUILayout.HelpBox("All tag options must have a matching number of entries.", MessageType.Error);
            }

            EditorGUILayout.PropertyField(tagSingleNamesProp, new GUIContent("Single Tag Name", ""));
            EditorGUILayout.PropertyField(tagSingleColorsProp, new GUIContent("Single Tag Color", ""));
            EditorGUILayout.PropertyField(tagSingleDisplayAboveHeadProp, new GUIContent("Display Single Tag Above Head", "Should this tag visibly appear above a player's head when toggled on?"));
        }

        serializedObject.ApplyModifiedProperties();
    }
}
