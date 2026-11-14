using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls the Game Over / Retry screen after the player falls in battle.
/// Allows instant retry or returning to the main menu.
/// </summary>
public class GameOverController : MonoBehaviour
{
    [Header("Scene Routing")]
    public string gameplaySceneName = "SampleScene";
    public string mainMenuSceneName = "MainMenu";

    public void RetryGame()
    {
        SoundEffects.PlayBowRelease(1f);
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void GoToMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
