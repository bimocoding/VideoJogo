using UnityEngine;

/// <summary>
/// Capa centralizada de entrada para el jugador.
/// - Se ejecuta antes que player.cs y PlayerCombatController.cs.
/// - Implementa SOCD (Last Input Priority) en ejes horizontales y verticales.
/// - Gestiona buffers con expiración y consumo explícito para evitar pérdidas y dobles disparos.
/// - Mantiene estados cacheados seguros para consultar desde FixedUpdate.
/// </summary>
[DefaultExecutionOrder(-100)]
public class PlayerInputHandler : MonoBehaviour
{
    [Header("Asignación de Teclas")]
    public KeyCode attackKey = KeyCode.J;
    public KeyCode dodgeKey = KeyCode.K;
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode healKey = KeyCode.H;
    public KeyCode crouchKey = KeyCode.S;

    [Header("Ventanas de Input Buffer (segundos)")]
    [Tooltip("Tiempo que se recuerda una pulsación de Salto antes de tocar suelo o salir de un bloqueo.")]
    public float jumpBufferDuration = 0.15f;
    [Tooltip("Tiempo que se recuerda una pulsación de Ataque antes de abrirse una ventana de combo o terminar una acción.")]
    public float attackBufferDuration = 0.14f;
    [Tooltip("Tiempo que se recuerda una pulsación de Esquive/Dash antes de terminar cooldown o recuperar control.")]
    public float dodgeBufferDuration = 0.12f;

    // Ejes resueltos con Last Input Priority (-1, 0, +1)
    public float MoveX { get; private set; }
    public float MoveY { get; private set; }

    // Estados mantenidos (seguros para Update y FixedUpdate)
    public bool JumpHeld { get; private set; }
    public bool UpHeld { get; private set; }
    public bool DownHeld { get; private set; }

    // Eventos puntuales de este frame (para acciones sin buffer o toggles)
    public bool JumpPressedThisFrame { get; private set; }
    public bool JumpReleasedThisFrame { get; private set; }
    public bool AttackPressedThisFrame { get; private set; }
    public bool DodgePressedThisFrame { get; private set; }
    public bool SprintTogglePressedThisFrame { get; private set; }
    public bool HealPressedThisFrame { get; private set; }
    public bool UpPressedThisFrame { get; private set; }
    public bool DownPressedThisFrame { get; private set; }

    // Estado de Buffers
    private float jumpBufferTimer;
    private float attackBufferTimer;
    private float dodgeBufferTimer;

    public bool JumpBufferActive => jumpBufferTimer > 0f;
    public bool AttackBufferActive => attackBufferTimer > 0f;
    public bool DodgeBufferActive => dodgeBufferTimer > 0f;

    // Snapshots direccionales al momento de bufferear un ataque (evita perder W+J o S+J en transiciones)
    public bool AttackBufferedUp { get; private set; }
    public bool AttackBufferedDown { get; private set; }

    // Estado interno SOCD (Last Input Priority)
    private int lastHorizontalDir = 0;
    private int lastVerticalDir = 0;

    private void Update()
    {
        ReadDirectionalAxesSOCD();
        ReadActionInputs();
        UpdateBuffers();
    }

    private void ReadDirectionalAxesSOCD()
    {
        bool leftHeld = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        bool rightHeld = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        bool leftDown = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
        bool rightDown = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);

        if (leftDown) lastHorizontalDir = -1;
        if (rightDown) lastHorizontalDir = 1;

        if (leftHeld && rightHeld)
        {
            MoveX = lastHorizontalDir != 0 ? lastHorizontalDir : 0f;
        }
        else if (leftHeld)
        {
            MoveX = -1f;
            lastHorizontalDir = -1;
        }
        else if (rightHeld)
        {
            MoveX = 1f;
            lastHorizontalDir = 1;
        }
        else
        {
            // Respaldo para mando / otros ejes del Legacy Input Manager
            float rawH = Input.GetAxisRaw("Horizontal");
            MoveX = Mathf.Abs(rawH) > 0.1f ? Mathf.Sign(rawH) : 0f;
            lastHorizontalDir = 0;
        }

