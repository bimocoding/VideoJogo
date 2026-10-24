using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// IA del Ghoul: patrulla su zona, detecta al jugador solo si está a su altura y con línea de visión,
/// avisa antes de atacar (destello) y golpea con un hitbox delante, no en círculo.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class GhoulAI : MonoBehaviour, IStunnable
{
    private enum State { Patrol, Chase, Windup, Recover }

    [Header("Objetivo")]
    public Transform targetPlayer;

    [Header("Movimiento")]
    public float moveSpeed = 2.2f;              // velocidad persiguiendo
    public float patrolSpeed = 0.9f;            // velocidad patrullando
    public float patrolRange = 2.5f;            // distancia que patrulla a cada lado de su punto inicial
    public float patrolPauseTime = 0.8f;        // pausa al darse la vuelta
    public float detectionRadius = 12.0f;
    public float verticalDetection = 1.2f;      // diferencia de altura máxima para "ver" al jugador
    public float loseInterestTime = 2.0f;       // segundos sin ver al jugador antes de volver a patrullar
    public float stopDistance = 0.65f;

    [Header("Ataque")]
    public float attackRange = 0.85f;
    public int attackDamage = 15;
    public float attackCooldown = 1.0f;
    public float attackHitDelay = 0.2f;         // tiempo de aviso (windup) antes del golpe
    public float attackRecoverTime = 0.25f;
    public Vector2 attackBoxSize = new Vector2(0.9f, 0.7f);
    public float lungeSpeed = 5.5f;             // embestida hacia delante al golpear
    public float lungeDuration = 0.14f;
    public float retreatSpeed = 2.5f;           // salto hacia atrás después de cada ataque
    public float retreatHop = 2.5f;

    [Header("Daño por contacto")]
    public int contactDamage = 10;              // tocar al ghoul quema

    [Header("Bajar de plataformas")]
    public float maxDropHeight = 2.2f;          // se deja caer de un borde si hay suelo a esta distancia (no a un abismo)
    public Color windupColor = new Color(1f, 0.85f, 0.4f, 1f);
    private float attackTimer = 0f;
    private bool isAttacking = false;
    private Coroutine attackRoutine;

    [Header("Aturdimiento / Stun al recibir golpes")]
    public float hitStunDuration = 0.75f;
    private float stunTimer = 0f;
    public bool IsStunned => stunTimer > 0f;

    [Header("Verificación de Suelo y Protección contra el Vacío")]
    public Transform groundCheck;
    public LayerMask groundLayer;
    public float groundCheckRadius = 0.25f;
    public float ledgeCheckDistanceAhead = 0.40f; // Distancia adelante para comprobar que hay suelo
    public float ledgeCheckDownDistance = 0.65f;   // Distancia hacia abajo para el rayo
    public float wallCheckDistance = 0.35f;
    public float fallThreshold = -4.5f;           // Umbral por debajo del cual se reubica si cae
    private bool isGrounded = true;
    private Vector3 lastSafeGroundedPos;

    private Rigidbody2D rb;
    private Animator anim;
    private Collider2D myCollider;
    private SpriteRenderer sr;
    private NewMonoBehaviourScript playerComp;
    private static readonly List<GhoulAI> allGhouls = new List<GhoulAI>();

    private State state = State.Patrol;
    private Vector3 homePos;
    private float patrolDir = 1f;
    private float patrolPauseTimer = 0f;
    private float lastSeenTimer = 0f;
    private float facing = -1f; // el sprite mira a la izquierda por defecto

    private float baseScaleX = 1.55f;
    private float baseScaleY = 1.55f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        myCollider = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();

        baseScaleX = Mathf.Abs(transform.localScale.x);
        if (baseScaleX < 0.1f) baseScaleX = 1.55f;
        baseScaleY = Mathf.Abs(transform.localScale.y);
        if (baseScaleY < 0.1f) baseScaleY = 1.55f;

        transform.localScale = new Vector3(baseScaleX, baseScaleY, 1f);
    }

    private void OnEnable()
    {
        if (!allGhouls.Contains(this)) allGhouls.Add(this);
        IgnoreOtherEnemies();
    }

    private void OnDisable()
    {
        allGhouls.Remove(this);
    }

    private void Start()
    {
        lastSafeGroundedPos = transform.position;
        homePos = transform.position;
        patrolDir = Random.value < 0.5f ? -1f : 1f;

        if (targetPlayer == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p == null) p = GameObject.Find("prota");
            if (p != null) targetPlayer = p.transform;
        }
        if (targetPlayer != null) playerComp = targetPlayer.GetComponent<NewMonoBehaviourScript>();

        if (groundLayer == 0)
        {
            groundLayer = LayerMask.GetMask("Floor");
            if (groundLayer == 0) groundLayer = (1 << 3);
        }

        IgnoreOtherEnemies();
    }

    public void IgnoreOtherEnemies()
    {
        if (myCollider == null) return;
        foreach (var other in allGhouls)
        {
            if (other != null && other != this && other.myCollider != null)
                Physics2D.IgnoreCollision(myCollider, other.myCollider, true);
        }
    }

    public void OnStunned(float duration = -1f)
    {
        stunTimer = (duration > 0f) ? duration : hitStunDuration;
        CancelAttack();
        attackTimer = Mathf.Max(attackTimer, 0.6f);
        // Un golpe siempre le hace notar al jugador
        state = State.Chase;
        lastSeenTimer = 0f;
    }

    private void CancelAttack()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        isAttacking = false;
        if (sr != null) sr.color = Color.white;
        if (anim != null)
        {
            anim.SetBool("isAttacking", false);
            anim.SetBool("isMoving", false);
        }
    }

    private void Update()
    {
        // Salvaguarda: si cae por debajo del umbral, reubicarlo de inmediato
        if (transform.position.y < fallThreshold)
        {
            transform.position = lastSafeGroundedPos + Vector3.up * 0.15f;
            rb.linearVelocity = Vector2.zero;
        }

        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            SetMoving(false);
            return;
        }

        if (attackTimer > 0f) attackTimer -= Time.deltaTime;
        CheckGrounded();

        if (targetPlayer == null)
        {
            var pObj = GameObject.Find("prota");
            if (pObj == null) { Patrol(); return; }
            targetPlayer = pObj.transform;
            playerComp = targetPlayer.GetComponent<NewMonoBehaviourScript>();
        }

        CheckContactDamage();
        if (isAttacking) return; // windup/recuperación controlados por la corrutina

        bool canSee = CanSeePlayer();
        if (canSee) lastSeenTimer = 0f;
        else lastSeenTimer += Time.deltaTime;

        if (state == State.Patrol && canSee) state = State.Chase;
        else if (state == State.Chase && lastSeenTimer > loseInterestTime) state = State.Patrol;

        if (state == State.Chase) Chase(canSee);
        else Patrol();
    }

    private bool PlayerIsTargetable()
    {
        if (playerComp == null) return true;
        return !playerComp.isDead && !playerComp.isHanging && !playerComp.isClimbing;
    }

    private bool CanSeePlayer()
    {
        if (!PlayerIsTargetable()) return false;
        Vector2 toPlayer = targetPlayer.position - transform.position;
        if (Mathf.Abs(toPlayer.x) > detectionRadius || Mathf.Abs(toPlayer.y) > verticalDetection) return false;

        // Línea de visión: un muro de por medio lo bloquea
        Vector2 eye = (Vector2)transform.position + Vector2.up * 0.35f;
        Vector2 target = (Vector2)targetPlayer.position + Vector2.up * 0.35f;
        return Physics2D.Linecast(eye, target, groundLayer).collider == null;
    }

    private void Chase(bool canSee)
    {
        float distX = targetPlayer.position.x - transform.position.x;
        float distY = Mathf.Abs(targetPlayer.position.y - transform.position.y);
        if (Mathf.Abs(distX) > 0.05f) Face(Mathf.Sign(distX));

        // Atacar si está delante, a su altura y en rango
        if (canSee && Mathf.Abs(distX) <= attackRange && distY <= 0.8f && attackTimer <= 0f)
        {
            attackRoutine = StartCoroutine(ExecuteAttackRoutine());
            return;
        }

        if (Mathf.Abs(distX) > stopDistance)
        {
            float moveDir = Mathf.Clamp(Mathf.Sign(distX) + CalculateSeparation(), -1f, 1f);
            // Si el jugador está más abajo, puede saltar del borde siempre que haya suelo cerca debajo
            bool playerBelow = targetPlayer.position.y < transform.position.y - 0.3f;
            bool canAdvance = CheckGroundAhead(moveDir) || (playerBelow && CanDropDown(moveDir));
            if (canAdvance && !CheckWallAhead(moveDir))
            {
                rb.linearVelocity = new Vector2(moveDir * moveSpeed, rb.linearVelocity.y);
                SetMoving(true);
                return;
            }
        }
        // En el borde o pegado al jugador: esperar
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        SetMoving(false);
    }

    private void Patrol()
    {
        if (patrolPauseTimer > 0f)
        {
            patrolPauseTimer -= Time.deltaTime;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            SetMoving(false);
            return;
        }

        bool outOfRange = (transform.position.x - homePos.x) * patrolDir > patrolRange;
        if (outOfRange || !CheckGroundAhead(patrolDir) || CheckWallAhead(patrolDir))
        {
            patrolDir = -patrolDir;
            patrolPauseTimer = patrolPauseTime;
            return;
        }

        Face(patrolDir);
        rb.linearVelocity = new Vector2(patrolDir * patrolSpeed, rb.linearVelocity.y);
        SetMoving(true);
    }

    private void Face(float dir)
    {
        facing = dir;
        if (sr != null) sr.flipX = dir > 0f;
    }

    private void SetMoving(bool moving)
    {
        if (anim != null) anim.SetBool("isMoving", moving);
    }

    private bool CheckGroundAhead(float dir)
    {
        if (Mathf.Abs(dir) < 0.01f) return true;
        float sign = Mathf.Sign(dir);
        float bottomY = (myCollider != null) ? myCollider.bounds.min.y : transform.position.y;
        Vector2 checkStart = new Vector2(transform.position.x + sign * ledgeCheckDistanceAhead, bottomY + 0.15f);
        foreach (var h in Physics2D.RaycastAll(checkStart, Vector2.down, ledgeCheckDownDistance, groundLayer))
        {
            if (h.collider != null && h.collider.gameObject != gameObject && !h.collider.isTrigger) return true;
        }
        return false;
    }

    private bool CanDropDown(float dir)
    {
        if (!isGrounded || Mathf.Abs(dir) < 0.01f) return false;
        float bottomY = (myCollider != null) ? myCollider.bounds.min.y : transform.position.y;
        Vector2 start = new Vector2(transform.position.x + Mathf.Sign(dir) * (ledgeCheckDistanceAhead + 0.2f), bottomY + 0.05f);
        var hit = Physics2D.Raycast(start, Vector2.down, maxDropHeight, groundLayer);
        return hit.collider != null && hit.collider.gameObject != gameObject;
    }

    // Tocar al ghoul hace daño (el jugador queda invulnerable un momento, así que no se acumula).
    // Se comprueba por solapamiento cada frame: no depende de que los colliders choquen físicamente.
    private void CheckContactDamage()
    {
        if (contactDamage <= 0 || IsStunned || myCollider == null || playerComp == null) return;
        if (playerComp.IsInvulnerable || !PlayerIsTargetable()) return;
        var b = myCollider.bounds;
        foreach (var c in Physics2D.OverlapBoxAll(b.center, (Vector2)b.size + Vector2.one * 0.06f, 0f))
        {
            if (c.GetComponentInParent<NewMonoBehaviourScript>() == playerComp)
            {
                playerComp.TakeDamage(contactDamage, false, transform);
                return;
            }
        }
    }

    private bool CheckWallAhead(float dir)
    {
        if (Mathf.Abs(dir) < 0.01f) return false;
        Vector2 origin = (myCollider != null) ? (Vector2)myCollider.bounds.center : (Vector2)transform.position + Vector2.up * 0.2f;
        var hit = Physics2D.Raycast(origin, new Vector2(Mathf.Sign(dir), 0f), wallCheckDistance, groundLayer);
        return hit.collider != null && hit.collider.gameObject != gameObject;
    }

    private float CalculateSeparation()
    {
        float separationForce = 0f;
        foreach (var other in allGhouls)
        {
            if (other == null || other == this) continue;
            float diff = transform.position.x - other.transform.position.x;
            if (Mathf.Abs(diff) < 0.65f && Mathf.Abs(diff) > 0.001f) separationForce += Mathf.Sign(diff) * 0.5f;
        }
        return separationForce;
    }

    private void CheckGrounded()
    {
        Vector2 checkPos = (groundCheck != null) ? (Vector2)groundCheck.position : (Vector2)transform.position + Vector2.down * 0.05f;
        isGrounded = false;
        foreach (var c in Physics2D.OverlapCircleAll(checkPos, groundCheckRadius, groundLayer))
        {
            if (c != null && c.gameObject != gameObject && !c.isTrigger) { isGrounded = true; break; }
        }

        if (isGrounded && Mathf.Abs(rb.linearVelocity.y) < 0.2f && transform.position.y > fallThreshold)
            lastSafeGroundedPos = transform.position;
    }

    private Vector2 AttackBoxCenter()
    {
        return (Vector2)transform.position + new Vector2(facing * attackBoxSize.x * 0.5f, 0.35f);
    }

    private IEnumerator ExecuteAttackRoutine()
    {
        isAttacking = true;
        state = State.Windup;
        attackTimer = attackCooldown;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        SetMoving(false);

        if (anim != null)
        {
            anim.SetTrigger("attack");
            anim.SetBool("isAttacking", true);
        }

        // Aviso visual: parpadeo de color antes del golpe para que el jugador pueda reaccionar
        float t = 0f;
        while (t < attackHitDelay)
        {
            t += Time.deltaTime;
            if (sr != null) sr.color = (Mathf.FloorToInt(t * 20f) % 2 == 0) ? windupColor : Color.white;
            yield return null;
        }
        if (sr != null) sr.color = Color.white;

        // Golpe: embestida corta hacia delante; solo alcanza lo que está delante del ghoul
        bool landed = false;
        for (float l = 0f; l < lungeDuration; l += Time.deltaTime)
        {
            if (IsStunned) break;
            float lungeDir = CheckGroundAhead(facing) ? facing : 0f; // no se tira al vacío embistiendo
            rb.linearVelocity = new Vector2(lungeDir * lungeSpeed, rb.linearVelocity.y);
            transform.localScale = new Vector3(baseScaleX * 1.15f, baseScaleY * 0.9f, 1f);
            if (!landed && PlayerIsTargetable())
            {
                foreach (var col in Physics2D.OverlapBoxAll(AttackBoxCenter(), attackBoxSize, 0f))
                {
                    var p = col.GetComponentInParent<NewMonoBehaviourScript>();
                    if (p != null)
                    {
                        p.TakeDamage(attackDamage, false, transform);
                        landed = true;
                        break;
                    }
                }
            }
            yield return null;
        }
        transform.localScale = new Vector3(baseScaleX, baseScaleY, 1f);

        // Salto hacia atrás tras atacar: no se queda quieto delante del jugador y vuelve a la carga
        float backDir = CheckGroundAhead(-facing) ? -facing : 0f;
        rb.linearVelocity = new Vector2(backDir * retreatSpeed, backDir != 0f ? retreatHop : rb.linearVelocity.y);

        state = State.Recover;
        yield return new WaitForSeconds(attackRecoverTime);

        if (anim != null) anim.SetBool("isAttacking", false);
        isAttacking = false;
        state = State.Chase;
        attackRoutine = null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(detectionRadius * 2f, verticalDetection * 2f, 0f));

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(AttackBoxCenter(), attackBoxSize);

        Gizmos.color = Color.cyan;
        Vector3 home = Application.isPlaying ? homePos : transform.position;
        Gizmos.DrawLine(home + Vector3.left * patrolRange, home + Vector3.right * patrolRange);
    }
}
