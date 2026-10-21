using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SprintUI : MonoBehaviour
{
    public static SprintUI Instance { get; private set; }

    [Header("Referencias UI")]
    public CanvasGroup canvasGroup;
    public RectTransform container;
    public Image backgroundImage;
    public Image accentBorderImage;
    public TextMeshProUGUI keyBadgeText;
    public TextMeshProUGUI sprintText;

    [Header("Ajustes Visuales")]
    public float fadeSpeed = 12f;

    [Header("Paleta de Colores (Coherente con Health Bar y Heal Indicator)")]
    // Fondo oscuro con leve tinte oro/ámbar
    public Color normalBgColor = new Color(0.08f, 0.09f, 0.11f, 0.92f);
    public Color activeBorderColor = new Color(1f, 0.85f, 0.2f, 1f);      // Dorado SF
    public Color activeTextColor = new Color(1f, 0.90f, 0.35f, 1f);        // Amarillo/Dorado brillante
    public Color keyBadgeColor = new Color(0.24f, 0.18f, 0.05f, 1f);       // Fondo botón tecla Shift
    public Color keyBadgeTextColor = new Color(1f, 0.88f, 0.2f, 1f);      // Letras SHIFT

    private NewMonoBehaviourScript playerRef;

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
        }
    }

    void Start()
    {
        FindPlayer();
        ApplyThemeStyles();
    }

    private void FindPlayer()
    {
        if (playerRef == null)
        {
            playerRef = Object.FindAnyObjectByType<NewMonoBehaviourScript>();
        }
    }

    private void ApplyThemeStyles()
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = normalBgColor;
        }
        if (accentBorderImage != null)
        {
            accentBorderImage.color = activeBorderColor;
        }
        if (sprintText != null)
        {
            sprintText.color = activeTextColor;
            sprintText.text = "SPRINT ACTIVADO";
        }
        if (keyBadgeText != null)
        {
            keyBadgeText.color = keyBadgeTextColor;
        }
    }

    void Update()
    {
        if (playerRef == null)
        {
            FindPlayer();
            return;
        }

        // Se muestra SIEMPRE que el Sprint esté activado (toggle con Shift)
        bool shouldShow = playerRef.isRunning;
        float targetAlpha = shouldShow ? 1f : 0f;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
        }
    }
}
