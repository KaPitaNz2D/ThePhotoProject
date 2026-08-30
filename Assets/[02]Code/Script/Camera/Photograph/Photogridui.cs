using UnityEngine;

/// <summary>
/// เปิด/ปิด Overlay เส้นกริด (เช่น Rule of Thirds) ตาม SystemState
/// โชว์เฉพาะตอนอยู่ในโหมดถ่ายรูปเท่านั้น ไม่ต้องแตะ CameraController/PhotoShooter เลย
/// </summary>
public class PhotoGridUI : MonoBehaviour
{
    [Tooltip("GameObject ของเส้นกริด (เช่น Panel ที่มีเส้นแบ่ง 3x3 อยู่ข้างใน)")]
    public GameObject gridOverlay;

    private void Start()
    {
        if (StateManager.Instance != null)
        {
            StateManager.Instance.OnSystemStateChanged += HandleSystemStateChanged;
            SetGridVisible(StateManager.Instance.CurrentSystemState == StateManager.SystemState.Photograph);
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
        SetGridVisible(newState == StateManager.SystemState.Photograph);
    }

    private void SetGridVisible(bool visible)
    {
        if (gridOverlay != null) gridOverlay.SetActive(visible);
    }
}