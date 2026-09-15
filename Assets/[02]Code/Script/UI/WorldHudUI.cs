using UnityEngine;

// โชว์เฉพาะตอนเดินสำรวจโลกอยู่ (SystemState.Normal) ซ่อนทั้งหมดทันทีที่เปิด Photograph/Journal/Storage/Talking/Pause
// Pattern เดียวกับ PhotoGridUI.cs
public class WorldHudUI : MonoBehaviour
{
    [Tooltip("Root ของ HUD ทั้งชุด (ไอคอนกลาง + ปุ่มรอบข้างทั้งหมด) — ปิด/เปิดทีเดียวทั้งก้อน")]
    public GameObject hudRoot;

    private void Start()
    {
        if (StateManager.Instance != null)
        {
            StateManager.Instance.OnSystemStateChanged += HandleSystemStateChanged;
            SetHudVisible(StateManager.Instance.CurrentSystemState == StateManager.SystemState.Normal);
        }
    }

    private void OnDestroy()
    {
        if (StateManager.Instance != null)
        {
            StateManager.Instance.OnSystemStateChanged -= HandleSystemStateChanged;
        }
    }

    private void HandleSystemStateChanged(StateManager.SystemState oldState, StateManager.SystemState newState)
    {
        SetHudVisible(newState == StateManager.SystemState.Normal);
    }

    private void SetHudVisible(bool visible)
    {
        if (hudRoot != null) hudRoot.SetActive(visible);
    }
}
