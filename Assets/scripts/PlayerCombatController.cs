using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controlador definitivo de combate 2D con "Delay Combos" estilo Devil May Cry.
/// Integra el Platformer Character Pack de Merakintsugi con soporte para:
/// - Capas separadas de VFX (humo y destellos de impacto)
/// - Cancelación de recovery frames al encadenar golpes (corte al frame 0)
/// - Ramificación por pausa rítmica (EarlyComboWindow vs DelayComboWindow en Hit 1)
/// - Rama A: Combo básico de 3 golpes (estocada, corte horizontal y recarga mágica con knockback)
/// - Rama B: Combo de 5 golpes con variación rápida, doble corte con 2 pulsos, embestida con humo, elevador y finisher vertical en 3 fases con bloqueo estricto del eje X
/// - Rama C: Dodge Cancel con K en cualquier frame terrestre y atajo desde Slide / Backdodge a Hit 4 Lunge
/// - Rama D: Ataque vertical directo (W + J) y combo aéreo neutral de 2 golpes (Jump Attack 1 y 2)
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerCombatController : MonoBehaviour
{
    public enum CombatState
    {
        Idle,
        Hit1,              // prota_atk1 (1.2 m/s)
        Hit2Basic,         // prota_atk2 (0 m/s, corte horizontal)
        Hit3Basic,         // prota_atk3 (0 m/s, recarga mágica con knockback en destello)
        Hit2Variation,     // prota_atk2_1 (1.8 m/s, estocada rasante)
        Hit3Variation,     // prota_atk2_2 (2.2 m/s, doble corte con 2 pulsos)
        Hit4Lunge,         // prota_dodge_atk1 (4.2 m/s, lunge con humo)
        Hit5Launcher,      // prota_dodge_atk2 (0 m/s horiz, +7.8 m/s vertical, elevador)
        VerticalFinisher,  // prota_vertical_atk (3 fases: freeze, dive -14 m/s, impacto AoE 45 dmg con bloqueo X)
        DirectLauncher,    // W + J (ascendente directo +7.8 m/s)
        JumpAttack1,       // prota_jump_atk (aire neutral 1)
        JumpAttack2        // prota_jump_atk2 (aire neutral 2)
    }

    [Header("Referencias Principales")]
    [SerializeField] private NewMonoBehaviourScript player;
    [SerializeField] private Rigidbody2D rb2d;
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Transform vfxSpawner;

    [Header("Prefabs de Efectos Visuales (VFX)")]
    public GameObject hitFxPrefab;
    public GameObject smokeFxPrefab;

    [Header("Teclas de Entrada")]
    public KeyCode attackKey = KeyCode.J;
    public KeyCode dodgeKey = KeyCode.K;

    [Header("Configuración de Hit 1 y Delay Combo")]
    [Tooltip("Impulso frontal del Hit 1")]
    public float hit1LungeForce = 1.2f;
    [Tooltip("Duración total estimada del clip Hit 1")]
    public float hit1Duration = 0.708f;
    [Range(0f, 1f)] public float earlyWindowStartNorm = 0.35f;
    [Range(0f, 1f)] public float earlyWindowEndNorm = 0.65f;
    [Range(0f, 1f)] public float delayWindowStartNorm = 0.66f;
    [Range(0f, 1f)] public float delayWindowEndNorm = 0.90f;

    [Header("Configuración Rama A (Básico 3-Hit)")]
    public float hit2BasicDuration = 0.792f;
    public float hit2BasicReach = 1.25f;
    public float hit3BasicDuration = 1.720f;
    public float hit3BasicReach = 1.55f;
    public float hit3FlashTiming = 0.28f;
    public float hit3KnockbackForce = 7.5f;

    [Header("Configuración Rama B (5-Hit Variación + Finisher)")]
    public float hit2VarLungeForce = 1.8f;
    public float hit2VarDuration = 0.293f;
    public float hit2VarReach = 1.30f;
    public float hit3VarLungeForce = 2.2f;
    public float hit3VarDuration = 0.446f;
    public float hit3VarReach = 1.45f;
    public float hit4LungeForce = 4.2f;
    public float hit4LungeDuration = 0.792f;
    public float hit4LungeReach = 1.40f;
    public float hit5LauncherJumpForce = 7.8f;
    public float hit5LauncherDuration = 0.667f;
    public float hit5LauncherReach = 1.50f;

    [Header("Configuración Vertical Finisher Slam (3 Fases)")]
    public float verticalWindUpDuration = 0.10f;
    public float verticalDiveSpeed = -14.0f;
    public float verticalImpactAoERadius = 2.0f;
    public int verticalImpactAoEDamage = 45;
    public float verticalFinisherDuration = 1.542f;

    [Header("Configuración Combo Aéreo Estándar")]
    public float jumpAttack1Duration = 0.450f;
    public float jumpAttack1Reach = 1.55f;
    public float jumpAttack2Duration = 0.583f;
    public float jumpAttack2Reach = 1.65f;

    [Header("Feedback de Combate")]
    public Color delayWindowFeedbackColor = new Color(0.6f, 0.9f, 1f, 1f);
    public float delayFeedbackFlashDuration = 0.08f;

    // Estado público
    public CombatState CurrentState { get; private set; } = CombatState.Idle;
    public bool IsInCombatAction => CurrentState != CombatState.Idle;
    public bool IsHorizontalLocked { get; private set; } = false;
    public bool CanTriggerAirSlam { get; set; } = false;

    // Temporizadores internos
    private float stateTimer = 0f;
    private float currentStateDuration = 0f;
    private bool delayGleamTriggered = false;
    private Coroutine activeAttackRoutine = null;
    private Coroutine verticalFinisherRoutine = null;
    private Coroutine gleamRoutine = null;
    private Coroutine hitstopRoutine = null;
    private float originalGravityScale = 1f;

    // Eventos desacoplados
    public static Action<int> OnComboHit;
    public static Action OnComboReset;
    public static Action OnDelayWindowOpened;

    private int comboCounter = 0;
    private float comboResetTimer = 0f;
    public float comboResetMaxTime = 1.4f;

    public NewMonoBehaviourScript Player => player != null ? player : (player = GetComponent<NewMonoBehaviourScript>());
    public Rigidbody2D Rb2D => rb2d != null ? rb2d : (rb2d = GetComponent<Rigidbody2D>());
    public Animator Anim => animator != null ? animator : (animator = GetComponent<Animator>());
    public SpriteRenderer SR => spriteRenderer != null ? spriteRenderer : (spriteRenderer = GetComponent<SpriteRenderer>());

    private void OnValidate()
    {
        EnsureReferences();
    }

    private void Awake()
    {
        EnsureReferences();
        if (Rb2D != null) originalGravityScale = Rb2D.gravityScale;
    }

    public void EnsureReferences()
    {
        if (player == null) player = GetComponent<NewMonoBehaviourScript>();
        if (rb2d == null) rb2d = GetComponent<Rigidbody2D>();
        if (Anim == null) animator = GetComponent<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        if (vfxSpawner == null)
        {
            Transform existing = transform.Find("VFX_Spawner");
            if (existing != null)
            {
                vfxSpawner = existing;
                vfxSpawner.localPosition = new Vector3(0f, 0.18f, 0f);
            }
            else
            {
                GameObject sp = new GameObject("VFX_Spawner");
                sp.transform.SetParent(transform);
                sp.transform.localPosition = new Vector3(0f, 0.18f, 0f);
                vfxSpawner = sp.transform;
            }
        }
        else
        {
            vfxSpawner.localPosition = new Vector3(0f, 0.18f, 0f);
        }
    }

    private void Update()
    {
        if (comboResetTimer > 0f)
        {
            comboResetTimer -= Time.deltaTime;
            if (comboResetTimer <= 0f)
            {
                comboCounter = 0;
                OnComboReset?.Invoke();
            }
        }

        if (CurrentState == CombatState.Idle)
        {
            // Reset de flag aéreo si aterriza y no está en combate
            if (Player != null && Player.isGrounded)
            {
                CanTriggerAirSlam = false;
            }
        }
    }

    /// <summary>
    /// Llamado en FixedUpdate para garantizar físicas consistentes y bloqueo inamovible en X.
    /// </summary>
    public void HandleFixedUpdate()
    {
        if (IsHorizontalLocked && rb2d != null)
        {
            // REGLA ESTRICTA DE BLOQUEO EN EJE X:
            // Forzar velocidad horizontal a 0 en cada FixedUpdate durante Vertical Finisher
            Rb2D.linearVelocity = new Vector2(0f, Rb2D.linearVelocity.y);
        }
    }

    /// <summary>
    /// Procesa todas las entradas de combate (J, K, W+J, S+J).
    /// </summary>
    public void HandleCombatInputs()
    {
        // Si el jugador está muerto o en estados no cancelables, no procesar
        if (Player != null && (Player.isDead || Player.isClimbing || Player.isHanging || Player.isHealing))
        {
            return;
        }

        // 1. Manejo activo del estado de combate actual
        if (CurrentState != CombatState.Idle)
        {
            UpdateActiveCombatState();
            return;
        }

        // 2. Si está en Idle de combate, procesar inicios de ataque:
        bool isGrounded = player != null ? Player.isGrounded : true;

        // RAMA D: ATAQUE VERTICAL DIRECTO (W + J en suelo o aire)
        bool isUpHeld = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || Input.GetAxisRaw("Vertical") > 0.5f;
        if (isUpHeld && Input.GetKeyDown(attackKey))
        {
            OrientCharacterFromInput();
            ExecuteDirectLauncher();
            return;
        }

        // VERTICAL FINISHER SLAM DESDE EL AIRE (S + J o J con CanTriggerAirSlam)
        bool isDownHeld = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || Input.GetAxisRaw("Vertical") < -0.5f;
        if (!isGrounded)
        {
            if ((isDownHeld || CanTriggerAirSlam) && Input.GetKeyDown(attackKey))
            {
                StartVerticalFinisher();
                return;
            }

            // COMBO AÉREO ESTÁNDAR (J en aire neutral)
            if (Input.GetKeyDown(attackKey))
            {
                OrientCharacterFromInput();
                ExecuteJumpAttack(1);
                return;
            }
        }
        else
        {
            // APERTURA COMÚN: HIT 1 EN EL SUELO (J)
            if (Input.GetKeyDown(attackKey))
            {
                OrientCharacterFromInput();
                ExecuteHit1();
                return;
            }
        }
    }

    /// <summary>
    /// Actualiza el estado de ataque en curso, temporizadores y ventanas de transición.
    /// </summary>
    private void UpdateActiveCombatState()
    {
        stateTimer += Time.deltaTime;
        float normTime = currentStateDuration > 0.001f ? (stateTimer / currentStateDuration) : 1f;

        // CANCELACIÓN CON K (DODGE CANCEL)
        // Permitido en cualquier frame de cualquier ataque terrestre excepto durante la caída del Vertical Finisher
        if (CurrentState != CombatState.VerticalFinisher && Input.GetKeyDown(dodgeKey))
        {
            if (TryDodgeCancel()) return;
        }

        // ==========================================
        // GESTIÓN POR ESTADOS
        // ==========================================
        switch (CurrentState)
        {
            case CombatState.Hit1:
                UpdateHit1State(normTime);
                break;

            case CombatState.Hit2Basic:
                UpdateStandardComboState(normTime, 0.30f, 0.90f, () => ExecuteHit3Basic());
                break;

            case CombatState.Hit3Basic:
                UpdateRecoveryOnlyState(normTime);
                break;

            case CombatState.Hit2Variation:
                UpdateStandardComboState(normTime, 0.30f, 0.90f, () => ExecuteHit3Variation());
                break;

            case CombatState.Hit3Variation:
                UpdateStandardComboState(normTime, 0.30f, 0.90f, () => ExecuteHit4Lunge());
                break;

            case CombatState.Hit4Lunge:
                UpdateStandardComboState(normTime, 0.30f, 0.90f, () => ExecuteHit5Launcher());
                break;

            case CombatState.Hit5Launcher:
                UpdateLauncherState(normTime);
                break;

            case CombatState.JumpAttack1:
                UpdateAerialComboState(normTime, 0.25f, 0.90f, () => ExecuteJumpAttack(2));
                break;

            case CombatState.JumpAttack2:
                UpdateAerialRecoveryState(normTime);
                break;

            case CombatState.DirectLauncher:
                UpdateLauncherState(normTime);
                break;

            case CombatState.VerticalFinisher:
                // Manejado enteramente por su Corrutina en 3 fases
                break;
        }
    }

    #region Hit 1 & Delay Combo Logic

    private void ExecuteHit1()
    {
        SetCombatState(CombatState.Hit1, hit1Duration);
        delayGleamTriggered = false;

        Vector2 lookDir = GetFacingVector();

        // Impulso frontal de 1.2 m/s
        if (Rb2D != null)
        {
            Rb2D.linearVelocity = new Vector2(lookDir.x * hit1LungeForce, Rb2D.linearVelocity.y);
        }

        PlayAnimationState("Attack1", 0f);
        SetAnimatorCombatBools(isAttacking: true);

        if (activeAttackRoutine != null) StopCoroutine(activeAttackRoutine);
        activeAttackRoutine = StartCoroutine(SingleHitboxRoutine(0.12f, 1.15f, 0.75f, 1, 3.5f, 0f, false));
    }

    private void UpdateHit1State(float normTime)
    {
        // 1. Destello visual / feedback cuando se abre la DelayComboWindow (al 66%)
        if (normTime >= delayWindowStartNorm && !delayGleamTriggered)
        {
            delayGleamTriggered = true;
            TriggerDelayGleamFeedback();
            OnDelayWindowOpened?.Invoke();
        }

        // 2. Comprobación de input J
        if (Input.GetKeyDown(attackKey))
        {
            // Orientación libre del personaje con A o D entre golpes
            OrientCharacterFromInput();

            // Ventana Rápida (EarlyComboWindow: 35% - 65%): Rama A (Basic 3-Hit Combo)
            if (normTime >= earlyWindowStartNorm && normTime <= earlyWindowEndNorm)
            {
                ExecuteHit2Basic();
                return;
            }

            // Ventana de Pausa (DelayComboWindow: 66% - 90%): Rama B (5-Hit Combo Variation)
            if (normTime >= delayWindowStartNorm && normTime <= delayWindowEndNorm)
            {
                ExecuteHit2Variation();
                return;
            }
        }

        // 3. Si termina la animación sin encadenar (>90%), recuperación suave a Idle
        if (normTime >= 0.95f)
        {
            FinishCombat();
        }
    }

    private void TriggerDelayGleamFeedback()
    {
        if (gleamRoutine != null) StopCoroutine(gleamRoutine);
        gleamRoutine = StartCoroutine(DelayGleamRoutine());
    }

    private IEnumerator DelayGleamRoutine()
    {
        if (SR != null)
        {
            Color orig = SR.color;
            SR.color = delayWindowFeedbackColor;
            yield return new WaitForSeconds(delayFeedbackFlashDuration);
            if (SR != null) SR.color = orig;
        }
        gleamRoutine = null;
    }

    #endregion

    #region Branch A: Basic 3-Hit Combo

    private void ExecuteHit2Basic()
    {
        // Corta inmediatamente recuperación y pasa a frame 0 de prota_atk2
        SetCombatState(CombatState.Hit2Basic, hit2BasicDuration);

        // Frena el impulso: velocity.x = 0
        if (Rb2D != null)
        {
            Rb2D.linearVelocity = new Vector2(0f, Rb2D.linearVelocity.y);
        }

        PlayAnimationState("Attack2", 0f);
        SetAnimatorCombatBools(isAttacking: true);

        if (activeAttackRoutine != null) StopCoroutine(activeAttackRoutine);
        activeAttackRoutine = StartCoroutine(SingleHitboxRoutine(0.12f, hit2BasicReach, 0.75f, 2, 4.0f, 0f, false));
    }

    private void ExecuteHit3Basic()
    {
        // Corta recuperación y pasa a frame 0 de prota_atk3
        SetCombatState(CombatState.Hit3Basic, hit3BasicDuration);

        // Frena el impulso: velocity.x = 0
        if (Rb2D != null)
        {
            Rb2D.linearVelocity = new Vector2(0f, Rb2D.linearVelocity.y);
        }

        PlayAnimationState("Attack3", 0f);
        SetAnimatorCombatBools(isAttacking: true);

        if (activeAttackRoutine != null) StopCoroutine(activeAttackRoutine);
        // Activa Hitbox exactamente en el frame del destello (0.28s) aplicando Knockback horizontal épico
        activeAttackRoutine = StartCoroutine(SingleHitboxRoutine(hit3FlashTiming, hit3BasicReach, 0.85f, 3, hit3KnockbackForce, 0f, true));
    }

    #endregion

    #region Branch B: 5-Hit Combo Variation + Vertical Finisher

    private void ExecuteHit2Variation()
    {
        // Corta recuperación y pasa al frame 0 de prota_atk2_1
        SetCombatState(CombatState.Hit2Variation, hit2VarDuration);

        Vector2 lookDir = GetFacingVector();

        // Estocada rasante con impulso hacia adelante a 1.8 m/s
        if (Rb2D != null)
        {
            Rb2D.linearVelocity = new Vector2(lookDir.x * hit2VarLungeForce, Rb2D.linearVelocity.y);
        }

        PlayAnimationState("Attack2_Variation", 0f);
        SetAnimatorCombatBools(isAttacking: true, isVariation: true);

        if (activeAttackRoutine != null) StopCoroutine(activeAttackRoutine);
        activeAttackRoutine = StartCoroutine(SingleHitboxRoutine(0.10f, hit2VarReach, 0.80f, 2, 4.5f, 0f, false));
    }

    private void ExecuteHit3Variation()
    {
        // Corta recuperación y pasa al frame 0 de prota_atk2_2
        SetCombatState(CombatState.Hit3Variation, hit3VarDuration);

        Vector2 lookDir = GetFacingVector();

        // Doble tajo continuo avanzando a 2.2 m/s
        if (Rb2D != null)
        {
            Rb2D.linearVelocity = new Vector2(lookDir.x * hit3VarLungeForce, Rb2D.linearVelocity.y);
        }

        PlayAnimationState("Attack3_Variation", 0f);
        SetAnimatorCombatBools(isAttacking: true, isVariation: true);

        if (activeAttackRoutine != null) StopCoroutine(activeAttackRoutine);
        // Activa 2 pulsos de hitbox continuos
        activeAttackRoutine = StartCoroutine(DoublePulseHitboxRoutine(0.10f, 0.24f, hit3VarReach, 0.85f, 3, 5.0f));
    }

    public void ExecuteHit4Lunge()
    {
        // Hit 4 Lunge (prota_dodge_atk1)
        SetCombatState(CombatState.Hit4Lunge, hit4LungeDuration);

        Vector2 lookDir = GetFacingVector();

        // Embestida frontal deslizante de largo alcance a 4.2 m/s
        if (Rb2D != null)
        {
            Rb2D.linearVelocity = new Vector2(lookDir.x * hit4LungeForce, Rb2D.linearVelocity.y);
        }

        PlayAnimationState("DodgeAttack1", 0f);
        SetAnimatorCombatBools(isDodgeAttacking: true);

        if (activeAttackRoutine != null) StopCoroutine(activeAttackRoutine);
        activeAttackRoutine = StartCoroutine(SingleHitboxRoutine(0.14f, hit4LungeReach, 0.80f, 4, 5.5f, 0f, false));
    }

    private void ExecuteHit5Launcher()
    {
        // Hit 5 Elevador (prota_dodge_atk2)
        SetCombatState(CombatState.Hit5Launcher, hit5LauncherDuration);

        // Frena el avance horizontal y aplica velocity.y = +7.8 m/s
        if (Rb2D != null)
        {
            Rb2D.linearVelocity = new Vector2(0f, hit5LauncherJumpForce);
        }

        PlayAnimationState("DodgeAttack2", 0f);
        SetAnimatorCombatBools(isDodgeAttacking: true);

        // Activa flag para habilitar el Vertical Finisher Slam en el aire
        CanTriggerAirSlam = true;

        if (activeAttackRoutine != null) StopCoroutine(activeAttackRoutine);
        // Aplica +7.8 m/s vertical de launcher al enemigo
        activeAttackRoutine = StartCoroutine(SingleHitboxRoutine(0.16f, hit5LauncherReach, 1.2f, 5, 2.5f, hit5LauncherJumpForce, true));
    }

    public void StartVerticalFinisher()
    {
        if (verticalFinisherRoutine != null) StopCoroutine(verticalFinisherRoutine);
        verticalFinisherRoutine = StartCoroutine(VerticalFinisherSlamRoutine());
    }

    /// <summary>
    /// Corrutina de 3 Fases del Vertical Finisher Slam con BLOQUEO ESTRICTO DEL EJE X.
    /// </summary>
    private IEnumerator VerticalFinisherSlamRoutine()
    {
        SetCombatState(CombatState.VerticalFinisher, verticalFinisherDuration);

        // REGLA ESTRICTA DE BLOQUEO EN EJE X (Inamovible hacia los lados):
        // Desde el primer frame en el aire hasta concluir impacto y recuperación en suelo
        IsHorizontalLocked = true;
        CanTriggerAirSlam = false;

        PlayAnimationState("VerticalAttack", 0f);
        SetAnimatorCombatBools(isVerticalAttacking: true);

        // =========================================================================
        // FASE 1: WIND-UP EN AIRE (~0.10s)
        // Congela al personaje (velocity = 0, gravityScale = 0)
        // =========================================================================
        if (Rb2D != null)
        {
            Rb2D.linearVelocity = Vector2.zero;
            Rb2D.gravityScale = 0f;
        }

        yield return new WaitForSeconds(verticalWindUpDuration);

        // =========================================================================
        // FASE 2: LOOP DE CAÍDA
        // Aplica velocity = (0, -14.0 m/s) hasta detectar isGrounded
        // =========================================================================
        if (Rb2D != null)
        {
            Rb2D.gravityScale = originalGravityScale;
            Rb2D.linearVelocity = new Vector2(0f, verticalDiveSpeed);
        }

        // Mantener/esperar hasta tocar el suelo
        float fallTimeout = 2.5f;
        float fallTimer = 0f;
        while (fallTimer < fallTimeout)
        {
            fallTimer += Time.deltaTime;

            if (Rb2D != null)
            {
                // Forzar estricta verticalidad
                Rb2D.linearVelocity = new Vector2(0f, verticalDiveSpeed);
            }

            if (Player != null && Player.isGrounded && fallTimer > 0.05f)
            {
                break;
            }

            yield return null;
        }

        // =========================================================================
        // FASE 3: IMPACTO EN SUELO Y RECUPERACIÓN
        // Fija velocity = 0, instancia onda expansiva / humo, aplica daño crítico AoE (45 dmg)
        // y solo desbloquea el movimiento lateral al terminar el clip
        // =========================================================================
        if (Rb2D != null)
        {
            Rb2D.linearVelocity = Vector2.zero;
        }

        // Audio épico y temblor de cámara
        HitSfx.PlaySlash(true);
        TriggerHitstop(0.09f);

        // Daño crítico AoE a los enemigos en el radio de impacto
        ExecuteAoEImpactDamage(transform.position, verticalImpactAoERadius, verticalImpactAoEDamage);

        // Esperar frames de impacto y recuperación hasta concluir la animación
        float remainingRecoveryTime = 0.55f;
        yield return new WaitForSeconds(remainingRecoveryTime);

        // Desbloquear movimiento lateral y volver a Idle
        IsHorizontalLocked = false;
        FinishCombat();
        verticalFinisherRoutine = null;
    }

    private void ExecuteAoEImpactDamage(Vector2 center, float radius, int damage)
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(center, radius);
        var damaged = new HashSet<HitReceiver>();

        foreach (var col in colliders)
        {
            if (col.gameObject == gameObject) continue;

            var receiver = col.GetComponent<HitReceiver>();
            if (receiver != null && !damaged.Contains(receiver))
            {
                damaged.Add(receiver);
                Vector2 hitPoint = col.ClosestPoint(center);
                Vector2 pushDir = ((Vector2)col.transform.position - center).normalized;
                receiver.OnHit(hitPoint, 5, center, pushDir, 3.5f);

                SpawnHitFX(hitPoint);
            }
        }
    }

    #endregion

    #region Branch C: Dodge Cancel & Dodge Shortcut (Tecla K)

    /// <summary>
    /// Cancela de inmediato el ataque terrestre activo, apaga hitboxes y dispara un esquive o slide.
    /// </summary>
    public bool TryDodgeCancel()
    {
        if (CurrentState == CombatState.VerticalFinisher) return false;
        if (player == null || !Player.isGrounded) return false;

        // Cancelar rutinas activas y apagar hitboxes
        if (activeAttackRoutine != null)
        {
            StopCoroutine(activeAttackRoutine);
            activeAttackRoutine = null;
        }

        // Desactivar flags de combate
        SetAnimatorCombatBools(false, false, false, false, false);
        CurrentState = CombatState.Idle;

        // Delegar inicio de slide o dodge según entrada a player.cs
        Player.CancelAttackIntoDodgeOrSlide();
        return true;
    }

    /// <summary>
    /// Atajo de combo: Si el jugador presiona J durante Slide o Backdodge, entra directamente a Hit 4 Lunge.
    /// </summary>
    public bool TryDodgeLungeShortcut()
    {
        if (player == null) return false;
        if (Player.isSliding || Player.isDodging || Player.isBackdodging)
        {
            // Interrumpir dodge/slide
            Player.InterruptDodgeAndSlide();
            OrientCharacterFromInput();
            ExecuteHit4Lunge();
            return true;
        }
        return false;
    }

    #endregion

    #region Branch D: Command & Aerial Attacks

    private void ExecuteDirectLauncher()
    {
        SetCombatState(CombatState.DirectLauncher, hit5LauncherDuration);

        // Impulso vertical launcher directo a +7.8 m/s
        if (Rb2D != null)
        {
            Rb2D.linearVelocity = new Vector2(0f, hit5LauncherJumpForce);
        }

        PlayAnimationState("DodgeAttack2", 0f);
        SetAnimatorCombatBools(isDodgeAttacking: true);

        CanTriggerAirSlam = true;

        if (activeAttackRoutine != null) StopCoroutine(activeAttackRoutine);
        activeAttackRoutine = StartCoroutine(SingleHitboxRoutine(0.16f, hit5LauncherReach, 1.2f, 5, 2.5f, hit5LauncherJumpForce, true));
    }

    private void ExecuteJumpAttack(int step)
    {
        CombatState targetState = (step == 1) ? CombatState.JumpAttack1 : CombatState.JumpAttack2;
        float duration = (step == 1) ? jumpAttack1Duration : jumpAttack2Duration;
        float reach = (step == 1) ? jumpAttack1Reach : jumpAttack2Reach;

        SetCombatState(targetState, duration);

        // REGLA: Conservar la inercia del salto horizontal, dando una leve sustentación vertical
        if (Rb2D != null)
        {
            float airLift = Mathf.Max(Rb2D.linearVelocity.y * 0.5f, 1.2f);
            Rb2D.linearVelocity = new Vector2(Rb2D.linearVelocity.x, airLift);
        }

        string stateName = (step == 1) ? "JumpAttack" : "JumpAttack2";
        PlayAnimationState(stateName, 0f);
        SetAnimatorCombatBools(isJumpAttacking: true);

        if (activeAttackRoutine != null) StopCoroutine(activeAttackRoutine);
        activeAttackRoutine = StartCoroutine(SingleHitboxRoutine(0.10f, reach, 0.95f, step, 3.5f, 0f, false));
    }

    private void UpdateAerialComboState(float normTime, float chainStart, float chainEnd, Action nextAction)
    {
        // Si aterriza mientras ataca en el aire, terminar ataque suavemente
        if (Player != null && Player.isGrounded && stateTimer > 0.05f)
        {
            FinishCombat();
            return;
        }

        // Si presiona S + J o CanTriggerAirSlam, entra a Finisher
        bool isDownHeld = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || Input.GetAxisRaw("Vertical") < -0.5f;
        if ((isDownHeld || CanTriggerAirSlam) && Input.GetKeyDown(attackKey))
        {
            StartVerticalFinisher();
            return;
        }

        // Encadenar a Jump Attack 2 si pulsa J en la ventana
        if (Input.GetKeyDown(attackKey) && normTime >= chainStart && normTime <= chainEnd)
        {
            OrientCharacterFromInput();
            nextAction?.Invoke();
            return;
        }

        if (normTime >= 0.95f)
        {
            FinishCombat();
        }
    }

    private void UpdateAerialRecoveryState(float normTime)
    {
        if (Player != null && Player.isGrounded && stateTimer > 0.05f)
        {
            FinishCombat();
            return;
        }

        // Finisher disponible si pulsa S+J
        bool isDownHeld = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || Input.GetAxisRaw("Vertical") < -0.5f;
        if ((isDownHeld || CanTriggerAirSlam) && Input.GetKeyDown(attackKey))
        {
            StartVerticalFinisher();
            return;
        }

        if (normTime >= 0.95f)
        {
            FinishCombat();
        }
    }

    #endregion

    #region Generic Combo Helpers & Recovery

    private void UpdateStandardComboState(float normTime, float chainStart, float chainEnd, Action nextAction)
    {
        if (Input.GetKeyDown(attackKey) && normTime >= chainStart && normTime <= chainEnd)
        {
            OrientCharacterFromInput();
            nextAction?.Invoke();
            return;
        }

        if (normTime >= 0.95f)
        {
            FinishCombat();
        }
    }

    private void UpdateLauncherState(float normTime)
    {
        // Al ascender, si el jugador pulsa J o S+J, encadena al Finisher vertical
        if (Input.GetKeyDown(attackKey) && stateTimer >= 0.12f)
        {
            StartVerticalFinisher();
            return;
        }

        if (normTime >= 0.95f)
        {
            FinishCombat();
        }
    }

    private void UpdateRecoveryOnlyState(float normTime)
    {
        if (normTime >= 0.95f)
        {
            FinishCombat();
        }
    }

    public void InterruptCombat()
    {
        if (activeAttackRoutine != null)
        {
            StopCoroutine(activeAttackRoutine);
            activeAttackRoutine = null;
        }
        if (verticalFinisherRoutine != null)
        {
            StopCoroutine(verticalFinisherRoutine);
            verticalFinisherRoutine = null;
        }

        IsHorizontalLocked = false;
        if (Rb2D != null) Rb2D.gravityScale = originalGravityScale;

        FinishCombat();
    }

    private void FinishCombat()
    {
        CurrentState = CombatState.Idle;
        stateTimer = 0f;
        currentStateDuration = 0f;
        SetAnimatorCombatBools(false, false, false, false, false);
    }

    private void SetCombatState(CombatState newState, float duration)
    {
        CurrentState = newState;
        stateTimer = 0f;
        currentStateDuration = duration;
        comboResetTimer = comboResetMaxTime;
    }

    #endregion

    #region Hitboxes & Damage Routines

    private IEnumerator SingleHitboxRoutine(float delay, float reach, float height, int comboHitIndex, float pushForce, float knockUpForce, bool isEpic)
    {
        yield return new WaitForSeconds(delay);

        HitSfx.PlaySlash(isEpic);

        Vector2 lookDir = GetFacingVector();
        Vector2 center = (Vector2)transform.position + Vector2.up * (height * 0.5f) + lookDir * (reach * 0.5f);
        Vector2 size = new Vector2(reach, height);

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f);
        bool connected = false;
        var damagedReceivers = new HashSet<HitReceiver>();

        foreach (var col in hits)
        {
            if (col.gameObject == gameObject) continue;

            var receiver = col.GetComponent<HitReceiver>();
            if (receiver != null && !damagedReceivers.Contains(receiver))
            {
                damagedReceivers.Add(receiver);
                connected = true;
                Vector2 hitPoint = col.ClosestPoint(center);

                // Aplica retroceso horizontal y vertical adecuado
                receiver.OnHit(hitPoint, comboHitIndex, transform.position, lookDir, knockUpForce);
                SpawnHitFX(hitPoint);
            }
        }

        if (connected)
        {
            comboCounter++;
            comboResetTimer = comboResetMaxTime;
            OnComboHit?.Invoke(comboCounter);
            TriggerHitstop(isEpic ? 0.08f : 0.05f);
        }
    }

    private IEnumerator DoublePulseHitboxRoutine(float delay1, float delay2, float reach, float height, int comboHitIndex, float pushForce)
    {
        // Primer pulso
        yield return new WaitForSeconds(delay1);
        HitSfx.PlaySlash(false);
        ApplyPulseHitbox(reach, height, comboHitIndex, pushForce * 0.5f);

        // Segundo pulso
        float remainingDelay = Mathf.Max(0.01f, delay2 - delay1);
        yield return new WaitForSeconds(remainingDelay);
        HitSfx.PlaySlash(false);
        ApplyPulseHitbox(reach, height, comboHitIndex, pushForce);
    }

    private void ApplyPulseHitbox(float reach, float height, int comboHitIndex, float pushForce)
    {
        Vector2 lookDir = GetFacingVector();
        Vector2 center = (Vector2)transform.position + Vector2.up * (height * 0.5f) + lookDir * (reach * 0.5f);
        Vector2 size = new Vector2(reach, height);

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f);
        bool connected = false;
        var damaged = new HashSet<HitReceiver>();

        foreach (var col in hits)
        {
            if (col.gameObject == gameObject) continue;

            var receiver = col.GetComponent<HitReceiver>();
            if (receiver != null && !damaged.Contains(receiver))
            {
                damaged.Add(receiver);
                connected = true;
                Vector2 hitPoint = col.ClosestPoint(center);
                receiver.OnHit(hitPoint, comboHitIndex, transform.position, lookDir, 0f);
                SpawnHitFX(hitPoint);
            }
        }

        if (connected)
        {
            comboCounter++;
            comboResetTimer = comboResetMaxTime;
            OnComboHit?.Invoke(comboCounter);
            TriggerHitstop(0.04f);
        }
    }

    #endregion

    #region Visual Effects & Spawners

    public void SpawnHitFX(Vector2 hitPoint)
    {
        if (hitFxPrefab != null)
        {
            Instantiate(hitFxPrefab, hitPoint, Quaternion.identity);
        }
    }

    public void SpawnFeetSmoke(float xOffsetBehind = 0.30f, float yOffset = 0.18f)
    {
        if (smokeFxPrefab != null)
        {
            Vector2 lookDir = GetFacingVector();
            float baseY = vfxSpawner != null ? vfxSpawner.position.y : (transform.position.y + yOffset);
            if (Mathf.Abs(yOffset - 0.18f) > 0.05f)
            {
                baseY = transform.position.y + yOffset;
            }

            Vector3 spawnPos = new Vector3(transform.position.x - lookDir.x * xOffsetBehind, baseY, transform.position.z);
            GameObject smoke = Instantiate(smokeFxPrefab, spawnPos, Quaternion.identity);

            // Sincronizar giro con la orientación del personaje
            if (lookDir.x < 0f)
            {
                smoke.transform.localScale = new Vector3(-1f, 1f, 1f);
            }
        }
    }

    public void SpawnAirDashSmoke()
    {
        if (smokeFxPrefab != null)
        {
            Vector2 lookDir = GetFacingVector();
            // En el aire: aparece directamente detrás del personaje a la altura de su centro/cintura
            Vector3 spawnPos = new Vector3(transform.position.x - lookDir.x * 0.35f, transform.position.y + 0.35f, transform.position.z);
            GameObject smoke = Instantiate(smokeFxPrefab, spawnPos, Quaternion.identity);

            // Sincronizar giro horizontal según dirección del dash
            if (lookDir.x < 0f)
            {
                smoke.transform.localScale = new Vector3(-1f, 1f, 1f);
            }
        }
    }

    public void TriggerHitstop(float duration = 0.05f)
    {
        if (hitstopRoutine != null) StopCoroutine(hitstopRoutine);
        hitstopRoutine = StartCoroutine(HitstopRoutine(duration));
    }

    private IEnumerator HitstopRoutine(float duration)
    {
        if (Anim != null) Anim.speed = 0.05f;
        yield return new WaitForSeconds(duration);
        if (Anim != null) Anim.speed = 1.0f;
        hitstopRoutine = null;
    }

    #endregion

    #region Helpers & Orientación

    /// <summary>
    /// Orientación libre del personaje con A o D entre golpes sin alterar la ruta del combo.
    /// </summary>
    public void OrientCharacterFromInput()
    {
        if (IsHorizontalLocked) return;

        float h = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(h) > 0.05f)
        {
            int newFacing = (h > 0f) ? -1 : 1; // Convención del proyecto: -1 = Derecha (+X), 1 = Izquierda (-X)
            if (Player != null)
            {
                Player.facingDirection = newFacing;
                Player.transform.localScale = new Vector3(newFacing * Mathf.Abs(Player.transform.localScale.x), Player.transform.localScale.y, 1f);
            }
        }
    }

    public Vector2 GetFacingVector()
    {
        int facing = (player != null) ? Player.facingDirection : -1;
        return (facing == -1) ? Vector2.right : Vector2.left;
    }

    private void PlayAnimationState(string stateName, float normalizedTime = 0f)
    {
        if (Anim != null)
        {
            Anim.Play(stateName, 0, normalizedTime);
        }
    }

    private void SetAnimatorCombatBools(bool isAttacking = false, bool isDodgeAttacking = false, bool isVerticalAttacking = false, bool isJumpAttacking = false, bool isVariation = false)
    {
        if (Anim == null) return;

        Anim.SetBool("isAttacking", isAttacking);
        Anim.SetBool("isDodgeAttacking", isDodgeAttacking);
        Anim.SetBool("isVerticalAttacking", isVerticalAttacking);
        Anim.SetBool("isJumpAttacking", isJumpAttacking);
        Anim.SetBool("isVariation", isVariation);

        if (Player != null)
        {
            Player.isAttacking = isAttacking;
            Player.isDodgeAttacking = isDodgeAttacking;
            Player.isVerticalAttacking = isVerticalAttacking;
            Player.isJumpAttacking = isJumpAttacking;
        }
    }

    #endregion
}
