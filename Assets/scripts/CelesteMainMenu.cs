using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// Menú principal estilo Celeste:
/// Fondo atmosférico de montaña con parallax, 60 copos de nieve procedurales,
/// título dinámico con brillo pulsante y botones "ESCALAR" y "CONTROLES".
/// Estructura jerárquica corregida: el panel de controles es hermano del menú principal,
/// por lo que la transición y visualización de controles es 100% independiente y fluida.
/// </summary>
public class CelesteMainMenu : MonoBehaviour
{
    public static CelesteMainMenu Instance { get; private set; }

    // ── Estado público ──
    public bool IsMenuActive => menuActive;
    private bool menuActive = true;
    private bool showingControls = false;
    private bool isTransitioning = false;

    // ── Grupos de Canvas ──
    private CanvasGroup rootGroup;
    private CanvasGroup mainGroup;
    private CanvasGroup controlsGroup;
    private GameObject menuRoot;

    // ── Animación ──
    private TextMeshProUGUI titleText;
    private Image mountainFg;
    private Image mountainBg;
    private float titlePulseTimer;
    private RectTransform snowContainer;

    // ── Colores estilo Celeste ──
    private static readonly Color celesteSky       = new Color(0.10f, 0.06f, 0.22f, 1f);   // Púrpura noche
    private static readonly Color celesteMountain  = new Color(0.18f, 0.12f, 0.32f, 1f);   // Montaña lejana
    private static readonly Color celesteMountFg   = new Color(0.12f, 0.08f, 0.24f, 1f);   // Montaña frontal
    private static readonly Color celesteAccent    = new Color(0.85f, 0.30f, 0.35f, 1f);   // Rojo cereza
    private static readonly Color celesteGold      = new Color(1f, 0.85f, 0.40f, 1f);      // Dorado Celeste
    private static readonly Color celesteSnow      = new Color(0.95f, 0.95f, 1f, 0.7f);    // Nieve
    private static readonly Color btnNormal        = new Color(0.20f, 0.15f, 0.30f, 0.88f);
    private static readonly Color btnHover         = new Color(0.85f, 0.30f, 0.35f, 1f);
    private static readonly Color btnTextNormal    = new Color(0.92f, 0.92f, 0.96f, 1f);

    // ── Partículas de Nieve ──
    private RectTransform[] snowflakes;
    private float[] snowSpeeds;
    private float[] snowDrifts;
    private const int SNOW_COUNT = 60;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        BuildMenu();

