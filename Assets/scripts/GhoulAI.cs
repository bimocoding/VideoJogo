using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class GhoulAI : MonoBehaviour
{
    [Header("Objetivo")]
    public Transform targetPlayer;

    [Header("Movimiento")]
    public float moveSpeed = 2.2f;
    public float detectionRadius = 12.0f;
    public float stopDistance = 0.65f;

    [Header("Ataque")]
    public float attackRange = 0.85f;
    public int attackDamage = 15;
    public float attackCooldown = 1.6f;
    public float attackHitDelay = 0.35f;
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
    public float fallThreshold = -4.5f;           // Umbral por debajo del cual se reubica si cae
    private bool isGrounded = true;
    private Vector3 lastSafeGroundedPos;

    [Header("Componentes")]
    private Rigidbody2D rb;
    private Animator anim;
    private Collider2D myCollider;
    private SpriteRenderer sr;
    private static readonly List<GhoulAI> allGhouls = new List<GhoulAI>();

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
        if (!allGhouls.Contains(this))
        {
            allGhouls.Add(this);
        }
        IgnoreOtherEnemies();
    }

    private void OnDisable()
    {
        allGhouls.Remove(this);
    }

    private void Start()
    {
        lastSafeGroundedPos = transform.position;

        if (targetPlayer == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) targetPlayer = p.transform;
            else
            {
                var pObj = GameObject.Find("prota");
                if (pObj != null) targetPlayer = pObj.transform;
            }
        }

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
            {
                Physics2D.IgnoreCollision(myCollider, other.myCollider, true);
            }
        }
    }

    public void OnStunned(float duration = -1f)
    {
        stunTimer = (duration > 0f) ? duration : hitStunDuration;

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        isAttacking = false;
        if (anim != null)
        {
            anim.SetBool("isAttacking", false);
            anim.SetBool("isMoving", false);
        }

        attackTimer = Mathf.Max(attackTimer, 0.6f);
    }

    private void Update()
    {
        // Salvaguarda: si cae por debajo del umbral de plataforma segura, reubicarlo de inmediato
        if (transform.position.y < fallThreshold)
        {
            transform.position = lastSafeGroundedPos + Vector3.up * 0.15f;
            rb.linearVelocity = Vector2.zero;
        }

        // Temporizador de aturdimiento
        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            if (anim != null)
            {
                anim.SetBool("isMoving", false);
            }
            return;
        }

        if (attackTimer > 0f)
        {
            attackTimer -= Time.deltaTime;
        }

        CheckGrounded();

        if (targetPlayer == null)
        {
            var pObj = GameObject.Find("prota");
            if (pObj != null) targetPlayer = pObj.transform;
            else return;
        }

        float distX = targetPlayer.position.x - transform.position.x;
        float distTotal = Vector2.Distance(transform.position, targetPlayer.position);

        // Orientación del sprite hacia el jugador
        if (Mathf.Abs(distX) > 0.05f && !isAttacking)
        {
            if (sr != null)
            {
                sr.flipX = (distX > 0f);
            }
        }

        // Ataque si está en rango y listo
        if (distTotal <= attackRange && attackTimer <= 0f && !isAttacking)
        {
            attackRoutine = StartCoroutine(ExecuteAttackRoutine());
            return;
        }

        // Persecución inteligente con detección de bordes para NO caerse al vacío
        if (!isAttacking)
        {
            if (distTotal <= detectionRadius && Mathf.Abs(distX) > stopDistance)
            {
                float moveDir = Mathf.Sign(distX);

                moveDir += CalculateSeparation();
                moveDir = Mathf.Clamp(moveDir, -1f, 1f);

                // Comprobación de borde / precipicio al frente
                bool hasGroundAhead = CheckGroundAhead(moveDir);

                if (hasGroundAhead)
                {
                    rb.linearVelocity = new Vector2(moveDir * moveSpeed, rb.linearVelocity.y);
                    if (anim != null) anim.SetBool("isMoving", true);
                }
                else
                {
                    // No hay suelo adelante (borde/abismo): se detiene para no caer
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                    if (anim != null) anim.SetBool("isMoving", false);
                }
            }
            else
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                if (anim != null) anim.SetBool("isMoving", false);
            }
        }
    }

    /// <summary>
    /// Verifica si hay suelo firme al frente en la dirección de avance para no caer por un borde
    /// </summary>
    private bool CheckGroundAhead(float dir)
    {
        if (Mathf.Abs(dir) < 0.01f) return true;
        float sign = Mathf.Sign(dir);
        float bottomY = (myCollider != null) ? myCollider.bounds.min.y : transform.position.y;
        Vector2 checkStart = new Vector2(transform.position.x + sign * ledgeCheckDistanceAhead, bottomY + 0.15f);
        var hits = Physics2D.RaycastAll(checkStart, Vector2.down, ledgeCheckDownDistance, groundLayer);
        foreach (var h in hits)
        {
            if (h.collider != null && h.collider.gameObject != gameObject && !h.collider.isTrigger)
            {
                return true;
            }
        }
        return false;
    }

    private float CalculateSeparation()
    {
        float separationForce = 0f;
        foreach (var other in allGhouls)
        {
            if (other != null && other != this)
            {
                float diff = transform.position.x - other.transform.position.x;
                if (Mathf.Abs(diff) < 0.65f && Mathf.Abs(diff) > 0.001f)
                {
                    separationForce += (Mathf.Sign(diff) * 0.5f);
                }
            }
        }
        return separationForce;
    }

    private void CheckGrounded()
    {
        Vector2 checkPos = (groundCheck != null) ? (Vector2)groundCheck.position : (Vector2)transform.position + Vector2.down * 0.05f;
        var cols = Physics2D.OverlapCircleAll(checkPos, groundCheckRadius, groundLayer);
        isGrounded = false;
        foreach (var c in cols)
        {
            if (c != null && c.gameObject != gameObject && !c.isTrigger)
            {
                isGrounded = true;
                break;
            }
        }

        if (isGrounded && Mathf.Abs(rb.linearVelocity.y) < 0.2f && transform.position.y > fallThreshold)
        {
            lastSafeGroundedPos = transform.position;
        }
    }

    private IEnumerator ExecuteAttackRoutine()
    {
        isAttacking = true;
        attackTimer = attackCooldown;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        if (anim != null)
        {
            anim.SetTrigger("attack");
            anim.SetBool("isAttacking", true);
        }

        yield return new WaitForSeconds(attackHitDelay);

        if (!IsStunned && targetPlayer != null)
        {
            float dist = Vector2.Distance(transform.position, targetPlayer.position);
            if (dist <= attackRange * 1.35f)
            {
                var playerComp = targetPlayer.GetComponent<NewMonoBehaviourScript>();
                if (playerComp != null)
                {
                    playerComp.TakeDamage(attackDamage, false);
                }
            }
        }

        yield return new WaitForSeconds(0.40f);

        if (anim != null)
        {
            anim.SetBool("isAttacking", false);
        }
        isAttacking = false;
        attackRoutine = null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.blue;
        Vector2 checkPos = (groundCheck != null) ? (Vector2)groundCheck.position : (Vector2)transform.position + Vector2.down * 0.05f;
        Gizmos.DrawWireSphere(checkPos, groundCheckRadius);

        // Rayo de detección de borde
        Gizmos.color = Color.green;
        float bottomY = (myCollider != null) ? myCollider.bounds.min.y : transform.position.y;
        Vector2 fwdCheck = new Vector2(transform.position.x + ledgeCheckDistanceAhead, bottomY + 0.15f);
        Gizmos.DrawLine(fwdCheck, fwdCheck + Vector2.down * ledgeCheckDownDistance);
    }
}
