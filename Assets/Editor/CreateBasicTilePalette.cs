using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class CreateBasicTilePalette
{
    [MenuItem("Tools/Rebuild Basic Tile Palette")]
    public static void Execute()
    {
        string texturePath = "Assets/fantasy assets/basic.png";
        
        // 1. Crear carpetas si no existen
        string tilesFolder = "Assets/fantasy assets/Tiles";
        if (!AssetDatabase.IsValidFolder(tilesFolder))
        {
            AssetDatabase.CreateFolder("Assets/fantasy assets", "Tiles");
        }

        string paletteFolder = "Assets/fantasy assets/Palettes";
        if (!AssetDatabase.IsValidFolder(paletteFolder))
        {
            AssetDatabase.CreateFolder("Assets/fantasy assets", "Palettes");
        }

        // 2. Cargar todos los sub-sprites de basic.png
        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
        List<Sprite> sprites = new List<Sprite>();
        foreach (var obj in allAssets)
        {
            if (obj is Sprite sp)
            {
                sprites.Add(sp);
            }
        }

        if (sprites.Count == 0)
        {
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            allAssets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
            foreach (var obj in allAssets)
            {
                if (obj is Sprite sp)
                {
                    sprites.Add(sp);
                }
            }
        }

        // 3. Crear Tile assets para cada sprite
        Dictionary<string, Tile> tileDict = new Dictionary<string, Tile>();
        foreach (var sp in sprites)
        {
            string tileAssetPath = $"{tilesFolder}/{sp.name}.asset";
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(tileAssetPath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sp;
                tile.color = Color.white;
                tile.colliderType = Tile.ColliderType.Grid;
                AssetDatabase.CreateAsset(tile, tileAssetPath);
            }
            else
            {
                tile.sprite = sp;
                tile.colliderType = Tile.ColliderType.Grid;
                EditorUtility.SetDirty(tile);
            }
            tileDict[sp.name] = tile;
        }

        // 4. Configurar prefab de la Paleta: BasicPalette.prefab
        string palettePrefabPath = $"{paletteFolder}/BasicPalette.prefab";
        BuildPalettePrefab(palettePrefabPath, tileDict, "BasicPalette");

        // 5. También actualizar 'New Tile Palette.prefab' existente
        string existingPalettePath = "Assets/Pixel Art Platformer - Village Props v2.3.1/Textures/New Tile Palette.prefab";
        if (File.Exists(existingPalettePath))
        {
            BuildPalettePrefab(existingPalettePath, tileDict, "New Tile Palette");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=green>[CreateBasicTilePalette] ¡Paletas configuradas con éxito con el tileset 'basic'!</color>");
    }

    private static void BuildPalettePrefab(string prefabPath, Dictionary<string, Tile> tileDict, string rootName)
    {
        GameObject paletteGO = new GameObject(rootName);
        Grid grid = paletteGO.AddComponent<Grid>();
        grid.cellSize = new Vector3(1f, 1f, 0f);

        GameObject layerGO = new GameObject("Layer1");
        layerGO.transform.SetParent(paletteGO.transform);
        Tilemap tilemap = layerGO.AddComponent<Tilemap>();
        TilemapRenderer tmRenderer = layerGO.AddComponent<TilemapRenderer>();

        Material defaultMat = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        if (defaultMat != null)
        {
            tmRenderer.sharedMaterial = defaultMat;
        }

        foreach (var kvp in tileDict)
        {
            string name = kvp.Key;
            Tile tile = kvp.Value;

            string[] parts = name.Split('_');
            if (parts.Length == 3 && int.TryParse(parts[1], out int r) && int.TryParse(parts[2], out int c))
            {
                Vector3Int cellPos = new Vector3Int(c, -r, 0);
                tilemap.SetTile(cellPos, tile);
            }
            else
            {
                tilemap.SetTile(new Vector3Int(0, 0, 0), tile);
            }
        }

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(paletteGO, prefabPath);

        GridPalette gridPaletteSettings = ScriptableObject.CreateInstance<GridPalette>();
        gridPaletteSettings.cellSizing = GridPalette.CellSizing.Automatic;
        AssetDatabase.AddObjectToAsset(gridPaletteSettings, savedPrefab);

        GameObject.DestroyImmediate(paletteGO);
        EditorUtility.SetDirty(savedPrefab);
    }
}
