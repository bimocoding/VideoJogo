using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MCPForUnity.Editor.Services;

[InitializeOnLoad]
public static class SetupCombatAndEnemy
{
    // Version 1.2 auto-trigger
    static SetupCombatAndEnemy()
    {
        EditorApplication.delayCall += Initialize;
    }

    [MenuItem("Combat/Setup Combat, Enemy & MCP")]
    public static void Initialize()
    {
        ConnectMCP();
        SetupAll();
    }

    public static async void ConnectMCP()
    {
        try
        {
            if (MCPServiceLocator.Bridge != null && !MCPServiceLocator.Bridge.IsRunning)
            {
                Debug.Log("[MCP] Iniciando conexion con MCP Bridge...");
                bool ok = await MCPServiceLocator.Bridge.StartAsync();
                Debug.Log($"[MCP] MCP Bridge conectado: {ok}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[MCP] Error al conectar MCP Bridge: {ex.Message}");
        }
    }

    public static void SetupAll()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "SampleScene")
        {
            scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        }

        // 1. Crear prefab de Hit FX si no existe
        GameObject hitFxPrefab = SetupHitFxPrefab();

        // 2. Configurar dodo_0 como Training Dummy
        SetupDodoTrainingDummy();

        // 3. Crear enemigo Ghoul adicional
        SetupGhoulEnemy();

        // 4. Configurar Prota con HitFxPrefab
        SetupProta(hitFxPrefab);

        // 5. Configurar UI de Combos
        SetupComboUI();

        // 6. Ejecutar configuración de Fase 3 (Dodge & Vertical Attack)
        SetupPhase3Combat.ExecutePhase3Setup();

