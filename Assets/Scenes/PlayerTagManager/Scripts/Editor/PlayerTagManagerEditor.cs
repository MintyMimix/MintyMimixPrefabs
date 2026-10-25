
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerTagManager))]
public class PlayerTagManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty("tagGroups"),
            new GUIContent("Tags", "Tags can have multiple mutually exclusive states." +
            "\nFor example, the 'Allow Headpats' group tag could have the states 'Headpats OK', 'Ask First', and 'No Headpats'." +
            "\n\nMake as many PlayerTagGroup objects as you'd like group tags and configure their settings." +
            "\n\nClick and drag all of those PlayerTagGroup objects into this array.")
            );
        EditorGUILayout.PropertyField(serializedObject.FindProperty("tagClusters"),
            new GUIContent("Tag Clusters", "Clusters allow you to group together tags in the same line for display purposes. Completely optional.")
            );

        EditorGUILayout.PropertyField(serializedObject.FindProperty("prefIndicatorDisplayOwnTags"),
			new GUIContent("'Hide Own Tags' Indicator", "Objects that should be toggled ON/OFF when the user has the 'Hide Own Tags' preference option enabled."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("prefIndicatorDisplayTagsPlayer"),
			new GUIContent("'Hide Tags from Others' Indicator", "Objects that should be toggled ON/OFF when the user has the 'Hide Tags from Others' preference option enabled."));
		EditorGUILayout.PropertyField(serializedObject.FindProperty("prefIndicatorDisplayTagsGlobal"),
			new GUIContent("'Hide All Tags' Indicator", "Objects that should be toggled ON/OFF when the user has the 'Hide All Tags' preference option enabled."));


		serializedObject.ApplyModifiedProperties();
    }
}
