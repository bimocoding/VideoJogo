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
    public float attackHitDelay = 0.35f; // Momento del golpe sincronizado con el frame de zarpazo
    private float attackTimer = 0f;
    private bool isAttacking = false;
    private Coroutine attackRoutine;

    [Header("Aturdimiento / Stun al recibir golpes del Prota")]
    public float hitStunDuration = 0.75f;
    private float stunTimer = 0f;
    public bool IsStunned => stunTimer > 0f;

    [Header("Verificación de Suelo y Bordes")]
    public Transform groundCheck;
    public LayerMask groundLayer;
    public float groundCheckRadius = 0.25f;
    private bool isGrounded = true;

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

        // Respetar escala base
        baseScaleX = Mathf.Abs(transform.localScale.x);
        if (baseScaleX < 0.1f) baseScaleX = 1.55f;
        baseScaleY = Mathf.Abs(transform.localScale.y);
        if (baseScaleY < 0.1f) baseScaleY = 1.55f;

        // Mantener escala estrictamente positiva en transform
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

        // Configuración de capa de suelo: Floor (capa 3) y Default (0)
        if (groundLayer == 0)
        {
            groundLayer = LayerMask.GetMask("Floor", "Ground", "Default");
            if (groundLayer == 0) groundLayer = (1 << 3) | (1 << 0);
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

    /// <summary>
    /// Llamado cuando el Prota le propina un golpe. Interrumpe ataques en curso y lo aturde.
    /// </summary>
    public void OnStunned(float duration = -1f)
    {
        stunTimer = (duration > 0f) ? duration : hitStunDuration;

        // Cancelar ataque en curso para que no dañe a la PJ si el jugador ataca primero
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

        // Restablecer cooldown para que no ataque de inmediato al salir del aturdimiento
        attackTimer = Mathf.Max(attackTimer, 0.6f);
    }

    private void Update()
    {
        // Temporizador de aturdimiento por combo del prota
        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            // Durante el aturdimiento el ghoul no puede atacar ni perseguir
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

        // ORIENTACIÓN CORRECTA:
        // El sprite original por defecto mira a la IZQUIERDA.
        // Si el objetivo está a la DERECHA (distX > 0) -> se voltea (flipX = true).
        // Si el objetivo está a la IZQUIERDA (distX < 0) -> no se voltea (flipX = false).
        if (Mathf.Abs(distX) > 0.05f && !isAttacking)
        {
            if (sr != null)
            {
                sr.flipX = (distX > 0f);
            }
        }

        // Si está en rango de ataque y el cooldown terminó
        if (distTotal <= attackRange && attackTimer <= 0f && !isAttacking)
        {
            attackRoutine = StartCoroutine(ExecuteAttackRoutine());
            return;
        }

        // Movimiento hacia el jugador si está dentro del radio de detección
        if (!isAttacking)
        {
            if (distTotal <= detectionRadius && Mathf.Abs(distX) > stopDistance)
            {
                float moveDir = Mathf.Sign(distX);

                // Separación suave entre ghouls
                moveDir += CalculateSeparation();
                moveDir = Mathf.Clamp(moveDir, -1f, 1f);

                rb.linearVelocity = new Vector2(moveDir * moveSpeed, rb.linearVelocity.y);

                if (anim != null)
                {
                    anim.SetBool("isMoving", true);
                }
            }
            else
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                if (anim != null)
                {
                    anim.SetBool("isMoving", false);
                }
            }
        }
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
        isGrounded = Physics2D.OverlapCircle(checkPos, groundCheckRadius, groundLayer);
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

        // Si fue aturdido durante el viento previo del ataque, no aplicar daño
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
    }
}
