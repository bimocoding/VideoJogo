using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility: Tools > Setup Celeste UI
/// Añade los componentes CelesteMainMenu y CelestePauseMenu a la escena activa.
/// Si ya existen, los reemplaza limpiamente.
/// </summary>
public class SetupCelesteUI : Editor
{
    [MenuItem("Tools/Setup Celeste UI")]
    public static void Setup()
    {
        // ── Limpiar instancias anteriores ──
        CleanExisting<CelesteMainMenu>("CelesteMainMenu_Root");
        CleanExisting<CelestePauseMenu>("CelestePauseMenu_Root");

        // También limpiar por nombre por si el componente se perdió
        DestroyByName("CelesteMainMenu");
        DestroyByName("CelestePauseMenu");
        DestroyByName("CelesteMainMenu_Root");
        DestroyByName("CelestePauseMenu_Root");
        DestroyByName("CelesteMainMenu_Canvas");
        DestroyByName("CelestePauseMenu_Canvas");

        // ── Crear Main Menu ──
        var mainMenuGO = new GameObject("CelesteMainMenu_Root");
        mainMenuGO.AddComponent<CelesteMainMenu>();
        Undo.RegisterCreatedObjectUndo(mainMenuGO, "Create Celeste Main Menu");

        // ── Crear Pause Menu ──
        var pauseMenuGO = new GameObject("CelestePauseMenu_Root");
        pauseMenuGO.AddComponent<CelestePauseMenu>();
        Undo.RegisterCreatedObjectUndo(pauseMenuGO, "Create Celeste Pause Menu");

        // ── Marcar escena dirty ──
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("<color=cyan>[CelesteUI]</color> ✓ Menú principal y menú de pausa estilo Celeste creados correctamente.\n" +
                  "  • <b>Main Menu</b>: Se muestra al iniciar el juego (con botones ESCALAR y CONTROLES)\n" +
                  "  • <b>Pause Menu</b>: Presiona ESC durante el juego (CONTINUAR / REINICIAR)");
    }

    private static void CleanExisting<T>(string fallbackName) where T : MonoBehaviour
    {
        // Buscar por componente
        var existing = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
        foreach (var comp in existing)
        {
            Undo.DestroyObjectImmediate(comp.gameObject);
        }
    }

    private static void DestroyByName(string name)
    {
        var go = GameObject.Find(name);
        if (go != null)
        {
            Undo.DestroyObjectImmediate(go);
        }
    }
}
