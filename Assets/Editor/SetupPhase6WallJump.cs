using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SetupPhase6WallJump
{
    [MenuItem("Combat/Setup Phase 6 - Wall Jump From Ledge")]
    public static void ExecutePhase6Setup()
    {
        Debug.Log("<color=cyan>[PHASE 6]</color> Iniciando configuración de Wall Jump desde agarre...");

        // 1. Configurar Animator Controller para transición de LedgeHang a Jump
        SetupAnimatorTransitions();

        // 2. Crear corredor de prueba con dos muros opuestos para Wall Jump
        SetupWallJumpTestCorridor();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=green>[PHASE 6]</color> ¡Configuración de Wall Jump completada con éxito!");
    }

    private static void SetupAnimatorTransitions()
    {
        string controllerPath = "Assets/Characters/Prota/Animations/ProtaController.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            Debug.LogError($"[PHASE 6] No se encontró el AnimatorController en {controllerPath}");
            return;
        }

        AnimatorStateMachine rootSm = controller.layers[0].stateMachine;

        AnimatorState stateLedgeHang = FindState(rootSm, "LedgeHang");
        AnimatorState stateJump = FindState(rootSm, "Jump");

        if (stateLedgeHang != null && stateJump != null)
        {
            // Transición directa de LedgeHang a Jump con trigger "jump"
            EnsureTransitionWithTrigger(stateLedgeHang, stateJump, "jump");
            Debug.Log("[PHASE 6] Transición LedgeHang -> Jump configurada con éxito.");
        }

        EditorUtility.SetDirty(controller);
    }

    private static void SetupWallJumpTestCorridor()
    {
        string corridorName = "Test_WallJumpCorridor";
        GameObject corridorGo = GameObject.Find(corridorName);
        if (corridorGo == null)
        {
            corridorGo = new GameObject(corridorName);
            corridorGo.transform.position = Vector3.zero;

            int floorLayer = LayerMask.NameToLayer("Floor");
            if (floorLayer == -1) floorLayer = 0;

            // Muro izquierdo (saliente a la derecha del muro, el PJ se cuelga mirando a la izquierda)
            GameObject leftWall = new GameObject("Pillar_Left");
            leftWall.transform.SetParent(corridorGo.transform);
            leftWall.layer = floorLayer;
            leftWall.transform.position = new Vector3(-6.2f, -3.8f, 0f);

            var colLeft = leftWall.AddComponent<BoxCollider2D>();
            colLeft.size = new Vector2(0.6f, 2.4f);

            var srLeft = leftWall.AddComponent<SpriteRenderer>();
            srLeft.sortingOrder = 3;
            srLeft.color = new Color(0.25f, 0.45f, 0.65f, 0.95f); // Azul piedra visible
            srLeft.sprite = CreateBoxSprite();

            // Muro derecho (saliente a la izquierda del muro, el PJ se cuelga mirando a la derecha)
            GameObject rightWall = new GameObject("Pillar_Right");
            rightWall.transform.SetParent(corridorGo.transform);
            rightWall.layer = floorLayer;
            rightWall.transform.position = new Vector3(-3.4f, -3.8f, 0f);

            var colRight = rightWall.AddComponent<BoxCollider2D>();
            colRight.size = new Vector2(0.6f, 2.4f);

            var srRight = rightWall.AddComponent<SpriteRenderer>();
            srRight.sortingOrder = 3;
            srRight.color = new Color(0.25f, 0.45f, 0.65f, 0.95f); // Azul piedra visible
            srRight.sprite = CreateBoxSprite();

            Debug.Log("[PHASE 6] Corredor de prueba con 2 pilares opuestos creado en la escena.");
        }
        else
        {
            Debug.Log("[PHASE 6] Corredor de prueba ya existe en la escena.");
        }

        EditorUtility.SetDirty(corridorGo);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(corridorGo.scene);
    }

    private static Sprite CreateBoxSprite()
    {
        Texture2D tex = new Texture2D(16, 16);
        Color[] pixels = new Color[16 * 16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
    }

    private static AnimatorState FindState(AnimatorStateMachine sm, string name)
    {
        foreach (var s in sm.states)
        {
            if (s.state.name == name) return s.state;
        }
        return null;
    }

    private static void EnsureTransitionWithTrigger(AnimatorState from, AnimatorState to, string triggerName)
    {
        foreach (var t in from.transitions)
        {
            if (t.destinationState == to)
            {
                foreach (var c in t.conditions)
                {
                    if (c.parameter == triggerName) return;
                }
            }
        }

        var transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.05f;
        transition.AddCondition(AnimatorConditionMode.If, 0, triggerName);
    }
}
