using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SetupCompleteCombos
{
    [MenuItem("Combat/Setup Complete Combos (Basic 3-Hit & 5-Hit Variation)")]
    public static void Execute()
    {
        Debug.Log("<color=cyan>[COMBOS]</color> Iniciando configuración completa de combos...");

        // 1. Crear AnimationClips para 2x-1 y 2x-2 en Assets/Characters/Prota/Animations/
        AnimationClip clip2_1 = SetupClipFromAseprite(
            "Assets/Characters/Prota/atk/2x-1 atk/2x-1 atk.aseprite",
            "Assets/Characters/Prota/Animations/prota_atk2_1.anim",
            "prota_atk2_1",
            16f);

        AnimationClip clip2_2 = SetupClipFromAseprite(
            "Assets/Characters/Prota/atk/2x-2 atk/2x-2 atk.aseprite",
            "Assets/Characters/Prota/Animations/prota_atk2_2.anim",
            "prota_atk2_2",
            18f);

        // 2. Configurar Animator Controller
        SetupAnimator(clip2_1, clip2_2);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=green>[COMBOS]</color> ¡Clips y Animator Controller configurados con éxito!");
    }

    private static AnimationClip SetupClipFromAseprite(string asepritePath, string destAnimPath, string clipName, float targetFps)
    {
        Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(asepritePath);
        AnimationClip sourceClip = null;
        List<Sprite> sprites = new List<Sprite>();

        foreach (var obj in subAssets)
        {
            if (obj is AnimationClip c) sourceClip = c;
            if (obj is Sprite s) sprites.Add(s);
        }

        AnimationClip destClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(destAnimPath);

        if (sourceClip != null)
        {
            if (destClip == null)
            {
                destClip = Object.Instantiate(sourceClip);
                destClip.name = clipName;
                AssetDatabase.CreateAsset(destClip, destAnimPath);
            }
            else
            {
                EditorUtility.CopySerialized(sourceClip, destClip);
                destClip.name = clipName;
            }

            destClip.frameRate = targetFps;
            var settings = AnimationUtility.GetAnimationClipSettings(destClip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(destClip, settings);
            EditorUtility.SetDirty(destClip);
            Debug.Log($"[COMBOS] Clip copiado desde Aseprite a {destAnimPath} ({destClip.length}s)");
            return destClip;
        }

        Debug.LogError($"[COMBOS] No se encontró AnimationClip en {asepritePath}");
        return null;
    }

    private static void SetupAnimator(AnimationClip clip2_1, AnimationClip clip2_2)
    {
        string controllerPath = "Assets/Characters/Prota/Animations/ProtaController.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            Debug.LogError($"[COMBOS] No se encontró {controllerPath}");
            return;
        }

        // Asegurar parámetros necesarios
        AddParameterIfNotExists(controller, "isVariation", AnimatorControllerParameterType.Bool);
        AddParameterIfNotExists(controller, "attackVariation", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists(controller, "comboStep", AnimatorControllerParameterType.Int);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        // Buscar estados existentes
        AnimatorState stateAtk1 = FindState(sm, "Attack1");
        AnimatorState stateAtk2 = FindState(sm, "Attack2");
        AnimatorState stateAtk3 = FindState(sm, "Attack3");
        AnimatorState stateDodge1 = FindState(sm, "DodgeAttack1");
        AnimatorState stateDodge2 = FindState(sm, "DodgeAttack2");
        AnimatorState stateVertical = FindState(sm, "VerticalAttack");
        AnimatorState stateIdle = FindState(sm, "Idle");
        AnimatorState stateFall = FindState(sm, "Fall");

        // Crear o actualizar estados de variación
        AnimatorState stateVar2 = FindOrCreateState(sm, "Attack2_Variation", clip2_1, new Vector3(350, -50, 0));
        AnimatorState stateVar3 = FindOrCreateState(sm, "Attack3_Variation", clip2_2, new Vector3(350, 20, 0));

        // 1. Attack1 -> Attack2_Variation (cuando se activa la variación del combo)
        // Condición: attackVariation trigger O (attack trigger && isVariation == true)
        EnsureTransitionWithTriggerAndBool(stateAtk1, stateVar2, "attack", "isVariation", true);
        EnsureTransitionWithTrigger(stateAtk1, stateVar2, "attackVariation");

        // 2. Attack2_Variation -> Attack3_Variation
        EnsureTransitionWithTrigger(stateVar2, stateVar3, "attack");
        EnsureTransitionWithTrigger(stateVar2, stateDodge1, "dodgeAttack"); // Cancelación con K

        // 3. Attack3_Variation -> DodgeAttack1 (Paso 4 del combo de variación)
        EnsureTransitionWithTrigger(stateVar3, stateDodge1, "attack");
        EnsureTransitionWithTrigger(stateVar3, stateDodge1, "dodgeAttack");

        // 4. DodgeAttack1 -> DodgeAttack2 (Paso 5 del combo)
        EnsureTransitionWithTrigger(stateDodge1, stateDodge2, "attack");
        EnsureTransitionWithTrigger(stateDodge1, stateDodge2, "dodgeAttack");

        // 5. DodgeAttack2 -> VerticalAttack (Finisher vertical)
        EnsureTransitionWithTrigger(stateDodge2, stateVertical, "attack");
        EnsureTransitionWithTrigger(stateDodge2, stateVertical, "verticalAttack");

        // Salidas hacia Idle si no se continúa el combo
        EnsureExitTransition(stateVar2, stateIdle, 0.95f);
        EnsureExitTransition(stateVar3, stateIdle, 0.95f);

        EditorUtility.SetDirty(controller);
        Debug.Log("[COMBOS] Árbol de Animator Controller actualizado con Attack2_Variation y Attack3_Variation.");
    }

    private static void AddParameterIfNotExists(AnimatorController controller, string paramName, AnimatorControllerParameterType type)
    {
        foreach (var p in controller.parameters)
        {
            if (p.name == paramName) return;
        }
        controller.AddParameter(paramName, type);
    }

    private static AnimatorState FindState(AnimatorStateMachine sm, string stateName)
    {
        foreach (var cs in sm.states)
        {
            if (cs.state.name == stateName) return cs.state;
        }
        return null;
    }

    private static AnimatorState FindOrCreateState(AnimatorStateMachine sm, string stateName, Motion motion, Vector3 position)
    {
        AnimatorState state = FindState(sm, stateName);
        if (state == null)
        {
            state = sm.AddState(stateName, position);
        }
        state.motion = motion;
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
                    if (c.parameter == triggerName) return; // Ya existe
                }
            }
        }

        var trans = from.AddTransition(to);
        trans.hasExitTime = false;
        trans.duration = 0.05f;
        trans.AddCondition(AnimatorConditionMode.If, 0, triggerName);
    }

    private static void EnsureTransitionWithTriggerAndBool(AnimatorState from, AnimatorState to, string triggerName, string boolParam, bool boolValue)
    {
        foreach (var t in from.transitions)
        {
            if (t.destinationState == to)
            {
                bool hasTrigger = false;
                bool hasBool = false;
                foreach (var c in t.conditions)
                {
                    if (c.parameter == triggerName) hasTrigger = true;
                    if (c.parameter == boolParam) hasBool = true;
                }
                if (hasTrigger && hasBool) return;
            }
        }

        var trans = from.AddTransition(to);
        trans.hasExitTime = false;
        trans.duration = 0.05f;
        trans.AddCondition(AnimatorConditionMode.If, 0, triggerName);
        trans.AddCondition(boolValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, boolParam);
    }

    private static void EnsureExitTransition(AnimatorState from, AnimatorState to, float exitTime)
    {
        foreach (var t in from.transitions)
        {
            if (t.destinationState == to && t.hasExitTime) return;
        }

        var trans = from.AddTransition(to);
        trans.hasExitTime = true;
        trans.exitTime = exitTime;
        trans.duration = 0.1f;
    }
}
