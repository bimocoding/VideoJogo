using System.Collections;
using UnityEngine;

// IA de enemigo que puede ser aturdida por un golpe (GhoulAI, WizardAI, AngelBossAI)
public interface IStunnable
{
    void OnStunned(float duration);
}

// Daño de la explosión de fuego al morir: llega con un pequeño retraso para que el jugador pueda alejarse
public class DelayedExplosion : MonoBehaviour
{
    public int damage = 15;
    public float radius = 0.8f;
    public float delay = 0.3f;

    IEnumerator Start()
    {
        yield return new WaitForSeconds(delay);
        foreach (var c in Physics2D.OverlapCircleAll(transform.position, radius))
        {
            var p = c.GetComponentInParent<NewMonoBehaviourScript>();
            if (p != null) { p.TakeDamage(damage, false, transform); break; }
        }
    }
}

public class HitReceiver : MonoBehaviour
{
    [Header("Jefe (sin retroceso ni aturdimiento)")]
    public bool isBoss = false;

    [Header("Salud y Daño")]
    public int maxHealth = 100;
    public int currentHealth = 100;
    public int damagePerHit = 25; // 25 de daño fijo por cada ataque
    public bool isImmortal = false;

    [Header("Retroalimentación Visual")]
    public float flashDuration = 0.12f;
    public Color hitFlashColor = new Color(1f, 0.25f, 0.25f, 1f);
    public bool enableHitSquash = true;
    public Vector2 squashScale = new Vector2(1.25f, 0.8f);

    [Header("Físicas de Retroceso Ligero (Knockback)")]
    public float baseKnockbackForce = 4.5f;   // Empuje horizontal al recibir un golpe
    public float verticalKnockback = 1.5f;    // Pequeño salto para que el retroceso se lea bien
    public float finisherKnockbackMultiplier = 1.6f; // El tercer golpe del combo empuja más
    public float groundDamping = 8.0f;       // Fricción para frenado suave tras el empuje

