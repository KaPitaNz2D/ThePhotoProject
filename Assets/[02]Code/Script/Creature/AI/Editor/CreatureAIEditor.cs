using UnityEditor;
using UnityEngine;

/// <summary>
/// ปุ่มสั่งสัตว์บน Inspector ของ CreatureAI (ตอน Play) สำหรับอัดคลิป — ดู CreatureAI.Scripted* และ CreatureDirector (ปุ่มลัด F5-F9)
/// "เดินไปจุดกลาง Scene View" = เดินไปตรง Pivot ของกล้อง Scene View (กด F ที่วัตถุ หรือเลื่อนกล้องไปเล็งจุดนั้นก่อน)
/// </summary>
[CustomEditor(typeof(CreatureAI))]
public class CreatureAIEditor : Editor
{
    private bool eatOnArrive = true;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (!Application.isPlaying) return;

        CreatureAI ai = (CreatureAI)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("สั่งด้วยมือ (อัดคลิป)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("State", $"{ai.CurrentState}{(ai.IsScripted ? "  (ถูกสั่งอยู่ — AI ไม่รับรู้ผู้เล่น)" : "")}");

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("กินหญ้า")) ai.ScriptedEat();
            if (GUILayout.Button("ยืนนิ่ง")) ai.ScriptedIdle();
        }

        eatOnArrive = EditorGUILayout.Toggle("ถึงแล้วกินหญ้าต่อ", eatOnArrive);
        if (GUILayout.Button("เดินไปจุดกลาง Scene View"))
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null && !ai.ScriptedWalkTo(view.pivot, eatOnArrive))
            {
                Debug.LogWarning("[CreatureAI] จุดกลาง Scene View ไม่มี NavMesh ใกล้ๆ — เล็งกล้อง Scene View ไปที่พื้นก่อน", ai);
            }
        }

        using (new EditorGUI.DisabledScope(!ai.IsScripted))
        {
            if (GUILayout.Button("คืนให้ AI คุมเอง")) ai.ReleaseScripted();
        }

        Repaint();
    }
}
