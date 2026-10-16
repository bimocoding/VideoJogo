using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SetupPhase4CrouchAndSlide
{
    [MenuItem("Combat/Setup Phase 4 - Crouch & Slide")]
    public static void ExecutePhase4Setup()
    {
        Debug.Log("<color=cyan>[PHASE 4]</color> Iniciando configuración de Crouch y Slide...");

        // 1. Rebanar texturas
        SliceTexture(
            "Assets/Characters/Prota/crouching/sprite sheets/crouching.png",
            53, 50, 3, 5, 13, "crouch_", new Vector2(28f / 53f, 0f));

        SliceTexture(
            "Assets/Characters/Prota/slide/sprite sheets/slide_merged.png",
            144, 64, 3, 6, 17, "slide_", new Vector2(40f / 144f, 1f / 64f));

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // 2. Crear clips de animación
        AnimationClip clipCrouchLoop = CreateSubClip(
            "Assets/Characters/Prota/crouching/sprite sheets/crouching.png",
            "crouch_", 2, 8, "Assets/Characters/Prota/Animations/prota_crouch.anim", 12f, true);

        AnimationClip clipSlide = CreateClip(
            "Assets/Characters/Prota/slide/sprite sheets/slide_merged.png",
            "slide_", 17, "Assets/Characters/Prota/Animations/prota_slide.anim", 24f, false);

        // 3. Integrar en Animator Controller
        SetupAnimatorController(clipCrouchLoop, clipSlide);

        // 4. Crear estructura de techo bajo en la escena para pruebas
        SetupLowCeilingTestObstacle();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=green>[PHASE 4]</color> ¡Configuración de Crouch, Slide y estructura de prueba completada con éxito!");
    }

    private static void SliceTexture(string path, int cellWidth, int cellHeight, int cols, int rows, int count, string prefix, Vector2 pivot)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"[PHASE 4] No se encontró TextureImporter en {path}");
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
        Debug.Log($"[PHASE 4] Textura {path} rebanada en {count} sprites con prefijo {prefix}.");
    }

    private static AnimationClip CreateClip(string texturePath, string prefix, int count, string clipPath, float fps, bool loop)
    {
        return CreateSubClip(texturePath, prefix, 0, count - 1, clipPath, fps, loop);
    }

    private static AnimationClip CreateSubClip(string texturePath, string prefix, int startIdx, int endIdx, string clipPath, float fps, bool loop)
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
        for (int i = startIdx; i <= endIdx; i++)
        {
            string sName = $"{prefix}{i}";
            if (spriteDict.TryGetValue(sName, out Sprite sp))
            {
                orderedSprites.Add(sp);
            }
            else
            {
                Debug.LogWarning($"[PHASE 4] Sprite {sName} no encontrado en {texturePath}");
            }
        }

        if (orderedSprites.Count == 0)
        {
            Debug.LogError($"[PHASE 4] No se pudieron cargar sprites para {clipPath}");
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
        Debug.Log($"[PHASE 4] Clip {clipPath} creado/actualizado con {orderedSprites.Count} fotogramas a {fps} FPS (Loop: {loop}).");
        return clip;
    }

    private static void SetupAnimatorController(AnimationClip clipCrouch, AnimationClip clipSlide)
    {
        string controllerPath = "Assets/Characters/Prota/Animations/ProtaController.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            Debug.LogError($"[PHASE 4] No se encontró el AnimatorController en {controllerPath}");
            return;
        }

        // Parámetros
        AddBoolIfNotExists(controller, "isCrouching");
        AddBoolIfNotExists(controller, "isSliding");
        AddTriggerIfNotExists(controller, "slide");

        AnimatorStateMachine rootSm = controller.layers[0].stateMachine;

        // Estados
        AnimatorState stateCrouch = FindOrCreateState(rootSm, "Crouch", clipCrouch);
        AnimatorState stateSlide = FindOrCreateState(rootSm, "Slide", clipSlide);

        AnimatorState stateIdle = FindState(rootSm, "Idle");
        AnimatorState stateRun = FindState(rootSm, "Run");
        AnimatorState stateWalk = FindState(rootSm, "Walk");

        // 1. Transición Idle -> Crouch con isCrouching = true
        if (stateIdle != null && stateCrouch != null)
        {
            EnsureTransitionWithBool(stateIdle, stateCrouch, "isCrouching", true);
            EnsureTransitionWithBool(stateCrouch, stateIdle, "isCrouching", false);
        }

        // 2. Transición Walk -> Crouch y Run -> Crouch
        if (stateWalk != null && stateCrouch != null)
        {
            EnsureTransitionWithBool(stateWalk, stateCrouch, "isCrouching", true);
        }
        if (stateRun != null && stateCrouch != null)
        {
            EnsureTransitionWithBool(stateRun, stateCrouch, "isCrouching", true);
        }

        // 3. Transición Run / Walk / Idle -> Slide con trigger slide o isSliding = true
        if (stateRun != null && stateSlide != null)
        {
            EnsureTransitionWithTrigger(stateRun, stateSlide, "slide");
        }
        if (stateIdle != null && stateSlide != null)
        {
            EnsureTransitionWithTrigger(stateIdle, stateSlide, "slide");
        }
        if (stateWalk != null && stateSlide != null)
        {
            EnsureTransitionWithTrigger(stateWalk, stateSlide, "slide");
        }

        // 4. Salida de Slide -> Crouch (si terminó debajo de un techo bajo)
        if (stateSlide != null && stateCrouch != null)
        {
            EnsureTransitionWithBool(stateSlide, stateCrouch, "isCrouching", true);
        }

        // 5. Salida de Slide -> Idle (si tiene espacio para levantarse)
        if (stateSlide != null && stateIdle != null)
        {
            EnsureExitTransition(stateSlide, stateIdle, 0.90f);
        }

        EditorUtility.SetDirty(controller);
        Debug.Log("[PHASE 4] AnimatorController actualizado con estados Crouch y Slide.");
    }

    private static void SetupLowCeilingTestObstacle()
    {
        string obstacleName = "Test_LowCeilingStructure";
        GameObject obstacle = GameObject.Find(obstacleName);
        if (obstacle == null)
        {
            obstacle = new GameObject(obstacleName);
            // El suelo está en Y ~ -4.45f.
            // Altura normal del jugador: 0.55m (tope en Y ~ -3.90f).
            // Colocamos el techo a Y = -4.05f con grosor 0.35m.
            // Despeje libre sobre el suelo: ~0.40m.
            // El PJ de pie (0.55m) choca y no cabe.
            // El PJ agachado o deslizado (0.30m) pasa perfectamente.
            obstacle.transform.position = new Vector3(-3.5f, -3.95f, 0f);

            var col = obstacle.AddComponent<BoxCollider2D>();
            col.size = new Vector2(2.5f, 0.35f);

            var sr = obstacle.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 3;
            sr.color = new Color(0.65f, 0.25f, 0.15f, 0.9f); // Bloque visible color ladrillo/óxido

            // Crear un sprite simple procedural de 1x1 blanco para el SpriteRenderer
            Texture2D tex = new Texture2D(16, 16);
            Color[] pixels = new Color[16 * 16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(2.5f, 0.35f);

            // Asignar layer Default para que groundLayer lo detecte
            obstacle.layer = LayerMask.NameToLayer("Default");

            Debug.Log("[PHASE 4] Estructura de techo bajo 'Test_LowCeilingStructure' creada en (-3.5, -3.95) con despeje de 0.40m.");
        }
    }

    private static void AddTriggerIfNotExists(AnimatorController controller, string paramName)
    {
        foreach (var p in controller.parameters)
        {
            if (p.name == paramName && p.type == AnimatorControllerParameterType.Trigger) return;
        }
        controller.AddParameter(paramName, AnimatorControllerParameterType.Trigger);
    }

    private static void AddBoolIfNotExists(AnimatorController controller, string paramName)
    {
        foreach (var p in controller.parameters)
        {
            if (p.name == paramName && p.type == AnimatorControllerParameterType.Bool) return;
        }
        controller.AddParameter(paramName, AnimatorControllerParameterType.Bool);
    }

    private static AnimatorState FindState(AnimatorStateMachine sm, string stateName)
    {
        foreach (var cs in sm.states)
        {
            if (cs.state.name == stateName) return cs.state;
        }
        return null;
    }

    private static AnimatorState FindOrCreateState(AnimatorStateMachine sm, string stateName, Motion motion)
    {
        AnimatorState state = FindState(sm, stateName);
        if (state == null)
        {
            state = sm.AddState(stateName);
        }
        state.motion = motion;
        return state;
    }

    private static void EnsureTransitionWithBool(AnimatorState fromState, AnimatorState toState, string boolParam, bool value)
    {
        foreach (var t in fromState.transitions)
        {
            if (t.destinationState == toState)
            {
                t.hasExitTime = false;
                t.duration = 0.05f;
                return;
            }
        }

        var transition = fromState.AddTransition(toState);
        transition.hasExitTime = false;
        transition.duration = 0.05f;
        transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, boolParam);
    }

    private static void EnsureTransitionWithTrigger(AnimatorState fromState, AnimatorState toState, string triggerName)
    {
        foreach (var t in fromState.transitions)
        {
            if (t.destinationState == toState)
            {
                t.hasExitTime = false;
                t.duration = 0f;
                return;
            }
        }

        var transition = fromState.AddTransition(toState);
        transition.hasExitTime = false;
        transition.duration = 0f;
        transition.AddCondition(AnimatorConditionMode.If, 0, triggerName);
    }

    private static void EnsureExitTransition(AnimatorState fromState, AnimatorState toState, float exitTime)
    {
        foreach (var t in fromState.transitions)
        {
            if (t.destinationState == toState)
            {
                t.hasExitTime = true;
                t.exitTime = exitTime;
                t.duration = 0.05f;
                return;
            }
        }

        var transition = fromState.AddTransition(toState);
        transition.hasExitTime = true;
        transition.exitTime = exitTime;
        transition.duration = 0.05f;
    }
}