        if (menuActive)
        {
            Time.timeScale = 0f;
            rootGroup.alpha = 1f;
            rootGroup.blocksRaycasts = true;
            rootGroup.interactable = true;

            mainGroup.alpha = 1f;
            mainGroup.blocksRaycasts = true;
            mainGroup.interactable = true;

            controlsGroup.alpha = 0f;
            controlsGroup.blocksRaycasts = false;
            controlsGroup.interactable = false;
        }
    }

    void Update()
    {
        if (!menuActive) return;

        // Pulso suave del título
        titlePulseTimer += Time.unscaledDeltaTime;
        float glow = 0.85f + 0.15f * Mathf.Sin(titlePulseTimer * 1.5f);
        if (titleText != null)
        {
            titleText.color = new Color(celesteGold.r, celesteGold.g, celesteGold.b, glow);
        }

        // Parallax sutil de montañas
        float breathe = Mathf.Sin(Time.unscaledTime * 0.3f) * 6f;
        if (mountainBg != null) mountainBg.rectTransform.anchoredPosition = new Vector2(breathe, 0f);
        if (mountainFg != null) mountainFg.rectTransform.anchoredPosition = new Vector2(breathe * 1.6f, 0f);

        // Nieve cayendo
        AnimateSnow();

        // Tecla ESC para volver si está en controles
        if (showingControls && !isTransitioning && Input.GetKeyDown(KeyCode.Escape))
        {
            OnBackFromControls();
        }
    }

    // ═══════════════════════════════════════════════════
    //  CONSTRUCCIÓN DEL MENÚ
    // ═══════════════════════════════════════════════════

    private void BuildMenu()
    {
        menuRoot = new GameObject("CelesteMainMenu_Canvas");
        menuRoot.transform.SetParent(transform, false);

        // Canvas Overlay
        var canvas = menuRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        var scaler = menuRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        menuRoot.AddComponent<GraphicRaycaster>();

        // EventSystem
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        // Root CanvasGroup (para el fadeout al empezar a jugar)
        rootGroup = menuRoot.AddComponent<CanvasGroup>();

        // ── 1. Fondo Atmosférico (permanece visible tanto en menú como en controles) ──
        var bgPanel = CreatePanel(menuRoot.transform, "BackgroundPanel", Vector2.zero, Vector2.one);
        CreateImage(bgPanel.transform, "Sky", Vector2.zero, Vector2.one, celesteSky);
        mountainBg = CreateMountain(bgPanel.transform, "MountainBG", celesteMountain, -100f, 0.65f);
        mountainFg = CreateMountain(bgPanel.transform, "MountainFG", celesteMountFg, -40f, 0.45f);

        var snowGO = CreatePanel(bgPanel.transform, "SnowContainer", Vector2.zero, Vector2.one);
        snowContainer = snowGO.GetComponent<RectTransform>();
        BuildSnowflakes(snowContainer);

        CreateImage(bgPanel.transform, "Vignette", Vector2.zero, Vector2.one, new Color(0, 0, 0, 0.35f));

        // ── 2. Panel Principal (Título + Botones) ──
        var mainPanel = CreatePanel(menuRoot.transform, "MainMenuPanel", Vector2.zero, Vector2.one);
        mainGroup = mainPanel.AddComponent<CanvasGroup>();

        // Título principal
        titleText = CreateTMPText(mainPanel.transform, "Title", "VIDEOJOGO",
            new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f),
            74, celesteGold, FontStyles.Bold, TextAlignmentOptions.Center);
        titleText.rectTransform.sizeDelta = new Vector2(900, 120);
        titleText.textWrappingMode = TextWrappingModes.NoWrap;

        // Subtítulo
        var sub = CreateTMPText(mainPanel.transform, "Subtitle", "~ Sube a la cima ~",
            new Vector2(0.5f, 0.63f), new Vector2(0.5f, 0.63f),
            24, new Color(0.75f, 0.70f, 0.85f, 0.85f), FontStyles.Italic, TextAlignmentOptions.Center);
        sub.rectTransform.sizeDelta = new Vector2(600, 50);

        // Botones
        float btnY = 0.42f;
        float btnSpacing = 0.095f;
        CreateCelesteButton(mainPanel.transform, "BtnPlay", "ESCALAR", new Vector2(0.5f, btnY), OnPlayClicked);
        CreateCelesteButton(mainPanel.transform, "BtnControls", "CONTROLES", new Vector2(0.5f, btnY - btnSpacing), OnControlsClicked);

        // Hint inferior
        var hint = CreateTMPText(mainPanel.transform, "Hint", "Haz clic para seleccionar",
            new Vector2(0.5f, 0.12f), new Vector2(0.5f, 0.12f),
            18, new Color(0.7f, 0.7f, 0.8f, 0.5f), FontStyles.Normal, TextAlignmentOptions.Center);
        hint.rectTransform.sizeDelta = new Vector2(500, 40);

        // ── 3. Panel de Controles (Hermano directo de MainMenuPanel bajo menuRoot) ──
        BuildControlsPanel(menuRoot.transform);
    }

    // ── Panel de Controles ──
    private void BuildControlsPanel(Transform parent)
    {
        var panel = CreatePanel(parent, "ControlsPanel", Vector2.zero, Vector2.one);
        controlsGroup = panel.AddComponent<CanvasGroup>();
        controlsGroup.alpha = 0f;
        controlsGroup.blocksRaycasts = false;
        controlsGroup.interactable = false;

        // Fondo oscuro translúcido para resaltar el texto sobre las montañas
        CreateImage(panel.transform, "ControlsBackdrop", Vector2.zero, Vector2.one, new Color(0.05f, 0.03f, 0.12f, 0.90f));

        // Título de controles
        var title = CreateTMPText(panel.transform, "ControlsTitle", "CONTROLES",
            new Vector2(0.5f, 0.90f), new Vector2(0.5f, 0.90f),
            50, celesteGold, FontStyles.Bold, TextAlignmentOptions.Center);
        title.rectTransform.sizeDelta = new Vector2(600, 70);

        // Línea decorativa
        var line = CreateImage(panel.transform, "Line", celesteAccent);
        var lineRT = line.rectTransform;
        lineRT.anchorMin = new Vector2(0.28f, 0.855f);
        lineRT.anchorMax = new Vector2(0.72f, 0.860f);
        lineRT.offsetMin = Vector2.zero;
        lineRT.offsetMax = Vector2.zero;

        // Lista de acciones y teclas
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

            // Acción (columna izquierda)
            var act = CreateTMPText(panel.transform, $"Action_{i}", actions[i],
                new Vector2(0.26f, y), new Vector2(0.26f, y),
                19, celesteAccent, FontStyles.Bold, TextAlignmentOptions.Left);
            act.rectTransform.sizeDelta = new Vector2(340, 32);

            // Tecla (columna derecha)
            var key = CreateTMPText(panel.transform, $"Key_{i}", actions[i + 1],
                new Vector2(0.74f, y), new Vector2(0.74f, y),
                19, btnTextNormal, FontStyles.Normal, TextAlignmentOptions.Right);
            key.rectTransform.sizeDelta = new Vector2(440, 32);
        }

        // Botón volver
        CreateCelesteButton(panel.transform, "BtnBack", "◄  VOLVER", new Vector2(0.5f, 0.08f), OnBackFromControls);
    }

    // ═══════════════════════════════════════════════════
    //  TRANSICIONES ENTRE PANELES
    // ═══════════════════════════════════════════════════

    private void OnControlsClicked()
    {
        if (isTransitioning) return;
        showingControls = true;
        StartCoroutine(Crossfade(mainGroup, controlsGroup));
    }

    private void OnBackFromControls()
    {
        if (isTransitioning) return;
        showingControls = false;
        StartCoroutine(Crossfade(controlsGroup, mainGroup));
    }

    private IEnumerator Crossfade(CanvasGroup from, CanvasGroup to)
    {
        isTransitioning = true;
        to.alpha = 0f;
        to.blocksRaycasts = true;
        to.interactable = true;

        float duration = 0.22f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            from.alpha = Mathf.Lerp(1f, 0f, t);
            to.alpha = Mathf.Lerp(0f, 1f, t);
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

    private void OnPlayClicked()
    {
        if (!menuActive || isTransitioning) return;
        menuActive = false;
        StartCoroutine(FadeOutAndStartGame());
    }

    private IEnumerator FadeOutAndStartGame()
    {
        isTransitioning = true;
        float duration = 0.45f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            rootGroup.alpha = 1f - (elapsed / duration);
            yield return null;
        }

        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;

        // Desactivar el GameObject completo para asegurar que no bloquee clics ni consuma CPU
        menuRoot.SetActive(false);

        // Reanudar el tiempo del juego al 100%
        Time.timeScale = 1f;
        isTransitioning = false;

        // Transición suave de entrada al juego
        if (ScreenFader.Instance != null)
        {
            ScreenFader.Instance.FadeIn(0.4f);
        }
    }

    // ═══════════════════════════════════════════════════
    //  NIEVE PROCEDURAL
    // ═══════════════════════════════════════════════════

    private void BuildSnowflakes(RectTransform container)
    {
        snowflakes = new RectTransform[SNOW_COUNT];
        snowSpeeds = new float[SNOW_COUNT];
        snowDrifts = new float[SNOW_COUNT];

        for (int i = 0; i < SNOW_COUNT; i++)
        {
            var flake = new GameObject($"Snow_{i}");
            flake.transform.SetParent(container, false);

            var rt = flake.AddComponent<RectTransform>();
            float size = Random.Range(2.5f, 6f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchorMin = new Vector2(Random.Range(0f, 1f), Random.Range(0f, 1.2f));
            rt.anchorMax = rt.anchorMin;
            rt.anchoredPosition = Vector2.zero;

            var img = flake.AddComponent<Image>();
            float alpha = Random.Range(0.35f, 0.85f);
            img.color = new Color(celesteSnow.r, celesteSnow.g, celesteSnow.b, alpha);

            snowflakes[i] = rt;
            snowSpeeds[i] = Random.Range(18f, 50f);
            snowDrifts[i] = Random.Range(-10f, 10f);
        }
    }

    private void AnimateSnow()
    {
        if (snowflakes == null) return;

        for (int i = 0; i < snowflakes.Length; i++)
        {
            if (snowflakes[i] == null) continue;

            var anchor = snowflakes[i].anchorMin;
            anchor.y -= snowSpeeds[i] * Time.unscaledDeltaTime / 1080f;
            anchor.x += snowDrifts[i] * Time.unscaledDeltaTime / 1920f;
            anchor.x += Mathf.Sin(Time.unscaledTime * 0.8f + i * 0.5f) * 0.0002f;

            if (anchor.y < -0.05f)
            {
                anchor.y = 1.05f;
                anchor.x = Random.Range(0f, 1f);
            }
            if (anchor.x < -0.05f) anchor.x = 1.05f;
            if (anchor.x > 1.05f) anchor.x = -0.05f;

            snowflakes[i].anchorMin = anchor;
            snowflakes[i].anchorMax = anchor;
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

    private Image CreateMountain(Transform parent, string name, Color color, float offsetY, float height)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(-0.1f, 0f);
        rt.anchorMax = new Vector2(1.1f, height);
        rt.offsetMin = new Vector2(0, offsetY);
        rt.offsetMax = new Vector2(0, offsetY);

        var img = go.AddComponent<Image>();
        img.color = color;

        CreatePeak(rt, color * 1.15f, 0.15f, 0.25f);
        CreatePeak(rt, color * 1.10f, 0.40f, 0.35f);
        CreatePeak(rt, color * 1.20f, 0.65f, 0.30f);
        CreatePeak(rt, color * 1.08f, 0.85f, 0.20f);

        return img;
    }

    private void CreatePeak(RectTransform mountain, Color color, float xCenter, float extraHeight)
    {
        var peak = new GameObject("Peak");
        peak.transform.SetParent(mountain, false);
        var rt = peak.AddComponent<RectTransform>();
        float w = Random.Range(0.08f, 0.16f);
        rt.anchorMin = new Vector2(xCenter - w, 1f);
        rt.anchorMax = new Vector2(xCenter + w, 1f);
        rt.sizeDelta = new Vector2(0, extraHeight * 400f);
        rt.pivot = new Vector2(0.5f, 0f);

        var img = peak.AddComponent<Image>();
        img.color = color;
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
        rt.sizeDelta = new Vector2(360, 60);

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

        // Texto
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 28;
        tmp.color = btnTextNormal;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;

        // Indicador lateral tipo Celeste (barrita roja cereza en hover)
        var indicator = new GameObject("Indicator");
        indicator.transform.SetParent(go.transform, false);
        var indRT = indicator.AddComponent<RectTransform>();
        indRT.anchorMin = new Vector2(0f, 0f);
        indRT.anchorMax = new Vector2(0.018f, 1f);
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
