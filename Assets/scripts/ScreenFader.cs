using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fundido a negro de pantalla completa. Se crea solo al iniciar el juego y hace un fade-in desde negro.
/// Uso desde otros scripts: ScreenFader.Instance.FadeOut(0.5f) / FadeIn(0.5f)
/// </summary>
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    public float startFadeDuration = 1.0f;
    public float startHoldTime = 0.15f;      // un instante en negro antes de aclarar

    private Image overlay;
    private Coroutine fadeRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateOnStart()
    {
        if (Instance != null) return;
        new GameObject("ScreenFader").AddComponent<ScreenFader>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
        SetAlpha(1f);
    }

    void Start()
    {
        fadeRoutine = StartCoroutine(FadeRoutine(1f, 0f, startFadeDuration, startHoldTime));
        // Al recargar la escena (reintentar tras Game Over) también entra con fundido
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(1f, 0f, startFadeDuration, startHoldTime));
    }

    public void FadeIn(float duration) => StartFade(0f, duration);
    public void FadeOut(float duration) => StartFade(1f, duration);

    private void StartFade(float to, float duration)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(overlay.color.a, to, duration, 0f));
    }

    private IEnumerator FadeRoutine(float from, float to, float duration, float hold)
    {
        overlay.raycastTarget = true;
        SetAlpha(from);
        if (hold > 0f) yield return new WaitForSecondsRealtime(hold);

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            SetAlpha(Mathf.Lerp(from, to, k));
            yield return null;
        }
        SetAlpha(to);
        overlay.raycastTarget = to > 0.01f;
        fadeRoutine = null;
    }

    private void BuildOverlay()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // por encima de toda la UI

        var imgObj = new GameObject("Overlay");
        imgObj.transform.SetParent(transform, false);
        var rt = imgObj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        overlay = imgObj.AddComponent<Image>();
        overlay.color = Color.black;
    }

    private void SetAlpha(float a)
    {
        var c = overlay.color;
        c.a = a;
        overlay.color = c;
    }
}
