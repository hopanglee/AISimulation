#if UNITY_EDITOR
using UnityEditor;
using Sirenix.OdinInspector.Editor;
using UnityEngine;

[CustomEditor(typeof(Actor), true)]
public class ActorEditor : OdinEditor
{
    public override void OnInspectorGUI()
    {
        // 기본 Odin Inspector 그리기
        base.OnInspectorGUI();

        Actor actor = (Actor)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Movable Positions", EditorStyles.boldLabel);

        // actor의 sensor가 null이 아니고 movable positions가 있을 때
        if (actor != null && actor.sensor != null)
        {
            var movablePositions = actor.sensor.GetMovablePositions();
            if (movablePositions != null && movablePositions.Count > 0)
            {
                foreach (var kvp in movablePositions)
                {
                    if (GUILayout.Button($"Move To: {kvp.Key}"))
                    {
                        actor.Move(kvp.Key);
                    }
                }
            }
            else
            {
                EditorGUILayout.LabelField(
                    "No movable positions. Please run 'Update Lookable Entities'."
                );
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
