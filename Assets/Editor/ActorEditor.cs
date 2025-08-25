#if UNITY_EDITOR
using UnityEditor;
using Sirenix.OdinInspector.Editor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(Actor), true)]
public class ActorEditor : OdinEditor
{
    private bool showLookableEntities = false;
    private bool showCollectibleEntities = false;
    private bool showInteractableEntities = false;
    private bool showMovableEntities = false;
    private bool showMovableAreas = false;
    
    public override void OnInspectorGUI()
    {
        // 기본 Odin Inspector 그리기
        base.OnInspectorGUI();

        Actor actor = (Actor)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sensor Information", EditorStyles.boldLabel);

        if (actor != null && actor.sensor != null)
        {
            // Update Lookable Entities 버튼
            if (GUILayout.Button("Update Lookable Entities"))
            {
                actor.sensor.UpdateLookableEntities();
                Debug.Log($"[{actor.Name}] Lookable entities updated.");
            }
            
            EditorGUILayout.Space();
            
            // Lookable Entities Dropdown
            showLookableEntities = EditorGUILayout.Foldout(showLookableEntities, "Lookable Entities");
            if (showLookableEntities)
            {
                var lookableEntities = actor.sensor.GetLookableEntities();
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"Count: {lookableEntities.Count}");
                foreach (var kvp in lookableEntities)
                {
                    EditorGUILayout.LabelField($"• {kvp.Key}: {kvp.Value?.Name ?? "null"}");
                }
                EditorGUI.indentLevel--;
            }
            
            // Collectible Entities Dropdown
            showCollectibleEntities = EditorGUILayout.Foldout(showCollectibleEntities, "Collectible Entities");
            if (showCollectibleEntities)
            {
                var collectibleEntities = actor.sensor.GetCollectibleEntities();
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"Count: {collectibleEntities.Count}");
                foreach (var kvp in collectibleEntities)
                {
                    EditorGUILayout.LabelField($"• {kvp.Key}: {kvp.Value?.Name ?? "null"}");
                }
                EditorGUI.indentLevel--;
            }
            
            // Interactable Entities Dropdown
            showInteractableEntities = EditorGUILayout.Foldout(showInteractableEntities, "Interactable Entities");
            if (showInteractableEntities)
            {
                var interactableEntities = actor.sensor.GetInteractableEntities();
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"Actors: {interactableEntities.actors.Count}");
                foreach (var kvp in interactableEntities.actors)
                {
                    EditorGUILayout.LabelField($"  • {kvp.Key}: {kvp.Value?.Name ?? "null"}");
                }
                EditorGUILayout.LabelField($"Items: {interactableEntities.items.Count}");
                foreach (var kvp in interactableEntities.items)
                {
                    EditorGUILayout.LabelField($"  • {kvp.Key}: {kvp.Value?.Name ?? "null"}");
                }
                EditorGUILayout.LabelField($"Buildings: {interactableEntities.buildings.Count}");
                foreach (var kvp in interactableEntities.buildings)
                {
                    EditorGUILayout.LabelField($"  • {kvp.Key}: {kvp.Value?.Name ?? "null"}");
                }
                EditorGUILayout.LabelField($"Props: {interactableEntities.props.Count}");
                foreach (var kvp in interactableEntities.props)
                {
                    EditorGUILayout.LabelField($"  • {kvp.Key}: {kvp.Value?.Name ?? "null"}");
                }
                EditorGUI.indentLevel--;
            }
            
            // Movable Entities Dropdown
            showMovableEntities = EditorGUILayout.Foldout(showMovableEntities, "Movable Entities");
            if (showMovableEntities)
            {
                var movableEntities = actor.sensor.GetMovableEntities();
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"Count: {movableEntities.Count}");
                foreach (var entityKey in movableEntities)
                {
                    EditorGUILayout.LabelField($"• {entityKey}");
                }
                EditorGUI.indentLevel--;
            }
            
            // Movable Areas Dropdown
            showMovableAreas = EditorGUILayout.Foldout(showMovableAreas, "Movable Areas");
            if (showMovableAreas)
            {
                var movableAreas = actor.sensor.GetMovableAreas();
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"Count: {movableAreas.Count}");
                foreach (var areaName in movableAreas)
                {
                    EditorGUILayout.LabelField($"• {areaName}");
                }
                EditorGUI.indentLevel--;
            }
        }
        else if (actor != null)
        {
            EditorGUILayout.LabelField(
                "Sensor not found. Please ensure Actor has a Sensor component."
            );
        }
    }
}
#endif
