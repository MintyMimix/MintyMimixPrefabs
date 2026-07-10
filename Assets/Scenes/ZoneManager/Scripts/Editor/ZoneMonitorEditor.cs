using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ZoneMonitor))]
public class ZoneMonitorEditor : Editor
{
    SerializedProperty listMode;
    SerializedProperty outputType;
    SerializedProperty zoneTriggers;
    SerializedProperty zoneTrigger;
    SerializedProperty templatePanel;
    SerializedProperty zoneManager;

    private void OnEnable()
    {
        listMode = serializedObject.FindProperty("listMode");
        outputType = serializedObject.FindProperty("outputType");
        zoneTriggers = serializedObject.FindProperty("zoneTriggers");
        zoneTrigger = serializedObject.FindProperty("zoneTrigger");
        templatePanel = serializedObject.FindProperty("templatePanel");
        zoneManager = serializedObject.FindProperty("zoneManager");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(listMode);
        EditorGUILayout.PropertyField(outputType);

        if ((ZoneMonitorListMode)listMode.enumValueIndex == ZoneMonitorListMode.MultipleZones)
        {
            EditorGUILayout.PropertyField(zoneTriggers, true);
        }
        else if ((ZoneMonitorListMode)listMode.enumValueIndex == ZoneMonitorListMode.SingleZone)
        {
            EditorGUILayout.PropertyField(zoneTrigger);
        }
        EditorGUILayout.PropertyField(templatePanel);
        EditorGUILayout.PropertyField(zoneManager);
        serializedObject.ApplyModifiedProperties();
    }
}