        // Guardar cambios en la escena
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("<color=green>[SETUP COMBAT]</color> ¡Configuración de combate, enemigos, Fase 3 y UI completada con éxito!");
    }

    private static GameObject SetupHitFxPrefab()
    {
        string prefabPath = "Assets/Characters/Prota/hit fx/HitFXPrefab.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null) return existing;

        GameObject fxGo = new GameObject("HitFX");
        var sr = fxGo.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;

        var anim = fxGo.AddComponent<Animator>();
        fxGo.AddComponent<HitEffect>();

        // Crear Animator Controller simple para el clip hit_fx.anim
        string animClipPath = "Assets/Characters/Prota/Animations/hit_fx.anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animClipPath);
        if (clip != null)
        {
            string controllerPath = "Assets/Characters/Prota/Animations/HitFxController.controller";
            var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddMotion(clip);
            anim.runtimeAnimatorController = controller;
        }

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(fxGo, prefabPath);
        GameObject.DestroyImmediate(fxGo);
        return saved;
    }

    private static void SetupDodoTrainingDummy()
    {
        GameObject dodo = GameObject.Find("dodo_0");
        if (dodo == null) return;

        // Collider para recibir golpes
        var boxCol = dodo.GetComponent<BoxCollider2D>();
        if (boxCol == null) boxCol = dodo.AddComponent<BoxCollider2D>();
        boxCol.size = new Vector2(0.55f, 0.65f);
        boxCol.offset = new Vector2(0f, 0f);

        // Rigidbody2D con física
        var rb = dodo.GetComponent<Rigidbody2D>();
        if (rb == null) rb = dodo.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.mass = 1.2f;
        rb.gravityScale = 2.5f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.linearDamping = 3f;

        // HitReceiver con salud y retroceso
        var receiver = dodo.GetComponent<HitReceiver>();
        if (receiver == null) receiver = dodo.AddComponent<HitReceiver>();
        receiver.maxHealth = 100;
        receiver.currentHealth = 100;
        receiver.isImmortal = true;
        receiver.baseKnockbackForce = 2.5f;
        receiver.verticalKnockback = 1.2f;

        Debug.Log("[SETUP COMBAT] dodo_0 configurado como Training Dummy con HitReceiver y BoxCollider2D.");
    }

    private static void SetupGhoulEnemy()
    {
        GameObject ghoul = GameObject.Find("Enemy_Ghoul");
        if (ghoul == null)
        {
            ghoul = new GameObject("Enemy_Ghoul");
            ghoul.transform.position = new Vector3(0.5f, -4.45f, 0f);

            var sr = ghoul.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 2;

            // Cargar sprite de burning-ghoul
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/burning-ghoul/run 1/sprites/f-01.png") as Sprite[];
            if (sprites != null && sprites.Length > 0)
            {
                sr.sprite = sprites[0];
            }
            else
            {
                Sprite single = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/burning-ghoul/run 1/sprites/f-01.png");
                if (single != null) sr.sprite = single;
            }

            var boxCol = ghoul.AddComponent<BoxCollider2D>();
            boxCol.size = new Vector2(0.45f, 0.5f);
            boxCol.offset = new Vector2(0f, 0.25f);

            var rb = ghoul.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.mass = 1.0f;
            rb.gravityScale = 2.5f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.linearDamping = 3f;

            var receiver = ghoul.AddComponent<HitReceiver>();
            receiver.maxHealth = 100;
            receiver.currentHealth = 100;
            receiver.isImmortal = true;
            receiver.baseKnockbackForce = 3.0f;
            receiver.verticalKnockback = 1.5f;

            Debug.Log("[SETUP COMBAT] Enemy_Ghoul creado y posicionado en (0.5, -4.45) con HitReceiver.");
        }
    }

    private static void SetupProta(GameObject hitFxPrefab)
    {
        GameObject prota = GameObject.Find("prota");
        if (prota == null) return;

        var playerScript = prota.GetComponent<NewMonoBehaviourScript>();
        if (playerScript != null && hitFxPrefab != null)
        {
            playerScript.hitFxPrefab = hitFxPrefab;
            EditorUtility.SetDirty(prota);
            Debug.Log("[SETUP COMBAT] hitFxPrefab asignado a Prota.");
        }
    }

    private static void SetupComboUI()
    {
        // Limpiar cualquier NewMonoBehaviourScript residual en ComboCounter
        GameObject existingCounter = GameObject.Find("ComboCounter");
        if (existingCounter != null)
        {
            var strayScript = existingCounter.GetComponent<NewMonoBehaviourScript>();
            if (strayScript != null)
            {
                Object.DestroyImmediate(strayScript);
                Debug.Log("[SETUP COMBAT] Se eliminó script residual NewMonoBehaviourScript de ComboCounter.");
            }
        }

        var existingCombo = Object.FindAnyObjectByType<ComboUI>();
        if (existingCombo != null) return;

        // Crear Canvas
        GameObject canvasGo = new GameObject("CombatUI_Canvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        // Contenedor del contador de combo
        GameObject containerGo = new GameObject("ComboCounter");
        containerGo.transform.SetParent(canvasGo.transform, false);
        RectTransform containerRect = containerGo.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.85f, 0.65f);
        containerRect.anchorMax = new Vector2(0.85f, 0.65f);
        containerRect.anchoredPosition = Vector2.zero;
        containerRect.sizeDelta = new Vector2(200f, 100f);

        CanvasGroup cg = containerGo.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        ComboUI comboUI = containerGo.AddComponent<ComboUI>();
        comboUI.canvasGroup = cg;
        comboUI.container = containerRect;

        // Texto del número
        GameObject countGo = new GameObject("CountText");
        countGo.transform.SetParent(containerGo.transform, false);
        RectTransform countRect = countGo.AddComponent<RectTransform>();
        countRect.anchorMin = new Vector2(0f, 0.3f);
        countRect.anchorMax = new Vector2(1f, 1f);
        countRect.offsetMin = Vector2.zero;
        countRect.offsetMax = Vector2.zero;

        var countTmp = countGo.AddComponent<TextMeshProUGUI>();
        countTmp.text = "2";
        countTmp.fontSize = 52;
        countTmp.fontStyle = FontStyles.Bold | FontStyles.Italic;
        countTmp.alignment = TextAlignmentOptions.Center;
        countTmp.color = new Color(1f, 0.85f, 0.1f, 1f); // Amarillo dorado
        comboUI.comboCountText = countTmp;

        // Texto del título "HITS!"
        GameObject titleGo = new GameObject("TitleText");
        titleGo.transform.SetParent(containerGo.transform, false);
        RectTransform titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0f);
        titleRect.anchorMax = new Vector2(1f, 0.35f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "HITS!";
        titleTmp.fontSize = 26;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = Color.white;
        comboUI.comboTitleText = titleTmp;

        Debug.Log("[SETUP COMBAT] CombatUI_Canvas y ComboUI creados correctamente en la escena.");
    }
}
