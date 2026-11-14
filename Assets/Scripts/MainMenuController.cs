using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls the Main Menu UI, buttons, and navigation into gameplay.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Scene Routing")]
    public string gameplaySceneName = "SampleScene";

    [Header("UI Buttons")]
    public UnityEngine.UI.Button playButton;
    public UnityEngine.UI.Button quitButton;

    private void Start()
    {
        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(PlayGame);
        }
        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(QuitGame);
        }
    }

    public void PlayGame()
    {
        SoundEffects.PlayBowRelease(1f);
        SceneManager.LoadScene(gameplaySceneName);
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
