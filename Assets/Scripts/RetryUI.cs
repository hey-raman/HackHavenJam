using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls the in-game Game Over / Retry UI overlay shown upon player defeat.
/// Allows instant restart or returning to the main menu.
/// </summary>
public class RetryUI : MonoBehaviour
{
    public static RetryUI Instance { get; private set; }

    [Header("UI Reference")]
    public GameObject retryPanel;
    public UnityEngine.UI.Button retryButton;
    public UnityEngine.UI.Button mainMenuButton;
    public string mainMenuSceneName = "MainMenu";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (retryPanel != null)
        {
            retryPanel.SetActive(false);
        }
    }

    private void Start()
    {
        if (retryButton != null)
        {
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(Retry);
        }
        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveAllListeners();
            mainMenuButton.onClick.AddListener(GoToMainMenu);
        }
    }

    public void ShowRetry()
    {
        if (retryPanel != null)
        {
            retryPanel.SetActive(true);
        }
    }

    public void Retry()
    {
        SoundEffects.PlayBowRelease(1f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
