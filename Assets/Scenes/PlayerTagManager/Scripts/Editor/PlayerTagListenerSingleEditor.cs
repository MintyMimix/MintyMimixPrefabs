#if UNITY_EDITOR
using UnityEditor;
using UdonSharpEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerTagListenerSingle))]
public class PlayerTagListenerSingleEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SerializedProperty playerTagManagerProp = serializedObject.FindProperty("playerTagManager");
        EditorGUILayout.PropertyField(playerTagManagerProp);
        PlayerTagManager playerTagManager = (PlayerTagManager)playerTagManagerProp.objectReferenceValue;
        if (playerTagManager == null) { EditorGUILayout.HelpBox("Assign the PlayerTagManager to this object to output a tag's state.", MessageType.Warning); }
        else
        {
            SerializedProperty tagSingleToMonitorProp = serializedObject.FindProperty("tagSingleToMonitor");
            SerializedProperty tagSingleNamesProp = new SerializedObject(playerTagManager).FindProperty("tagSingleNames");
            string[] tagSingleNames = new string[tagSingleNamesProp.arraySize];
            for (int i = 0; i < tagSingleNames.Length; i++) { tagSingleNames[i] = tagSingleNamesProp.GetArrayElementAtIndex(i).stringValue; }
            if (tagSingleNames.Length == 0) { EditorGUILayout.HelpBox("The PlayerTagManager currently has no tag names.", MessageType.Error); }
            tagSingleToMonitorProp.intValue = EditorGUILayout.Popup("Toggle Tag To Monitor", Mathf.Clamp(tagSingleToMonitorProp.intValue, 0, tagSingleNames.Length - 1), tagSingleNames);

            SerializedProperty tagOutputTypeProp = serializedObject.FindProperty("outputType");
            EditorGUILayout.PropertyField(tagOutputTypeProp);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("outputNameSeparator"));
            if (tagOutputTypeProp.enumValueIndex == (int)SingleOutputType.NameAndTag)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("outputTagSeparator"));
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("optionalOutputTextElement"), new GUIContent("Output Text Element (Optional)", "Assign a TMP_Text to display the output. Leave blank to skip."));
        }
        serializedObject.ApplyModifiedProperties();
    }
}
#endif