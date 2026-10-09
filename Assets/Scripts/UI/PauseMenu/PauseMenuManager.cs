using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuManager : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("The panel/GameObject containing the pause menu UI.")]
    [SerializeField] private GameObject pausePanel;

    [Header("Scenes")]
    [Tooltip("Exact name of the main menu / start screen scene.")]
    [SerializeField] private string mainMenuSceneName = "StartScreen";

    [Header("Input")]
    [Tooltip("Key used to toggle pause on/off.")]
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;

    [Header("Optional Audio")]
    [SerializeField] private AudioSource buttonClickSound;

    public static bool IsPaused { get; private set; }

    private void Start()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(pauseKey))
        {
            if (IsPaused)
                Resume();
            else
                Pause();
        }
    }

    public void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f; // Freezes physics/animation; UI and this script still run (unscaled).
        if (pausePanel != null) pausePanel.SetActive(true);
    }

    public void Resume()
    {
        PlayClickSound();
        IsPaused = false;
        Time.timeScale = 1f;
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    public void RestartLevel()
    {
        PlayClickSound();
        Time.timeScale = 1f;
        IsPaused = false;
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    public void QuitToMainMenu()
    {
        PlayClickSound();
        Time.timeScale = 1f;
        IsPaused = false;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitGame()
    {
        PlayClickSound();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void PlayClickSound()
    {
        if (buttonClickSound != null)
        {
            buttonClickSound.Play();
        }
    }
}