        bool upHeld = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        bool downHeld = Input.GetKey(crouchKey) || Input.GetKey(KeyCode.DownArrow);
        bool upDown = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
        bool downDown = Input.GetKeyDown(crouchKey) || Input.GetKeyDown(KeyCode.DownArrow);

        UpPressedThisFrame = upDown;
        DownPressedThisFrame = downDown;

        if (upDown) lastVerticalDir = 1;
        if (downDown) lastVerticalDir = -1;

        if (upHeld && downHeld)
        {
            MoveY = lastVerticalDir != 0 ? lastVerticalDir : 0f;
        }
        else if (upHeld)
        {
            MoveY = 1f;
            lastVerticalDir = 1;
        }
        else if (downHeld)
        {
            MoveY = -1f;
            lastVerticalDir = -1;
        }
        else
        {
            float rawV = Input.GetAxisRaw("Vertical");
            MoveY = Mathf.Abs(rawV) > 0.5f ? Mathf.Sign(rawV) : 0f;
            lastVerticalDir = 0;
        }

        UpHeld = MoveY > 0.5f;
        DownHeld = MoveY < -0.5f;
    }

    private void ReadActionInputs()
    {
        JumpPressedThisFrame = Input.GetButtonDown("Jump") || Input.GetKeyDown(jumpKey);
        JumpHeld = Input.GetButton("Jump") || Input.GetKey(jumpKey);
        JumpReleasedThisFrame = Input.GetButtonUp("Jump") || Input.GetKeyUp(jumpKey);

        AttackPressedThisFrame = Input.GetKeyDown(attackKey);
        DodgePressedThisFrame = Input.GetKeyDown(dodgeKey);
        SprintTogglePressedThisFrame = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
        HealPressedThisFrame = Input.GetKeyDown(healKey);
    }

    private void UpdateBuffers()
    {
        if (JumpPressedThisFrame)
        {
            jumpBufferTimer = jumpBufferDuration;
        }
        else if (jumpBufferTimer > 0f)
        {
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - Time.deltaTime);
        }

        if (AttackPressedThisFrame)
        {
            attackBufferTimer = attackBufferDuration;
            AttackBufferedUp = UpHeld || UpPressedThisFrame;
            AttackBufferedDown = DownHeld || DownPressedThisFrame;
        }
        else if (attackBufferTimer > 0f)
        {
            attackBufferTimer = Mathf.Max(0f, attackBufferTimer - Time.deltaTime);
            if (attackBufferTimer <= 0f)
            {
                AttackBufferedUp = false;
                AttackBufferedDown = false;
            }
        }

        if (DodgePressedThisFrame)
        {
            dodgeBufferTimer = dodgeBufferDuration;
        }
        else if (dodgeBufferTimer > 0f)
        {
            dodgeBufferTimer = Mathf.Max(0f, dodgeBufferTimer - Time.deltaTime);
        }
    }

    // =========================================================
    // MÉTODOS DE CONSUMO (evitan dobles disparos en el frame)
    // =========================================================
    public void ConsumeJump()
    {
        jumpBufferTimer = 0f;
    }

    public void ConsumeAttack()
    {
        attackBufferTimer = 0f;
        AttackBufferedUp = false;
        AttackBufferedDown = false;
    }

    public void ConsumeDodge()
    {
        dodgeBufferTimer = 0f;
    }

    public void ClearAllBuffers()
    {
        jumpBufferTimer = 0f;
        attackBufferTimer = 0f;
        dodgeBufferTimer = 0f;
        AttackBufferedUp = false;
        AttackBufferedDown = false;
    }

    /// <summary>
    /// Helper de dirección relativa al facingDirection del proyecto (-1 = Derecha, +1 = Izquierda).
    /// Devuelve +1 si empuja hacia adelante, -1 si empuja hacia atrás, 0 si está neutral.
    /// </summary>
    public int GetRelativeHorizontalIntent(int facingDirection)
    {
        if (Mathf.Abs(MoveX) < 0.1f) return 0;
        int facingSign = (facingDirection == -1) ? 1 : -1;
        return (Mathf.Sign(MoveX) == facingSign) ? 1 : -1;
    }
}
