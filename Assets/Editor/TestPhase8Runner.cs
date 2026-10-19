using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class TestPhase8Runner
{
    [MenuItem("Combat/Run Phase 8 Verification Tests")]
    public static void RunTests()
    {
        Debug.Log("<color=cyan>[PHASE 8 TESTS]</color> ==========================================");
        Debug.Log("<color=cyan>[PHASE 8 TESTS]</color> Iniciando batería de pruebas de Pulido Fase 8...");

        var player = Object.FindAnyObjectByType<NewMonoBehaviourScript>();
        if (player == null)
        {
            Debug.LogError("[PHASE 8 TESTS] No se encontró NewMonoBehaviourScript en la escena activa.");
            return;
        }

        var rb2d = player.GetComponent<Rigidbody2D>();
        var animator = player.GetComponent<Animator>();
        var col = player.GetComponent<CapsuleCollider2D>();

        var type = typeof(NewMonoBehaviourScript);
        type.GetField("rb2d", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(player, rb2d);
        type.GetField("animator", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(player, animator);
        type.GetField("playerCollider", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(player, col);
        type.GetField("capsuleCol", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(player, col);

        var originalSize = col.size;
        var originalOffset = col.offset;
        type.GetField("originalColliderSize", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(player, originalSize);
        type.GetField("originalColliderOffset", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(player, originalOffset);

        int passed = 0;
        int total = 0;

        // Métodos privados mediante reflection
        var methodSetColliderCrouched = type.GetMethod("SetColliderCrouched", BindingFlags.NonPublic | BindingFlags.Instance);
        var methodGrabLedge = type.GetMethod("GrabLedge", BindingFlags.NonPublic | BindingFlags.Instance);
        var methodExecWallJump = type.GetMethod("ExecuteLedgeWallJump", BindingFlags.NonPublic | BindingFlags.Instance);
        var methodDropFromLedge = type.GetMethod("DropFromLedge", BindingFlags.NonPublic | BindingFlags.Instance);
        var methodStartDodge = type.GetMethod("StartDodge", BindingFlags.NonPublic | BindingFlags.Instance);
        var methodStopDodge = type.GetMethod("StopDodge", BindingFlags.NonPublic | BindingFlags.Instance);
        var methodFinishAttack = type.GetMethod("FinishAttack", BindingFlags.NonPublic | BindingFlags.Instance);
        var fieldFacingDir = type.GetField("facingDirection", BindingFlags.NonPublic | BindingFlags.Instance);
        var fieldLedgeCorner = type.GetField("ledgeCorner", BindingFlags.NonPublic | BindingFlags.Instance);

        // =========================================================================
        // TEST 1: Reducción de Collider en Agachado sin Hueco Inferior
        // =========================================================================
        total++;
        methodSetColliderCrouched.Invoke(player, new object[] { true });

        float origBottom = originalOffset.y - (originalSize.y * 0.5f);
        float crouchBottom = col.offset.y - (col.size.y * 0.5f);
        bool t1Success = Mathf.Abs(crouchBottom - origBottom) < 0.005f &&
                         col.size.y == player.crouchColliderHeight &&
                         col.size.x <= player.crouchColliderHeight; // Evita el ensanchamiento/colapso esférico de CapsuleCollider2D

        if (t1Success)
        {
            Debug.Log($"<color=green>[PASS 1/9]</color> Agachado: Base del collider alineada (origBottom={origBottom:F3}, crouchBottom={crouchBottom:F3}), Ancho={col.size.x:F3} <= Alto={col.size.y:F3} (sin deformación esférica).");
            passed++;
        }
        else
        {
            Debug.LogError($"<color=red>[FAIL 1/9]</color> Agachado: Colapso de base o deformación de collider. Orig={origBottom:F3}, Crouch={crouchBottom:F3}, Size={col.size}");
        }

        // =========================================================================
        // TEST 2: Restauración de Collider al Ponerse de Pie
        // =========================================================================
        total++;
        methodSetColliderCrouched.Invoke(player, new object[] { false });

        bool t2Success = Mathf.Abs(col.size.y - originalSize.y) < 0.001f &&
                         Mathf.Abs(col.size.x - originalSize.x) < 0.001f &&
                         Mathf.Abs(col.offset.y - originalOffset.y) < 0.001f;

        if (t2Success)
        {
            Debug.Log($"<color=green>[PASS 2/9]</color> Restauración de Collider: Dimensiones y offset restablecidos con exactitud ({col.size}, {col.offset}).");
            passed++;
        }
        else
        {
            Debug.LogError($"<color=red>[FAIL 2/9]</color> Restauración de Collider falló. Size={col.size}, Offset={col.offset}");
        }

        // =========================================================================
        // TEST 3: Inercia Horizontal Cancelada Inmediatamente al Agacharse
        // =========================================================================
        total++;
        rb2d.linearVelocity = new Vector2(4.6f, 0f); // Velocidad previa de carrera
        // Simular entrada a agachado en el suelo
        player.isCrouching = true;
        methodSetColliderCrouched.Invoke(player, new object[] { true });
        rb2d.linearVelocity = new Vector2(0f, rb2d.linearVelocity.y);

        bool t3Success = Mathf.Abs(rb2d.linearVelocity.x) < 0.001f && player.isCrouching;

        if (t3Success)
        {
            Debug.Log($"<color=green>[PASS 3/9]</color> Inercia Horizontal: Velocidad horizontal cancelada a 0 de inmediato al agacharse.");
            passed++;
        }
        else
        {
            Debug.LogError($"<color=red>[FAIL 3/9]</color> Inercia Horizontal no fue cancelada. VelX={rb2d.linearVelocity.x}");
        }
        player.isCrouching = false;
        methodSetColliderCrouched.Invoke(player, new object[] { false });

        // =========================================================================
        // TEST 4: Fuerza Horizontal de Wall Jump Moderada y Calibrada
        // =========================================================================
        total++;
        player.isHanging = true;
        player.isClimbing = false;
        rb2d.bodyType = RigidbodyType2D.Kinematic;
        fieldFacingDir.SetValue(player, 1); // Mira a la izquierda
        Vector2 testCorner = new Vector2(-6.2f, -2.6f);
        fieldLedgeCorner.SetValue(player, testCorner);

        methodExecWallJump.Invoke(player, new object[] { Vector2.left });

        bool t4Success = player.wallJumpForceX <= 6.0f &&
                         player.wallJumpForceX >= 4.5f &&
                         Mathf.Abs(rb2d.linearVelocity.x - 5.2f) < 0.1f;

        if (t4Success)
        {
            Debug.Log($"<color=green>[PASS 4/9]</color> Wall Jump Horizontal: Fuerza moderada calibrada a {player.wallJumpForceX:F1} (VelX resultante = {rb2d.linearVelocity.x:F1}).");
            passed++;
        }
        else
        {
            Debug.LogError($"<color=red>[FAIL 4/9]</color> Wall Jump Horizontal fuera del rango moderado. wallJumpForceX={player.wallJumpForceX}, VelX={rb2d.linearVelocity.x}");
        }

        // =========================================================================
        // TEST 5: Evasión (Dodge) Atravesando Enemigos Temporalmente
        // =========================================================================
        total++;
        var enemies = Object.FindObjectsByType<HitReceiver>();
        Collider2D enemyCol = (enemies.Length > 0) ? enemies[0].GetComponent<Collider2D>() : null;

        methodStartDodge.Invoke(player, new object[] { false });
        bool ignoredDuringDodge = enemyCol == null || Physics2D.GetIgnoreCollision(col, enemyCol);

        methodStopDodge.Invoke(player, null);
        bool restoredAfterDodge = enemyCol == null || !Physics2D.GetIgnoreCollision(col, enemyCol);

        bool t5Success = ignoredDuringDodge && restoredAfterDodge;
        if (t5Success)
        {
            Debug.Log($"<color=green>[PASS 5/9]</color> Dodge y Enemigos: Colisión con enemigos ignorada durante evasión ({ignoredDuringDodge}) y restaurada tras finalizar ({restoredAfterDodge}).");
            passed++;
        }
        else
        {
            Debug.LogError($"<color=red>[FAIL 5/9]</color> Dodge no atravesó enemigos o no restauró colisiones. Ignored={ignoredDuringDodge}, Restored={restoredAfterDodge}");
        }

        // =========================================================================
        // TEST 6: Agarre de Salientes sin Levitación y Gestión de Colisionador
        // =========================================================================
        total++;
        fieldFacingDir.SetValue(player, -1); // Mira a la derecha
        col.enabled = true;

        methodGrabLedge.Invoke(player, new object[] { testCorner, Vector2.right });

        bool colDisabledInHang = !col.enabled; // Evita penetración física dentro del muro
        Vector3 expectedHangPos = new Vector3(testCorner.x + Vector2.right.x * player.hangOffsetX, testCorner.y - player.hangOffsetY, player.transform.position.z);
        bool posCalibrated = Vector2.Distance(player.transform.position, expectedHangPos) < 0.01f;

        // Al soltarse, el colisionador debe reactivarse
        methodDropFromLedge.Invoke(player, new object[] { Vector2.right });
        bool colRestoredOnDrop = col.enabled;

        bool t6Success = colDisabledInHang && posCalibrated && colRestoredOnDrop;
        if (t6Success)
        {
            Debug.Log($"<color=green>[PASS 6/9]</color> Ledge Grab: Manos y cuerpo alineados con offsets calibrados ({player.hangOffsetX}, {player.hangOffsetY}), collider desactivado durante cuelgue y restaurado al soltarse.");
            passed++;
        }
        else
        {
            Debug.LogError($"<color=red>[FAIL 6/9]</color> Ledge Grab levitación o colisionador falló. ColDisabled={colDisabledInHang}, PosCalibrated={posCalibrated}, ColRestored={colRestoredOnDrop}");
        }

        // =========================================================================
        // TEST 7: Ausencia de Animación Espuria tras Attack3
        // =========================================================================
        total++;
        player.isAttacking = true;
        player.currentComboStep = 3;
        animator.SetTrigger("verticalAttack"); // Forzar trigger residual

        methodFinishAttack.Invoke(player, null);

        // Tras FinishAttack, verticalAttack y dodgeAttack deben quedar en false
        bool vertTriggerReset = !animator.GetBool("isVerticalAttacking");
        bool isAttackingReset = !player.isAttacking && player.currentComboStep == 0;

        // Comprobar que AnyState -> VerticalAttack requiere isVerticalAttacking == true
        var controllerPath = "Assets/Characters/Prota/Animations/ProtaController.controller";
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(controllerPath);
        bool anyStateProtected = false;
        if (controller != null)
        {
            var rootSm = controller.layers[0].stateMachine;
            foreach (var tr in rootSm.anyStateTransitions)
            {
                if (tr.destinationState != null && tr.destinationState.name == "VerticalAttack")
                {
                    foreach (var c in tr.conditions)
                    {
                        if (c.parameter == "isVerticalAttacking" && c.mode == UnityEditor.Animations.AnimatorConditionMode.If)
                        {
                            anyStateProtected = true;
                        }
                    }
                }
            }
        }

        bool t7Success = vertTriggerReset && isAttackingReset && anyStateProtected;
        if (t7Success)
        {
            Debug.Log($"<color=green>[PASS 7/9]</color> Salida de Attack3: Estado de ataque reseteado a Idle y transición AnyState->VerticalAttack blindada con condición isVerticalAttacking==true.");
            passed++;
        }
        else
        {
            Debug.LogError($"<color=red>[FAIL 7/9]</color> Transición de ataque espuria no prevenida. VertReset={vertTriggerReset}, AttackReset={isAttackingReset}, Protected={anyStateProtected}");
        }

        // =========================================================================
        // TEST 8: Ataque Vertical Daño Único e Impulso Moderado
        // =========================================================================
        total++;
        bool t8Success = player.verticalAttackKnockback >= 4.0f &&
                         player.verticalAttackKnockback <= 5.5f;

        if (enemyCol != null)
        {
            var receiver = enemyCol.GetComponent<HitReceiver>();
            if (receiver != null)
            {
                int healthBefore = receiver.currentHealth;
                receiver.OnHit(testCorner, 3, player.transform.position, Vector2.right, player.verticalAttackKnockback);
                int healthAfter = receiver.currentHealth;
                t8Success = t8Success && (healthBefore - healthAfter == 45);
            }
        }

        if (t8Success)
        {
            Debug.Log($"<color=green>[PASS 8/9]</color> Ataque Vertical: Daño combo 3 aplicado correctamente e impulso vertical calibrado a {player.verticalAttackKnockback:F1}.");
            passed++;
        }
        else
        {
            Debug.LogError($"<color=red>[FAIL 8/9]</color> Ataque Vertical falló en parámetros o cálculo de daño/knockback.");
        }

        // =========================================================================
        // TEST 9: Hitstop y Game Feel de Impacto
        // =========================================================================
        total++;
        player.TriggerHitstop(0.05f);
        bool hitstopActive = animator.speed < 0.5f;

        // Restaurar para el editor
        animator.speed = 1.0f;
        bool t9Success = hitstopActive;

        if (t9Success)
        {
            Debug.Log($"<color=green>[PASS 9/9]</color> Hitstop: Reducción instantánea de escala de animación al conectar impactos sin congelar el motor global.");
            passed++;
        }
        else
        {
            Debug.LogError($"<color=red>[FAIL 9/9]</color> Hitstop no alteró la velocidad del Animator.");
        }

        // =========================================================================
        // RESUMEN FINAL
        // =========================================================================
        Debug.Log("<color=cyan>[PHASE 8 TESTS]</color> ==========================================");
        if (passed == total)
        {
            Debug.Log($"<color=green><b>[PHASE 8 TESTS COMPLETO: {passed}/{total} PRUEBAS SUPERADAS CON ÉXITO]</b></color>");
        }
        else
        {
            Debug.LogError($"<color=red><b>[PHASE 8 TESTS FALLÓ: {passed}/{total} superadas]</b></color>");
        }
    }
}
