using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class DebugGameReset : MonoBehaviour
{
    public string mainMenuSceneName = "MainMenu";

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            ResetGame();
        }
    }

    private void ResetGame() => ResetToMainMenu(mainMenuSceneName);

    /// <summary>ปุ่ม R (Debug) — ลบเซฟทั้งหมด (รูป/เควส/Journal/บอร์ด/ตัวแปร Yarn) แล้วกลับเมนูหลัก เหมือนกดปุ่ม Reset ในเมนู</summary>
    public static void ResetToMainMenu(string mainMenuSceneName)
    {
        SaveSystem.DeleteAll();
        ReturnToMainMenu(mainMenuSceneName);
    }

    /// <summary>กลับเมนูหลักโดยเก็บความคืบหน้าไว้ (ทุกระบบเซฟทันทีที่ข้อมูลเปลี่ยนอยู่แล้ว) — ปุ่ม Back to Menu ในหน้า Pause</summary>
    public static void ReturnToMainMenu(string mainMenuSceneName)
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene(mainMenuSceneName);
    }
}
