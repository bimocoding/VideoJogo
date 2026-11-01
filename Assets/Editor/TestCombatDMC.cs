using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class TestCombatDMC
{
    [MenuItem("Combat/Test DMC Delay Combos (Complete Specification)")]
    public static void RunTests()
    {
        Debug.Log("<color=cyan>==============================================================</color>");
        Debug.Log("<color=cyan>[DMC COMBAT QA]</color> INICIANDO VERIFICACIÓN COMPLETA DE COMBATE CON DELAY COMBOS");
        Debug.Log("<color=cyan>==============================================================</color>");

        var player = Object.FindAnyObjectByType<NewMonoBehaviourScript>();
        if (player == null)
        {
            Debug.LogError("[DMC COMBAT QA] No se encontró NewMonoBehaviourScript en la escena activa.");
            return;
        }

        var combat = player.GetComponent<PlayerCombatController>();
        if (combat == null)
        {
            Debug.LogError("[DMC COMBAT QA] No se encontró PlayerCombatController en el GameObject del jugador.");
            return;
        }

        var rb2d = player.GetComponent<Rigidbody2D>();
        var animator = player.GetComponent<Animator>();

        int passed = 0;
        int failed = 0;
        int total = 0;

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

        // Test 1: Hit 1 Inicialización e Impulso (1.2 m/s)
        player.isGrounded = true;
        player.facingDirection = -1; // Mira a la derecha (+X)
        combat.InterruptCombat();
        
        var methodExecuteHit1 = typeof(PlayerCombatController).GetMethod("ExecuteHit1", BindingFlags.NonPublic | BindingFlags.Instance);
        methodExecuteHit1.Invoke(combat, null);

        AssertTest("1. Hit 1 (prota_atk1) Apertura común",
            combat.CurrentState == PlayerCombatController.CombatState.Hit1 &&
            Mathf.Approximately(rb2d.linearVelocity.x, 1.2f),
            $"Estado={combat.CurrentState}, VelX={rb2d.linearVelocity.x} (Esperado 1.2 m/s)");

        // Test 2: Rama A - Hit 2 Básico (Early Window) corta recovery y frena impulso (vel.x = 0)
        var methodExecuteHit2Basic = typeof(PlayerCombatController).GetMethod("ExecuteHit2Basic", BindingFlags.NonPublic | BindingFlags.Instance);
        methodExecuteHit2Basic.Invoke(combat, null);

        AssertTest("2. Rama A - Hit 2 Básico (prota_atk2)",
            combat.CurrentState == PlayerCombatController.CombatState.Hit2Basic &&
            Mathf.Approximately(rb2d.linearVelocity.x, 0f),
            $"Estado={combat.CurrentState}, VelX={rb2d.linearVelocity.x} (Frenado a 0 m/s)");

        // Test 3: Rama A - Hit 3 Básico (Giro mágico con knockback)
        var methodExecuteHit3Basic = typeof(PlayerCombatController).GetMethod("ExecuteHit3Basic", BindingFlags.NonPublic | BindingFlags.Instance);
        methodExecuteHit3Basic.Invoke(combat, null);

        AssertTest("3. Rama A - Hit 3 Básico (prota_atk3)",
            combat.CurrentState == PlayerCombatController.CombatState.Hit3Basic &&
            Mathf.Approximately(rb2d.linearVelocity.x, 0f) &&
            combat.hit3KnockbackForce >= 7.0f,
            $"Estado={combat.CurrentState}, KnockbackForce={combat.hit3KnockbackForce} (Esperado >= 7.0f)");

        // Test 4: Rama B - Hit 2 Variación (Delay Window tras pausa en Hit 1 a 1.8 m/s)
        var methodExecuteHit2Var = typeof(PlayerCombatController).GetMethod("ExecuteHit2Variation", BindingFlags.NonPublic | BindingFlags.Instance);
        methodExecuteHit2Var.Invoke(combat, null);

        AssertTest("4. Rama B - Hit 2 Variación (prota_atk2_1)",
            combat.CurrentState == PlayerCombatController.CombatState.Hit2Variation &&
            Mathf.Approximately(rb2d.linearVelocity.x, 1.8f),
            $"Estado={combat.CurrentState}, VelX={rb2d.linearVelocity.x} (Esperado 1.8 m/s)");

        // Test 5: Rama B - Hit 3 Variación (Doble tajo a 2.2 m/s)
        var methodExecuteHit3Var = typeof(PlayerCombatController).GetMethod("ExecuteHit3Variation", BindingFlags.NonPublic | BindingFlags.Instance);
        methodExecuteHit3Var.Invoke(combat, null);

        AssertTest("5. Rama B - Hit 3 Variación (prota_atk2_2)",
            combat.CurrentState == PlayerCombatController.CombatState.Hit3Variation &&
            Mathf.Approximately(rb2d.linearVelocity.x, 2.2f),
            $"Estado={combat.CurrentState}, VelX={rb2d.linearVelocity.x} (Esperado 2.2 m/s)");

        // Test 6: Rama B - Hit 4 Lunge (prota_dodge_atk1 a 4.2 m/s + humo)
        combat.ExecuteHit4Lunge();

        AssertTest("6. Rama B - Hit 4 Lunge (prota_dodge_atk1)",
            combat.CurrentState == PlayerCombatController.CombatState.Hit4Lunge &&
            Mathf.Approximately(rb2d.linearVelocity.x, 4.2f),
            $"Estado={combat.CurrentState}, VelX={rb2d.linearVelocity.x} (Esperado 4.2 m/s)");

        // Test 7: Rama B - Hit 5 Elevador (Launcher ascendente a +7.8 m/s y activa canTriggerAirSlam)
        var methodExecuteHit5Launcher = typeof(PlayerCombatController).GetMethod("ExecuteHit5Launcher", BindingFlags.NonPublic | BindingFlags.Instance);
        methodExecuteHit5Launcher.Invoke(combat, null);

        AssertTest("7. Rama B - Hit 5 Elevador (Launcher a +7.8 m/s)",
            combat.CurrentState == PlayerCombatController.CombatState.Hit5Launcher &&
            Mathf.Approximately(rb2d.linearVelocity.x, 0f) &&
            Mathf.Approximately(rb2d.linearVelocity.y, 7.8f) &&
            combat.CanTriggerAirSlam == true,
            $"Estado={combat.CurrentState}, VelY={rb2d.linearVelocity.y}, CanAirSlam={combat.CanTriggerAirSlam}");

        // Test 8: Vertical Finisher Slam - Bloqueo estricto del eje X (IsHorizontalLocked)
        combat.StartVerticalFinisher();

        AssertTest("8. Vertical Finisher Slam (Bloqueo eje X)",
            combat.CurrentState == PlayerCombatController.CombatState.VerticalFinisher &&
            combat.IsHorizontalLocked == true,
            $"Estado={combat.CurrentState}, IsHorizontalLocked={combat.IsHorizontalLocked}");

        // Forzar FixedUpdate para probar bloqueo
        rb2d.linearVelocity = new Vector2(5.5f, -14.0f);
        combat.HandleFixedUpdate();

        AssertTest("8.1 Físicas FixedUpdate fuerzan VelX = 0 durante Slam",
            Mathf.Approximately(rb2d.linearVelocity.x, 0f),
            $"VelX={rb2d.linearVelocity.x} (Garantizado 0 m/s sin deslizamiento lateral)");

        combat.InterruptCombat();

        // Test 9: Rama C - Atajo de Combo desde Slide / Backdodge a Hit 4 Lunge
        player.isSliding = true;
        bool shortcutSuccess = combat.TryDodgeLungeShortcut();

        AssertTest("9. Rama C - Atajo con J en Slide hacia Hit 4 Lunge",
            shortcutSuccess && combat.CurrentState == PlayerCombatController.CombatState.Hit4Lunge && player.isSliding == false,
            $"AtajoExitoso={shortcutSuccess}, Estado={combat.CurrentState}, isSliding={player.isSliding}");

        combat.InterruptCombat();

        // Test 10: Rama D - Ataque Vertical Directo (W + J launcher)
        var methodDirectLauncher = typeof(PlayerCombatController).GetMethod("ExecuteDirectLauncher", BindingFlags.NonPublic | BindingFlags.Instance);
        methodDirectLauncher.Invoke(combat, null);

        AssertTest("10. Rama D - Ataque Vertical Directo (W + J)",
            combat.CurrentState == PlayerCombatController.CombatState.DirectLauncher &&
            Mathf.Approximately(rb2d.linearVelocity.y, 7.8f) &&
            combat.CanTriggerAirSlam == true,
            $"Estado={combat.CurrentState}, VelY={rb2d.linearVelocity.y}, CanAirSlam={combat.CanTriggerAirSlam}");

        combat.InterruptCombat();

        // Test 11: Rama D - Combo Aéreo Estándar 1 y 2
        var methodJumpAtk = typeof(PlayerCombatController).GetMethod("ExecuteJumpAttack", BindingFlags.NonPublic | BindingFlags.Instance);
        methodJumpAtk.Invoke(combat, new object[] { 1 });

        AssertTest("11.1 Combo Aéreo - Jump Attack 1",
            combat.CurrentState == PlayerCombatController.CombatState.JumpAttack1,
            $"Estado={combat.CurrentState}");

        methodJumpAtk.Invoke(combat, new object[] { 2 });

        AssertTest("11.2 Combo Aéreo - Jump Attack 2 encadenado",
            combat.CurrentState == PlayerCombatController.CombatState.JumpAttack2,
            $"Estado={combat.CurrentState}");

        combat.InterruptCombat();

        // Test 12: Independencia de orientación A/D
        player.facingDirection = -1; // Mira a la derecha (+X)
        // No altera el estado de combate al cambiar de orientación
        AssertTest("12. Orientación independiente",
            combat.CurrentState == PlayerCombatController.CombatState.Idle,
            $"facingDirection={player.facingDirection}");

        Debug.Log("<color=cyan>==============================================================</color>");
        Debug.Log($"<color={(failed == 0 ? "green" : "red")}>[DMC COMBAT QA RESUMEN]</color> Total: {total} | Superados: {passed} | Fallidos: {failed}");
        Debug.Log("<color=cyan>==============================================================</color>");
    }
}
