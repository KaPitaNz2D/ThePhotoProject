using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine.UIElements;

/// <summary>
/// ปุ่มบน Scene view สำหรับเปิด/ปิดป้าย State ของสัตว์ทุกตัว (วาดโดย CreatureDebug)
///   - AI State: ป้ายชื่อ State เหนือหัวสัตว์ (เห็นทุกระยะ)
///   - Detail: ป้ายละเอียดแบบเดียวกับ Game view (เฉพาะตัวที่อยู่ใกล้กล้องใน labelMaxDistance)
/// ค่าจำไว้ใน EditorPrefs ข้ามการเปิดโปรเจกต์ ถ้า Overlay หายไป กด ` ใน Scene view แล้วเลือก Creature Debug
/// </summary>
[Overlay(typeof(SceneView), "Creature Debug", true)]
public class CreatureDebugSceneOverlay : ToolbarOverlay
{
    CreatureDebugSceneOverlay() : base(StateLabelToggle.Id, DetailLabelToggle.Id) { }
}

[InitializeOnLoad]
static class CreatureDebugScenePrefs
{
    public const string LabelsKey = "CreatureDebug.SceneLabels";
    public const string DetailKey = "CreatureDebug.SceneLabelDetail";

    static CreatureDebugScenePrefs()
    {
        CreatureDebug.SceneLabels = EditorPrefs.GetBool(LabelsKey, false);
        CreatureDebug.SceneLabelDetail = EditorPrefs.GetBool(DetailKey, false);
    }
}

[EditorToolbarElement(Id, typeof(SceneView))]
class StateLabelToggle : EditorToolbarToggle
{
    public const string Id = "CreatureDebug/StateLabels";

    public StateLabelToggle()
    {
        text = "AI State";
        tooltip = "โชว์ State ปัจจุบันเหนือหัวสัตว์ทุกตัวใน Scene view (ตอน Play และเปิด Gizmos)";
        SetValueWithoutNotify(CreatureDebug.SceneLabels);
        this.RegisterValueChangedCallback(evt =>
        {
            CreatureDebug.SceneLabels = evt.newValue;
            EditorPrefs.SetBool(CreatureDebugScenePrefs.LabelsKey, evt.newValue);
            SceneView.RepaintAll();
        });
    }
}

[EditorToolbarElement(Id, typeof(SceneView))]
class DetailLabelToggle : EditorToolbarToggle
{
    public const string Id = "CreatureDebug/DetailLabels";

    public DetailLabelToggle()
    {
        text = "Detail";
        tooltip = "ป้ายละเอียด: ระยะถึงผู้เล่น วงรับรู้ และตัวจับเวลาของ State (เฉพาะตัวที่อยู่ใกล้กล้อง) ต้องเปิด AI State ด้วย";
        SetValueWithoutNotify(CreatureDebug.SceneLabelDetail);
        this.RegisterValueChangedCallback(evt =>
        {
            CreatureDebug.SceneLabelDetail = evt.newValue;
            EditorPrefs.SetBool(CreatureDebugScenePrefs.DetailKey, evt.newValue);
            SceneView.RepaintAll();
        });
    }
}
