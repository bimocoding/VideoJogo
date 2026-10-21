using System.Collections;
using UnityEngine;

public class HitReceiver : MonoBehaviour
{
    [Header("Salud y Modo Dummy")]
    public int maxHealth = 100;
    public int currentHealth = 100;
    public bool isImmortal = true; // Permite golpear infinitamente para pruebas
    public float respawnDelay = 1.0f;

    [Header("Retroalimentación Visual")]
    public float flashDuration = 0.12f;
    public Color hitFlashColor = new Color(1f, 0.25f, 0.25f, 1f);
    public bool enableHitSquash = true;
    public Vector2 squashScale = new Vector2(1.25f, 0.8f);

    [Header("Físicas de Retroceso Ligero (Knockback)")]
    public float baseKnockbackForce = 2.2f;   // Empuje ligero horizontal
    public float verticalKnockback = 0.0f;     // Sin elevación vertical para mantenerlo en el suelo
    public float groundDamping = 8.0f;        // Fricción para que frene suavemente tras el empuje

    private SpriteRenderer sr;
    private Color originalColor = Color.white;
    private float flashTimer = 0f;
    private Vector3 originalScale = Vector3.one;
    private Rigidbody2D rb;
    private bool isDead = false;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) originalColor = sr.color;
        originalScale = transform.localScale;
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearDamping = groundDamping;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
        currentHealth = maxHealth;
    }

    void Update()
    {
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f && sr != null)
            {
                sr.color = originalColor;
            }
        }

        if (enableHitSquash)
        {
            transform.localScale = Vector3.MoveTowards(transform.localScale, originalScale, 6f * Time.deltaTime);
        }
    }

    public void OnHit(Vector2 hitPoint, int comboStep)
    {
        OnHit(hitPoint, comboStep, Vector2.zero, Vector2.zero);
    }

    public void OnHit(Vector2 hitPoint, int comboStep, Vector2 attackerPos, Vector2 attackDirection, float customVerticalKnockback = 0f)
    {
        if (isDead) return;

        int damage = comboStep * 15; // 15 para atk1, 30 para atk2, 45 para atk3
        currentHealth -= damage;

        // Flash visual de impacto
        if (sr != null)
        {
            sr.color = hitFlashColor;
            flashTimer = flashDuration;
        }

        // Squash reactivo de impacto
        if (enableHitSquash)
        {
            transform.localScale = new Vector3(originalScale.x * squashScale.x, originalScale.y * squashScale.y, originalScale.z);
        }

        // Notificar aturdimiento a componentes de IA (GhoulAI u otros) para que no pueda golpear durante el combo
        var ghoulAI = GetComponent<GhoulAI>();
        if (ghoulAI != null)
        {
            ghoulAI.OnStunned(0.75f);
        }

        // Empuje físico ligero hacia atrás según el lado desde el que se golpea
        if (rb != null)
        {
            float pushDir = 0f;

            // 1. Si se provee la dirección del ataque del PJ
            if (attackDirection.sqrMagnitude > 0.001f)
            {
                pushDir = Mathf.Sign(attackDirection.x);
            }
            // 2. Si se provee la posición del atacante
            else if (attackerPos != Vector2.zero)
            {
                pushDir = (transform.position.x >= attackerPos.x) ? 1f : -1f;
            }
            // 3. Fallback con hitPoint
            else
            {
                pushDir = (transform.position.x >= hitPoint.x) ? 1f : -1f;
            }

            // Multiplicador según el golpe del combo
            float comboBonus = 1f + ((comboStep - 1) * 0.2f);
            float finalPushSpeed = pushDir * baseKnockbackForce * comboBonus;
            float vertForce = (customVerticalKnockback > 0f) ? customVerticalKnockback : verticalKnockback;

            // Aplicar velocidad directa con amortiguación
            rb.linearDamping = groundDamping;
            rb.linearVelocity = new Vector2(finalPushSpeed, vertForce);

            Debug.Log($"<color=orange>[HIT RECEIVER]</color> {gameObject.name} golpeado desde {(pushDir > 0 ? "izquierda" : "derecha")}, empujado hacia {(pushDir > 0 ? "DERECHA" : "IZQUIERDA")} (vel: {finalPushSpeed:F2}, vertical: {vertForce:F2}). HP: {currentHealth}/{maxHealth}");
        }

        if (currentHealth <= 0)
        {
            if (isImmortal)
            {
                StartCoroutine(RespawnRoutine());
            }
            else
            {
                Die();
            }
        }
    }

    private IEnumerator RespawnRoutine()
    {
        isDead = true;
        yield return new WaitForSeconds(respawnDelay);
        currentHealth = maxHealth;
        isDead = false;
        if (sr != null) sr.color = originalColor;
        Debug.Log($"<color=green>[HIT RECEIVER]</color> {gameObject.name} restauró salud a {maxHealth} HP para seguir testeando.");
    }

    private void Die()
    {
        isDead = true;
        Destroy(gameObject, 0.2f);
    }
}
