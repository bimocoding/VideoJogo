using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

/// <summary>
/// Level Generator v4 - Authentic Pixel Art Platformer Level
/// Uses the correct tile mapping from basic.png:
/// - Top grass surface: basic_0_2 (corner L), basic_0_3..5 (flat grass), basic_0_6 (corner R)
/// - Left cliff wall: basic_1_0 / basic_2_0 / basic_3_0
/// - Right cliff wall: basic_1_8 / basic_2_8 / basic_3_8
/// - Inner dirt fill: basic_1_4 / basic_2_4
/// - Bottom edge: basic_6_0 (corner L), basic_6_1 (bottom center), basic_6_8 (corner R)
/// - Thin platforms: basic_1_10 (L cap), basic_1_11 (plank), basic_1_12 (R cap)
/// - Background depth: cols 17-25 (darker palette variants)
/// </summary>
public class LevelGenerator : EditorWindow
{
    [MenuItem("Tools/Generate Level 1")]
    public static void GenerateLevel()
    {
        var builder = new LevelBuilder();
        builder.Build();
        Debug.Log("<color=green>[LevelGenerator] ¡Nivel 1 generado con estética correcta y enemigos!</color>");
    }
}

public class LevelBuilder
{
    private Dictionary<string, TileBase> tiles = new Dictionary<string, TileBase>();
    private Grid grid;
    private Tilemap mainTilemap;
    private Tilemap bgTilemap;

    private const int GROUND_Y = -8;     // Nivel de superficie base
    private const int FILL_BOTTOM = -24; // Relleno subterráneo profundo para evitar vacíos inferiores

    public void Build()
    {
        RestoreTileColliders();
        LoadTiles();
        SetupTilemaps();
        ClearTilemaps();

        // 1. Zona Segura Inicial (0 - 32)
        BuildSection1_SafeZone();

        // 2. Plataformeo Básico y Terrazas (32 - 76)
        BuildSection2_BasicPlatforming();

        // 3. Ascenso a la Cumbre del Monte (78 - 128)
        BuildSection3_VerticalClimb();

        // 4. Pasajes de Deslizamiento (Slide) (130 - 168)
        BuildSection4_SlideTunnels();

        // 5. La Gran Grieta del Sprint (170 - 224)
        BuildSection5_SprintChasm();

        // 6. La Torre de Salientes (Wall Jump & Ledge Grab) (226 - 256)
        BuildSection6_TowerOfLedges();

        // 7. Arena de Combate y Estrado Final (258 - 306)
        BuildSection7_ArenaAndGoal();

        // Fondo de Profundidad Subterráneo
        BuildDeepBackground();

        // Ajustes de Cámara, Protagonista, Meta y Múltiples Ghouls
        AdjustCamera();
        SetupProtagonist();
        PopulateEnemies();
        SetupVictoryUI();

        // Guardar escena
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }

    private void RestoreTileColliders()
    {
        // Los tiles de superficie de suelo DEBEN ser sólidos para que el personaje camine encima
        for (int i = 2; i <= 6; i++)
        {
            string path = $"Assets/fantasy assets/Tiles/basic_0_{i}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile != null && tile.colliderType != Tile.ColliderType.Grid)
            {
                tile.colliderType = Tile.ColliderType.Grid;
                EditorUtility.SetDirty(tile);
            }
        }
        AssetDatabase.SaveAssets();
    }

