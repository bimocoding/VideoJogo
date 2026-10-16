using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class SetupPhase5Dodge
{
    [MenuItem("Combat/Setup Phase 5 - Dodge, Backdodge & Sprint")]
    public static void ExecutePhase5Setup()
    {
        Debug.Log("<color=cyan>[PHASE 5]</color> Iniciando configuración de Dodge, Backdodge y Sprint HUD...");

        // 1. Rebanar texturas
        // Backdodge: 384x384 -> 4 columnas x 6 filas = 24 celdas de 96x64
        SliceTexture(
            "Assets/Characters/Prota/back dodge/sprite sheets/back dodge.png",
            96, 64, 4, 6, 24, "backdodge_", new Vector2(56f / 96f, 0f));

        // Forward Dodge: 672x64 -> 6 columnas x 1 fila = 6 celdas de 112x64
        SliceTexture(
            "Assets/Characters/Prota/aerial dash/sprite sheets/aerial dash_without illusion.png",
            112, 64, 6, 1, 6, "dodge_", new Vector2(38f / 112f, 0f));

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // 2. Crear clips de animación
        AnimationClip clipBackdodge = CreateClip(
            "Assets/Characters/Prota/back dodge/sprite sheets/back dodge.png",
            "backdodge_", 24, "Assets/Characters/Prota/Animations/prota_backdodge.anim", 44f, false);

        AnimationClip clipDodge = CreateClip(
            "Assets/Characters/Prota/aerial dash/sprite sheets/aerial dash_without illusion.png",
            "dodge_", 6, "Assets/Characters/Prota/Animations/prota_dodge.anim", 14f, false);

        // 3. Integrar en Animator Controller
        SetupAnimatorController(clipDodge, clipBackdodge);

        // 4. Configurar Sprint Indicator en CombatUI_Canvas
        SetupSprintHUD();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=green>[PHASE 5]</color> ¡Configuración de Dodge, Backdodge y Sprint completada con éxito!");
    }

    private static void SliceTexture(string path, int cellWidth, int cellHeight, int cols, int rows, int count, string prefix, Vector2 pivot)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"[PHASE 5] No se encontró TextureImporter en {path}");
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;

        var sheet = new SpriteMetaData[count];
        for (int i = 0; i < count; i++)
        {
            int c = i % cols;
            int r = i / cols;
            int unityRow = rows - 1 - r;

            SpriteMetaData smd = new SpriteMetaData();
            smd.name = $"{prefix}{i}";
            smd.rect = new Rect(c * cellWidth, unityRow * cellHeight, cellWidth, cellHeight);
            smd.alignment = 9; // Custom
            smd.pivot = pivot;
            sheet[i] = smd;
        }

#pragma warning disable 0618
        importer.spritesheet = sheet;
