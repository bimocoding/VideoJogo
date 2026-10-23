using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class VictoryUI : MonoBehaviour
{
    public static VictoryUI Instance { get; private set; }

    [Header("Referencias Visuales")]
    public CanvasGroup canvasGroup;
    public Image darkBanner;
    public Image fullBlackFade;
    public TextMeshProUGUI victoryText;
    public TextMeshProUGUI subText;

    [Header("Tiempos Cinemáticos")]
    public float bannerFadeDuration = 1.0f;
    public float textScaleDuration = 3.0f;
    public float holdDuration = 2.0f;
    public float fadeOutDuration = 0.8f;

    private bool isDisplaying = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        if (fullBlackFade != null)
        {
            fullBlackFade.color = new Color(0f, 0f, 0f, 0f);
        }
    }

    public void TriggerVictory()
    {
        if (isDisplaying) return;
        StartCoroutine(VictorySequenceRoutine());
    }

    private IEnumerator VictorySequenceRoutine()
    {
        isDisplaying = true;

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }

        float elapsed = 0f;
        Vector3 initialScale = new Vector3(0.90f, 0.90f, 1f);
        Vector3 targetScale = new Vector3(1.10f, 1.10f, 1f);

        if (victoryText != null)
        {
            victoryText.transform.localScale = initialScale;
        }

        // 1. Aparición suave y triunfal del banner y texto
        while (elapsed < bannerFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / bannerFadeDuration;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
            }
            if (victoryText != null)
            {
                victoryText.transform.localScale = Vector3.Lerp(initialScale, targetScale, elapsed / textScaleDuration);
            }
            yield return null;
        }

        if (canvasGroup != null) canvasGroup.alpha = 1f;

        // 2. Mantener la pantalla de victoria con zoom cinemático lento
        float holdElapsed = 0f;
        while (holdElapsed < holdDuration)
        {
            holdElapsed += Time.deltaTime;
            elapsed += Time.deltaTime;
            if (victoryText != null)
            {
                victoryText.transform.localScale = Vector3.Lerp(initialScale, targetScale, elapsed / textScaleDuration);
            }
            yield return null;
        }

        // 3. Fundido a negro
        float fadeElapsed = 0f;
        while (fadeElapsed < fadeOutDuration)
        {
            fadeElapsed += Time.deltaTime;
            float alpha = fadeElapsed / fadeOutDuration;
            if (fullBlackFade != null)
            {
                fullBlackFade.color = new Color(0f, 0f, 0f, alpha);
            }
            yield return null;
        }

        if (fullBlackFade != null)
        {
            fullBlackFade.color = Color.black;
        }

        yield return new WaitForSeconds(0.3f);

        // 4. Reiniciar el nivel
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
