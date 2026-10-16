using System;
using System.Collections;
using UnityEngine;
using TMPro;

public class NewMonoBehaviourScript : MonoBehaviour
{
    [Header("Velocidades de Movimiento")]
    public float walkSpeed = 2.3f;              // Velocidad de caminata (estado por defecto)
    public float runSpeed = 4.6f;               // Velocidad de carrera (al activar Shift)
    public bool isRunning = false;              // Estado toggle de carrera

    [Header("Aceleración y Fricción")]
    public float groundAcceleration = 16f;
    public float groundDeceleration = 18f;
    public float airAcceleration = 10f;
    public float airDeceleration = 8f;

    [Header("Salto Estilo Celeste")]
    public float jumpForce = 12.5f;
    public float fallMultiplier = 2.8f;
    public float lowJumpMultiplier = 2.2f;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.15f;

    [Header("Apex Hang Time (Pico del Salto)")]
    public float apexThreshold = 2.0f;
    [Range(0f, 1f)]
    public float apexGravityMultiplier = 0.4f;
    public float apexSpeedBonus = 1.15f;

    [Header("Agarre de Bordes (Ledge Grab)")]
    public bool enableLedgeGrab = true;
    public float maxHangTime = 3.0f;            // Tiempo máximo colgado antes de soltarse automáticamente
    public float wallCheckDistance = 0.38f;
    public float chestCheckHeight = 0.30f;
    public float headCheckHeight = 0.62f;
    public float hangOffsetX = 0.18f;
    public float hangOffsetY = 0.52f;
    public float climbDuration = 0.66f;         // Duración exacta de los 8 frames de trepada (8 frames a 12 fps)

    public bool isHanging = false;
    public bool isClimbing = false;
    private float hangTimer = 0f;
    private float ledgeCooldown = 0f;
    private Vector2 ledgeCorner;

    [Header("Squash & Stretch (Juice Visual)")]
    public bool enableSquashAndStretch = true;
    public Vector2 jumpStretch = new Vector2(0.85f, 1.2f);
    public Vector2 landSquash = new Vector2(1.2f, 0.8f);
    public float squashRecoverySpeed = 12f;

    [Header("Detección de Suelo")]
    public Transform groundCheck;
    public float groundRadius = 0.15f;
    public LayerMask groundLayer;

    [Header("Sistema de Combate (Tecla J)")]
    public KeyCode attackKey = KeyCode.J;
    public GameObject hitFxPrefab;
    public LayerMask enemyLayer;
    public float attack1Duration = 0.45f;
    public float attack2Duration = 0.48f;
    public float attack3Duration = 0.75f;
    public float comboChainWindowStart = 0.16f; // Momento a partir del cual se puede presionar J para encadenar
    public float comboResetTime = 1.25f;        // Tiempo para reiniciar contador si no ataca
    public float attack1Reach = 1.15f;
    public float attack2Reach = 1.25f;
    public float attack3Reach = 1.55f;
    public float attackLungeForce = 1.2f;

    // Estado de combate
    public bool isAttacking = false;
    public int currentComboStep = 0; // 0=idle, 1=atk1, 2=atk2, 3=atk3
    private bool comboQueued = false;
    private float currentAttackDuration = 0f;
    private float attackTimer = 0f;
    private float comboResetTimer = 0f;
    private int connectedComboHits = 0;
    private Coroutine attackRoutine;

    [Header("Sistema de Combate Fase 3 (Dodge Attack & Vertical Attack)")]
    public KeyCode dodgeKey = KeyCode.K;
    public float dodgeCancelWindowStart = 0.10f; // Inicio de ventana para cancelar con K
    public float dodgeCancelWindowEnd = 0.42f;   // Fin de ventana para cancelar con K
    public float dodgeAttack1Duration = 0.45f;
    public float dodgeAttack2Duration = 0.50f;
    public float dodgeAttack1Lunge = 4.2f;
    public float dodgeAttack2Lunge = 2.8f;
    public float dodgeAttack1Reach = 1.4f;
    public float dodgeAttack2Reach = 1.5f;

