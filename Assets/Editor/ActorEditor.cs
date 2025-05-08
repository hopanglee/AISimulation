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

        // toMovable에 저장된 각 위치에 대해 버튼 생성
        if (actor != null)
        {
            // actor의 toMovable 필드가 null이 아니고 요소가 있을 때
            var movableField = typeof(Actor).GetField(
                "toMovable",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
            var toMovable = movableField.GetValue(actor) as SerializableDictionary<string, Vector3>;
            if (toMovable != null && toMovable.Count > 0)
            {
                foreach (var kvp in toMovable)
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
                    "No movable positions. Please run 'Update Movable Positions'."
                );
            }
        }
    }
}
#endif
