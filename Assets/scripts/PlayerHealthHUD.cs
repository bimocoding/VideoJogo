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

    [Header("Referencias de Stamina (Stamina Bar - Top Left)")]
    public RectTransform staminaBarContainer;
    public Image staminaFillImage;
    public Image staminaCatchupImage;
    public TextMeshProUGUI staminaNumbersText;

    [Header("Referencias de Curación (Heal Indicator - Bottom Left)")]
    public RectTransform healContainer;
    public Image healIconImage;
    public Image healCooldownOverlay;
    public TextMeshProUGUI healKeyText;
    public TextMeshProUGUI healChargesText;
    public TextMeshProUGUI healCooldownText;
    public Image[] chargeDots;

    [Header("Ajustes Visuales de Salud (Rojo)")]
    public float catchupSpeed = 40f;
    public Color normalHealthColor = new Color(0.92f, 0.18f, 0.18f, 1f); // Rojo vivo
    public Color lowHealthColor = new Color(0.65f, 0.08f, 0.08f, 1f);   // Rojo crítico
    public Color catchupColor = new Color(0.95f, 0.45f, 0.20f, 0.9f);   // Naranja-rojo chip damage

    [Header("Ajustes Visuales de Stamina (Verde)")]
    public Color normalStaminaColor = new Color(0.18f, 0.88f, 0.38f, 1f); // Verde vivo
    public Color lowStaminaColor = new Color(0.12f, 0.50f, 0.22f, 1f);    // Verde oscuro
    public Color staminaCatchupColor = new Color(0.40f, 0.95f, 0.55f, 0.85f); // Verde claro chip
    public Color insufficientStaminaFlashColor = new Color(1f, 0.3f, 0.3f, 1f);

    private float targetFill = 1f;
    private float currentFill = 1f;
    private float catchupFill = 1f;
    private float catchupDelayTimer = 0f;

    private float targetStaminaFill = 1f;
    private float currentStaminaFill = 1f;
    private float catchupStaminaFill = 1f;
    private float catchupStaminaDelayTimer = 0f;
    private Coroutine staminaFlashRoutine = null;

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
        PlayerStamina.OnStaminaChanged += HandleStaminaChanged;
        PlayerStamina.OnStaminaInsufficient += HandleStaminaInsufficient;
    }

    void OnDisable()
    {
        NewMonoBehaviourScript.OnHealthChanged -= HandleHealthChanged;
        NewMonoBehaviourScript.OnHealStateChanged -= HandleHealStateChanged;
        PlayerStamina.OnStaminaChanged -= HandleStaminaChanged;
        PlayerStamina.OnStaminaInsufficient -= HandleStaminaInsufficient;
    }

    void Start()
    {
        var player = Object.FindAnyObjectByType<NewMonoBehaviourScript>();
        if (player != null)
        {
            HandleHealthChanged(player.currentHealth, player.maxHealth);
            HandleHealStateChanged(player.healChargesRemaining, player.maxHealCharges, player.healCooldownTimer, player.healCooldown);
        }

        var stamina = Object.FindAnyObjectByType<PlayerStamina>();
        if (stamina != null)
        {
            HandleStaminaChanged(stamina.CurrentStamina, stamina.MaxStamina);
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

    public void HandleStaminaChanged(float current, float max)
    {
        float newTarget = max > 0.001f ? current / max : 0f;
        if (newTarget < targetStaminaFill)
        {
            catchupStaminaDelayTimer = 0.35f;
        }
        targetStaminaFill = newTarget;
        currentStaminaFill = newTarget;

        if (staminaFillImage != null && staminaFlashRoutine == null)
        {
            staminaFillImage.fillAmount = currentStaminaFill;
            staminaFillImage.color = (currentStaminaFill > 0.25f) ? normalStaminaColor : lowStaminaColor;
        }

        if (staminaNumbersText != null)
        {
            staminaNumbersText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }
    }

    public void HandleStaminaInsufficient()
    {
        if (staminaFlashRoutine != null) StopCoroutine(staminaFlashRoutine);
        staminaFlashRoutine = StartCoroutine(StaminaInsufficientFlashRoutine());
    }

    private IEnumerator StaminaInsufficientFlashRoutine()
    {
        if (staminaFillImage != null)
        {
            staminaFillImage.color = insufficientStaminaFlashColor;
            yield return new WaitForSeconds(0.12f);
            staminaFillImage.color = (currentStaminaFill > 0.25f) ? normalStaminaColor : lowStaminaColor;
        }
        staminaFlashRoutine = null;
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
        // ── Catchup de Salud ──
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

        // ── Catchup de Stamina ──
        if (catchupStaminaDelayTimer > 0f)
        {
            catchupStaminaDelayTimer -= Time.deltaTime;
        }
        else if (catchupStaminaFill > currentStaminaFill)
        {
            catchupStaminaFill = Mathf.MoveTowards(catchupStaminaFill, currentStaminaFill, Time.deltaTime * (catchupSpeed / 80f));
            if (staminaCatchupImage != null)
            {
                staminaCatchupImage.fillAmount = catchupStaminaFill;
            }
        }
        else if (catchupStaminaFill < currentStaminaFill)
        {
            catchupStaminaFill = currentStaminaFill;
            if (staminaCatchupImage != null)
            {
                staminaCatchupImage.fillAmount = catchupStaminaFill;
            }
        }
    }
}
