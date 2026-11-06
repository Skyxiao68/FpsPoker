using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartScreenManager : MonoBehaviour
{
    [Header("Scene To Load")]
    [Tooltip("Exact name of the gameplay scene to load when Play is pressed.")]
    [SerializeField] private string gameplaySceneName = "GameScene";

    [SerializeField] private GameObject levelIntroCanvasPrefab;
    [SerializeField] private GameObject levelSelectCanvasPrefab;

    [Header("Optional UI")]
    [Tooltip("Settings panel to show/hide. Leave empty if you don't have one.")]
    [SerializeField] private GameObject settingsPanel;

    [Tooltip("Main menu panel (buttons). Used to hide the menu while settings is open.")]
    [SerializeField] private GameObject mainMenuPanel;

    [Header("Optional Audio")]
    [SerializeField] private AudioSource buttonClickSound;

    private bool loadingLevel;

    private void Start()
    {
        // Make sure the settings panel starts closed and main menu starts visible.
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);

        // Ensure the game isn't paused if we came back here from a pause menu "Quit to Menu".
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (GameFlowManager.Instance != null && GameFlowManager.Instance.IsGameOver)
            ShowGameOver();
        else if (GameFlowManager.Instance != null && GameFlowManager.Instance.AwaitingNextLevel)
            ShowLevelMenu();
    }

    public void PlayGame()
    {
        if (loadingLevel)
            return;

        PlayClickSound();

        // 第一次玩：先走独立教程场景。通过或跳过都会写 TutorialProgress，
        // 之后再点 Play 就直接进牌桌（教程入口以后再单独做）。
        if (!TutorialProgress.IsDone)
        {
            loadingLevel = true;
            if (mainMenuPanel != null)
                mainMenuPanel.SetActive(false);
            SceneFader.LoadScene("TutorialScene");
            return;
        }

        int level = GameFlowManager.Instance != null
            ? GameFlowManager.Instance.CurrentLevel : 1;
        StartCoroutine(LoadLevel(level, false));
    }

    private IEnumerator LoadLevel(int level, bool nextLevel)
    {
        loadingLevel = true;
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);

        if (levelIntroCanvasPrefab != null)
        {
            GameObject canvas = Instantiate(levelIntroCanvasPrefab);
            LevelIntroScreen intro = canvas.GetComponentInChildren<LevelIntroScreen>(true);
            if (intro != null)
                yield return intro.Show(level);
            Destroy(canvas);
        }

        if (nextLevel && GameFlowManager.Instance != null)
            GameFlowManager.Instance.BeginSelectedLevel();
        else
            SceneManager.LoadScene(gameplaySceneName);
    }

    private void ShowLevelMenu()
    {
        if (levelSelectCanvasPrefab == null)
        {
            Debug.LogError("StartScreenManager: Level Select Canvas is not assigned.");
            return;
        }

        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);

        GameObject menu = Instantiate(levelSelectCanvasPrefab);
        Button shopButton = null;
        Button nextButton = null;
        foreach (Button button in menu.GetComponentsInChildren<Button>(true))
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            string text = label != null ? label.text.Trim() : string.Empty;
            if (text.StartsWith("Level 1", StringComparison.Ordinal))
                shopButton = button;
            else if (text.StartsWith("Level 2", StringComparison.Ordinal))
                nextButton = button;
            else
                button.gameObject.SetActive(false);
        }

        SetButton(shopButton, "Shop", () => GameFlowManager.Instance.BeginShop());
        SetButton(nextButton, $"Level {GameFlowManager.Instance.CurrentLevel}",
            () => StartCoroutine(LoadLevel(GameFlowManager.Instance.CurrentLevel, true)));
    }

    private void ShowGameOver()
    {
        if (mainMenuPanel == null)
            return;

        foreach (Button button in mainMenuPanel.GetComponentsInChildren<Button>(true))
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null && label.text.Trim() == "Start")
            {
                label.text = "Game Over";
                button.interactable = false;
            }
        }
    }

    private void SetButton(Button button, string label, Action action)
    {
        if (button == null)
        {
            Debug.LogError($"StartScreenManager: {label} button is missing.");
            return;
        }

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = label;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            PlayClickSound();
            action();
        });
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
