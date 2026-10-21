using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealthHUD : MonoBehaviour
{
    public static PlayerHealthHUD Instance { get; private set; }

    [Header("Referencias de Vida (Street Fighter Bar - Top Left)")]
    public RectTransform healthBarContainer;
    public Image healthFillImage;
    public Image healthCatchupImage;
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI healthNumbersText;

    [Header("Referencias de Curación (Heal Indicator - Bottom Left)")]
    public RectTransform healContainer;
    public Image healIconImage;
    public Image healCooldownOverlay;
    public TextMeshProUGUI healKeyText;
    public TextMeshProUGUI healChargesText;
    public TextMeshProUGUI healCooldownText;
    public Image[] chargeDots;

    [Header("Ajustes Visuales")]
    public float catchupSpeed = 40f;
    public Color normalHealthColor = new Color(1.0f, 0.82f, 0.1f, 1f); // Dorado / Amarillo SF
    public Color lowHealthColor = new Color(0.95f, 0.2f, 0.15f, 1f);   // Rojo crítico
    public Color catchupColor = new Color(0.85f, 0.15f, 0.15f, 0.9f);   // Rojo chip damage SF

    private float targetFill = 1f;
    private float currentFill = 1f;
    private float catchupFill = 1f;
    private float catchupDelayTimer = 0f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnEnable()
    {
        NewMonoBehaviourScript.OnHealthChanged += HandleHealthChanged;
        NewMonoBehaviourScript.OnHealStateChanged += HandleHealStateChanged;
    }

    void OnDisable()
    {
        NewMonoBehaviourScript.OnHealthChanged -= HandleHealthChanged;
        NewMonoBehaviourScript.OnHealStateChanged -= HandleHealStateChanged;
    }

    void Start()
    {
        var player = Object.FindAnyObjectByType<NewMonoBehaviourScript>();
        if (player != null)
        {
            HandleHealthChanged(player.currentHealth, player.maxHealth);
            HandleHealStateChanged(player.healChargesRemaining, player.maxHealCharges, player.healCooldownTimer, player.healCooldown);
        }
    }

    public void HandleHealthChanged(int current, int max)
    {
        float newTarget = max > 0 ? (float)current / max : 0f;
        if (newTarget < targetFill)
        {
            catchupDelayTimer = 0.5f; // Pausa clásica antes del drenado suave
        }
        targetFill = newTarget;
        currentFill = newTarget;

        if (healthFillImage != null)
        {
            healthFillImage.fillAmount = currentFill;
            healthFillImage.color = (currentFill > 0.3f) ? normalHealthColor : lowHealthColor;
        }

        if (healthNumbersText != null)
        {
            healthNumbersText.text = $"{current} / {max}";
        }
    }

    public void HandleHealStateChanged(int charges, int maxCharges, float cooldownTimer, float maxCooldown)
    {
        if (healChargesText != null)
        {
            healChargesText.text = $"{charges}/{maxCharges}";
            healChargesText.color = charges > 0 ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1f);
        }

        if (chargeDots != null)
        {
            for (int i = 0; i < chargeDots.Length; i++)
            {
                if (chargeDots[i] != null)
                {
                    chargeDots[i].color = (i < charges) ? new Color(0.2f, 1f, 0.45f, 1f) : new Color(0.25f, 0.25f, 0.25f, 0.6f);
                }
            }
        }

        bool onCooldown = cooldownTimer > 0f;
        if (healCooldownOverlay != null)
        {
            healCooldownOverlay.gameObject.SetActive(onCooldown || charges <= 0);
            if (maxCooldown > 0f)
            {
                healCooldownOverlay.fillAmount = onCooldown ? (cooldownTimer / maxCooldown) : (charges <= 0 ? 1f : 0f);
            }
        }

        if (healCooldownText != null)
        {
            if (onCooldown)
            {
                healCooldownText.gameObject.SetActive(true);
                healCooldownText.text = $"{cooldownTimer:F1}s";
            }
            else
            {
                healCooldownText.gameObject.SetActive(false);
            }
        }

        if (healIconImage != null)
        {
            healIconImage.color = (charges > 0 && !onCooldown) ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.85f);
        }
    }

    void Update()
    {
        if (catchupDelayTimer > 0f)
        {
            catchupDelayTimer -= Time.deltaTime;
        }
        else if (catchupFill > currentFill)
        {
            catchupFill = Mathf.MoveTowards(catchupFill, currentFill, Time.deltaTime * (catchupSpeed / 100f));
            if (healthCatchupImage != null)
            {
                healthCatchupImage.fillAmount = catchupFill;
            }
        }
        else if (catchupFill < currentFill)
        {
            catchupFill = currentFill;
            if (healthCatchupImage != null)
            {
                healthCatchupImage.fillAmount = catchupFill;
            }
        }
    }
}
