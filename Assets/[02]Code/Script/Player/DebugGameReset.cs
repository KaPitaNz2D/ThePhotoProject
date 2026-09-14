using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class DebugGameReset : MonoBehaviour
{
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

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.ResetAllQuests();
        }

        if (PhotoStorage.Instance != null)
        {
            PhotoStorage.Instance.ClearAllPhotos();
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