    [Header("Ataque Vertical (W + J)")]
    public float verticalAttackJumpForce = 8.5f;
    public float verticalAttackDuration = 0.65f;
    public float verticalAttackReachX = 1.35f;
    public float verticalAttackReachY = 2.2f;
    public float verticalAttackKnockback = 5.5f; // Impulso vertical hacia arriba del enemigo

    // Estados adicionales de combate
    public bool isDodgeAttacking = false;
    public bool isVerticalAttacking = false;
    public int currentDodgeStep = 0; // 0=none, 1=dodgeAtk1, 2=dodgeAtk2

    // Helper para saber si está en cualquier acción de combate
    public bool IsInCombatAction => isAttacking || isDodgeAttacking || isVerticalAttacking;

    // Eventos de combate desacoplados
    public static Action<int> OnComboHit;
    public static Action OnComboReset;

    [Header("Respawn al Vacío (Checkpoints)")]
    public float fallThreshold = -9f;
    public Vector3 lastGroundedPos;

    // Componentes y estado interno
    private Rigidbody2D rb2d;
    private Animator animator;
    private float move;
    private bool isGrounded;
    private bool wasGrounded;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private int facingDirection = 1;
    private Vector3 currentScale = Vector3.one;

    void Start()
    {
        rb2d = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        lastGroundedPos = transform.position;
        facingDirection = transform.localScale.x < 0 ? 1 : -1;

        if (groundCheck == null)
        {
            var childCheck = transform.Find("GroundCheck");
            if (childCheck != null) groundCheck = childCheck;
            else
            {
                var go = new GameObject("GroundCheck");
                go.transform.SetParent(transform);
                go.transform.localPosition = new Vector3(0f, -0.05f, 0f);
                groundCheck = go.transform;
            }
        }

        if (groundLayer.value == 0)
        {
            groundLayer = LayerMask.GetMask("Floor");
            if (groundLayer.value == 0) groundLayer = (1 << 3) | (1 << 0);
        }
    }

    void Update()
    {
        if (ledgeCooldown > 0f) ledgeCooldown -= Time.deltaTime;

        // Si está en la animación de trepar, congelar inputs de movimiento
        if (isClimbing) return;

        // Comportamiento cuando está colgado del borde
        if (isHanging)
        {
            HandleLedgeInputs();
            return;
        }

        // Sistema de combate con J (combos), K (dodge attacks) y W+J (ataque vertical)
        HandleCombatInputs();

        // Movimiento horizontal A/D o flechas (bloqueado durante cualquier acción de combate)
        move = IsInCombatAction ? 0f : Input.GetAxisRaw("Horizontal");

        // Conmutador / Toggle de Correr con Shift
        if (!IsInCombatAction && (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)))
        {
            isRunning = !isRunning;
        }

        // Respawn si cae al abismo
        if (transform.position.y < fallThreshold)
        {
            transform.position = lastGroundedPos + Vector3.up * 0.2f;
            rb2d.linearVelocity = Vector2.zero;
        }

        // Buffer de salto (no saltar durante el combate)
        if (!IsInCombatAction && Input.GetButtonDown("Jump"))
            jumpBufferCounter = jumpBufferTime;
        else
            jumpBufferCounter -= Time.deltaTime;

        // Orientación / Flip (no flipear durante el combate)
        if (!IsInCombatAction)
        {
            if (move > 0.01f) facingDirection = -1;
            else if (move < -0.01f) facingDirection = 1;
        }

        // Disparo de salto
        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
        {
            ExecuteJump();
        }

        // Salto variable: soltar el botón corta el ascenso
        if (Input.GetButtonUp("Jump") && rb2d.linearVelocity.y > 0f)
        {
            rb2d.linearVelocity = new Vector2(rb2d.linearVelocity.x, rb2d.linearVelocity.y * 0.5f);
        }

