using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class ClearMapAndBackground
{
    [MenuItem("Tools/Clear Map And Background")]
    public static void Execute()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        }

        // 1. Limpiar todos los tiles de todos los Tilemaps en la escena
        Tilemap[] tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include);
        int totalCleared = 0;
        foreach (var tm in tilemaps)
        {
            BoundsInt bounds = tm.cellBounds;
            int count = 0;
            foreach (var pos in bounds.allPositionsWithin)
            {
                if (tm.HasTile(pos)) count++;
            }

            tm.ClearAllTiles();
            EditorUtility.SetDirty(tm);
            totalCleared += count;
            Debug.Log($"[ClearMapAndBackground] Limpiados {count} tiles en Tilemap '{tm.gameObject.name}'.");
        }

        // 2. Objetos de mapa y fondo a eliminar
        HashSet<string> targetsToDelete = new HashSet<string>
        {
            "windrise-background_0",
            "bg_panel_0",
            "bg_panel_1",
            "bg_panel_2",
            "bg_panel_3",
            "dodo_0",
            "SummitChest",
            "Test_LowCeilingStructure",
            "Test_WallJumpCorridor",
            "Pillar_Left",
            "Pillar_Right"
        };

        GameObject[] allGos = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        List<GameObject> toDestroy = new List<GameObject>();

        foreach (var go in allGos)
        {
            if (go == null) continue;
            if (targetsToDelete.Contains(go.name) || go.name.StartsWith("bg_panel_") || go.name.StartsWith("windrise-background"))
            {
                if (!toDestroy.Contains(go))
                {
                    toDestroy.Add(go);
                }
            }
        }

        int destroyedCount = 0;
        foreach (var go in toDestroy)
        {
            if (go != null)
            {
                string name = go.name;
                Undo.DestroyObjectImmediate(go);
                destroyedCount++;
                Debug.Log($"[ClearMapAndBackground] Eliminado objeto: {name}");
            }
        }

        // 3. Ajustar posición inicial del protagonista al origen visible (0, 0, 0)
        GameObject prota = GameObject.Find("idle") ?? GameObject.Find("prota");
        if (prota == null)
        {
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) prota = playerObj;
        }

        if (prota != null)
        {
            prota.transform.position = new Vector3(0f, 0f, 0f);
            EditorUtility.SetDirty(prota);
            Debug.Log($"[ClearMapAndBackground] Protagonista reposicionado en (0, 0, 0).");
        }

        // 4. Ajustar enemigo Ghoul cerca si existe
        GameObject ghoul = GameObject.Find("Enemy_Ghoul");
        if (ghoul != null)
        {
            ghoul.transform.position = new Vector3(4f, 0f, 0f);
            EditorUtility.SetDirty(ghoul);
            Debug.Log($"[ClearMapAndBackground] Enemy_Ghoul reposicionado en (4, 0, 0).");
        }

        // 5. Guardar la escena
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"<color=green>[ClearMapAndBackground] ¡Éxito! {totalCleared} tiles borrados, {destroyedCount} objetos de mapa/fondo eliminados. La escena está completamente limpia para el nuevo mapa.</color>");
    }
}
