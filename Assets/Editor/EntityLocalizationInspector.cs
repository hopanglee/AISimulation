using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector.Editor;

[CustomEditor(typeof(Entity), true)]
public class EntityLocalizationInspector : OdinEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Localization Tools", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        if (GUILayout.Button("Copy EN → KR (Name, Preposition, Status)"))
        {
            ApplyToTargets(CopyEnToKr);
        }

        if (GUILayout.Button("Clear KR Fields"))
        {
            ApplyToTargets(ClearKr);
        }

        EditorGUILayout.EndVertical();
    }

    private void ApplyToTargets(System.Action<SerializedObject> action)
    {
        foreach (var t in targets)
        {
            var so = new SerializedObject(t);
            action.Invoke(so);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(t);
        }
    }

    private void CopyEnToKr(SerializedObject so)
    {
        var nameEn = so.FindProperty("_name");
        var nameKr = so.FindProperty("_nameKr");
        var prepEn = so.FindProperty("_preposition");
        var prepKr = so.FindProperty("_prepositionKr");
        var descEn = so.FindProperty("_currentStatusDescription");
        var descKr = so.FindProperty("_currentStatusDescriptionKr");

        if (nameEn != null && nameKr != null) nameKr.stringValue = nameEn.stringValue;
        if (prepEn != null && prepKr != null) prepKr.stringValue = prepEn.stringValue;
        if (descEn != null && descKr != null) descKr.stringValue = descEn.stringValue;
    }

    private void ClearKr(SerializedObject so)
    {
        var nameKr = so.FindProperty("_nameKr");
        var prepKr = so.FindProperty("_prepositionKr");
        var descKr = so.FindProperty("_currentStatusDescriptionKr");

        if (nameKr != null) nameKr.stringValue = string.Empty;
        if (prepKr != null) prepKr.stringValue = string.Empty;
        if (descKr != null) descKr.stringValue = string.Empty;
    }
}