    private SpriteRenderer sr;
    private Color originalColor = Color.white;
    private float flashTimer = 0f;
    private Vector3 originalScale = Vector3.one;
    private Rigidbody2D rb;
    private Collider2D myCollider;
    private bool isDead = false;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) originalColor = sr.color;
        originalScale = transform.localScale;
        rb = GetComponent<Rigidbody2D>();
        myCollider = GetComponent<Collider2D>();
        if (rb != null)
        {
            rb.linearDamping = groundDamping;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
        currentHealth = maxHealth;
        isImmortal = false;
    }

    void Update()
    {
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f && sr != null && !isDead)
            {
                sr.color = originalColor;
            }
        }

        if (enableHitSquash && !isDead)
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

        // Cada ataque reduce exactamente 25 de vida
        int damage = damagePerHit;
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

        // Notificar aturdimiento a la IA (los jefes no se aturden)
        if (!isBoss)
        {
            foreach (var stunnable in GetComponents<IStunnable>()) stunnable.OnStunned(0.75f);
        }

        // Empuje físico hacia atrás respetando bordes para no caer al vacío (los jefes no retroceden)
        if (rb != null && !isBoss && rb.bodyType == RigidbodyType2D.Dynamic)
        {
            float pushDir = 0f;

            if (attackDirection.sqrMagnitude > 0.001f)
            {
                pushDir = Mathf.Sign(attackDirection.x);
            }
            else if (attackerPos != Vector2.zero)
            {
                pushDir = (transform.position.x >= attackerPos.x) ? 1f : -1f;
            }
            else
            {
                pushDir = (transform.position.x >= hitPoint.x) ? 1f : -1f;
            }

            // Comprobar si el empuje causaría que caiga al vacío
            if (WouldKnockbackCauseFall(pushDir))
            {
                // Frenar empuje en el borde para protegerlo de caerse
                pushDir = 0f;
            }

            float push = baseKnockbackForce * (comboStep >= 3 ? finisherKnockbackMultiplier : 1f);
            float finalPushSpeed = pushDir * push;
            float vertForce = (customVerticalKnockback > 0f) ? customVerticalKnockback : (pushDir != 0f ? verticalKnockback : 0f);

            rb.linearDamping = groundDamping;
            rb.linearVelocity = new Vector2(finalPushSpeed, vertForce);

            Debug.Log($"<color=orange>[HIT RECEIVER]</color> {gameObject.name} recibió golpe (-{damage} HP). Vida restante: {currentHealth}/{maxHealth}");
        }

        if (currentHealth <= 0)
        {
            currentHealth = 0;
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

    /// <summary>
    /// Comprueba si empujar al enemigo en pushDir lo lanzaría al vacío
    /// </summary>
    private bool WouldKnockbackCauseFall(float pushDir)
    {
        if (Mathf.Abs(pushDir) < 0.01f) return false;
        float bottomY = myCollider != null ? myCollider.bounds.min.y : transform.position.y;
        Vector2 checkStart = new Vector2(transform.position.x + Mathf.Sign(pushDir) * 0.45f, bottomY + 0.15f);
        LayerMask floorMask = LayerMask.GetMask("Floor", "Ground", "Default");
        if (floorMask == 0) floorMask = (1 << 3) | (1 << 0);
        
        var hits = Physics2D.RaycastAll(checkStart, Vector2.down, 0.65f, floorMask);
        foreach (var h in hits)
        {
            if (h.collider != null && h.collider.gameObject != gameObject && !h.collider.isTrigger)
            {
                return false; // Hay suelo firme debajo
            }
        }
        return true; // Precipicio detectado
    }

    private IEnumerator RespawnRoutine()
    {
        isDead = true;
        yield return new WaitForSeconds(1.0f);
        currentHealth = maxHealth;
        isDead = false;
        if (sr != null) sr.color = originalColor;
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        // Desactivar colisiones para que no estorbe al personaje
        if (myCollider != null) myCollider.enabled = false;
        var cols = GetComponentsInChildren<Collider2D>();
        foreach (var c in cols) c.enabled = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        foreach (var stunnable in GetComponents<IStunnable>())
            if (stunnable is MonoBehaviour mb) mb.enabled = false;

        if (isBoss) camara.Shake(0.4f, 0.6f);
        SpawnDeathFx();

        StartCoroutine(DeathSequence());
    }

    [Header("Efecto de Muerte")]
    public Sprite[] deathFxFrames;            // explosión de fuego "enemy-death" (solo enemigos de fuego)
    public float deathFxFps = 18f;
    public int explosionDamage = 15;          // la explosión daña al jugador si está cerca
    public float explosionRadius = 0.8f;

    private void SpawnDeathFx()
    {
        if (deathFxFrames == null || deathFxFrames.Length == 0) return;
        var fx = new GameObject("EnemyDeathFx");
        // La explosión nace a la altura del cuerpo (el collider del ghoul solo cubre sus pies)
        Vector3 pos = transform.position + Vector3.up * 0.45f;
        fx.transform.position = pos;
        fx.transform.localScale = Vector3.one * (isBoss ? 2f : 1.2f);

        camara.Shake(0.12f, 0.2f);
        if (explosionDamage > 0)
        {
            var blast = fx.AddComponent<DelayedExplosion>();
            blast.damage = explosionDamage;
            blast.radius = explosionRadius;
        }
        var fxSr = fx.AddComponent<SpriteRenderer>();
        fxSr.sortingOrder = 10;
        var anim = fx.AddComponent<SpriteFrameAnimator>();
        anim.frames = deathFxFrames;
        anim.fps = deathFxFps;
        anim.loop = false;
        anim.destroyOnFinish = true;
    }

    private IEnumerator DeathSequence()
    {
        if (sr != null) sr.color = hitFlashColor;

        float elapsed = 0f;
        float fadeTime = 0.35f;
        Vector3 startScale = transform.localScale;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeTime;
            if (sr != null)
            {
                Color c = hitFlashColor;
                c.a = Mathf.Lerp(1f, 0f, t);
                sr.color = c;
            }
            transform.localScale = Vector3.Lerp(startScale, startScale * 0.3f, t);
            yield return null;
        }

        Destroy(gameObject);
    }
}
