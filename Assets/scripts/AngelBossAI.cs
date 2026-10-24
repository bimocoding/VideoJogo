using System.Collections;
using UnityEngine;

/// <summary>
/// Jefe final: Ángel. Vuela sobre la arena, avisa con un destello y se lanza en picada hacia el jugador.
/// Con menos de la mitad de vida además dispara abanicos de bolas de fuego. No tiene retroceso (HitReceiver.isBoss).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class AngelBossAI : MonoBehaviour, IStunnable
{
    [Header("Objetivo y Arena")]
    public Transform targetPlayer;
    public float arenaMinX;
    public float arenaMaxX;
    public float arenaFloorY;                    // altura del suelo de la arena
    public float activationDistance = 7f;
    public Rect cameraArea;                      // rectángulo que la cámara muestra fijo durante el combate

    [Header("Animación (fotogramas)")]
    public Sprite[] idleFrames;
    public Sprite[] attackFrames;
    public float idleFps = 10f;
    public float attackFps = 10f;

    [Header("Vuelo")]
    public float hoverHeight = 2.6f;             // altura sobre el suelo mientras planea
    public float hoverSpeed = 2.2f;
    public float bobAmplitude = 0.15f;
    public float bobFrequency = 2f;

    [Header("Picada")]
    public float diveInterval = 2.6f;
    public float diveTelegraph = 0.55f;
    public float diveSpeed = 14f;
    public float diveRecover = 0.5f;
    public int contactDamage = 20;
    public float contactRadius = 0.45f;

    [Header("Fase 2: bolas de fuego")]
    public Sprite[] fireballFrames;
    public int fireballDamage = 14;
    public float fireballSpeed = 3.2f;
    public int fireballsPerVolley = 3;
    public float volleySpread = 22f;

    public bool IsActive { get; private set; }

    private SpriteFrameAnimator animator;
    private SpriteRenderer sr;
    private Rigidbody2D rb;
    private HitReceiver health;
    private NewMonoBehaviourScript playerComp;
    private float actionTimer;
    private bool busy;
    private bool useVolleyNext;
    private float baseFacingScale = 1f;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        health = GetComponent<HitReceiver>();
        animator = GetComponent<SpriteFrameAnimator>();
        if (animator == null) animator = gameObject.AddComponent<SpriteFrameAnimator>();
    }

    void Start()
    {
        if (targetPlayer == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) targetPlayer = p.transform;
        }
        if (targetPlayer != null) playerComp = targetPlayer.GetComponent<NewMonoBehaviourScript>();
        animator.Play(idleFrames, idleFps, true);
        actionTimer = diveInterval;
        BossMusic.Prewarm(); // generar la música antes de que empiece el combate (evita un tirón)
    }

    public void OnStunned(float duration) { } // el jefe no se aturde

    // HitReceiver desactiva la IA al morir: liberar la cámara y apagar la música
    void OnDisable()
    {
        if (!IsActive) return;
        camara.Unlock();
        BossMusic.Stop();
    }

    void Update()
    {
        if (targetPlayer == null) return;
        if (playerComp != null && playerComp.isDead)
        {
            if (IsActive) BossMusic.Stop();
            return;
        }

        if (!IsActive)
        {
            bool inArena = cameraArea.width > 0f
                ? targetPlayer.position.x > cameraArea.xMin + 0.8f
                : Vector2.Distance(transform.position, targetPlayer.position) < activationDistance;
            if (inArena)
            {
                IsActive = true;
                camara.Shake(0.2f, 0.4f);
                if (cameraArea.width > 0f) camara.LockToArea(cameraArea);
                BossMusic.Play();
            }
            return;
        }

        if (busy) return;

        // Planear sobre el jugador dentro de la arena
        float targetX = Mathf.Clamp(targetPlayer.position.x, arenaMinX, arenaMaxX);
        float targetY = arenaFloorY + hoverHeight + Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
        Vector2 next = Vector2.MoveTowards(rb.position, new Vector2(targetX, targetY), hoverSpeed * Time.deltaTime);
        rb.MovePosition(next);
        FacePlayer();

        actionTimer -= Time.deltaTime;
        if (actionTimer <= 0f)
        {
            bool phase2 = health != null && health.currentHealth <= health.maxHealth / 2;
            if (phase2 && useVolleyNext) StartCoroutine(VolleyRoutine());
            else StartCoroutine(DiveRoutine());
            useVolleyNext = phase2 && !useVolleyNext;
        }
    }

    private void FacePlayer()
    {
        if (targetPlayer != null) sr.flipX = targetPlayer.position.x < transform.position.x;
    }

    private IEnumerator DiveRoutine()
    {
        busy = true;

        // Aviso: parpadeo antes de lanzarse
        for (float t = 0f; t < diveTelegraph; t += Time.deltaTime)
        {
            sr.color = (Mathf.FloorToInt(t * 16f) % 2 == 0) ? new Color(1f, 0.6f, 0.6f) : Color.white;
            yield return null;
        }
        sr.color = Color.white;

        animator.Play(attackFrames, attackFps, false);
        Vector2 target = new Vector2(Mathf.Clamp(targetPlayer.position.x, arenaMinX, arenaMaxX), arenaFloorY + 0.4f);
        Vector2 dir = (target - rb.position).normalized;
        bool hit = false;

        // Picada en línea recta hasta el punto marcado o el suelo
        while (rb.position.y > arenaFloorY + 0.4f && Vector2.Distance(rb.position, target) > 0.1f)
        {
            rb.MovePosition(Vector2.MoveTowards(rb.position, target, diveSpeed * Time.deltaTime));
            if (!hit && Vector2.Distance(rb.position + Vector2.up * 0.3f, (Vector2)targetPlayer.position + Vector2.up * 0.35f) < contactRadius + 0.3f)
            {
                playerComp?.TakeDamage(contactDamage, false, transform);
                hit = true;
            }
            yield return null;
        }

        camara.Shake(0.15f, 0.2f);
        yield return new WaitForSeconds(diveRecover); // ventana para castigarlo en el suelo

        animator.Play(idleFrames, idleFps, true);
        actionTimer = diveInterval;
        busy = false;
    }

    private IEnumerator VolleyRoutine()
    {
        busy = true;
        animator.Play(attackFrames, attackFps, false);
        yield return new WaitForSeconds(0.25f);

        Vector2 origin = rb.position + Vector2.up * 0.3f;
        Vector2 baseDir = ((Vector2)targetPlayer.position + Vector2.up * 0.35f) - origin;
        float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
        for (int i = 0; i < fireballsPerVolley; i++)
        {
            float offset = (fireballsPerVolley == 1) ? 0f : Mathf.Lerp(-volleySpread, volleySpread, i / (float)(fireballsPerVolley - 1));
            float a = (baseAngle + offset) * Mathf.Deg2Rad;
            WizardAI.CreateFireball(origin, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), fireballSpeed, fireballDamage, null, fireballFrames);
        }

        yield return new WaitForSeconds(0.5f);
        animator.Play(idleFrames, idleFps, true);
        actionTimer = diveInterval * 0.8f;
        busy = false;
    }
}