        // Sincronización con Animator
        if (animator != null)
        {
            animator.SetFloat("speed", Mathf.Abs(move));
            animator.SetBool("isRunning", isRunning);
            bool visuallyGrounded = isGrounded || (coyoteTimeCounter > 0.05f);
            animator.SetBool("isGrounded", visuallyGrounded);
            animator.SetFloat("yVelocity", rb2d.linearVelocity.y);
            animator.SetBool("isHanging", false);
            animator.SetBool("isClimbing", false);
            animator.SetBool("isAttacking", isAttacking);
            animator.SetBool("isDodgeAttacking", isDodgeAttacking);
            animator.SetBool("isVerticalAttacking", isVerticalAttacking);
        }

        // Recuperación de Squash & Stretch
        if (enableSquashAndStretch)
        {
            currentScale.x = Mathf.MoveTowards(currentScale.x, 1f, squashRecoverySpeed * Time.deltaTime);
            currentScale.y = Mathf.MoveTowards(currentScale.y, 1f, squashRecoverySpeed * Time.deltaTime);
            transform.localScale = new Vector3(facingDirection * currentScale.x, currentScale.y, 1f);
        }
        else
        {
            transform.localScale = new Vector3(facingDirection, 1f, 1f);
        }
    }

    void FixedUpdate()
    {
        if (isClimbing || isHanging) return;

        wasGrounded = isGrounded;
        Vector2 checkPos = (groundCheck != null) ? (Vector2)groundCheck.position : (Vector2)transform.position + Vector2.down * 0.05f;
        isGrounded = Physics2D.OverlapCircle(checkPos, groundRadius, groundLayer);

        // Frenado suave al atacar en el suelo
        if (isAttacking && isGrounded)
        {
            rb2d.linearVelocity = new Vector2(Mathf.MoveTowards(rb2d.linearVelocity.x, 0f, groundDeceleration * Time.fixedDeltaTime), rb2d.linearVelocity.y);
            return;
        }

        // Aterrizaje
        if (!wasGrounded && isGrounded)
        {
            if (enableSquashAndStretch && rb2d.linearVelocity.y < -2.0f)
            {
                currentScale = new Vector3(landSquash.x, landSquash.y, 1f);
            }
        }

        // Coyote time y checkpoint
        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
            if (Mathf.Abs(rb2d.linearVelocity.y) < 0.1f)
            {
                lastGroundedPos = transform.position;
            }
        }
        else
        {
            coyoteTimeCounter -= Time.fixedDeltaTime;

            // Detección automática de salientes (Ledge Grab) en el aire
            if (enableLedgeGrab && ledgeCooldown <= 0f)
            {
                CheckLedgeGrab();
            }
        }

        bool isApex = !isGrounded && Mathf.Abs(rb2d.linearVelocity.y) < apexThreshold;

        // Selección de velocidad según estado Caminar / Correr (solo si no está en acción de combate)
        if (!IsInCombatAction)
        {
            float currentMaxSpeed = isRunning ? runSpeed : walkSpeed;
            float targetSpeed = move * currentMaxSpeed;
            if (isApex) targetSpeed *= apexSpeedBonus;

            float speedDiff = targetSpeed - rb2d.linearVelocity.x;
            float accelRate;
            if (isGrounded)
                accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? groundAcceleration : groundDeceleration;
            else
                accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? airAcceleration : airDeceleration;

            float force = speedDiff * accelRate;
            rb2d.AddForce(new Vector2(force, 0f), ForceMode2D.Force);
        }

        // Curva de gravedad Celeste
        if (rb2d.linearVelocity.y < 0f)
        {
            rb2d.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime;
        }
        else if (rb2d.linearVelocity.y > 0f && !Input.GetButton("Jump"))
        {
            rb2d.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1f) * Time.fixedDeltaTime;
        }
        else if (isApex)
        {
            rb2d.linearVelocity += Vector2.up * Physics2D.gravity.y * (apexGravityMultiplier - 1f) * Time.fixedDeltaTime;
        }
    }

    private void CheckLedgeGrab()
    {
        // No agarrar si está en combate (ataque, dodge attack o vertical attack)
        if (IsInCombatAction) return;

        // No agarrar si el jugador está presionando S/Abajo para bajar
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) return;

        Vector2 lookDir = (facingDirection == -1) ? Vector2.right : Vector2.left;

        // No agarrar si se está moviendo en dirección contraria al muro
        if (move * -lookDir.x > 0.1f) return;

        // No agarrar si está subiendo con velocidad muy alta hacia arriba
        if (rb2d.linearVelocity.y > 2.5f) return;

        Vector2 chestPos = (Vector2)transform.position + Vector2.up * chestCheckHeight;
        Vector2 headPos = (Vector2)transform.position + Vector2.up * headCheckHeight;

        RaycastHit2D wallHit = Physics2D.Raycast(chestPos, lookDir, wallCheckDistance, groundLayer);
        RaycastHit2D headHit = Physics2D.Raycast(headPos, lookDir, wallCheckDistance + 0.1f, groundLayer);

        // Condición de borde: hay muro al pecho, pero aire libre sobre la cabeza
        if (wallHit.collider != null && headHit.collider == null)
        {
            Vector2 rayDownStart = new Vector2(wallHit.point.x + lookDir.x * 0.05f, headPos.y);
            RaycastHit2D ledgeHit = Physics2D.Raycast(rayDownStart, Vector2.down, headCheckHeight - chestCheckHeight + 0.15f, groundLayer);

            if (ledgeHit.collider != null)
            {
                GrabLedge(new Vector2(wallHit.point.x, ledgeHit.point.y), lookDir);
            }
        }
    }

    private void GrabLedge(Vector2 corner, Vector2 lookDir)
    {
        isHanging = true;
        isClimbing = false;
        hangTimer = 0f;
        ledgeCorner = corner;

        rb2d.linearVelocity = Vector2.zero;
        rb2d.bodyType = RigidbodyType2D.Kinematic;

        // Con los sprites de ledge_master centrados en el punto de agarre (40, 66),
        // la esquina del borde coincide exactamente con la posición del GameObject.
        transform.position = new Vector3(corner.x, corner.y, transform.position.z);
        transform.localScale = new Vector3(facingDirection, 1f, 1f);

        if (animator != null)
        {
            animator.SetTrigger("ledgeGrab");
            animator.SetBool("isHanging", true);
            animator.SetBool("isClimbing", false);
        }
    }

    private void HandleLedgeInputs()
    {
        Vector2 lookDir = (facingDirection == -1) ? Vector2.right : Vector2.left;

        // Temporizador de 3 segundos
        hangTimer += Time.deltaTime;
        if (hangTimer >= maxHangTime)
        {
            DropFromLedge(lookDir);
            return;
        }

        // 1. Subir con ESPACIO (o W / Arriba)
        if (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            StartCoroutine(ClimbLedgeRoutine(lookDir));
            return;
        }

        // 2. Soltarse y bajar con S (o Abajo)
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            DropFromLedge(lookDir);
            return;
        }
    }

    private void DropFromLedge(Vector2 lookDir)
    {
        isHanging = false;
        isClimbing = false;
        rb2d.bodyType = RigidbodyType2D.Dynamic;
        ledgeCooldown = 0.65f; // Cooldown generoso para caer limpiamente

        // Al cambiar a animación de caída (cuyo pivot está en los pies),
        // colocamos el transform en la posición real de los pies (0.55m más abajo del borde)
        // y ligeramente separado de la pared para evitar tirones y bucles.
        transform.position = new Vector3(ledgeCorner.x - lookDir.x * 0.12f, ledgeCorner.y - 0.55f, transform.position.z);

        if (animator != null)
        {
            animator.SetBool("isHanging", false);
            animator.SetBool("isClimbing", false);
        }

        // Impulso hacia abajo y ligeramente alejado del muro
        rb2d.linearVelocity = new Vector2(-lookDir.x * 0.8f, -2.5f);
    }

    private IEnumerator ClimbLedgeRoutine(Vector2 lookDir)
    {
        isClimbing = true;
        isHanging = false;

        if (animator != null)
        {
            animator.SetBool("isHanging", false);
            animator.SetBool("isClimbing", true);
            animator.SetTrigger("climb");
        }

        // Mantener posición en la esquina durante la animación de subida
        // ya que los fotogramas contienen visualmente toda la acción de escalar.
        transform.position = new Vector3(ledgeCorner.x, ledgeCorner.y, transform.position.z);

        // Esperar la duración de la animación (7 frames a 12 fps = ~0.58s)
        yield return new WaitForSeconds(climbDuration);

        // Posicionar al personaje de pie sobre la superficie de la plataforma (pivote en pies para Idle)
        Vector3 finalPos = new Vector3(ledgeCorner.x + lookDir.x * 0.18f, ledgeCorner.y, transform.position.z);
        transform.position = finalPos;

        isClimbing = false;
        rb2d.bodyType = RigidbodyType2D.Dynamic;
        rb2d.linearVelocity = Vector2.zero;
        ledgeCooldown = 0.40f;

        if (animator != null)
        {
            animator.SetBool("isClimbing", false);
        }
    }

    private void ExecuteJump()
    {
        rb2d.linearVelocity = new Vector2(rb2d.linearVelocity.x, jumpForce);
        jumpBufferCounter = 0f;
        coyoteTimeCounter = 0f;

        if (enableSquashAndStretch)
        {
            currentScale = new Vector3(jumpStretch.x, jumpStretch.y, 1f);
        }

        if (animator != null)
        {
            animator.SetTrigger("jump");
        }
    }

    // =========================================================================
    // SISTEMA DE COMBATE FASE 3 (COMBOS J, DODGE ATTACK K, ATAQUE VERTICAL W+J)
    // =========================================================================
    private void HandleCombatInputs()
    {
        // No permitir atacar en estados incompatibles (colgado o trepando de un borde)
        if (isHanging || isClimbing) return;

        // Contador de reseteo de combo si transcurre demasiado tiempo sin atacar
        if (comboResetTimer > 0f)
        {
            comboResetTimer -= Time.deltaTime;
            if (comboResetTimer <= 0f)
            {
                connectedComboHits = 0;
                OnComboReset?.Invoke();
            }
        }

        // 1. Si está en Ataque Vertical
        if (isVerticalAttacking)
        {
            attackTimer += Time.deltaTime;
            if (attackTimer >= verticalAttackDuration)
            {
                FinishVerticalAttack();
            }
            return;
        }

        // 2. Si está en Dodge Attack
        if (isDodgeAttacking)
        {
            attackTimer += Time.deltaTime;

            // Posibilidad de encadenar DodgeAttack1 a DodgeAttack2 con K durante la ventana de cancelación
            if (currentDodgeStep == 1 && Input.GetKeyDown(dodgeKey) && attackTimer >= dodgeCancelWindowStart && attackTimer <= dodgeCancelWindowEnd)
            {
                ExecuteDodgeAttack(2);
                return;
            }

            if (attackTimer >= currentAttackDuration)
            {
                FinishDodgeAttack();
            }
            return;
        }

        // 3. Si está en ataque normal (Combo con J)
        if (isAttacking)
        {
            attackTimer += Time.deltaTime;

            // CANCELACIÓN A DODGE ATTACK CON K DURANTE LA VENTANA CONFIGURABLE
            if (Input.GetKeyDown(dodgeKey) && attackTimer >= dodgeCancelWindowStart && attackTimer <= dodgeCancelWindowEnd)
            {
                int targetDodgeStep = (currentComboStep == 2) ? 2 : 1;
                ExecuteDodgeAttack(targetDodgeStep);
                return;
            }

            // CANCELACIÓN O ENCADENAMIENTO A ATAQUE VERTICAL CON W+J o UP+J
            bool upHeldDuringAtk = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            if (upHeldDuringAtk && Input.GetKeyDown(attackKey) && attackTimer >= comboChainWindowStart)
            {
                ExecuteVerticalAttack();
                return;
            }

            // Ventana para registrar la pulsación del siguiente golpe normal
            if (Input.GetKeyDown(attackKey) && attackTimer >= comboChainWindowStart && !comboQueued && currentComboStep < 3)
            {
                comboQueued = true;
            }

            // Al terminar la duración del golpe actual
            if (attackTimer >= currentAttackDuration)
            {
                if (comboQueued)
                {
                    ExecuteAttack(currentComboStep + 1);
                }
                else
                {
                    FinishAttack();
                }
            }
            return;
        }

        // 4. Iniciar Ataque Vertical desde el suelo con W + J o Flecha Arriba + J
        bool verticalInput = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) && Input.GetKeyDown(attackKey);
        if (isGrounded && verticalInput)
        {
            ExecuteVerticalAttack();
            return;
        }

        // 5. Iniciar nuevo combo normal con J (solo disponible en el suelo)
        if (isGrounded && Input.GetKeyDown(attackKey))
        {
            ExecuteAttack(1);
        }
    }

    private void ExecuteAttack(int step)
    {
        isAttacking = true;
        isDodgeAttacking = false;
        isVerticalAttacking = false;
        currentComboStep = step;
        currentDodgeStep = 0;
        comboQueued = false;
        attackTimer = 0f;
        comboResetTimer = comboResetTime;

        if (step == 1) currentAttackDuration = attack1Duration;
        else if (step == 2) currentAttackDuration = attack2Duration;
        else currentAttackDuration = attack3Duration;

        Vector2 lookDir = (facingDirection == -1) ? Vector2.right : Vector2.left;

        // Impulso hacia adelante para darle dinamismo y peso al espadazo
        rb2d.linearVelocity = new Vector2(lookDir.x * attackLungeForce, rb2d.linearVelocity.y);

        if (animator != null)
        {
            animator.SetInteger("comboStep", step);
            animator.SetTrigger("attack");
            animator.SetBool("isAttacking", true);
            animator.SetBool("isDodgeAttacking", false);
            animator.SetBool("isVerticalAttacking", false);
        }

        if (attackRoutine != null) StopCoroutine(attackRoutine);
        attackRoutine = StartCoroutine(AttackHitboxRoutine(step, lookDir));
    }

    private IEnumerator AttackHitboxRoutine(int step, Vector2 lookDir)
    {
        // Sincronización con el fotograma exacto del impacto
        float hitDelay = (step == 3) ? 0.28f : 0.12f;
        yield return new WaitForSeconds(hitDelay);

        float reach = (step == 1) ? attack1Reach : ((step == 2) ? attack2Reach : attack3Reach);
        Vector2 attackCenter = (Vector2)transform.position + Vector2.up * 0.35f + lookDir * (reach * 0.5f);
        Vector2 attackSize = new Vector2(reach, 0.75f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(attackCenter, attackSize, 0f);
        bool connected = false;
        var damagedReceivers = new System.Collections.Generic.HashSet<HitReceiver>();

        foreach (var col in hits)
        {
            if (col.gameObject == gameObject) continue;

            var receiver = col.GetComponent<HitReceiver>();
            if (receiver != null && !damagedReceivers.Contains(receiver))
            {
                damagedReceivers.Add(receiver);
                connected = true;
                Vector2 hitPoint = col.ClosestPoint(attackCenter);
                receiver.OnHit(hitPoint, step, transform.position, lookDir);

                if (hitFxPrefab != null)
                {
                    Instantiate(hitFxPrefab, hitPoint, Quaternion.identity);
                }
            }
        }

        if (connected)
        {
            connectedComboHits++;
            comboResetTimer = comboResetTime;
            OnComboHit?.Invoke(connectedComboHits);
        }
    }

    private void FinishAttack()
    {
        isAttacking = false;
        currentComboStep = 0;
        comboQueued = false;

        if (animator != null)
        {
            animator.SetInteger("comboStep", 0);
            animator.SetBool("isAttacking", false);
        }
    }

    // ==========================================
    // DODGE ATTACKS (TECLA K DESDE COMBO)
    // ==========================================
    private void ExecuteDodgeAttack(int step)
    {
        if (attackRoutine != null) StopCoroutine(attackRoutine);

        isAttacking = false;
        isDodgeAttacking = true;
        isVerticalAttacking = false;
        currentDodgeStep = step;
        currentComboStep = 0;
        comboQueued = false;
        attackTimer = 0f;
        comboResetTimer = comboResetTime;

        currentAttackDuration = (step == 1) ? dodgeAttack1Duration : dodgeAttack2Duration;
        float lunge = (step == 1) ? dodgeAttack1Lunge : dodgeAttack2Lunge;

        Vector2 lookDir = (facingDirection == -1) ? Vector2.right : Vector2.left;

        // Impulso horizontal evasivo/deslizante hacia adelante conservando gravedad
        rb2d.linearVelocity = new Vector2(lookDir.x * lunge, rb2d.linearVelocity.y);

        if (animator != null)
        {
            animator.SetTrigger("dodgeAttack");
            animator.SetBool("isDodgeAttacking", true);
            animator.SetBool("isAttacking", false);
            animator.SetBool("isVerticalAttacking", false);
        }

        attackRoutine = StartCoroutine(DodgeAttackHitboxRoutine(step, lookDir));
    }

    private IEnumerator DodgeAttackHitboxRoutine(int step, Vector2 lookDir)
    {
        float hitDelay = (step == 1) ? 0.14f : 0.18f;
        yield return new WaitForSeconds(hitDelay);

        float reach = (step == 1) ? dodgeAttack1Reach : dodgeAttack2Reach;
        Vector2 attackCenter = (Vector2)transform.position + Vector2.up * 0.35f + lookDir * (reach * 0.5f);
        Vector2 attackSize = new Vector2(reach, 0.8f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(attackCenter, attackSize, 0f);
        bool connected = false;
        var damagedReceivers = new System.Collections.Generic.HashSet<HitReceiver>();

        foreach (var col in hits)
        {
            if (col.gameObject == gameObject) continue;

            var receiver = col.GetComponent<HitReceiver>();
            if (receiver != null && !damagedReceivers.Contains(receiver))
            {
                damagedReceivers.Add(receiver);
                connected = true;
                Vector2 hitPoint = col.ClosestPoint(attackCenter);
                // Mayor daño o multiplicador en dodge attack (combo 2 o 3)
                receiver.OnHit(hitPoint, step + 1, transform.position, lookDir);

                if (hitFxPrefab != null)
                {
                    Instantiate(hitFxPrefab, hitPoint, Quaternion.identity);
                }
            }
        }

        if (connected)
        {
            connectedComboHits++;
            comboResetTimer = comboResetTime;
            OnComboHit?.Invoke(connectedComboHits);
        }
    }

    private void FinishDodgeAttack()
    {
        isDodgeAttacking = false;
        currentDodgeStep = 0;

        if (animator != null)
        {
            animator.SetBool("isDodgeAttacking", false);
        }
    }

    // ==========================================
    // ATAQUE VERTICAL (W + J / ARRIBA + J)
    // ==========================================
    private void ExecuteVerticalAttack()
    {
        if (attackRoutine != null) StopCoroutine(attackRoutine);

        isAttacking = false;
        isDodgeAttacking = false;
        isVerticalAttacking = true;
        currentComboStep = 0;
        currentDodgeStep = 0;
        comboQueued = false;
        attackTimer = 0f;
        comboResetTimer = comboResetTime;

        Vector2 lookDir = (facingDirection == -1) ? Vector2.right : Vector2.left;

        // Impulso vertical con ligera proyección frontal
        rb2d.linearVelocity = new Vector2(lookDir.x * 1.5f, verticalAttackJumpForce);

        if (animator != null)
        {
            animator.SetTrigger("verticalAttack");
            animator.SetBool("isVerticalAttacking", true);
            animator.SetBool("isAttacking", false);
            animator.SetBool("isDodgeAttacking", false);
        }

        attackRoutine = StartCoroutine(VerticalAttackHitboxRoutine(lookDir));
    }

    private IEnumerator VerticalAttackHitboxRoutine(Vector2 lookDir)
    {
        // Impacto durante la fase de ascenso de la espada
        yield return new WaitForSeconds(0.12f);

        Vector2 attackCenter = (Vector2)transform.position + Vector2.up * 1.1f + lookDir * 0.35f;
        Vector2 attackSize = new Vector2(verticalAttackReachX, verticalAttackReachY);

        Collider2D[] hits = Physics2D.OverlapBoxAll(attackCenter, attackSize, 0f);
        bool connected = false;
        var damagedReceivers = new System.Collections.Generic.HashSet<HitReceiver>();

        foreach (var col in hits)
        {
            if (col.gameObject == gameObject) continue;

            var receiver = col.GetComponent<HitReceiver>();
            if (receiver != null && !damagedReceivers.Contains(receiver))
            {
                damagedReceivers.Add(receiver);
                connected = true;
                Vector2 hitPoint = col.ClosestPoint(attackCenter);
                // Ataque vertical: fuerza de daño combo 3 y elevación vertical del enemigo
                receiver.OnHit(hitPoint, 3, transform.position, lookDir, verticalAttackKnockback);

                if (hitFxPrefab != null)
                {
                    Instantiate(hitFxPrefab, hitPoint, Quaternion.identity);
                }
            }
        }

        if (connected)
        {
            connectedComboHits++;
            comboResetTimer = comboResetTime;
            OnComboHit?.Invoke(connectedComboHits);
        }
    }

    private void FinishVerticalAttack()
    {
        isVerticalAttacking = false;

        if (animator != null)
        {
            animator.SetBool("isVerticalAttacking", false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 lookDir = (facingDirection == -1) ? Vector2.right : Vector2.left;
        Vector2 chestPos = (Vector2)transform.position + Vector2.up * chestCheckHeight;
        Vector2 headPos = (Vector2)transform.position + Vector2.up * headCheckHeight;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(chestPos, chestPos + lookDir * wallCheckDistance);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(headPos, headPos + lookDir * (wallCheckDistance + 0.1f));

        // Gizmo del área de ataque normal
        float reach = (currentComboStep == 3) ? attack3Reach : ((currentComboStep == 2) ? attack2Reach : attack1Reach);
        Vector2 attackCenter = (Vector2)transform.position + Vector2.up * 0.35f + lookDir * (reach * 0.5f);
        Vector2 attackSize = new Vector2(reach, 0.75f);

        Gizmos.color = isAttacking ? Color.red : new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawWireCube(attackCenter, attackSize);

        // Gizmo del área de dodge attack
        float dodgeReach = (currentDodgeStep == 2) ? dodgeAttack2Reach : dodgeAttack1Reach;
        Vector2 dodgeCenter = (Vector2)transform.position + Vector2.up * 0.35f + lookDir * (dodgeReach * 0.5f);
        Vector2 dodgeSize = new Vector2(dodgeReach, 0.8f);
        Gizmos.color = isDodgeAttacking ? Color.cyan : new Color(0f, 1f, 1f, 0.2f);
        Gizmos.DrawWireCube(dodgeCenter, dodgeSize);

        // Gizmo del área de ataque vertical
        Vector2 vertCenter = (Vector2)transform.position + Vector2.up * 1.1f + lookDir * 0.35f;
        Vector2 vertSize = new Vector2(verticalAttackReachX, verticalAttackReachY);
        Gizmos.color = isVerticalAttacking ? Color.yellow : new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawWireCube(vertCenter, vertSize);
    }
}


