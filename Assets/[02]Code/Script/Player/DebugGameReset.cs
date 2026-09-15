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

    private void ResetGame()
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
