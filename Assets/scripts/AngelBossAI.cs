using System.Collections;
using UnityEngine;

/// <summary>
/// Jefe final: Ángel. Vuela sobre la arena y se lanza en picada hacia donde VAS a estar (predice tu movimiento).
/// Al estrellarse lanza una onda de choque por el suelo que hay que saltar. Tocar su cuerpo hace daño.
/// Con menos de la mitad de vida encadena dos picadas y dispara abanicos de bolas de fuego.
/// No tiene retroceso (HitReceiver.isBoss).
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
    public float hoverHeight = 3.0f;             // altura sobre el suelo mientras planea
    public float hoverSpeed = 2.6f;
    public float bobAmplitude = 0.15f;
    public float bobFrequency = 2f;

    [Header("Picada")]
    public float diveInterval = 2.2f;
    public float diveTelegraph = 0.4f;
    public float diveSpeed = 14f;
    public float diveLeadTime = 0.3f;            // apunta a donde estará el jugador dentro de este tiempo
    public float diveHoming = 0.35f;             // fracción de la picada en la que aún corrige el rumbo
    public float diveRecover = 0.55f;
    public int diveDamage = 25;

    [Header("Onda de choque al estrellarse")]
    public int shockwaveDamage = 15;
    public float shockwaveRadius = 2.2f;         // distancia horizontal: hay que estar saltando para esquivarla

    [Header("Daño por contacto")]
    public int contactDamage = 15;

    [Header("Fase 2: bolas de fuego y picada doble")]
    public Sprite[] fireballFrames;
    public int fireballDamage = 14;
    public float fireballSpeed = 3.6f;
    public int fireballsPerVolley = 5;
    public float volleySpread = 30f;

    public bool IsActive { get; private set; }

    private SpriteFrameAnimator animator;
    private SpriteRenderer sr;
    private Rigidbody2D rb;
    private Collider2D body;
    private HitReceiver health;
    private NewMonoBehaviourScript playerComp;
    private Rigidbody2D playerRb;
    private float actionTimer;
    private bool busy;
    private bool useVolleyNext;
    private float gruntCooldown;

    private bool Phase2 => health != null && health.currentHealth <= health.maxHealth / 2;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        body = GetComponent<Collider2D>();
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
        if (targetPlayer != null)
        {
            playerComp = targetPlayer.GetComponent<NewMonoBehaviourScript>();
            playerRb = targetPlayer.GetComponent<Rigidbody2D>();
        }
        animator.Play(idleFrames, idleFps, true);
        actionTimer = diveInterval;
        BossMusic.Prewarm(); // generar la música antes de que empiece el combate (evita un tirón)
    }

    public void OnStunned(float duration) { } // el jefe no se aturde

    // Lo llama HitReceiver cuando el jugador lo golpea
    public void OnHurt()
    {
        if (gruntCooldown > 0f || health == null || health.currentHealth <= 0) return;
        BossSfx.Play(BossSfx.Sound.Grunt);
        gruntCooldown = 0.7f;
    }

    // HitReceiver desactiva la IA al morir: rugido final. La cámara y la música se liberan
    // al terminar la secuencia de explosiones (HitReceiver.BossDeathRoutine)
    void OnDisable()
    {
        if (IsActive && health != null && health.currentHealth <= 0) BossSfx.Play(BossSfx.Sound.DeathRoar);
    }

    void Update()
    {
        if (gruntCooldown > 0f) gruntCooldown -= Time.deltaTime;
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
                camara.Shake(0.25f, 0.6f);
                if (cameraArea.width > 0f) camara.LockToArea(cameraArea);
                BossMusic.Play();
                BossSfx.Play(BossSfx.Sound.Roar);
                actionTimer = 1.4f; // deja escuchar el rugido antes del primer ataque
            }
            return;
        }

        CheckContactDamage();
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
            if (Phase2 && useVolleyNext) StartCoroutine(VolleyRoutine());
            else StartCoroutine(DiveSequence());
            useVolleyNext = Phase2 && !useVolleyNext;
        }
    }

    private void FacePlayer()
    {
        if (targetPlayer != null) sr.flipX = targetPlayer.position.x < transform.position.x;
    }

    // Tocar el cuerpo del jefe hace daño (el jugador tiene invulnerabilidad tras cada golpe)
    private void CheckContactDamage()
    {
        if (body == null || playerComp == null || playerComp.IsInvulnerable) return;
        var b = body.bounds;
        foreach (var c in Physics2D.OverlapBoxAll(b.center, b.size, 0f))
        {
            if (c.GetComponentInParent<NewMonoBehaviourScript>() == playerComp)
            {
                playerComp.TakeDamage(contactDamage, false, transform);
                return;
            }
        }
    }

    private float FeetOffset => body != null ? rb.position.y - body.bounds.min.y : 0.6f;

    private Vector2 PredictPlayer()
    {
        Vector2 p = targetPlayer.position;
        if (playerRb != null) p.x += playerRb.linearVelocity.x * diveLeadTime;
        p.x = Mathf.Clamp(p.x, arenaMinX, arenaMaxX);
        return new Vector2(p.x, arenaFloorY + FeetOffset);
    }

    private IEnumerator DiveSequence()
    {
        busy = true;
        int dives = Phase2 ? 2 : 1;
        for (int i = 0; i < dives; i++)
        {
            yield return DiveRoutine(i > 0);
            if (i < dives - 1)
            {
                // Rebote rápido hacia arriba antes de la segunda picada
                Vector2 up = new Vector2(rb.position.x, arenaFloorY + hoverHeight * 0.8f);
                while (Vector2.Distance(rb.position, up) > 0.1f)
                {
                    rb.MovePosition(Vector2.MoveTowards(rb.position, up, diveSpeed * 0.6f * Time.deltaTime));
                    CheckContactDamage();
                    yield return null;
                }
            }
        }
        animator.Play(idleFrames, idleFps, true);
        actionTimer = diveInterval * (Phase2 ? 0.8f : 1f);
        busy = false;
    }

    private IEnumerator DiveRoutine(bool quick)
    {
        // Aviso: chillido + parpadeo (más corto en la segunda picada)
        BossSfx.Play(BossSfx.Sound.Screech);
        float telegraph = quick ? diveTelegraph * 0.6f : diveTelegraph;
        for (float t = 0f; t < telegraph; t += Time.deltaTime)
        {
            sr.color = (Mathf.FloorToInt(t * 16f) % 2 == 0) ? new Color(1f, 0.55f, 0.55f) : Color.white;
            FacePlayer();
            yield return null;
        }
        sr.color = Color.white;

        animator.Play(attackFrames, attackFps, false);
        Vector2 start = rb.position;
        Vector2 target = PredictPlayer();
        float totalDist = Vector2.Distance(start, target);
        bool hit = false;

        // Picada: corrige el rumbo al principio y luego va en línea recta
        float floorCenterY = arenaFloorY + FeetOffset;
        while (rb.position.y > floorCenterY + 0.02f && Vector2.Distance(rb.position, target) > 0.05f)
        {
            float travelled = Vector2.Distance(start, rb.position);
            if (travelled < totalDist * diveHoming) target = PredictPlayer();

            rb.MovePosition(Vector2.MoveTowards(rb.position, target, diveSpeed * Time.deltaTime));
            if (!hit && body != null && playerComp != null && !playerComp.IsInvulnerable)
            {
                var b = body.bounds;
                b.Expand(0.15f);
                if (b.Contains(targetPlayer.position + Vector3.up * 0.35f))
                {
                    playerComp.TakeDamage(diveDamage, false, transform);
                    hit = true;
                }
            }
            yield return null;
        }

        // Estruendo + onda de choque por el suelo
        BossSfx.Play(BossSfx.Sound.Slam);
        camara.Shake(0.3f, 0.35f);
        // La onda recorre el suelo de la arena: en una plataforma estás a salvo
        bool playerGrounded = playerComp != null && playerComp.IsGrounded && targetPlayer.position.y < arenaFloorY + 0.3f;
        if (!hit && playerGrounded && Mathf.Abs(targetPlayer.position.x - rb.position.x) < shockwaveRadius)
            playerComp.TakeDamage(shockwaveDamage, false, transform);

        // Ventana para castigarlo en el suelo
        for (float t = 0f; t < diveRecover; t += Time.deltaTime)
        {
            CheckContactDamage();
            yield return null;
        }
    }

    private IEnumerator VolleyRoutine()
    {
        busy = true;
        BossSfx.Play(BossSfx.Sound.Roar);
        animator.Play(attackFrames, attackFps, false);
        yield return new WaitForSeconds(0.35f);

        for (int wave = 0; wave < 2; wave++)
        {
            Vector2 origin = (body != null ? (Vector2)body.bounds.center : rb.position);
            Vector2 baseDir = ((Vector2)targetPlayer.position + Vector2.up * 0.35f) - origin;
            float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
            for (int i = 0; i < fireballsPerVolley; i++)
            {
                float offset = (fireballsPerVolley == 1) ? 0f : Mathf.Lerp(-volleySpread, volleySpread, i / (float)(fireballsPerVolley - 1));
                float a = (baseAngle + offset) * Mathf.Deg2Rad;
                WizardAI.CreateFireball(origin, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), fireballSpeed, fireballDamage, null, fireballFrames);
            }
            yield return new WaitForSeconds(0.45f);
        }

        animator.Play(idleFrames, idleFps, true);
        actionTimer = diveInterval * 0.8f;
        busy = false;
    }
}
