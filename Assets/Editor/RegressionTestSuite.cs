using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class RegressionTestSuite
{
    [MenuItem("Combat/Run Full Regression Test Suite (Phase 7)")]
    public static void RunFullRegression()
    {
        Debug.Log("<color=cyan>==============================================================</color>");
        Debug.Log("<color=cyan>[PHASE 7 QA]</color> INICIANDO SUITE DE REGRESIÓN COMPLETA DE GAMEPLAY");
        Debug.Log("<color=cyan>==============================================================</color>");

        var player = Object.FindAnyObjectByType<NewMonoBehaviourScript>();
        if (player == null)
        {
            Debug.LogError("[PHASE 7 QA] No se encontró NewMonoBehaviourScript en la escena activa.");
            return;
        }

        var rb2d = player.GetComponent<Rigidbody2D>();
        var animator = player.GetComponent<Animator>();
        var col = player.GetComponent<Collider2D>();

        var type = typeof(NewMonoBehaviourScript);
        type.GetField("rb2d", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(player, rb2d);
        type.GetField("animator", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(player, animator);

        // Invocar SetupColliders si es necesario
        var methodSetupCol = type.GetMethod("SetupColliders", BindingFlags.NonPublic | BindingFlags.Instance);
        methodSetupCol?.Invoke(player, null);

        int passed = 0;
        int failed = 0;
        int total = 0;

        // Reflection helpers
        var fFacingDir = type.GetField("facingDirection", BindingFlags.NonPublic | BindingFlags.Instance);
        var fLedgeCorner = type.GetField("ledgeCorner", BindingFlags.NonPublic | BindingFlags.Instance);
        var fLastLedgeCorner = type.GetField("lastLedgeCorner", BindingFlags.NonPublic | BindingFlags.Instance);
        var fLedgeCooldown = type.GetField("ledgeCooldown", BindingFlags.NonPublic | BindingFlags.Instance);
        var fHangTimer = type.GetField("hangTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        var fDodgeTimer = type.GetField("dodgeTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        var fCurrentDodgeSpeed = type.GetField("currentDodgeSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
        var fDodgeDirection = type.GetField("dodgeDirection", BindingFlags.NonPublic | BindingFlags.Instance);
        var fDodgeCooldownTimer = type.GetField("dodgeCooldownTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        var fSlideTimer = type.GetField("slideTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        var fCurrentSlideSpeed = type.GetField("currentSlideSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
        var fAttackTimer = type.GetField("attackTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        var fCurrentAttackDuration = type.GetField("currentAttackDuration", BindingFlags.NonPublic | BindingFlags.Instance);
        var fComboResetTimer = type.GetField("comboResetTimer", BindingFlags.NonPublic | BindingFlags.Instance);

        var mStartDodge = type.GetMethod("StartDodge", BindingFlags.NonPublic | BindingFlags.Instance);
        var mStopDodge = type.GetMethod("StopDodge", BindingFlags.NonPublic | BindingFlags.Instance);
        var mStartSlide = type.GetMethod("StartSlide", BindingFlags.NonPublic | BindingFlags.Instance);
        var mStopSlide = type.GetMethod("StopSlide", BindingFlags.NonPublic | BindingFlags.Instance);
        var mExecuteAttack = type.GetMethod("ExecuteAttack", BindingFlags.NonPublic | BindingFlags.Instance);
        var mFinishAttack = type.GetMethod("FinishAttack", BindingFlags.NonPublic | BindingFlags.Instance);
        var mExecuteDodgeAttack = type.GetMethod("ExecuteDodgeAttack", BindingFlags.NonPublic | BindingFlags.Instance);
        var mFinishDodgeAttack = type.GetMethod("FinishDodgeAttack", BindingFlags.NonPublic | BindingFlags.Instance);
        var mExecuteVerticalAttack = type.GetMethod("ExecuteVerticalAttack", BindingFlags.NonPublic | BindingFlags.Instance);
        var mFinishVerticalAttack = type.GetMethod("FinishVerticalAttack", BindingFlags.NonPublic | BindingFlags.Instance);
        var mExecWallJump = type.GetMethod("ExecuteLedgeWallJump", BindingFlags.NonPublic | BindingFlags.Instance);
        var mDropFromLedge = type.GetMethod("DropFromLedge", BindingFlags.NonPublic | BindingFlags.Instance);
        var mSetColliderCrouched = type.GetMethod("SetColliderCrouched", BindingFlags.NonPublic | BindingFlags.Instance);

        void AssertTest(string testName, bool condition, string detail)
        {
            total++;
            if (condition)
            {
                passed++;
                Debug.Log($"<color=green>[PASS]</color> <b>{testName}</b>: {detail}");
            }
            else
            {
                failed++;
                Debug.LogError($"<color=red>[FAIL]</color> <b>{testName}</b>: {detail}");
            }
        }

        // =====================================================================
        // SECCIÓN 1: COMBATE Y COMBOS
        // =====================================================================
        Debug.Log("<color=yellow>--- [SECCIÓN 1] Validación de Combate y Combos ---</color>");

        // Test 1.1: Ejecución de Ataque 1
        mExecuteAttack.Invoke(player, new object[] { 1 });
        AssertTest("1.1 Ataque con J (Paso 1)",
            player.isAttacking && player.currentComboStep == 1 && (float)fCurrentAttackDuration.GetValue(player) == player.attack1Duration,
            $"isAttacking={player.isAttacking}, step={player.currentComboStep}, dur={fCurrentAttackDuration.GetValue(player)}");

        // Test 1.2: Encadenamiento a Ataque 2
        mExecuteAttack.Invoke(player, new object[] { 2 });
        AssertTest("1.2 Encadenamiento a Ataque 2",
            player.isAttacking && player.currentComboStep == 2 && (float)fCurrentAttackDuration.GetValue(player) == player.attack2Duration,
            $"isAttacking={player.isAttacking}, step={player.currentComboStep}");

        // Test 1.3: Encadenamiento a Ataque 3 (Fin de combo)
        mExecuteAttack.Invoke(player, new object[] { 3 });
        AssertTest("1.3 Encadenamiento a Ataque 3",
            player.isAttacking && player.currentComboStep == 3 && (float)fCurrentAttackDuration.GetValue(player) == player.attack3Duration,
            $"isAttacking={player.isAttacking}, step={player.currentComboStep}");

        // Test 1.4: Finalización de ataque normal
        mFinishAttack.Invoke(player, null);
        AssertTest("1.4 Finalización de ataque y retorno",
            !player.isAttacking && player.currentComboStep == 0,
            $"isAttacking={player.isAttacking}, step={player.currentComboStep}");

        // Test 1.5: Dodge Attack 1 y 2
        mExecuteDodgeAttack.Invoke(player, new object[] { 1 });
        AssertTest("1.5 Dodge Attack 1 ejecutado",
            player.isDodgeAttacking && player.currentDodgeStep == 1,
            $"isDodgeAttacking={player.isDodgeAttacking}, step={player.currentDodgeStep}");

        mExecuteDodgeAttack.Invoke(player, new object[] { 2 });
        AssertTest("1.6 Dodge Attack 2 encadenado",
            player.isDodgeAttacking && player.currentDodgeStep == 2,
            $"isDodgeAttacking={player.isDodgeAttacking}, step={player.currentDodgeStep}");

        mFinishDodgeAttack.Invoke(player, null);
        AssertTest("1.7 Finalización de Dodge Attack",
            !player.isDodgeAttacking && player.currentDodgeStep == 0,
            $"isDodgeAttacking={player.isDodgeAttacking}");

        // Test 1.8: Ataque Vertical (W+J)
        mExecuteVerticalAttack.Invoke(player, null);
        AssertTest("1.8 Ataque Vertical y elevación",
            player.isVerticalAttacking && rb2d.linearVelocity.y >= player.verticalAttackJumpForce - 0.1f,
            $"isVerticalAttacking={player.isVerticalAttacking}, VelY={rb2d.linearVelocity.y:F1}");

        mFinishVerticalAttack.Invoke(player, null);
        AssertTest("1.9 Finalización de Ataque Vertical",
            !player.isVerticalAttacking,
            $"isVerticalAttacking={player.isVerticalAttacking}");

        // =====================================================================
        // SECCIÓN 2: MOVIMIENTO, AGACHADO, COLLIDERS Y SLIDE
        // =====================================================================
        Debug.Log("<color=yellow>--- [SECCIÓN 2] Validación de Movimiento, Agachado, Colliders y Slide ---</color>");

        // Test 2.1: Reducción segura del collider al agacharse
        var capCol = player.GetComponent<CapsuleCollider2D>();
        var boxCol = player.GetComponent<BoxCollider2D>();
        float origHeight = (capCol != null) ? capCol.size.y : (boxCol != null ? boxCol.size.y : 0f);

        mSetColliderCrouched.Invoke(player, new object[] { true });
        float crouchHeight = (capCol != null) ? capCol.size.y : (boxCol != null ? boxCol.size.y : 0f);
        AssertTest("2.1 Reducción de collider físico en agachado",
            Mathf.Approximately(crouchHeight, player.crouchColliderHeight),
            $"Altura reducida: {crouchHeight:F2}m (Esperado {player.crouchColliderHeight:F2}m)");

        // Test 2.2: Detección de techo bajo (Estructura de prueba Test_LowCeilingStructure)
        var oldPos = player.transform.position;
        player.transform.position = new Vector3(-3.5f, -4.40f, 0f); // Debajo de Test_LowCeilingStructure (-3.5, -3.95)
        bool canStandUnderCeiling = player.CanStandUp();
        AssertTest("2.2 Detección de techo bajo (CanStandUp)",
            !canStandUnderCeiling,
            $"CanStandUp bajo techo={canStandUnderCeiling} (Bloqueo correcto)");

        // Test 2.3: Restauración segura del collider en espacio abierto
        player.transform.position = oldPos;
        bool canStandOpen = player.CanStandUp();
        mSetColliderCrouched.Invoke(player, new object[] { false });
        float restoredHeight = (capCol != null) ? capCol.size.y : (boxCol != null ? boxCol.size.y : 0f);
        AssertTest("2.3 Restauración de collider en espacio libre",
            canStandOpen && Mathf.Approximately(restoredHeight, origHeight),
            $"CanStandUp libre={canStandOpen}, Altura restaurada={restoredHeight:F2}m");

        // Test 2.4: Slide con K en carrera
        player.isRunning = true;
        fFacingDir.SetValue(player, -1); // Mirando a la derecha
        mStartSlide.Invoke(player, null);
        AssertTest("2.4 Inicio de Slide en carrera",
            player.isSliding && (float)fCurrentSlideSpeed.GetValue(player) == player.slideInitialSpeed,
            $"isSliding={player.isSliding}, speed={fCurrentSlideSpeed.GetValue(player)}");

        mStopSlide.Invoke(player, new object[] { false });
        AssertTest("2.5 Finalización de Slide",
            !player.isSliding,
            $"isSliding={player.isSliding}");

        // =====================================================================
        // SECCIÓN 3: DODGE NORMAL, BACKDODGE Y SPRINT BOOST
        // =====================================================================
        Debug.Log("<color=yellow>--- [SECCIÓN 3] Dodge Normal, Backdodge y Multiplicador Sprint ---</color>");

        // Test 3.1: Dodge Normal frontal (caminando / velocidad base)
        player.isRunning = false;
        fFacingDir.SetValue(player, -1); // Mira a la derecha (+X)
        mStartDodge.Invoke(player, new object[] { false }); // Normal forward
        float speedNorm = (float)fCurrentDodgeSpeed.GetValue(player);
        float dirNorm = (float)fDodgeDirection.GetValue(player);
        AssertTest("3.1 Dodge Normal frontal base",
            player.isDodging && !player.isBackdodging && Mathf.Approximately(speedNorm, player.dodgeSpeed) && dirNorm > 0f,
            $"isDodging={player.isDodging}, speed={speedNorm:F1}, dir={dirNorm:F1}");
        mStopDodge.Invoke(player, null);

        // Test 3.2: Dodge Normal frontal con Sprint activo (multiplicador 1.35x)
        player.isRunning = true;
        mStartDodge.Invoke(player, new object[] { false });
        float speedSprint = (float)fCurrentDodgeSpeed.GetValue(player);
        AssertTest("3.2 Dodge con boost de Sprint (1.35x)",
            Mathf.Approximately(speedSprint, player.dodgeSpeed * player.dodgeRunMultiplier),
            $"Velocidad Sprint: {speedSprint:F2} (Esperado {player.dodgeSpeed * player.dodgeRunMultiplier:F2})");
        mStopDodge.Invoke(player, null);

        // Test 3.3: Backdodge (movimiento opuesto manteniendo el frente)
        player.isRunning = false;
        fFacingDir.SetValue(player, -1); // Mira a la derecha
        mStartDodge.Invoke(player, new object[] { true }); // Backwards
        float dirBack = (float)fDodgeDirection.GetValue(player);
        int facingBack = (int)fFacingDir.GetValue(player);
        AssertTest("3.3 Backdodge en sentido opuesto sin invertir frente",
            player.isBackdodging && !player.isDodging && dirBack < 0f && facingBack == -1,
            $"isBackdodging={player.isBackdodging}, dir={dirBack:F1} (Negativo=Atrás), facing={facingBack} (Frente conservado)");
        mStopDodge.Invoke(player, null);

        // Test 3.4: Cooldown de evasión activado tras StopDodge
        float dodgeCd = (float)fDodgeCooldownTimer.GetValue(player);
        AssertTest("3.4 Cooldown de evasión activo tras dodge",
            dodgeCd >= player.dodgeCooldown - 0.05f,
            $"Dodge Cooldown={dodgeCd:F2}s (Esperado {player.dodgeCooldown:F2}s)");
        fDodgeCooldownTimer.SetValue(player, 0f); // Reset cooldown para siguientes pruebas

        // =====================================================================
        // SECCIÓN 4: WALL JUMP HORIZONTAL DESDE AGARRE
        // =====================================================================
        Debug.Log("<color=yellow>--- [SECCIÓN 4] Wall Jump Horizontal desde Agarre ---</color>");

        // Test 4.1: Wall Jump hacia la Derecha (agarrado a muro izquierdo)
        player.isHanging = true;
        fFacingDir.SetValue(player, 1); // Mirando a la izquierda hacia el muro
        fLedgeCorner.SetValue(player, new Vector2(-6.2f, -2.6f));
        mExecWallJump.Invoke(player, new object[] { Vector2.left });
        AssertTest("4.1 Wall Jump a la Derecha (D + ESPACIO)",
            !player.isHanging && player.isWallJumping && rb2d.linearVelocity.x > 4.5f && (int)fFacingDir.GetValue(player) == -1,
            $"isHanging={player.isHanging}, VelX={rb2d.linearVelocity.x:F1}, facing={fFacingDir.GetValue(player)}");

        // Test 4.2: Wall Jump hacia la Izquierda (agarrado a muro derecho)
        player.isHanging = true;
        fFacingDir.SetValue(player, -1); // Mirando a la derecha hacia el muro
        fLedgeCorner.SetValue(player, new Vector2(-3.4f, -2.6f));
        mExecWallJump.Invoke(player, new object[] { Vector2.right });
        AssertTest("4.2 Wall Jump a la Izquierda (A + ESPACIO)",
            !player.isHanging && player.isWallJumping && rb2d.linearVelocity.x < -4.5f && (int)fFacingDir.GetValue(player) == 1,
            $"isHanging={player.isHanging}, VelX={rb2d.linearVelocity.x:F1}, facing={fFacingDir.GetValue(player)}");

        // Test 4.3: Protección anti re-enganche en mismo muro
        Vector2 lastCorner = (Vector2)fLastLedgeCorner.GetValue(player);
        float ledgeCd = (float)fLedgeCooldown.GetValue(player);
        AssertTest("4.3 Protección anti re-enganche a la misma pared",
            ledgeCd > 0f && Vector2.Distance(new Vector2(-3.4f, -2.6f), lastCorner) < 0.6f,
            $"LedgeCooldown={ledgeCd:F2}s, CornerDistance={Vector2.Distance(new Vector2(-3.4f, -2.6f), lastCorner):F3}m");

        // =====================================================================
        // SECCIÓN 5: MATRIZ DE CONFLICTOS E INTEGRIDAD
        // =====================================================================
        Debug.Log("<color=yellow>--- [SECCIÓN 5] Matriz de Conflictos e Integridad ---</color>");

        // Test 5.1: Evasión bloquea inicio de ataque con J
        player.isDodging = true;
        var methodHandleCombat = type.GetMethod("HandleCombatInputs", BindingFlags.NonPublic | BindingFlags.Instance);
        methodHandleCombat.Invoke(player, null);
        AssertTest("5.1 Evasión bloquea activación de ataques con J",
            !player.isAttacking && player.isDodging,
            $"isAttacking={player.isAttacking} (Bloqueo exitoso durante dodge)");
        player.isDodging = false;

        // Test 5.2: Evasión bloquea agachado con S
        player.isDodging = true;
        var methodHandleCrouch = type.GetMethod("HandleCrouchInputs", BindingFlags.NonPublic | BindingFlags.Instance);
        methodHandleCrouch.Invoke(player, null);
        AssertTest("5.2 Evasión bloquea postura agachada con S",
            !player.isCrouching && player.isDodging,
            $"isCrouching={player.isCrouching} (Bloqueo exitoso durante dodge)");
        player.isDodging = false;

        // Test 5.3: Ataque bloquea movimiento horizontal y flip
        player.isAttacking = true;
        bool moveBlockedDuringAtk = player.IsInCombatAction;
        AssertTest("5.3 Ataque bloquea movimiento horizontal y volteo involuntario",
            moveBlockedDuringAtk,
            $"IsInCombatAction={moveBlockedDuringAtk}");
        player.isAttacking = false;

        // Test 5.4: Estado efectivo de carrera (IsEffectiveRunning)
        player.isRunning = true;
        var fieldMove = type.GetField("move", BindingFlags.NonPublic | BindingFlags.Instance);
        var fieldGrounded = type.GetField("isGrounded", BindingFlags.NonPublic | BindingFlags.Instance);
        fieldGrounded.SetValue(player, true);
        fieldMove.SetValue(player, 1.0f);
        bool runEffective = player.IsEffectiveRunning;
        AssertTest("5.4 Estado efectivo de carrera activo (IsEffectiveRunning)",
            runEffective,
            $"IsEffectiveRunning={runEffective}");

        // Test 5.5: Desactivación inmediata del indicador de carrera al saltar o agacharse
        player.isCrouching = true;
        bool runDeactivatedOnCrouch = !player.IsEffectiveRunning;
        AssertTest("5.5 Desactivación inmediata de sprint al agacharse",
            runDeactivatedOnCrouch,
            $"IsEffectiveRunning={player.IsEffectiveRunning} (Desactivado de inmediato)");
        player.isCrouching = false;
        player.isRunning = false;
        fieldMove.SetValue(player, 0f);

        // =====================================================================
        // RESTAURACIÓN Y REPORTE
        // =====================================================================
        player.isHanging = false;
        player.isClimbing = false;
        player.isSliding = false;
        player.isDodging = false;
        player.isBackdodging = false;
        player.isAttacking = false;
        player.isDodgeAttacking = false;
        player.isVerticalAttacking = false;
        player.isCrouching = false;
        player.isWallJumping = false;
        rb2d.bodyType = RigidbodyType2D.Dynamic;
        rb2d.linearVelocity = Vector2.zero;
        fLedgeCooldown.SetValue(player, 0f);
        fLastLedgeCorner.SetValue(player, Vector2.positiveInfinity);
        fDodgeCooldownTimer.SetValue(player, 0f);

        Debug.Log("<color=cyan>==============================================================</color>");
        if (failed == 0)
        {
            Debug.Log($"<color=cyan>[PHASE 7 QA RESULT]</color> <color=green><b>TODAS LAS PRUEBAS DE REGRESIÓN APROBADAS: {passed}/{total} (100% PASS)</b></color>");
        }
        else
        {
            Debug.LogError($"<color=cyan>[PHASE 7 QA RESULT]</color> <color=red><b>ERRORES ENCONTRADOS: {failed}/{total} FALLIDAS</b></color>");
        }
        Debug.Log("<color=cyan>==============================================================</color>");
    }
}
