using System.Collections;
using UnityEngine;

/// <summary>
/// Mago: enemigo a distancia. Se queda en su puesto, mira al jugador y le lanza bolas de fuego
/// cuando lo tiene a la vista. Si el jugador se le acerca mucho, se teletransporta hacia atrás.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class WizardAI : MonoBehaviour, IStunnable
{
    [Header("Objetivo")]
    public Transform targetPlayer;

    [Header("Animación (fotogramas)")]
    public Sprite[] idleFrames;
    public Sprite[] castFrames;
    public float idleFps = 8f;
    public float castFps = 12f;
    public int castReleaseFrame = 6;            // fotograma en que sale la bola de fuego

    [Header("Disparo")]
    public GameObject fireballPrefab;           // si está vacío se construye uno con fireballFrames
    public Sprite[] fireballFrames;
    public float attackRange = 7f;
    public float verticalRange = 3f;
    public float castCooldown = 2.4f;
    public float fireballSpeed = 3.5f;
    public int fireballDamage = 12;
    public Vector2 castOffset = new Vector2(0.25f, 0.35f);

    [Header("Teletransporte por su plataforma")]
    public float minDistanceFromPlayer = 2.0f;  // nunca aparece a menos de esta distancia del jugador
    public float teleportInterval = 4.5f;       // se reposiciona cada cierto tiempo mientras te ve
    public float panicDistance = 1.3f;          // si te acercas tanto, se teletransporta lejos de ti
    public float blinkCooldown = 2f;
    public LayerMask groundLayer;

    [Header("Aturdimiento")]
    public float hitStunDuration = 0.6f;

    private SpriteFrameAnimator animator;
    private SpriteRenderer sr;
    private Rigidbody2D rb;
    private NewMonoBehaviourScript playerComp;
    private float castTimer;
    private float blinkTimer;
    private float teleportTimer;
    private float stunTimer;
    private Vector2 homePos;
    private bool isCasting;
    private Coroutine castRoutine;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
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
        if (groundLayer == 0) groundLayer = LayerMask.GetMask("Floor");
        castTimer = Random.Range(0.5f, castCooldown);
        teleportTimer = teleportInterval;
        homePos = transform.position;
        animator.Play(idleFrames, idleFps, true);
    }

    public void OnStunned(float duration)
    {
        stunTimer = hitStunDuration;
        if (castRoutine != null) { StopCoroutine(castRoutine); castRoutine = null; }
        isCasting = false;
        animator.Play(idleFrames, idleFps, true);
        castTimer = Mathf.Max(castTimer, 0.8f);
    }

    void Update()
    {
        if (castTimer > 0f) castTimer -= Time.deltaTime;
        if (blinkTimer > 0f) blinkTimer -= Time.deltaTime;
        if (stunTimer > 0f) { stunTimer -= Time.deltaTime; return; }
        if (targetPlayer == null || isCasting) return;
        if (playerComp != null && playerComp.isDead) return;

        Vector2 toPlayer = targetPlayer.position - transform.position;
        if (Mathf.Abs(toPlayer.x) > 0.05f) sr.flipX = toPlayer.x < 0f; // el sprite lanza con la mano derecha

        if (Mathf.Abs(toPlayer.x) > attackRange || Mathf.Abs(toPlayer.y) > verticalRange) return;

        if (toPlayer.magnitude < panicDistance && blinkTimer <= 0f)
        {
            TryTeleport(true);
            return;
        }

        teleportTimer -= Time.deltaTime;
        if (teleportTimer <= 0f && blinkTimer <= 0f)
        {
            teleportTimer = teleportInterval + Random.Range(-1f, 1f);
            TryTeleport(false);
            return;
        }

        if (castTimer <= 0f && HasLineOfSight()) castRoutine = StartCoroutine(CastRoutine());
    }

    private bool HasLineOfSight()
    {
        Vector2 eye = (Vector2)transform.position + Vector2.up * castOffset.y;
        Vector2 target = (Vector2)targetPlayer.position + Vector2.up * 0.35f;
        return Physics2D.Linecast(eye, target, groundLayer).collider == null;
    }

    // Se teletransporta a cualquier punto de SU plataforma que esté dentro de la cámara,
    // nunca pegado al jugador; si huye, elige el punto más lejano al jugador
    private void TryTeleport(bool fleeFromPlayer)
    {
        blinkTimer = blinkCooldown;
        var col = GetComponent<Collider2D>();
        float feetY = col.bounds.min.y;
        float feetOffset = transform.position.y - feetY;

        // Bordes de la plataforma actual: avanzar mientras haya suelo a la misma altura y ningún muro
        float left = ScanPlatformEdge(-1f, feetY);
        float right = ScanPlatformEdge(1f, feetY);

        // Limitar a lo que ve la cámara
        var cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            float halfW = cam.orthographicSize * cam.aspect - 0.4f;
            left = Mathf.Max(left, cam.transform.position.x - halfW);
            right = Mathf.Min(right, cam.transform.position.x + halfW);
        }
        if (right - left < 0.6f) return;

        float bestX = 0f, bestScore = float.MinValue;
        for (int i = 0; i < 10; i++)
        {
            float x = Random.Range(left, right);
            if (Mathf.Abs(x - transform.position.x) < 0.6f) continue; // que se note el salto
            float distToPlayer = targetPlayer != null ? Mathf.Abs(x - targetPlayer.position.x) : 99f;
            if (distToPlayer < minDistanceFromPlayer) continue;       // nunca aparecer delante del jugador
            float score = fleeFromPlayer ? distToPlayer : Random.value;
            if (score > bestScore) { bestScore = score; bestX = x; }
        }

        if (bestScore > float.MinValue) StartCoroutine(BlinkRoutine(new Vector2(bestX, feetY + feetOffset)));
    }

    private float ScanPlatformEdge(float dir, float feetY)
    {
        const float step = 0.16f;
        float x = transform.position.x;
        for (int i = 0; i < 60; i++)
        {
            float next = x + dir * step;
            bool ground = Physics2D.Raycast(new Vector2(next, feetY + 0.05f), Vector2.down, 0.15f, groundLayer).collider != null;
            bool wall = Physics2D.OverlapCircle(new Vector2(next, feetY + 0.35f), 0.12f, groundLayer) != null;
            if (!ground || wall) break;
            x = next;
        }
        return x - dir * 0.15f; // margen para no quedar al borde
    }

    private IEnumerator BlinkRoutine(Vector2 dest)
    {
        isCasting = true;
        for (float a = 1f; a > 0f; a -= Time.deltaTime * 6f) { sr.color = new Color(1f, 1f, 1f, a); yield return null; }
        transform.position = dest;
        rb.linearVelocity = Vector2.zero;
        for (float a = 0f; a < 1f; a += Time.deltaTime * 6f) { sr.color = new Color(1f, 1f, 1f, a); yield return null; }
        sr.color = Color.white;
        isCasting = false;
        castTimer = Mathf.Min(castTimer, 0.4f); // contraataca rápido tras alejarse
    }

    private IEnumerator CastRoutine()
    {
        isCasting = true;
        castTimer = castCooldown;
        animator.Play(castFrames, castFps, false);

        while (animator.CurrentFrame < castReleaseFrame && !animator.IsFinished) yield return null;
        SpawnFireball();
        while (!animator.IsFinished) yield return null;

        animator.Play(idleFrames, idleFps, true);
        isCasting = false;
        castRoutine = null;
    }

    private void SpawnFireball()
    {
        if (targetPlayer == null) return;
        float side = sr.flipX ? -1f : 1f;
        Vector2 origin = (Vector2)transform.position + new Vector2(castOffset.x * side, castOffset.y);
        Vector2 dir = ((Vector2)targetPlayer.position + Vector2.up * 0.35f) - origin;
        CreateFireball(origin, dir, fireballSpeed, fireballDamage, fireballPrefab, fireballFrames);
    }

    // Compartido con el jefe: crea una bola de fuego lista para volar
    public static void CreateFireball(Vector2 origin, Vector2 dir, float speed, int damage, GameObject prefab, Sprite[] frames)
    {
        GameObject go;
        if (prefab != null) go = Instantiate(prefab, origin, Quaternion.identity);
        else
        {
            go = new GameObject("Fireball");
            go.transform.position = origin;
            go.tag = "Enemy";
            var fsr = go.AddComponent<SpriteRenderer>();
            fsr.sortingOrder = 5;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.09f;
            if (frames != null && frames.Length > 0)
            {
                var fa = go.AddComponent<SpriteFrameAnimator>();
                fa.frames = frames;
                fa.fps = 12f;
                fsr.sprite = frames[0];
            }
            go.AddComponent<Fireball>(); // sin HitReceiver: los golpes del jugador no la destruyen, hay que esquivarla
        }
        var fb = go.GetComponent<Fireball>();
        if (fb != null) fb.Launch(dir, speed, damage);
    }
}
