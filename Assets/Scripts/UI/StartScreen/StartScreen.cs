using UnityEngine;
using UnityEngine.SceneManagement;

public class StartScreenManager : MonoBehaviour
{
    [Header("Scene To Load")]
    [Tooltip("Exact name of the gameplay scene to load when Play is pressed.")]
    [SerializeField] private string gameplaySceneName = "GameScene";

    [Header("Optional UI")]
    [Tooltip("Settings panel to show/hide. Leave empty if you don't have one.")]
    [SerializeField] private GameObject settingsPanel;

    [Tooltip("Main menu panel (buttons). Used to hide the menu while settings is open.")]
    [SerializeField] private GameObject mainMenuPanel;

    [Header("Optional Audio")]
    [SerializeField] private AudioSource buttonClickSound;

    private void Start()
    {
        // Make sure the settings panel starts closed and main menu starts visible.
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);

        // Ensure the game isn't paused if we came back here from a pause menu "Quit to Menu".
        Time.timeScale = 1f;
    }

    public void PlayGame()
    {
        PlayClickSound();
        SceneManager.LoadScene(gameplaySceneName);
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

    public void OpenSettings()
    {
        PlayClickSound();
        if (settingsPanel != null) settingsPanel.SetActive(true);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
    }

    public void CloseSettings()
    {
        PlayClickSound();
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
    }

    private void PlayClickSound()
    {
        if (buttonClickSound != null)
        {
            buttonClickSound.Play();
        }
    }
}