#pragma warning restore 0618
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        Debug.Log($"[PHASE 5] Textura {path} rebanada en {count} sprites con prefijo {prefix}.");
    }

    private static AnimationClip CreateClip(string texturePath, string prefix, int count, string clipPath, float fps, bool loop)
    {
        Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
        Dictionary<string, Sprite> spriteDict = new Dictionary<string, Sprite>();
        foreach (var obj in subAssets)
        {
            if (obj is Sprite s)
            {
                spriteDict[s.name] = s;
            }
        }

        List<Sprite> orderedSprites = new List<Sprite>();
        for (int i = 0; i < count; i++)
        {
            string spriteName = $"{prefix}{i}";
            if (spriteDict.TryGetValue(spriteName, out Sprite sp))
            {
                orderedSprites.Add(sp);
            }
            else
            {
                Debug.LogWarning($"[PHASE 5] Sprite {spriteName} no encontrado en {texturePath}");
            }
        }

        if (orderedSprites.Count == 0)
        {
            Debug.LogError($"[PHASE 5] No se pudieron cargar sprites para {clipPath}");
            return null;
        }

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            clip.name = Path.GetFileNameWithoutExtension(clipPath);
            AssetDatabase.CreateAsset(clip, clipPath);
        }

        clip.frameRate = fps;

        EditorCurveBinding binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };

        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[orderedSprites.Count];
        for (int i = 0; i < orderedSprites.Count; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i / fps,
                value = orderedSprites[i]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.stopTime = orderedSprites.Count / fps;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        EditorUtility.SetDirty(clip);
        Debug.Log($"[PHASE 5] Clip {clipPath} creado con {orderedSprites.Count} fotogramas a {fps} FPS (Loop: {loop}).");
        return clip;
    }

    private static void SetupAnimatorController(AnimationClip clipDodge, AnimationClip clipBackdodge)
    {
        string controllerPath = "Assets/Characters/Prota/Animations/ProtaController.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            Debug.LogError($"[PHASE 5] No se encontró el AnimatorController en {controllerPath}");
            return;
        }

        // Parámetros
        AddBoolIfNotExists(controller, "isDodging");
        AddBoolIfNotExists(controller, "isBackdodging");
        AddTriggerIfNotExists(controller, "dodge");
        AddTriggerIfNotExists(controller, "backdodge");

        AnimatorStateMachine rootSm = controller.layers[0].stateMachine;

        // Estados
        AnimatorState stateDodge = FindOrCreateState(rootSm, "Dodge", clipDodge);
        AnimatorState stateBackdodge = FindOrCreateState(rootSm, "Backdodge", clipBackdodge);

        AnimatorState stateIdle = FindState(rootSm, "Idle");
        AnimatorState stateRun = FindState(rootSm, "Run");
        AnimatorState stateWalk = FindState(rootSm, "Walk");

        // 1. Transiciones hacia Dodge
        if (stateIdle != null && stateDodge != null)
        {
            EnsureTransitionWithTrigger(stateIdle, stateDodge, "dodge");
        }
        if (stateWalk != null && stateDodge != null)
        {
            EnsureTransitionWithTrigger(stateWalk, stateDodge, "dodge");
        }
        if (stateRun != null && stateDodge != null)
        {
            EnsureTransitionWithTrigger(stateRun, stateDodge, "dodge");
        }

        // 2. Transiciones hacia Backdodge
        if (stateIdle != null && stateBackdodge != null)
        {
            EnsureTransitionWithTrigger(stateIdle, stateBackdodge, "backdodge");
        }
        if (stateWalk != null && stateBackdodge != null)
        {
            EnsureTransitionWithTrigger(stateWalk, stateBackdodge, "backdodge");
        }
        if (stateRun != null && stateBackdodge != null)
        {
            EnsureTransitionWithTrigger(stateRun, stateBackdodge, "backdodge");
        }

        // 3. Salidas hacia Idle al finalizar la animación
        if (stateDodge != null && stateIdle != null)
        {
            EnsureExitTransition(stateDodge, stateIdle, 0.90f);
        }
        if (stateBackdodge != null && stateIdle != null)
        {
            EnsureExitTransition(stateBackdodge, stateIdle, 0.90f);
        }

        EditorUtility.SetDirty(controller);
        Debug.Log("[PHASE 5] AnimatorController actualizado con estados Dodge y Backdodge.");
    }

    private static void SetupSprintHUD()
    {
        // Buscar CombatUI_Canvas en la escena activa
        GameObject canvasGo = GameObject.Find("CombatUI_Canvas");
        if (canvasGo == null)
        {
            Canvas canvasComp = Object.FindAnyObjectByType<Canvas>();
            if (canvasComp != null) canvasGo = canvasComp.gameObject;
        }

        if (canvasGo == null)
        {
            Debug.LogWarning("[PHASE 5] No se encontró CombatUI_Canvas en la escena. Creando Canvas...");
            canvasGo = new GameObject("CombatUI_Canvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        // Buscar o crear SprintIndicator
        Transform existingIndicator = canvasGo.transform.Find("SprintIndicator");
        if (existingIndicator != null)
        {
            Debug.Log("[PHASE 5] SprintIndicator ya existe en CombatUI_Canvas.");
            return;
        }

        GameObject indicatorGo = new GameObject("SprintIndicator");
        indicatorGo.transform.SetParent(canvasGo.transform, false);

        RectTransform rect = indicatorGo.AddComponent<RectTransform>();
        // Anclar en la esquina inferior izquierda (0.08, 0.08) completamente separado de ComboUI (0.85, 0.65)
        rect.anchorMin = new Vector2(0.08f, 0.08f);
        rect.anchorMax = new Vector2(0.08f, 0.08f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(130f, 32f);

        CanvasGroup cg = indicatorGo.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        // Fondo semi-transparente
        Image bgImg = indicatorGo.AddComponent<Image>();
        bgImg.color = new Color(0.08f, 0.12f, 0.18f, 0.75f); // Azul pizarra oscuro translúcido

        // Texto con TextMeshPro
        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(indicatorGo.transform, false);
        RectTransform textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "⚡ SPRINT";
        tmp.fontSize = 18f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.2f, 0.95f, 1f, 1f); // Cyan brillante eléctrico

        SprintUI sprintUI = indicatorGo.AddComponent<SprintUI>();
        sprintUI.canvasGroup = cg;
        sprintUI.sprintText = tmp;
        sprintUI.container = rect;

        EditorUtility.SetDirty(canvasGo);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvasGo.scene);
        Debug.Log("[PHASE 5] SprintIndicator HUD creado e integrado en CombatUI_Canvas.");
    }

    private static void AddBoolIfNotExists(AnimatorController controller, string name)
    {
        foreach (var p in controller.parameters)
        {
            if (p.name == name && p.type == AnimatorControllerParameterType.Bool) return;
        }
        controller.AddParameter(name, AnimatorControllerParameterType.Bool);
    }

    private static void AddTriggerIfNotExists(AnimatorController controller, string name)
    {
        foreach (var p in controller.parameters)
        {
            if (p.name == name && p.type == AnimatorControllerParameterType.Trigger) return;
        }
        controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
    }

    private static AnimatorState FindState(AnimatorStateMachine sm, string name)
    {
        foreach (var s in sm.states)
        {
            if (s.state.name == name) return s.state;
        }
        return null;
    }

    private static AnimatorState FindOrCreateState(AnimatorStateMachine sm, string name, AnimationClip clip)
    {
        AnimatorState state = FindState(sm, name);
        if (state == null)
        {
            state = sm.AddState(name);
        }
        state.motion = clip;
        return state;
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

    private static void EnsureExitTransition(AnimatorState from, AnimatorState to, float exitTime)
    {
        foreach (var t in from.transitions)
        {
            if (t.destinationState == to && t.hasExitTime) return;
        }

        var transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = exitTime;
        transition.duration = 0.05f;
    }
}
