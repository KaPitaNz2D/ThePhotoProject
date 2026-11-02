using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Tooltip("ชื่อซีนที่ปุ่ม Play โหลด — ต้องอยู่ใน Build Settings")]
    public string gameSceneName = "PlayTest_Final";

    [Tooltip("ป๊อปอัปยืนยันการรีเซ็ตเกม (ปิดไว้ตอนเริ่ม) — ปุ่ม Reset มุมขวาล่างเปิดอันนี้")]
    public GameObject resetConfirmPopup;

    private void Start()
    {
        if (resetConfirmPopup != null) resetConfirmPopup.SetActive(false);
    }

    public void StartGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void ExitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    /// <summary>ปุ่ม Reset มุมขวาล่าง — เปิดป๊อปอัปเตือนก่อน ยังไม่ลบอะไร</summary>
    public void OpenResetConfirm()
    {
        if (resetConfirmPopup != null) resetConfirmPopup.SetActive(true);
    }

    /// <summary>ปุ่มยกเลิกในป๊อปอัป</summary>
    public void CancelReset()
    {
        if (resetConfirmPopup != null) resetConfirmPopup.SetActive(false);
    }

    /// <summary>ปุ่มยืนยันในป๊อปอัป — ลบรูปและความคืบหน้าทั้งหมดที่เซฟไว้ (SaveSystem) แล้วปิดป๊อปอัป</summary>
    public void ConfirmReset()
    {
        SaveSystem.DeleteAll();
        if (resetConfirmPopup != null) resetConfirmPopup.SetActive(false);
    }
}
