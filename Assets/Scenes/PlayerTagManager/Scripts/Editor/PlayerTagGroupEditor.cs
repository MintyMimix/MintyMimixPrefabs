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
        EditorGUILayout.PropertyField(tagGroupNameProp);
        if (string.IsNullOrEmpty(tagGroupNameProp.stringValue.Trim())) { EditorGUILayout.HelpBox("This Tag Group has no name.", MessageType.Warning); }
        SerializedProperty tagStateNamesProp = serializedObject.FindProperty("tagStateNames");
        SerializedProperty tagColorsProp = serializedObject.FindProperty("tagColors");
        SerializedProperty tagDisplayAboveHeadProp = serializedObject.FindProperty("tagDisplayAboveHead");

        if (tagStateNamesProp.arraySize != tagColorsProp.arraySize 
            || tagStateNamesProp.arraySize != tagDisplayAboveHeadProp.arraySize
            || tagColorsProp.arraySize != tagDisplayAboveHeadProp.arraySize)
        {
            EditorGUILayout.HelpBox("All tag options must have a matching number of states.", MessageType.Error);
        }

        EditorGUILayout.PropertyField(tagStateNamesProp);
        EditorGUILayout.PropertyField(tagColorsProp);
        EditorGUILayout.PropertyField(tagDisplayAboveHeadProp);

        serializedObject.ApplyModifiedProperties();
    }

}