    private void LoadTiles()
    {
        tiles.Clear();
        string[] searchFolders = { "Assets/fantasy assets/Tiles", "Assets/Pixel Art Platformer - Village Props v2.3.1/Textures" };
        foreach (var folder in searchFolders)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Tile", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                if (tile != null && !tiles.ContainsKey(tile.name))
                    tiles[tile.name] = tile;
            }
        }
    }

    private void SetupTilemaps()
    {
        grid = GameObject.Find("Grid")?.GetComponent<Grid>();
        if (grid == null)
        {
            var gridObj = new GameObject("Grid");
            grid = gridObj.AddComponent<Grid>();
            grid.cellSize = new Vector3(0.32f, 0.32f, 0f);
        }

        // 1. Tilemap Principal Sólido (Capa 3 - Floor)
        var mainTrans = grid.transform.Find("Tilemap");
        if (mainTrans == null)
        {
            var go = new GameObject("Tilemap");
            go.transform.SetParent(grid.transform, false);
            mainTilemap = go.AddComponent<Tilemap>();
            var rend = go.AddComponent<TilemapRenderer>();
            rend.sortingOrder = 0;
            go.layer = 3;
            go.tag = "Floor";
            var col = go.AddComponent<TilemapCollider2D>();
            col.usedByComposite = true;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            go.AddComponent<CompositeCollider2D>();
        }
        else
        {
            mainTilemap = mainTrans.GetComponent<Tilemap>();
        }

        // Eliminar tilemap superfluo de Decorations si existía
        var decorTrans = grid.transform.Find("Decorations");
        if (decorTrans != null)
        {
            Undo.DestroyObjectImmediate(decorTrans.gameObject);
        }

        // 2. Tilemap de Fondo (Profundidad, sin colisión, sortingOrder = -1)
        var bgTrans = grid.transform.Find("Background");
        if (bgTrans == null)
        {
            var go = new GameObject("Background");
            go.transform.SetParent(grid.transform, false);
            bgTilemap = go.AddComponent<Tilemap>();
            var rend = go.AddComponent<TilemapRenderer>();
            rend.sortingOrder = -1;
        }
        else
        {
            bgTilemap = bgTrans.GetComponent<Tilemap>();
        }
    }

    private void ClearTilemaps()
    {
        if (mainTilemap != null)
        {
            Undo.RecordObject(mainTilemap, "Clear Main Tilemap");
            mainTilemap.ClearAllTiles();
        }
        if (bgTilemap != null)
        {
            Undo.RecordObject(bgTilemap, "Clear BG Tilemap");
            bgTilemap.ClearAllTiles();
        }
    }

    private TileBase T(string name)
    {
        tiles.TryGetValue(name, out TileBase t);
        return t;
    }

    private void SetSolid(int x, int y, string name)
    {
        var tile = T(name);
        if (tile != null) mainTilemap.SetTile(new Vector3Int(x, y, 0), tile);
    }

    private void SetBg(int x, int y, string name)
    {
        var tile = T(name);
        if (tile != null && bgTilemap != null) bgTilemap.SetTile(new Vector3Int(x, y, 0), tile);
    }

    /// <summary>
    /// Construye un bloque sólido de terreno usando el autotile exacto del tileset:
    /// - Superficie superior: hierba frondosa (basic_0_2, basic_0_3..5, basic_0_6)
    /// - Paredes verticales: acantilado de roca (basic_1_0/2_0 a la izq, basic_1_8/2_8 a la der)
    /// - Relleno interior: tierra sólida limpia (basic_1_4 / basic_2_4)
    /// - Fondo inferior: roca subterránea (basic_6_0, basic_6_1, basic_6_8)
    /// </summary>
    private void BuildTerrain(int x, int y, int w, int h)
    {
        if (w <= 0 || h <= 0) return;
        int x2 = x + w - 1;
        int y2 = y + h - 1;

        for (int tx = x; tx <= x2; tx++)
        {
            for (int ty = y; ty <= y2; ty++)
            {
                bool isLeft = tx == x;
                bool isRight = tx == x2;
                bool isTop = ty == y2;
                bool isBottom = ty == y;

                string tile;
                if (isTop)
                {
                    if (isLeft && w > 1) tile = "basic_0_2";       // Esquina superior izquierda
                    else if (isRight && w > 1) tile = "basic_0_6"; // Esquina superior derecha
                    else
                    {
                        // Variaciones armónicas de hierba en la superficie plana
                        int v = (Mathf.Abs(tx * 7 + ty * 13) % 3) + 3; // basic_0_3, basic_0_4, basic_0_5
                        tile = $"basic_0_{v}";
                    }
                }
                else if (isBottom)
                {
                    if (isLeft && w > 1) tile = "basic_6_0";       // Esquina inferior izquierda
                    else if (isRight && w > 1) tile = "basic_6_8"; // Esquina inferior derecha
                    else tile = "basic_6_1";                       // Borde inferior plano
                }
                else if (isLeft && w > 1)
                {
                    tile = (ty % 2 == 0) ? "basic_1_0" : "basic_2_0"; // Pared vertical izquierda
                }
                else if (isRight && w > 1)
                {
                    tile = (ty % 2 == 0) ? "basic_1_8" : "basic_2_8"; // Pared vertical derecha
                }
                else
                {
                    tile = "basic_1_4"; // Relleno de tierra interior uniforme, liso y limpio (sin artefactos repetitivos)
                }

                SetSolid(tx, ty, tile);
            }
        }
    }

    /// <summary>
    /// Plataforma flotante delgada (1 tile de grosor) con sus extremos y tablón/hierba correctos:
    /// basic_1_10 (extremo izq), basic_1_11 (centro), basic_1_12 (extremo der)
    /// </summary>
    private void BuildPlatform(int x, int y, int w)
    {
        if (w <= 0) return;
        if (w == 1) { SetSolid(x, y, "basic_1_11"); return; }

        SetSolid(x, y, "basic_1_10");
        for (int tx = x + 1; tx < x + w - 1; tx++)
        {
            SetSolid(tx, y, "basic_1_11");
        }
        SetSolid(x + w - 1, y, "basic_1_12");
    }

    /// <summary>
    /// Construye estructuras de fondo (paredes, pilares, arcos) con los tonos oscuros de la paleta:
    /// basic_0_19..23 (arriba), basic_1_17..25 (muros), basic_2_20 (relleno oscuro), basic_6_17..25 (base)
    /// </summary>
    private void BuildBgBlock(int x, int y, int w, int h)
    {
        if (w <= 0 || h <= 0) return;
        int x2 = x + w - 1;
        int y2 = y + h - 1;

        for (int tx = x; tx <= x2; tx++)
        {
            for (int ty = y; ty <= y2; ty++)
            {
                bool isLeft = tx == x;
                bool isRight = tx == x2;
                bool isTop = ty == y2;
                bool isBottom = ty == y;

                string tile;
                if (isTop)
                {
                    if (isLeft && w > 1) tile = "basic_0_19";
                    else if (isRight && w > 1) tile = "basic_0_23";
                    else
                    {
                        int v = (Mathf.Abs(tx * 3 + ty * 5) % 3) + 20; // basic_0_20, 21, 22
                        tile = $"basic_0_{v}";
                    }
                }
                else if (isBottom)
                {
                    if (isLeft && w > 1) tile = "basic_6_17";
                    else if (isRight && w > 1) tile = "basic_6_25";
                    else tile = "basic_6_18";
                }
                else if (isLeft && w > 1)
                {
                    tile = (ty % 2 == 0) ? "basic_1_17" : "basic_2_17";
                }
                else if (isRight && w > 1)
                {
                    tile = (ty % 2 == 0) ? "basic_1_25" : "basic_2_25";
                }
                else
                {
                    tile = ((tx + ty) % 2 == 0) ? "basic_1_20" : "basic_2_20";
                }

                SetBg(tx, ty, tile);
            }
        }
    }

    private void BuildBgPlatform(int x, int y, int w)
    {
        if (w <= 0) return;
        if (w == 1) { SetBg(x, y, "basic_1_28"); return; }
        SetBg(x, y, "basic_1_27");
        for (int tx = x + 1; tx < x + w - 1; tx++)
        {
            SetBg(tx, y, "basic_1_28");
        }
        SetBg(x + w - 1, y, "basic_1_29");
    }

    // ============================================================
    // CONSTRUCCIÓN DE SECCIONES
    // ============================================================

    private void BuildSection1_SafeZone()
    {
        // Superficie continua y firme al inicio
        BuildTerrain(0, FILL_BOTTOM, 33, GROUND_Y - FILL_BOTTOM + 1);

        // Pequeño escalón de piedra y hierba
        BuildTerrain(18, GROUND_Y + 1, 5, 2);

        // Plataforma delgada superior para salto
        BuildPlatform(25, GROUND_Y + 5, 4);

        // Estructuras de fondo para dar profundidad a la arboleda
        BuildBgBlock(8, GROUND_Y + 1, 5, 9);
        BuildBgBlock(22, GROUND_Y + 1, 4, 11);
    }

    private void BuildSection2_BasicPlatforming()
    {
        // Brecha 1: 7 tiles (2.24m) -> Salto muy cómodo de caminata
        BuildTerrain(40, FILL_BOTTOM, 12, GROUND_Y - FILL_BOTTOM + 1);

        // Plataforma flotante para explorar
        BuildPlatform(44, GROUND_Y + 5, 4);

        // Brecha 2: 8 tiles (2.56m)
        BuildTerrain(60, FILL_BOTTOM, 11, GROUND_Y - FILL_BOTTOM + 2); // Terraza ligeramente elevada

        // Plataformas escalonadas
        BuildPlatform(73, GROUND_Y + 4, 3);
        BuildPlatform(78, GROUND_Y + 6, 3);

        // Fondo oscuro para sensación de profundidad
        BuildBgBlock(42, GROUND_Y + 1, 4, 13);
        BuildBgBlock(63, GROUND_Y + 2, 4, 15);
    }

    private void BuildSection3_VerticalClimb()
    {
        int bx = 83;
        BuildTerrain(bx, FILL_BOTTOM, 8, GROUND_Y - FILL_BOTTOM + 1);

        // Ascenso escalonado con incrementos de 4 tiles de altura (salto corto = 9.6 tiles máx)
        BuildTerrain(bx + 6, GROUND_Y + 4, 5, 2);
        BuildTerrain(bx + 12, GROUND_Y + 8, 5, 2);
        BuildTerrain(bx + 8, GROUND_Y + 12, 5, 2);

        // LA CUMBRE
        BuildTerrain(bx + 14, GROUND_Y + 16, 9, 2);

        // Descenso seguro y rítmico
        BuildTerrain(bx + 24, GROUND_Y + 12, 4, 2);
        BuildPlatform(bx + 29, GROUND_Y + 8, 4);
        BuildTerrain(bx + 34, GROUND_Y + 4, 4, 2);

        // Suelo de recepción
        BuildTerrain(bx + 38, FILL_BOTTOM, 10, GROUND_Y - FILL_BOTTOM + 1);

        // Pilares colosales de fondo
        BuildBgBlock(bx + 8, GROUND_Y + 1, 4, 19);
        BuildBgBlock(bx + 16, GROUND_Y + 1, 5, 23);
    }

    private void BuildSection4_SlideTunnels()
    {
        int bx = 132;
        // Suelo continuo
        BuildTerrain(bx, FILL_BOTTOM, 36, GROUND_Y - FILL_BOTTOM + 1);

        // TÚNEL 1 (Longitud 5 tiles): Techo a GROUND_Y + 3 (altura libre de 2 tiles para deslizamiento holgado)
        BuildTerrain(bx + 7, GROUND_Y + 3, 5, 4);
        // Ruta alternativa por arriba (plataformas)
        BuildPlatform(bx + 6, GROUND_Y + 8, 7);

        // Descanso intermedio
        BuildPlatform(bx + 14, GROUND_Y + 4, 3);

        // TÚNEL 2 (Longitud 6 tiles)
        BuildTerrain(bx + 19, GROUND_Y + 3, 6, 4);
        // Ruta alternativa por arriba
        BuildPlatform(bx + 18, GROUND_Y + 8, 8);

        // Fondo oscuro de cueva
        BuildBgBlock(bx + 6, GROUND_Y + 1, 20, 10);
    }

    private void BuildSection5_SprintChasm()
    {
        int bx = 170;
        // Pista de carrerilla para tomar velocidad de carrera (4.6m/s con Shift)
        BuildTerrain(bx, FILL_BOTTOM, 13, GROUND_Y - FILL_BOTTOM + 1);

        // Grieta 1: 14 tiles con isla de rescate en el centro
        BuildPlatform(bx + 19, GROUND_Y + 2, 3);

        // Pista intermedia
        BuildTerrain(bx + 27, FILL_BOTTOM, 12, GROUND_Y - FILL_BOTTOM + 1);

        // Grieta 2: 15 tiles con isla de rescate
        BuildPlatform(bx + 45, GROUND_Y + 3, 3);

        // Aterrizaje
        BuildTerrain(bx + 54, FILL_BOTTOM, 12, GROUND_Y - FILL_BOTTOM + 1);

        // Ruinas oscuras en el abismo
        BuildBgBlock(bx + 15, GROUND_Y - 4, 6, 12);
        BuildBgBlock(bx + 41, GROUND_Y - 4, 6, 14);
    }

    private void BuildSection6_TowerOfLedges()
    {
        int bx = 228;
        int innerW = 6;
        int shaftLeft = bx + 4;
        int shaftRight = shaftLeft + innerW + 1;

        // Suelo continuo de entrada y base de la torre
        BuildTerrain(bx, FILL_BOTTOM, 26, GROUND_Y - FILL_BOTTOM + 1);

        // Arco de entrada totalmente despejado a ras de suelo: el muro exterior comienza a +5 tiles
        BuildTerrain(shaftLeft - 2, GROUND_Y + 5, 2, 19);

        // Muro derecho de la torre
        BuildTerrain(shaftRight, GROUND_Y + 1, 2, 21);

        // Cornisas escalonadas (Ledges) para engancharse y hacer Wall Jump alternado:
        BuildPlatform(shaftLeft, GROUND_Y + 5, 2);
        BuildPlatform(shaftRight - 2, GROUND_Y + 10, 2);
        BuildPlatform(shaftLeft, GROUND_Y + 15, 2);
        BuildPlatform(shaftRight - 2, GROUND_Y + 20, 2);

        // Salida abierta por la cima hacia la derecha
        BuildTerrain(shaftRight, GROUND_Y + 22, 12, 3);

        // Interior oscuro de la torre
        BuildBgBlock(shaftLeft, GROUND_Y + 1, innerW, 24);
    }

    private void BuildSection7_ArenaAndGoal()
    {
        int bx = 254;

        // Descenso de la torre
        BuildTerrain(bx, GROUND_Y + 16, 5, 2);
        BuildTerrain(bx + 6, GROUND_Y + 11, 5, 2);
        BuildPlatform(bx + 12, GROUND_Y + 6, 4);

        // ARENA DE COMBATE AMPLIA
        BuildTerrain(bx + 14, FILL_BOTTOM, 24, GROUND_Y - FILL_BOTTOM + 1);

        // Plataformas tácticas de combate
        BuildPlatform(bx + 19, GROUND_Y + 4, 4);
        BuildPlatform(bx + 27, GROUND_Y + 7, 3);

        // Paso hacia la meta
        BuildTerrain(bx + 40, FILL_BOTTOM, 16, GROUND_Y - FILL_BOTTOM + 1);

        // ESTRADO DEL COFRE DE LA VICTORIA
        BuildTerrain(bx + 46, GROUND_Y + 1, 6, 2);

        // Fondo oscuro de la arena
        BuildBgBlock(bx + 16, GROUND_Y + 1, 8, 12);
        BuildBgBlock(bx + 32, GROUND_Y + 1, 8, 14);
    }

    private void BuildDeepBackground()
    {
        if (bgTilemap == null) return;
        // Rellenar toda la zona subterránea profunda con el tono de roca oscura de fondo
        for (int x = -4; x <= 312; x++)
        {
            for (int y = FILL_BOTTOM; y <= GROUND_Y - 1; y++)
            {
                if (mainTilemap.GetTile(new Vector3Int(x, y, 0)) == null)
                {
                    SetBg(x, y, "basic_2_20");
                }
            }
        }
    }

    private void AdjustCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        var camScript = cam.GetComponent<camara>();
        if (camScript != null)
        {
            Undo.RecordObject(camScript, "Configure Camera Bounds");
            camScript.useBounds = true;
            camScript.minY = -1.6f;
            camScript.maxY = 7.0f;
            camScript.minX = 1.0f;
            camScript.maxX = 98.0f;
            camScript.smoothTime = 0.12f;
            camScript.useDeadZone = true;
            camScript.deadZoneX = 0.4f;
            camScript.deadZoneY = 0.3f;
        }
        cam.transform.position = new Vector3(2.5f, -1.6f, -10f);
    }

    private void SetupProtagonist()
    {
        var player = GameObject.Find("prota");
        if (player != null)
        {
            player.tag = "Player";
            Undo.RecordObject(player.transform, "Position Prota");
            // Posición exacta sobre la hierba de inicio: Y = (-8 + 1)*0.32 + 0.02 = -2.22f
            player.transform.position = new Vector3(1.20f, -2.20f, 0f);
            player.transform.localScale = new Vector3(-1f, 1f, 1f); // Orientado a la derecha
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        // Cofre de la cumbre en el estrado
        var chest = GameObject.Find("SummitChest");
        if (chest == null)
        {
            chest = new GameObject("SummitChest");
            var sr = chest.AddComponent<SpriteRenderer>();
            var sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Pixel Art Platformer - Village Props v2.3.1/Textures/TX Chest Animation.png");
            foreach (var s in sprites)
            {
                if (s is Sprite sp && sp.name == "TX Chest Animation_0")
                {
                    sr.sprite = sp;
                    break;
                }
            }
            sr.sortingOrder = 2;
        }
        Undo.RecordObject(chest.transform, "Position SummitChest");
        chest.transform.position = new Vector3(303 * 0.32f, (GROUND_Y + 3) * 0.32f + 0.16f, 0f);
        chest.SetActive(true);

        // Configurar meta del cofre (ChestGoal)
        var chestGoal = chest.GetComponent<ChestGoal>();
        if (chestGoal == null) chestGoal = chest.AddComponent<ChestGoal>();
        var allChestSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Pixel Art Platformer - Village Props v2.3.1/Textures/TX Chest Animation.png");
        var frameList = new List<Sprite>();
        for (int i = 0; i <= 6; i++)
        {
            foreach (var a in allChestSprites)
            {
                if (a is Sprite s && s.name == $"TX Chest Animation_{i}")
                {
                    frameList.Add(s);
                    break;
                }
            }
        }
        chestGoal.openFrames = frameList.ToArray();
        var boxCol = chest.GetComponent<BoxCollider2D>();
        if (boxCol == null) boxCol = chest.AddComponent<BoxCollider2D>();
        boxCol.isTrigger = true;
        boxCol.size = new Vector2(1.2f, 1.2f);
        boxCol.offset = new Vector2(0f, 0.4f);

        // Desactivar estructuras de testeo obsoletas
        foreach (string name in new[] { "Test_LowCeilingStructure", "Test_WallJumpCorridor" })
        {
            var testObj = GameObject.Find(name);
            if (testObj != null) testObj.SetActive(false);
        }
    }

    private void PopulateEnemies()
    {
        // Contenedor principal 'Enemies'
        var enemiesParent = GameObject.Find("Enemies");
        if (enemiesParent == null)
        {
            enemiesParent = new GameObject("Enemies");
            Undo.RegisterCreatedObjectUndo(enemiesParent, "Create Enemies Container");
        }

        // Limpiar ghouls previos dentro del contenedor
        List<GameObject> toDestroy = new List<GameObject>();
        for (int i = 0; i < enemiesParent.transform.childCount; i++)
        {
            toDestroy.Add(enemiesParent.transform.GetChild(i).gameObject);
        }
        foreach (var g in toDestroy)
        {
            Undo.DestroyObjectImmediate(g);
        }

        // Eliminar Ghoul suelto en la raíz si existiese
        var looseGhoul = GameObject.Find("Enemy_Ghoul");
        if (looseGhoul != null)
        {
            Undo.DestroyObjectImmediate(looseGhoul);
        }

        var playerObj = GameObject.Find("prota");
        Transform playerTrans = playerObj != null ? playerObj.transform : null;

        // Posiciones estratégicas y dinámicas para los Ghouls a lo largo del nivel
        Vector3[] ghoulPositions = new Vector3[]
        {
            new Vector3(45 * 0.32f,  (GROUND_Y + 1) * 0.32f + 0.20f, 0f), // 1. Sección 2: Terraza básica
            new Vector3(97 * 0.32f,  (GROUND_Y + 17) * 0.32f + 0.20f, 0f), // 2. Sección 3: Guardián de la Cumbre
            new Vector3(154 * 0.32f, (GROUND_Y + 1) * 0.32f + 0.20f, 0f), // 3. Sección 4: Tras la salida del túnel
            new Vector3(197 * 0.32f, (GROUND_Y + 1) * 0.32f + 0.20f, 0f), // 4. Sección 5: Pista intermedia del sprint
            new Vector3(272 * 0.32f, (GROUND_Y + 1) * 0.32f + 0.20f, 0f), // 5. Sección 7: Entrada a la Arena
            new Vector3(280 * 0.32f, (GROUND_Y + 1) * 0.32f + 0.20f, 0f), // 6. Sección 7: Centro de la Arena
            new Vector3(288 * 0.32f, (GROUND_Y + 1) * 0.32f + 0.20f, 0f)  // 7. Sección 7: Retaguardia de la Arena
        };

        for (int i = 0; i < ghoulPositions.Length; i++)
        {
            GameObject ghoul = new GameObject($"Enemy_Ghoul_{i}");
            Undo.RegisterCreatedObjectUndo(ghoul, $"Spawn Ghoul {i}");
            ghoul.transform.SetParent(enemiesParent.transform, false);
            ghoul.transform.position = ghoulPositions[i];
            ghoul.transform.localScale = new Vector3(1.55f, 1.55f, 1f);

            ConfigureGhoul(ghoul, playerTrans);
        }
    }

    private static void ConfigureGhoul(GameObject ghoul, Transform playerTransform)
    {
        ghoul.layer = 0; // Default
        ghoul.tag = "Enemy";

        // SpriteRenderer
        var sr = ghoul.GetComponent<SpriteRenderer>();
        if (sr == null) sr = ghoul.AddComponent<SpriteRenderer>();
        var ghoulSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/burning-ghoul/run 1/sprites/f-01.png");
        if (ghoulSprite != null) sr.sprite = ghoulSprite;
        sr.sortingOrder = 3;

        // Animator con GhoulController
        var anim = ghoul.GetComponent<Animator>();
        if (anim == null) anim = ghoul.AddComponent<Animator>();
        var animCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Sprites/burning-ghoul/Animations/GhoulController.controller");
        if (animCtrl != null)
        {
            anim.runtimeAnimatorController = animCtrl;
        }

        // Rigidbody2D dinámico con amortiguación
        var rb = ghoul.GetComponent<Rigidbody2D>();
        if (rb == null) rb = ghoul.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.mass = 1.0f;
        rb.gravityScale = 2.5f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.linearDamping = 8.0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // BoxCollider2D ajustado
        var col = ghoul.GetComponent<BoxCollider2D>();
        if (col == null) col = ghoul.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.40f, 0.42f);
        col.offset = new Vector2(0f, 0.21f);

        // HitReceiver configurado: 100 HP, 25 de daño por golpe, mortal (isImmortal = false)
        var hr = ghoul.GetComponent<HitReceiver>();
        if (hr == null) hr = ghoul.AddComponent<HitReceiver>();
        hr.maxHealth = 100;
        hr.currentHealth = 100;
        hr.damagePerHit = 25;
        hr.isImmortal = false;
        hr.baseKnockbackForce = 1.8f;
        hr.groundDamping = 8.0f;

        // GhoulAI con protección contra abismos y ataque conectado al prota
        var ai = ghoul.GetComponent<GhoulAI>();
        if (ai == null) ai = ghoul.AddComponent<GhoulAI>();
        ai.targetPlayer = playerTransform;
        ai.moveSpeed = 2.2f;
        ai.detectionRadius = 12.0f;
        ai.stopDistance = 0.65f;
        ai.attackRange = 0.85f;
        ai.attackDamage = 15;
        ai.attackCooldown = 1.6f;
        ai.attackHitDelay = 0.35f;
        ai.ledgeCheckDistanceAhead = 0.40f;
        ai.ledgeCheckDownDistance = 0.65f;
        ai.fallThreshold = -4.5f;
        ai.groundLayer = 1 << 3; // Layer 3 (Floor)
        ai.enabled = true;
    }

    private void SetupVictoryUI()
    {
        var canvas = GameObject.Find("CombatUI_Canvas")?.GetComponent<Canvas>();
        if (canvas == null) return;

        var vicTrans = canvas.transform.Find("Victory_Screen");
        GameObject vicObj;
        if (vicTrans != null)
        {
            vicObj = vicTrans.gameObject;
        }
        else
        {
            vicObj = new GameObject("Victory_Screen");
            vicObj.transform.SetParent(canvas.transform, false);
            var rt = vicObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var cg = vicObj.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;

            var vicUI = vicObj.AddComponent<VictoryUI>();
            vicUI.canvasGroup = cg;

            // Dark Banner
            var bannerObj = new GameObject("DarkBanner");
            bannerObj.transform.SetParent(vicObj.transform, false);
            var bannerRT = bannerObj.AddComponent<RectTransform>();
            bannerRT.anchorMin = new Vector2(0f, 0.35f);
            bannerRT.anchorMax = new Vector2(1f, 0.65f);
            bannerRT.offsetMin = Vector2.zero;
            bannerRT.offsetMax = Vector2.zero;
            var bannerImg = bannerObj.AddComponent<UnityEngine.UI.Image>();
            bannerImg.color = new Color(0.04f, 0.04f, 0.06f, 0.92f);
            vicUI.darkBanner = bannerImg;

            // Font reference
            var fontRef = GameObject.Find("GameOver_Screen")?.GetComponentInChildren<TMPro.TextMeshProUGUI>(true)?.font;

            // Victory Text: ¡GANASTE!
            var textObj = new GameObject("Text_GANASTE");
            textObj.transform.SetParent(bannerObj.transform, false);
            var textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0f, 0.40f);
            textRT.anchorMax = new Vector2(1f, 0.95f);
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            if (fontRef != null) tmp.font = fontRef;
            tmp.text = "¡GANASTE!";
            tmp.fontSize = 72;
            tmp.fontStyle = TMPro.FontStyles.Bold;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = new Color(1.0f, 0.84f, 0.0f, 1.0f); // Dorado brillante
            vicUI.victoryText = tmp;

            // SubText: NIVEL COMPLETADO
            var subObj = new GameObject("Text_SUB");
            subObj.transform.SetParent(bannerObj.transform, false);
            var subRT = subObj.AddComponent<RectTransform>();
            subRT.anchorMin = new Vector2(0f, 0.08f);
            subRT.anchorMax = new Vector2(1f, 0.45f);
            subRT.offsetMin = Vector2.zero;
            subRT.offsetMax = Vector2.zero;
            var subTMP = subObj.AddComponent<TMPro.TextMeshProUGUI>();
            if (fontRef != null) subTMP.font = fontRef;
            subTMP.text = "NIVEL COMPLETADO";
            subTMP.fontSize = 26;
            subTMP.fontStyle = TMPro.FontStyles.Bold;
            subTMP.alignment = TMPro.TextAlignmentOptions.Center;
            subTMP.color = new Color(0.95f, 0.92f, 0.75f, 0.95f);
            vicUI.subText = subTMP;

            // FullBlackFade
            var fadeObj = new GameObject("FullBlackFade");
            fadeObj.transform.SetParent(vicObj.transform, false);
            var fadeRT = fadeObj.AddComponent<RectTransform>();
            fadeRT.anchorMin = Vector2.zero;
            fadeRT.anchorMax = Vector2.one;
            fadeRT.offsetMin = Vector2.zero;
            fadeRT.offsetMax = Vector2.zero;
            var fadeImg = fadeObj.AddComponent<UnityEngine.UI.Image>();
            fadeImg.color = new Color(0f, 0f, 0f, 0f);
            vicUI.fullBlackFade = fadeImg;

            Undo.RegisterCreatedObjectUndo(vicObj, "Create Victory UI");
        }
    }
}
