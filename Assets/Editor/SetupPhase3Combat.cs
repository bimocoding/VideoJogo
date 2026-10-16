using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SetupPhase3Combat
{
    [MenuItem("Combat/Setup Phase 3 - Dodge & Vertical Attack")]
    public static void ExecutePhase3Setup()
    {
        Debug.Log("<color=cyan>[PHASE 3]</color> Iniciando configuración de Dodge Attacks y Vertical Attack...");

        // 1. Configurar y rebanar texturas
        SliceTexture(
            "Assets/Characters/Prota/dodge atk/dodge atk 1x/sprite sheets/dodge atk 1x_merged.png",
            260, 56, 4, 5, 19, "dodge_atk1_", new Vector2(115f / 260f, 0f));

        SliceTexture(
            "Assets/Characters/Prota/dodge atk/dodge atk 2x/sprite sheets/dodge atk 2x_merged.png",
            112, 128, 4, 4, 16, "dodge_atk2_", new Vector2(70.5f / 112f, 28f / 128f));

        SliceTexture(
            "Assets/Characters/Prota/dodge atk/dodge atk 3x(vertical atk)/sprite sheets/dodge atk 3x_grid.png",
            224, 288, 6, 7, 37, "vertical_atk_", new Vector2(111f / 224f, 64f / 288f));

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // 2. Crear clips de animación
        AnimationClip clipDodge1 = CreateClip(
            "Assets/Characters/Prota/dodge atk/dodge atk 1x/sprite sheets/dodge atk 1x_merged.png",
            "dodge_atk1_", 19, "Assets/Characters/Prota/Animations/prota_dodge_atk1.anim", 24f);

        AnimationClip clipDodge2 = CreateClip(
            "Assets/Characters/Prota/dodge atk/dodge atk 2x/sprite sheets/dodge atk 2x_merged.png",
            "dodge_atk2_", 16, "Assets/Characters/Prota/Animations/prota_dodge_atk2.anim", 24f);

        AnimationClip clipVertical = CreateClip(
            "Assets/Characters/Prota/dodge atk/dodge atk 3x(vertical atk)/sprite sheets/dodge atk 3x_grid.png",
            "vertical_atk_", 37, "Assets/Characters/Prota/Animations/prota_vertical_atk.anim", 24f);

        // 3. Integrar en Animator Controller
        SetupAnimatorController(clipDodge1, clipDodge2, clipVertical);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=green>[PHASE 3]</color> ¡Configuración de clips y Animator Controller completada con éxito!");
    }

    private static void SliceTexture(string path, int cellWidth, int cellHeight, int cols, int rows, int count, string prefix, Vector2 pivot)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"[PHASE 3] No se encontró TextureImporter en {path}");
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
        Debug.Log($"[PHASE 3] Textura {path} rebanada en {count} sprites con prefijo {prefix}.");
    }

    private static AnimationClip CreateClip(string texturePath, string prefix, int count, string clipPath, float fps)
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
            string sName = $"{prefix}{i}";
            if (spriteDict.TryGetValue(sName, out Sprite sp))
            {
                orderedSprites.Add(sp);
            }
            else
            {
                Debug.LogWarning($"[PHASE 3] Sprite {sName} no encontrado en {texturePath}");
            }
        }

        if (orderedSprites.Count == 0)
        {
            Debug.LogError($"[PHASE 3] No se pudieron cargar sprites para {clipPath}");
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
        settings.loopTime = false;
        settings.stopTime = orderedSprites.Count / fps;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        EditorUtility.SetDirty(clip);
        Debug.Log($"[PHASE 3] Clip {clipPath} creado/actualizado con {orderedSprites.Count} fotogramas a {fps} FPS.");
        return clip;
    }

    private static void SetupAnimatorController(AnimationClip clipDodge1, AnimationClip clipDodge2, AnimationClip clipVertical)
    {
        string controllerPath = "Assets/Characters/Prota/Animations/ProtaController.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            Debug.LogError($"[PHASE 3] No se encontró el AnimatorController en {controllerPath}");
            return;
        }

        // Parámetros
        AddTriggerIfNotExists(controller, "dodgeAttack");
        AddTriggerIfNotExists(controller, "verticalAttack");
        AddBoolIfNotExists(controller, "isDodgeAttacking");
        AddBoolIfNotExists(controller, "isVerticalAttacking");

        AnimatorStateMachine rootSm = controller.layers[0].stateMachine;

        // Estados
        AnimatorState stateDodge1 = FindOrCreateState(rootSm, "DodgeAttack1", clipDodge1);
        AnimatorState stateDodge2 = FindOrCreateState(rootSm, "DodgeAttack2", clipDodge2);
        AnimatorState stateVertical = FindOrCreateState(rootSm, "VerticalAttack", clipVertical);

        AnimatorState stateAtk1 = FindState(rootSm, "Attack1");
        AnimatorState stateAtk2 = FindState(rootSm, "Attack2");
        AnimatorState stateIdle = FindState(rootSm, "Idle");
        AnimatorState stateFall = FindState(rootSm, "Fall");

        // 1. Transición Attack1 -> DodgeAttack1 con trigger dodgeAttack
        if (stateAtk1 != null && stateDodge1 != null)
        {
            EnsureTransitionWithTrigger(stateAtk1, stateDodge1, "dodgeAttack");
        }

        // 2. Transición Attack2 -> DodgeAttack2 con trigger dodgeAttack
        if (stateAtk2 != null && stateDodge2 != null)
        {
            EnsureTransitionWithTrigger(stateAtk2, stateDodge2, "dodgeAttack");
        }

        // 3. Transición DodgeAttack1 -> DodgeAttack2 con trigger dodgeAttack
        if (stateDodge1 != null && stateDodge2 != null)
        {
            EnsureTransitionWithTrigger(stateDodge1, stateDodge2, "dodgeAttack");
        }

        // 4. Salida de DodgeAttack1 -> Idle
        if (stateDodge1 != null && stateIdle != null)
        {
            EnsureExitTransition(stateDodge1, stateIdle, 1.0f);
        }

        // 5. Salida de DodgeAttack2 -> Idle
        if (stateDodge2 != null && stateIdle != null)
        {
            EnsureExitTransition(stateDodge2, stateIdle, 1.0f);
        }

        // 6. Transición AnyState -> VerticalAttack con trigger verticalAttack
        if (stateVertical != null)
        {
            EnsureAnyStateTransition(rootSm, stateVertical, "verticalAttack");
        }

        // 7. Salida de VerticalAttack -> Fall
        if (stateVertical != null && stateFall != null)
        {
            EnsureExitTransition(stateVertical, stateFall, 0.90f);
        }

        EditorUtility.SetDirty(controller);
        Debug.Log("[PHASE 3] AnimatorController actualizado con estados DodgeAttack1, DodgeAttack2 y VerticalAttack.");
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

    private static void EnsureAnyStateTransition(AnimatorStateMachine sm, AnimatorState toState, string triggerName)
    {
        foreach (var t in sm.anyStateTransitions)
        {
            if (t.destinationState == toState)
            {
                t.hasExitTime = false;
                t.duration = 0f;
                return;
            }
        }

        var transition = sm.AddAnyStateTransition(toState);
        transition.hasExitTime = false;
        transition.duration = 0f;
        transition.AddCondition(AnimatorConditionMode.If, 0, triggerName);
    }
}
