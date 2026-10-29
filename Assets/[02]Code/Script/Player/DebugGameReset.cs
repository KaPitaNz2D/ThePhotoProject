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

    /// <summary>ล้างเควส/รูปทั้งหมดแล้วกลับเมนูหลัก — ใช้ทั้งปุ่ม R (Debug) และปุ่ม Back to Menu ในหน้า Pause</summary>
    public static void ResetToMainMenu(string mainMenuSceneName)
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.ResetAllQuests();
        }

        if (PhotoStorage.Instance != null)
        {
            PhotoStorage.Instance.ClearAllPhotos();
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }
}
