using System.Collections;
using TMPro;
using UnityEngine;

public class LevelIntroScreen : MonoBehaviour
{
    [SerializeField] TMP_Text levelText;
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] float holdTime = 1.5f;
    [SerializeField] float fadeTime = 0.5f;

    public IEnumerator Show(int levelNumber)
    {
        gameObject.SetActive(true);
        levelText.text = $"Level {levelNumber}";
        canvasGroup.alpha = 1f;

        // Use unscaled time so it works while the game is paused
        yield return new WaitForSecondsRealtime(holdTime);

        // Fade out
        for (float t = 0; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            canvasGroup.alpha = 1f - (t / fadeTime);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }
}