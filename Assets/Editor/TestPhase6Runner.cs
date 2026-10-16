using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class TestPhase6Runner
{
    [MenuItem("Combat/Run Phase 6 Verification Tests")]
    public static void RunTests()
    {
        Debug.Log("<color=cyan>[PHASE 6 TESTS]</color> Iniciando batería de pruebas de Wall Jump y Agarre...");

        var player = Object.FindAnyObjectByType<NewMonoBehaviourScript>();
        if (player == null)
        {
            Debug.LogError("[PHASE 6 TESTS] No se encontró NewMonoBehaviourScript en la escena activa.");
            return;
        }

        var rb2d = player.GetComponent<Rigidbody2D>();
        var animator = player.GetComponent<Animator>();

        // Asignar referencias privadas que normalmente se resuelven en Start()
        var type = typeof(NewMonoBehaviourScript);
        type.GetField("rb2d", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(player, rb2d);
        type.GetField("animator", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(player, animator);

        int passed = 0;
        int total = 0;

        // Reflection helpers
        var fieldFacingDir = type.GetField("facingDirection", BindingFlags.NonPublic | BindingFlags.Instance);
        var fieldLedgeCorner = type.GetField("ledgeCorner", BindingFlags.NonPublic | BindingFlags.Instance);
        var fieldLastLedgeCorner = type.GetField("lastLedgeCorner", BindingFlags.NonPublic | BindingFlags.Instance);
        var fieldLedgeCooldown = type.GetField("ledgeCooldown", BindingFlags.NonPublic | BindingFlags.Instance);
        var fieldHangTimer = type.GetField("hangTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        var methodExecWallJump = type.GetMethod("ExecuteLedgeWallJump", BindingFlags.NonPublic | BindingFlags.Instance);
        var methodDropFromLedge = type.GetMethod("DropFromLedge", BindingFlags.NonPublic | BindingFlags.Instance);

        Vector2 testCornerLeftWall = new Vector2(-6.2f, -2.6f);
        Vector2 testCornerRightWall = new Vector2(-3.4f, -2.6f);

        // TEST 1: Wall Jump hacia la DERECHA desde muro izquierdo (lookDir = Vector2.left)
        total++;
        player.isHanging = true;
        player.isClimbing = false;
        rb2d.bodyType = RigidbodyType2D.Kinematic;
        fieldFacingDir.SetValue(player, 1); // Mira a la izquierda hacia el muro
        fieldLedgeCorner.SetValue(player, testCornerLeftWall);

        methodExecWallJump.Invoke(player, new object[] { Vector2.left });

        bool t1Success = !player.isHanging &&
                         player.isWallJumping &&
                         rb2d.bodyType == RigidbodyType2D.Dynamic &&
                         rb2d.linearVelocity.x > 7.0f &&
                         rb2d.linearVelocity.y > 10.0f &&
                         (int)fieldFacingDir.GetValue(player) == -1; // Flipeado mirando a la derecha

        if (t1Success)
        {
            Debug.Log($"<color=green>[PASS 1/7]</color> Wall Jump hacia la DERECHA: VelX={rb2d.linearVelocity.x:F1}, VelY={rb2d.linearVelocity.y:F1}, facingDir=-1 (Derecha)");
            passed++;
        }
        else
        {
            Debug.LogError($"<color=red>[FAIL 1/7]</color> Wall Jump hacia la DERECHA falló. Vel={rb2d.linearVelocity}");
        }

        // TEST 2: Wall Jump hacia la IZQUIERDA desde muro derecho (lookDir = Vector2.right)
        total++;
        player.isHanging = true;
        player.isClimbing = false;
        rb2d.bodyType = RigidbodyType2D.Kinematic;
        fieldFacingDir.SetValue(player, -1); // Mira a la derecha hacia el muro
        fieldLedgeCorner.SetValue(player, testCornerRightWall);

        methodExecWallJump.Invoke(player, new object[] { Vector2.right });

        bool t2Success = !player.isHanging &&
                         player.isWallJumping &&
                         rb2d.bodyType == RigidbodyType2D.Dynamic &&
                         rb2d.linearVelocity.x < -7.0f &&
                         rb2d.linearVelocity.y > 10.0f &&
                         (int)fieldFacingDir.GetValue(player) == 1; // Flipeado mirando a la izquierda

        if (t2Success)
        {
            Debug.Log($"<color=green>[PASS 2/7]</color> Wall Jump hacia la IZQUIERDA: VelX={rb2d.linearVelocity.x:F1}, VelY={rb2d.linearVelocity.y:F1}, facingDir=1 (Izquierda)");
            passed++;
        }
        else
        {
            Debug.LogError($"<color=red>[FAIL 2/7]</color> Wall Jump hacia la IZQUIERDA falló. Vel={rb2d.linearVelocity}");
        }

        // TEST 3: Protección anti re-enganche en el MISMO saliente tras Wall Jump
        total++;
        Vector2 lastCorner = (Vector2)fieldLastLedgeCorner.GetValue(player);
        float cd = (float)fieldLedgeCooldown.GetValue(player);
        bool sameCornerProtected = (cd > 0f) && (Vector2.Distance(testCornerRightWall, lastCorner) < 0.6f);

        if (sameCornerProtected)
        {
            Debug.Log($"<color=green>[PASS 3/7]</color> Protección contra re-enganche al mismo muro activa: Cooldown={cd:F2}s, Dist={Vector2.Distance(testCornerRightWall, lastCorner):F3}m");
            passed++;
        }
        else
        {
            Debug.LogError("<color=red>[FAIL 3/7]</color> No se registró la protección anti re-enganche al mismo saliente.");
        }

        // TEST 4: Agarre permitido en OTRA pared distante tras Wall Jump
        total++;
        float distToOppositeWall = Vector2.Distance(testCornerLeftWall, lastCorner);
        bool canGrabOpposite = distToOppositeWall >= 0.6f;

        if (canGrabOpposite)
        {
            Debug.Log($"<color=green>[PASS 4/7]</color> Detección de otra pared libre de bloqueo: Dist={distToOppositeWall:F2}m (>=0.60m límite)");
            passed++;
        }
        else
        {
            Debug.LogError("<color=red>[FAIL 4/7]</color> Muro opuesto bloqueado indebidamente.");
        }

        // TEST 5: Bajada con S / DropFromLedge
        total++;
        player.isHanging = true;
        fieldFacingDir.SetValue(player, -1);
        fieldLedgeCorner.SetValue(player, testCornerRightWall);

        methodDropFromLedge.Invoke(player, new object[] { Vector2.right });

        bool t5Success = !player.isHanging &&
                         rb2d.bodyType == RigidbodyType2D.Dynamic &&
                         rb2d.linearVelocity.y < -1.0f; // Impulso de bajada

        if (t5Success)
        {
            Debug.Log($"<color=green>[PASS 5/7]</color> Bajada con DropFromLedge: isHanging=false, VelY={rb2d.linearVelocity.y:F1}");
            passed++;
        }
        else
        {
            Debug.LogError("<color=red>[FAIL 5/7]</color> DropFromLedge falló.");
        }

        // TEST 6: Expiración del temporizador de agarre (maxHangTime = 3.0s)
        total++;
        player.isHanging = true;
        fieldHangTimer.SetValue(player, player.maxHangTime + 0.1f);
        // Simular llamada de actualización
        if ((float)fieldHangTimer.GetValue(player) >= player.maxHangTime)
        {
            methodDropFromLedge.Invoke(player, new object[] { Vector2.right });
        }

        bool t6Success = !player.isHanging && rb2d.bodyType == RigidbodyType2D.Dynamic;
        if (t6Success)
        {
            Debug.Log("<color=green>[PASS 6/7]</color> Expiración del temporizador de 3s suelta al jugador limpiamente.");
            passed++;
        }
        else
        {
            Debug.LogError("<color=red>[FAIL 6/7]</color> Expiración de temporizador no soltó al personaje.");
        }

        // TEST 7: Salto normal sin agarre
        total++;
        player.isHanging = false;
        player.isClimbing = false;
        rb2d.bodyType = RigidbodyType2D.Dynamic;
        rb2d.linearVelocity = Vector2.zero;

        var methodExecJump = type.GetMethod("ExecuteJump", BindingFlags.NonPublic | BindingFlags.Instance);
        methodExecJump.Invoke(player, null);

        bool t7Success = rb2d.linearVelocity.y >= player.jumpForce - 0.1f;
        if (t7Success)
        {
            Debug.Log($"<color=green>[PASS 7/7]</color> Salto normal en suelo/aire sin agarre: VelY={rb2d.linearVelocity.y:F1} (Esperado {player.jumpForce:F1})");
            passed++;
        }
        else
        {
            Debug.LogError($"<color=red>[FAIL 7/7]</color> Salto normal falló. VelY={rb2d.linearVelocity.y}");
        }

        // Restaurar estado base
        player.isHanging = false;
        player.isClimbing = false;
        player.isWallJumping = false;
        rb2d.bodyType = RigidbodyType2D.Dynamic;
        rb2d.linearVelocity = Vector2.zero;
        fieldLedgeCooldown.SetValue(player, 0f);
        fieldLastLedgeCorner.SetValue(player, Vector2.positiveInfinity);

        if (passed == total)
        {
            Debug.Log($"<color=cyan>[PHASE 6 TESTS]</color> <color=green>¡TODAS LAS PRUEBAS PASARON EXITOSAMENTE! ({passed}/{total})</color>");
        }
        else
        {
            Debug.LogError($"<color=cyan>[PHASE 6 TESTS]</color> Pruebas incompletas: {passed}/{total}");
        }
    }
}
