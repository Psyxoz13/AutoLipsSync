using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LipsSyncTimelineAsset))]
public class LipsSyncTimelineAssetEditor : Editor
{
    private SerializedProperty _entriesField;
    private LipsSyncTimelineAsset _asset;

    private void OnEnable()
    {
        _entriesField = serializedObject.FindProperty(nameof(LipsSyncTimelineAsset.TimelineEntries));
        _asset = (LipsSyncTimelineAsset)target;
    }

    public override void OnInspectorGUI()
    {
        Span<LipsSyncTimelineEntry> entries = _asset.TimelineEntries;

        serializedObject.Update();

        EditorGUILayout.PropertyField(_entriesField, true);
        EditorGUILayout.Space();

        float totalSec = 0f;

        for (int i = 0; i < entries.Length; i++)
        {
            ref var entry = ref entries[i];

            totalSec += entry.Duration;
        }

        EditorGUILayout.LabelField($"Total Sec.: {totalSec:0.##}s");

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Edit Timeline", GUILayout.Width(200), GUILayout.Height(40)))
        {
            LipsSyncTimelineWindow.Open((LipsSyncTimelineAsset)target);        
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }
}
