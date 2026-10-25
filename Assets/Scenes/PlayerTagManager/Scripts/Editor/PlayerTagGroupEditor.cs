using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerTagGroup))]
public class PlayerTagGroupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SerializedProperty tagGroupNameProp = serializedObject.FindProperty("tagGroupName");
        EditorGUILayout.PropertyField(tagGroupNameProp, new GUIContent("Tag Name"));
        if (string.IsNullOrEmpty(tagGroupNameProp.stringValue.Trim())) { EditorGUILayout.HelpBox("This Tag Group has no name.", MessageType.Warning); }
        SerializedProperty tagPersistProp = serializedObject.FindProperty("tagPersist");
        EditorGUILayout.PropertyField(tagPersistProp, new GUIContent("Persist Tag Across Sessions", "Persist the tag across sessions/instances."));
        SerializedProperty scaleInPanelProp = serializedObject.FindProperty("scaleInPanel");
        EditorGUILayout.PropertyField(scaleInPanelProp);

        SerializedProperty tagStateNamesProp = serializedObject.FindProperty("tagStateNames");
        SerializedProperty tagDisplayTextProp = serializedObject.FindProperty("tagDisplayText");
        SerializedProperty tagUseImageProp = serializedObject.FindProperty("tagUseImage");
        SerializedProperty tagColorsProp = serializedObject.FindProperty("tagColors");
        SerializedProperty tagImagesProp = serializedObject.FindProperty("tagImages");
        SerializedProperty tagDisplayAboveHeadProp = serializedObject.FindProperty("tagDisplayAboveHead");

        if (tagStateNamesProp.arraySize != tagColorsProp.arraySize 
            || tagStateNamesProp.arraySize != tagDisplayAboveHeadProp.arraySize
            || tagColorsProp.arraySize != tagDisplayAboveHeadProp.arraySize)
        {
            EditorGUILayout.HelpBox("All tag options must have a matching number of states.", MessageType.Error);
        }


        EditorGUILayout.PropertyField(tagStateNamesProp);
        EditorGUILayout.PropertyField(tagDisplayTextProp, new GUIContent("Display Text", ""));
        EditorGUILayout.PropertyField(tagUseImageProp, new GUIContent("Use Images", ""));
        if (tagUseImageProp.boolValue)
        {
            EditorGUILayout.PropertyField(tagImagesProp);
        }
        else
        {
            EditorGUILayout.PropertyField(tagColorsProp);
        }
        EditorGUILayout.PropertyField(tagDisplayAboveHeadProp);

        serializedObject.ApplyModifiedProperties();
    }

}
