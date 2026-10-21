using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    public static GameOverUI Instance { get; private set; }

    [Header("Referencias Visuales")]
    public CanvasGroup canvasGroup;
    public Image darkBanner;
    public Image fullBlackFade;
    public TextMeshProUGUI youDiedText;
    public TextMeshProUGUI subText;

    [Header("Tiempos Cinemáticos Dark Souls")]
    public float bannerFadeDuration = 1.1f;
    public float textScaleDuration = 3.2f;
    public float holdDuration = 1.4f;
    public float fadeOutDuration = 0.9f;

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

    public void TriggerGameOver()
    {
        if (isDisplaying) return;
        StartCoroutine(DarkSoulsGameOverRoutine());
    }

    private IEnumerator DarkSoulsGameOverRoutine()
    {
        isDisplaying = true;

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }

        float elapsed = 0f;
        Vector3 initialTextScale = new Vector3(0.92f, 0.92f, 1f);
        Vector3 targetTextScale = new Vector3(1.10f, 1.10f, 1f);

        if (youDiedText != null)
        {
            youDiedText.transform.localScale = initialTextScale;
        }

        // 1. Aparición dramática del banner y texto
        while (elapsed < bannerFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / bannerFadeDuration;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
            }
            if (youDiedText != null)
            {
                youDiedText.transform.localScale = Vector3.Lerp(initialTextScale, targetTextScale, elapsed / textScaleDuration);
            }
            yield return null;
        }

        if (canvasGroup != null) canvasGroup.alpha = 1f;

        // 2. Sostener la pantalla de muerte con zoom lento
        float holdElapsed = 0f;
        while (holdElapsed < holdDuration)
        {
            holdElapsed += Time.deltaTime;
            elapsed += Time.deltaTime;
            if (youDiedText != null)
            {
                youDiedText.transform.localScale = Vector3.Lerp(initialTextScale, targetTextScale, elapsed / textScaleDuration);
            }
            yield return null;
        }

        // 3. Fundido a negro total
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

        yield return new WaitForSeconds(0.2f);

        // 4. Reiniciar la escena para volver a empezar
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
