using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

/// <summary>
/// Menú de pausa estilo Celeste: se activa con la tecla ESC durante el juego.
/// Congela el juego (timeScale = 0).
/// Incluye opciones de:
/// - CONTINUAR (Resume)
/// - CONTROLES (Ver lista de controles durante la partida)
/// - REINICIAR (Restart nivel desde el principio)
/// </summary>
public class CelestePauseMenu : MonoBehaviour
{
    public static CelestePauseMenu Instance { get; private set; }

    // ── Estado ──
    private bool isPaused = false;
    private bool showingControls = false;
    private bool isTransitioning = false;

    // ── Grupos de Canvas ──
    private CanvasGroup pauseRootGroup;
    private CanvasGroup mainPauseGroup;
    private CanvasGroup controlsGroup;
    private GameObject pauseRoot;

    // ── Animación ──
    private TextMeshProUGUI pauseTitle;
    private float pulseTimer;

    // ── Colores estilo Celeste ──
    private static readonly Color bgOverlay       = new Color(0.06f, 0.04f, 0.14f, 0.92f);
    private static readonly Color celesteAccent    = new Color(0.85f, 0.30f, 0.35f, 1f);
    private static readonly Color celesteGold      = new Color(1f, 0.85f, 0.40f, 1f);
    private static readonly Color btnNormal        = new Color(0.20f, 0.15f, 0.30f, 0.88f);
    private static readonly Color btnHover         = new Color(0.85f, 0.30f, 0.35f, 1f);
    private static readonly Color btnTextColor     = new Color(0.92f, 0.92f, 0.96f, 1f);
    private static readonly Color subtitleColor    = new Color(0.65f, 0.60f, 0.80f, 0.85f);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildPauseMenu();
    }

    void Update()
    {
        // No abrir pausa si el menú principal de inicio sigue activo
        if (CelesteMainMenu.Instance != null && CelesteMainMenu.Instance.IsMenuActive)
            return;

        if (Input.GetKeyDown(KeyCode.Escape) && !isTransitioning)
        {
            if (showingControls)
            {
                OnBackFromControls();
            }
            else if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }

        if (isPaused && pauseTitle != null)
        {
            pulseTimer += Time.unscaledDeltaTime;
            float glow = 0.85f + 0.15f * Mathf.Sin(pulseTimer * 2f);
            pauseTitle.color = new Color(celesteGold.r, celesteGold.g, celesteGold.b, glow);
        }
    }

    // ═══════════════════════════════════════════════════
    //  PAUSA / RESUME / RESTART
    // ═══════════════════════════════════════════════════

    public void PauseGame()
    {
        if (isPaused || isTransitioning) return;
        isPaused = true;
        showingControls = false;
        Time.timeScale = 0f;

        mainPauseGroup.alpha = 1f;
        mainPauseGroup.blocksRaycasts = true;
        mainPauseGroup.interactable = true;

        controlsGroup.alpha = 0f;
        controlsGroup.blocksRaycasts = false;
        controlsGroup.interactable = false;

        StartCoroutine(FadeIn());
    }

    public void ResumeGame()
    {
        if (!isPaused || isTransitioning) return;
        StartCoroutine(FadeOutAndResume());
    }

    public void RestartGame()
    {
        if (isTransitioning) return;
        StartCoroutine(FadeOutAndRestart());
    }

    private IEnumerator FadeIn()
    {
        isTransitioning = true;
        pauseRoot.SetActive(true);
        pauseRootGroup.blocksRaycasts = true;
        pauseRootGroup.interactable = true;

        float dur = 0.20f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            pauseRootGroup.alpha = Mathf.SmoothStep(0f, 1f, t / dur);
            yield return null;
        }
        pauseRootGroup.alpha = 1f;
        isTransitioning = false;
    }

    private IEnumerator FadeOutAndResume()
    {
        isTransitioning = true;
        float dur = 0.18f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            pauseRootGroup.alpha = 1f - (t / dur);
            yield return null;
        }

        pauseRootGroup.alpha = 0f;
        pauseRootGroup.blocksRaycasts = false;
        pauseRootGroup.interactable = false;
        pauseRoot.SetActive(false);

        Time.timeScale = 1f;
        isPaused = false;
        showingControls = false;
        isTransitioning = false;
    }

    private IEnumerator FadeOutAndRestart()
    {
        isTransitioning = true;
        float dur = 0.35f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        Time.timeScale = 1f;
        isPaused = false;
        isTransitioning = false;

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ── Transición a controles en Pausa ──
    private void OnControlsClicked()
    {
        if (isTransitioning) return;
        showingControls = true;
        StartCoroutine(Crossfade(mainPauseGroup, controlsGroup));
    }

    private void OnBackFromControls()
    {
        if (isTransitioning) return;
        showingControls = false;
        StartCoroutine(Crossfade(controlsGroup, mainPauseGroup));
    }

    private IEnumerator Crossfade(CanvasGroup from, CanvasGroup to)
    {
        isTransitioning = true;
        to.alpha = 0f;
        to.blocksRaycasts = true;
        to.interactable = true;

        float dur = 0.18f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = t / dur;
            from.alpha = Mathf.Lerp(1f, 0f, k);
            to.alpha = Mathf.Lerp(0f, 1f, k);
            yield return null;
        }

        from.alpha = 0f;
        from.blocksRaycasts = false;
        from.interactable = false;

        to.alpha = 1f;
        to.blocksRaycasts = true;
        to.interactable = true;
        isTransitioning = false;
    }

    // ═══════════════════════════════════════════════════
    //  CONSTRUCCIÓN DEL MENÚ DE PAUSA
    // ═══════════════════════════════════════════════════

    private void BuildPauseMenu()
    {
        pauseRoot = new GameObject("CelestePauseMenu_Canvas");
        pauseRoot.transform.SetParent(transform, false);

        var canvas = pauseRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 950;
        var scaler = pauseRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        pauseRoot.AddComponent<GraphicRaycaster>();

        pauseRootGroup = pauseRoot.AddComponent<CanvasGroup>();
        pauseRootGroup.alpha = 0f;
        pauseRootGroup.blocksRaycasts = false;
        pauseRootGroup.interactable = false;

        // Fondo oscuro general
        CreateImage(pauseRoot.transform, "DarkOverlay", Vector2.zero, Vector2.one, bgOverlay);

        // ── 1. Panel Principal de Pausa ──
        var mainPanel = CreatePanel(pauseRoot.transform, "MainPausePanel", Vector2.zero, Vector2.one);
        mainPauseGroup = mainPanel.AddComponent<CanvasGroup>();

        // Marco central
        CreateImage(mainPanel.transform, "CenterBox",
            new Vector2(0.24f, 0.22f), new Vector2(0.76f, 0.82f),
            new Color(0.10f, 0.07f, 0.18f, 0.75f));

        // Líneas horizontales de acento
        CreateImage(mainPanel.transform, "TopAccent",
            new Vector2(0.24f, 0.82f), new Vector2(0.76f, 0.824f), celesteAccent * 0.8f);
        CreateImage(mainPanel.transform, "BottomAccent",
            new Vector2(0.24f, 0.22f), new Vector2(0.76f, 0.224f), celesteAccent * 0.8f);

        // Título PAUSA
        pauseTitle = CreateTMPText(mainPanel.transform, "PauseTitle", "PAUSA",
            new Vector2(0.5f, 0.73f), new Vector2(0.5f, 0.73f),
            62, celesteGold, FontStyles.Bold, TextAlignmentOptions.Center);
        pauseTitle.rectTransform.sizeDelta = new Vector2(500, 80);

        var diamond = CreateTMPText(mainPanel.transform, "Diamond", "◆",
            new Vector2(0.5f, 0.655f), new Vector2(0.5f, 0.655f),
            20, celesteAccent, FontStyles.Normal, TextAlignmentOptions.Center);
        diamond.rectTransform.sizeDelta = new Vector2(50, 30);

        // Botones: CONTINUAR, CONTROLES, REINICIAR
        float btnY = 0.55f;
        float spacing = 0.09f;

        CreateCelesteButton(mainPanel.transform, "BtnResume", "CONTINUAR", new Vector2(0.5f, btnY), () => ResumeGame());
        CreateCelesteButton(mainPanel.transform, "BtnControls", "CONTROLES", new Vector2(0.5f, btnY - spacing), OnControlsClicked);
        CreateCelesteButton(mainPanel.transform, "BtnRestart", "REINICIAR", new Vector2(0.5f, btnY - spacing * 2f), () => RestartGame());

        // Hint
        var hint = CreateTMPText(mainPanel.transform, "EscHint", "ESC  para continuar",
            new Vector2(0.5f, 0.16f), new Vector2(0.5f, 0.16f),
            16, subtitleColor, FontStyles.Italic, TextAlignmentOptions.Center);
        hint.rectTransform.sizeDelta = new Vector2(400, 30);

        // Partículas decorativas
        BuildStaticParticles(mainPanel.transform, 24);

        // ── 2. Panel de Controles dentro de Pausa (Hermano de MainPausePanel) ──
        BuildPauseControlsPanel(pauseRoot.transform);

        // Desactivado al inicio
        pauseRoot.SetActive(false);
    }

    private void BuildPauseControlsPanel(Transform parent)
    {
        var panel = CreatePanel(parent, "PauseControlsPanel", Vector2.zero, Vector2.one);
        controlsGroup = panel.AddComponent<CanvasGroup>();
        controlsGroup.alpha = 0f;
        controlsGroup.blocksRaycasts = false;
        controlsGroup.interactable = false;

        // Fondo oscuro
        CreateImage(panel.transform, "ControlsBackdrop", Vector2.zero, Vector2.one, new Color(0.05f, 0.03f, 0.12f, 0.94f));

        var title = CreateTMPText(panel.transform, "ControlsTitle", "CONTROLES",
            new Vector2(0.5f, 0.90f), new Vector2(0.5f, 0.90f),
            50, celesteGold, FontStyles.Bold, TextAlignmentOptions.Center);
        title.rectTransform.sizeDelta = new Vector2(600, 70);

        var line = CreateImage(panel.transform, "Line", celesteAccent);
        var lineRT = line.rectTransform;
        lineRT.anchorMin = new Vector2(0.28f, 0.855f);
        lineRT.anchorMax = new Vector2(0.72f, 0.860f);
        lineRT.offsetMin = Vector2.zero;
        lineRT.offsetMax = Vector2.zero;

        string[] actions = {
            "MOVIMIENTO",       "A / D   ó   ←  →",
            "SALTAR",           "ESPACIO   ó   W",
            "DOBLE SALTO",      "ESPACIO en el aire",
            "CORRER",           "SHIFT (activar / desactivar)",
            "AGACHARSE",        "S   ó   ↓",
            "DESLIZARSE",       "S + SHIFT mientras corres",
            "ESQUIVAR (DODGE)", "L (dirección + L)",
            "DASH AÉREO",       "SHIFT en el aire",
            "ATACAR (COMBOS)",  "J (combo x3 normal / x5 variación)",
            "ATAQUE VERTICAL",  "W + J",
            "ATAQUE EN SALTO",  "J en el aire",
            "DODGE ATTACK",     "L  luego  J",
            "CURACIÓN",         "H (mantener, máx 3 cargas)",
            "WALL JUMP",        "Tocar pared + ESPACIO"
        };

        float startY = 0.80f;
        float rowH = 0.046f;

        for (int i = 0; i < actions.Length; i += 2)
        {
            float y = startY - (i / 2) * rowH;

            var act = CreateTMPText(panel.transform, $"Action_{i}", actions[i],
                new Vector2(0.26f, y), new Vector2(0.26f, y),
                19, celesteAccent, FontStyles.Bold, TextAlignmentOptions.Left);
            act.rectTransform.sizeDelta = new Vector2(340, 32);

            var key = CreateTMPText(panel.transform, $"Key_{i}", actions[i + 1],
                new Vector2(0.74f, y), new Vector2(0.74f, y),
                19, btnTextColor, FontStyles.Normal, TextAlignmentOptions.Right);
            key.rectTransform.sizeDelta = new Vector2(440, 32);
        }

        CreateCelesteButton(panel.transform, "BtnBack", "◄  VOLVER", new Vector2(0.5f, 0.08f), OnBackFromControls);
    }

    private void BuildStaticParticles(Transform parent, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var dot = new GameObject($"Particle_{i}");
            dot.transform.SetParent(parent, false);

            var rt = dot.AddComponent<RectTransform>();
            float size = Random.Range(2.5f, 5.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchorMin = new Vector2(Random.Range(0.24f, 0.76f), Random.Range(0.22f, 0.82f));
            rt.anchorMax = rt.anchorMin;
            rt.anchoredPosition = Vector2.zero;

            var img = dot.AddComponent<Image>();
            img.color = new Color(celesteAccent.r, celesteAccent.g, celesteAccent.b, Random.Range(0.15f, 0.35f));
        }
    }

    // ═══════════════════════════════════════════════════
    //  HELPERS DE INTERFAZ
    // ═══════════════════════════════════════════════════

    private GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    private Image CreateImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        var go = CreatePanel(parent, name, anchorMin, anchorMax);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private Image CreateImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private TextMeshProUGUI CreateTMPText(Transform parent, string name, string text,
        Vector2 anchorMin, Vector2 anchorMax,
        int fontSize, Color color, FontStyles style, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;

        return tmp;
    }

    private void CreateCelesteButton(Transform parent, string goName, string label, Vector2 anchorPos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorPos;
        rt.anchorMax = anchorPos;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(340, 56);

        var bgImg = go.AddComponent<Image>();
        bgImg.color = btnNormal;

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = btnNormal;
        colors.highlightedColor = btnHover;
        colors.pressedColor = celesteAccent * 0.8f;
        colors.selectedColor = btnHover;
        colors.fadeDuration = 0.12f;
        btn.colors = colors;
        btn.targetGraphic = bgImg;
        btn.onClick.AddListener(onClick);

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 26;
        tmp.color = btnTextColor;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;

        var indicator = new GameObject("Indicator");
        indicator.transform.SetParent(go.transform, false);
        var indRT = indicator.AddComponent<RectTransform>();
        indRT.anchorMin = new Vector2(0f, 0f);
        indRT.anchorMax = new Vector2(0.016f, 1f);
        indRT.offsetMin = Vector2.zero;
        indRT.offsetMax = Vector2.zero;
        var indImg = indicator.AddComponent<Image>();
        indImg.color = celesteAccent;
        indicator.SetActive(false);

        var trigger = go.AddComponent<EventTrigger>();
        var entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entryEnter.callback.AddListener((_) => { indicator.SetActive(true); });
        trigger.triggers.Add(entryEnter);

        var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        entryExit.callback.AddListener((_) => { indicator.SetActive(false); });
        trigger.triggers.Add(entryExit);
    }
}
