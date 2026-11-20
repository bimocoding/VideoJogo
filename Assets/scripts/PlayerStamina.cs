using System;
using UnityEngine;

/// <summary>
/// Sistema modular de Stamina para el jugador.
/// Solo se consume en ataques y evasiones/dashes (Dodge, Backdodge, Slide, AirDash).
/// Incluye eventos desacoplados para HUD y multiplicadores preparados para el selector de dificultad.
/// </summary>
public class PlayerStamina : MonoBehaviour
{
    [Header("Capacidad y Regeneración")]
    public float maxStamina = 100f;
    [SerializeField] private float currentStamina = 100f;
    [Tooltip("Segundos de espera tras gastar stamina antes de empezar a regenerar.")]
    public float regenDelay = 0.65f;
    [Tooltip("Stamina recuperada por segundo.")]
    public float regenRate = 34f;

    [Header("Costos de Evasión / Dash")]
    public float dodgeCost = 18f;
    public float backdodgeCost = 18f;
    public float slideCost = 15f;
    public float airDashCost = 20f;

    [Header("Costos de Ataque")]
    public float lightAttackCost = 12f;      // Hit1, Hit2Basic, Hit2Var, Hit3Var
    public float mediumAttackCost = 16f;     // Hit4Lunge, JumpAttack
    public float heavyAttackCost = 22f;      // Hit3Basic, Hit5Launcher, DirectLauncher
    public float verticalFinisherCost = 28f; // Vertical Finisher Slam

    [Header("Multiplicadores de Dificultad (Runtime)")]
    [Tooltip("Modificado por el selector de dificultad (ej. Fácil 0.75, Normal 1.0, Difícil 1.25).")]
    public float costMultiplier = 1f;
    public float regenMultiplier = 1f;

    // Eventos desacoplados para el HUD medieval
    public static Action<float, float> OnStaminaChanged;
    public static Action OnStaminaInsufficient;

    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public float NormalizedStamina => maxStamina > 0.001f ? currentStamina / maxStamina : 0f;

    private float regenDelayTimer = 0f;
    private NewMonoBehaviourScript player;

    private void Awake()
    {
        player = GetComponent<NewMonoBehaviourScript>();
        currentStamina = maxStamina;
    }

    private void Start()
    {
        NotifyChanged();
    }

    private void Update()
    {
        if (player != null && player.isDead) return;

        // No regenerar en mitad de un ataque, esquive o dash aéreo
        bool actionConsumingActive = player != null &&
            (player.IsInCombatAction || player.IsInEvasiveAction || player.isAirDashing);

        if (actionConsumingActive) return;

        if (regenDelayTimer > 0f)
        {
            regenDelayTimer -= Time.deltaTime;
            return;
        }

        if (currentStamina < maxStamina)
        {
            currentStamina = Mathf.Min(maxStamina, currentStamina + regenRate * regenMultiplier * Time.deltaTime);
            NotifyChanged();
        }
    }

    public float GetEffectiveCost(float baseCost)
    {
        return Mathf.Max(0f, baseCost * costMultiplier);
    }

    public bool CanAfford(float baseCost)
    {
        return currentStamina >= GetEffectiveCost(baseCost);
    }

    /// <summary>
    /// Intenta consumir stamina. Devuelve true si alcanza; si no, dispara evento de feedback y devuelve false.
    /// </summary>
    public bool TryConsume(float baseCost)
    {
        float effectiveCost = GetEffectiveCost(baseCost);
        if (currentStamina < effectiveCost)
        {
            OnStaminaInsufficient?.Invoke();
            return false;
        }

        currentStamina = Mathf.Max(0f, currentStamina - effectiveCost);
        regenDelayTimer = regenDelay;
        NotifyChanged();
        return true;
    }

    public void RestoreFull()
    {
        currentStamina = maxStamina;
        regenDelayTimer = 0f;
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        OnStaminaChanged?.Invoke(currentStamina, maxStamina);
    }
}
