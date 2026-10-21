using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SetupAerialDash
{
    [MenuItem("Combat/Setup Aerial Dash")]
    public static void ExecuteAerialDashSetup()
    {
        Debug.Log("<color=cyan>[AERIAL DASH]</color> Iniciando configuración de Aerial Dash...");

        string texturePath = "Assets/Characters/Prota/aerial dash/sprite sheets/aerial dash.png";
        
        // 1. Rebanar la textura aerial dash.png
        // Tamaño: 624x100 -> 6 columnas de 104px x 2 filas de 50px
        // Fila 0 (superior en la imagen, con efectos de ilusión y estela) -> unityRow = 1 (y=50)
        SliceAerialDashTexture(texturePath, 104, 50, 6, "air_dash_", new Vector2(38f / 104f, 0f));

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // 2. Crear AnimationClip prota_air_dash.anim
        string clipPath = "Assets/Characters/Prota/Animations/prota_air_dash.anim";
        AnimationClip clipAirDash = CreateClip(texturePath, "air_dash_", 6, clipPath, 20f, false);

        // 3. Integrar en Animator Controller
        SetupAnimatorController(clipAirDash);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=green>[AERIAL DASH]</color> ¡Configuración de Aerial Dash completada con éxito!");
    }

    private static void SliceAerialDashTexture(string path, int cellWidth, int cellHeight, int count, string prefix, Vector2 pivot)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"[AERIAL DASH] No se encontró TextureImporter en {path}");
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
            SpriteMetaData smd = new SpriteMetaData();
            smd.name = $"{prefix}{i}";
            // Top row with illusion effect has y = 50 to 100
            smd.rect = new Rect(i * cellWidth, 50f, cellWidth, cellHeight);
            smd.alignment = 9; // Custom
            smd.pivot = pivot;
            sheet[i] = smd;
        }

#pragma warning disable 0618
        importer.spritesheet = sheet;
#pragma warning restore 0618
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        Debug.Log($"[AERIAL DASH] Textura {path} rebanada en {count} sprites con prefijo {prefix}.");
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
                Debug.LogWarning($"[AERIAL DASH] Sprite {spriteName} no encontrado en {texturePath}");
            }
        }

        if (orderedSprites.Count == 0)
        {
            Debug.LogError($"[AERIAL DASH] No se pudieron cargar sprites para {clipPath}");
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
        Debug.Log($"[AERIAL DASH] Clip {clipPath} creado con {orderedSprites.Count} fotogramas a {fps} FPS (Loop: {loop}).");
        return clip;
    }

    private static void SetupAnimatorController(AnimationClip clipAirDash)
    {
        string controllerPath = "Assets/Characters/Prota/Animations/ProtaController.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            Debug.LogError($"[AERIAL DASH] No se encontró el AnimatorController en {controllerPath}");
            return;
        }

        // Parámetros
        AddBoolIfNotExists(controller, "isAirDashing");
        AddTriggerIfNotExists(controller, "airDash");

        AnimatorStateMachine rootSm = controller.layers[0].stateMachine;

        // Estado AirDash
        AnimatorState stateAirDash = FindOrCreateState(rootSm, "AirDash", clipAirDash);

        AnimatorState stateJump = FindState(rootSm, "Jump");
        AnimatorState stateFall = FindState(rootSm, "Fall");
        AnimatorState stateIdle = FindState(rootSm, "Idle");

        // 1. Transición Jump -> AirDash
        if (stateJump != null && stateAirDash != null)
        {
            EnsureTransitionWithTrigger(stateJump, stateAirDash, "airDash");
        }

        // 2. Transición Fall -> AirDash
        if (stateFall != null && stateAirDash != null)
        {
            EnsureTransitionWithTrigger(stateFall, stateAirDash, "airDash");
        }

        // 3. Salida AirDash -> Fall cuando termina el dash en el aire
        if (stateAirDash != null && stateFall != null)
        {
            EnsureExitTransition(stateAirDash, stateFall, 0.85f);
        }

        // 4. Salida AirDash -> Idle si aterriza durante el dash
        if (stateAirDash != null && stateIdle != null)
        {
            EnsureTransitionWithBool(stateAirDash, stateIdle, "isGrounded", true);
        }

        EditorUtility.SetDirty(controller);
        Debug.Log("[AERIAL DASH] AnimatorController actualizado con estado AirDash y transiciones.");
    }

    private static void AddBoolIfNotExists(AnimatorController controller, string paramName)
    {
        foreach (var p in controller.parameters)
        {
            if (p.name == paramName && p.type == AnimatorControllerParameterType.Bool) return;
        }
        controller.AddParameter(paramName, AnimatorControllerParameterType.Bool);
    }

    private static void AddTriggerIfNotExists(AnimatorController controller, string paramName)
    {
        foreach (var p in controller.parameters)
        {
            if (p.name == paramName && p.type == AnimatorControllerParameterType.Trigger) return;
        }
        controller.AddParameter(paramName, AnimatorControllerParameterType.Trigger);
    }

    private static AnimatorState FindState(AnimatorStateMachine sm, string stateName)
    {
        foreach (var childState in sm.states)
        {
            if (childState.state.name == stateName) return childState.state;
        }
        return null;
    }

    private static AnimatorState FindOrCreateState(AnimatorStateMachine sm, string stateName, Motion motion)
    {
        AnimatorState existing = FindState(sm, stateName);
        if (existing != null)
        {
            existing.motion = motion;
            return existing;
        }
        AnimatorState newState = sm.AddState(stateName);
        newState.motion = motion;
        return newState;
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
        AnimatorStateTransition trans = from.AddTransition(to);
        trans.AddCondition(AnimatorConditionMode.If, 0, triggerName);
        trans.hasExitTime = false;
        trans.duration = 0.05f;
    }

    private static void EnsureTransitionWithBool(AnimatorState from, AnimatorState to, string boolName, bool value)
    {
        foreach (var t in from.transitions)
        {
            if (t.destinationState == to)
            {
                foreach (var c in t.conditions)
                {
                    if (c.parameter == boolName) return;
                }
            }
        }
        AnimatorStateTransition trans = from.AddTransition(to);
        trans.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, boolName);
        trans.hasExitTime = false;
        trans.duration = 0.05f;
    }

    private static void EnsureExitTransition(AnimatorState from, AnimatorState to, float exitTime)
    {
        foreach (var t in from.transitions)
        {
            if (t.destinationState == to && t.hasExitTime) return;
        }
        AnimatorStateTransition trans = from.AddTransition(to);
        trans.hasExitTime = true;
        trans.exitTime = exitTime;
        trans.duration = 0.05f;
    }
}
