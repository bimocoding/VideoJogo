using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

/// <summary>
/// Level Generator v4 - Authentic Pixel Art Platformer Level
/// Uses the correct tile mapping from basic.png:
/// - Top grass surface: basic_1_0 (corner L), basic_0_3..5 (flat grass), basic_1_8 (corner R)
/// - Left cliff wall: basic_2_0 / basic_3_0
/// - Right cliff wall: basic_2_8 / basic_3_8
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

        BuildSection1_Meadow();          // 0 - 60
        BuildSection2_Jumps();           // 61 - 104
        BuildSection3_WizardOutpost();   // 105 - 140
        BuildSection4_Climb();           // 141 - 176
        BuildSection5_SlideTunnel();     // 177 - 206
        BuildSection6_SprintChasm();     // 207 - 252
        BuildSection7_Gauntlet();        // 253 - 284
        BuildSection8_BossArena();       // 285 - 322

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
                    if (isLeft && w > 1) tile = "basic_1_0";       // Esquina superior izquierda (basic_0_2 es pendiente)
                    else if (isRight && w > 1) tile = "basic_1_8"; // Esquina superior derecha (basic_0_6 es pendiente)
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
                    tile = (ty % 2 == 0) ? "basic_2_0" : "basic_3_0"; // Pared vertical izquierda (sin hierba)
                }
                else if (isRight && w > 1)
                {
                    tile = (ty % 2 == 0) ? "basic_2_8" : "basic_3_8"; // Pared vertical derecha (sin hierba)
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
    // ------------------------------------------------------------
    // Diseñado con las capacidades reales del personaje (1 tile = 0.32m):
    //  - Salto simple: ~3.5 tiles de alto  -> escalones de 2-3 tiles
    //  - Doble salto:  ~5 tiles de alto     -> rutas opcionales altas
    //  - Hueco caminando: ~6 tiles cómodo; corriendo (Shift): ~11 tiles
    //  - Agarre de bordes: muros de 4-6 tiles se suben colgándose
    //  - Deslizamiento (S): túneles de 2 tiles de alto
    // Cada sección enseña una mecánica en seguro y luego la pone a prueba.
    // ============================================================

    // Suelo macizo desde el fondo hasta topY (incluido), entre x1 y x2 (incluidos)
    private void Ground(int x1, int x2, int topY)
    {
        BuildTerrain(x1, FILL_BOTTOM, x2 - x1 + 1, topY - FILL_BOTTOM + 1);
    }

    // 1. Prado inicial: moverse, primer escalón, primer hueco pequeño y primer enemigo
    private void BuildSection1_Meadow()
    {
        Ground(0, 38, GROUND_Y);
        BuildTerrain(14, GROUND_Y + 1, 4, 2);       // escalón de 2: enseña a saltar
        BuildPlatform(26, GROUND_Y + 3, 4);         // plataforma opcional
        // Hueco de 3 tiles (39-41): imposible fallar
        Ground(42, 60, GROUND_Y);
        BuildPlatform(47, GROUND_Y + 3, 4);

        BuildBgBlock(6, GROUND_Y + 1, 5, 8);
        BuildBgBlock(30, GROUND_Y + 1, 4, 10);
        BuildBgBlock(52, GROUND_Y + 1, 5, 9);
    }

    // 2. Saltos crecientes: huecos de 4, 5 y 6 tiles con cambios de altura
    private void BuildSection2_Jumps()
    {
        // Hueco 61-64 (4)
        Ground(65, 72, GROUND_Y + 1);               // terraza +1
        // Hueco 73-77 (5) subiendo +2
        Ground(78, 84, GROUND_Y + 3);
        BuildPlatform(80, GROUND_Y + 8, 3);         // recompensa: solo con doble salto
        // Hueco 85-90 (6) bajando: más largo pero más fácil por la caída
        Ground(91, 145, GROUND_Y);              // suelo continuo hasta las colinas (evita costuras entre secciones)

        BuildBgBlock(67, GROUND_Y + 2, 4, 10);
        BuildBgBlock(95, GROUND_Y + 1, 6, 12);
    }

    // 3. Puesto del mago: aprender a cubrirse tras muros y acercarse por debajo
    private void BuildSection3_WizardOutpost()
    {
        BuildTerrain(112, GROUND_Y + 1, 2, 2);      // cobertura 1
        BuildTerrain(118, GROUND_Y + 1, 2, 2);      // cobertura 2
        BuildTerrain(123, GROUND_Y + 1, 3, 2);      // escalón hacia la torre
        BuildTerrain(126, GROUND_Y + 1, 6, 4);      // torre del mago (top +4)
        BuildPlatform(134, GROUND_Y + 3, 3);        // bajada

        BuildBgBlock(108, GROUND_Y + 1, 4, 11);
        BuildBgBlock(127, GROUND_Y + 5, 4, 9);
    }

    // 4. Ascenso por bloques: escalones de 3 tiles con huecos de 2, ideales para agarrarse al borde
    private void BuildSection4_Climb()
    {
        // Pilares macizos con abismos entre ellos: si fallas el salto caes al vacío y reapareces, en vez de quedar atrapado
        Ground(146, 149, GROUND_Y + 3);
        Ground(152, 155, GROUND_Y + 6);
        Ground(158, 161, GROUND_Y + 9);
        Ground(164, 170, GROUND_Y + 12);            // cumbre (mago vigía)
        Ground(171, 206, GROUND_Y);                 // bajada y suelo hasta el túnel
        BuildPlatform(172, GROUND_Y + 8, 3);        // escalera de bajada
        BuildPlatform(175, GROUND_Y + 4, 3);

        BuildBgBlock(150, GROUND_Y + 1, 3, 14);
        BuildBgBlock(162, GROUND_Y + 1, 3, 18);
    }

    // 5. Túnel de deslizamiento: techo a 2 tiles, con un ghoul esperando a la salida
    private void BuildSection5_SlideTunnel()
    {
        BuildTerrain(181, GROUND_Y + 3, 10, 5);     // techo bajo (hueco libre de 2 tiles)
        BuildPlatform(181, GROUND_Y + 10, 10);      // ruta alta alternativa (doble salto desde el techo)

        BuildBgBlock(180, GROUND_Y + 1, 12, 9);
    }

    // 6. Grietas de carrera: 10 tiles (corriendo) y 13 tiles con isla de apoyo
    private void BuildSection6_SprintChasm()
    {
        // Hueco 207-216 (10): hay que correr con Shift
        Ground(217, 226, GROUND_Y);
        // Hueco 227-239 (13) con isla en el medio
        BuildPlatform(232, GROUND_Y + 1, 3);
        Ground(240, 322, GROUND_Y);             // suelo continuo hasta el final del nivel

        BuildBgBlock(210, GROUND_Y - 5, 5, 12);
        BuildBgBlock(229, GROUND_Y - 5, 5, 13);
    }

    // 7. Prueba combinada: plataformas a distintas alturas con ghouls y un mago arriba
    private void BuildSection7_Gauntlet()
    {
        BuildPlatform(257, GROUND_Y + 3, 5);
        BuildTerrain(265, GROUND_Y + 1, 6, 3);      // bloque top +3
        BuildPlatform(273, GROUND_Y + 6, 4);        // nido del mago
        BuildPlatform(279, GROUND_Y + 3, 3);

        BuildBgBlock(262, GROUND_Y + 1, 5, 12);
        BuildBgBlock(276, GROUND_Y + 1, 4, 14);
    }

    // 8. Arena del jefe: suelo plano, dos plataformas para esquivar picadas y muro final
    private void BuildSection8_BossArena()
    {
        BuildTerrain(285, GROUND_Y + 7, 2, 10);     // arco de entrada (se pasa por debajo)
        BuildPlatform(293, GROUND_Y + 3, 4);
        BuildPlatform(306, GROUND_Y + 3, 4);
        BuildTerrain(321, GROUND_Y + 1, 2, 14);     // muro final del nivel

        BuildBgBlock(288, GROUND_Y + 1, 32, 16);
    }

    public const int BOSS_ARENA_MIN_X = 289;
    public const int BOSS_ARENA_MAX_X = 318;
    public const int CHEST_X = 316;

    private void BuildDeepBackground()
    {
        if (bgTilemap == null) return;
        // Rellenar toda la zona subterránea profunda con el tono de roca oscura de fondo
        for (int x = -4; x <= 324; x++)
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

            // Animación de muerte: fotogramas 0-17 de "hard hit" (cae y queda tendido)
            var protaScript = player.GetComponent<NewMonoBehaviourScript>();
            if (protaScript != null)
            {
                var frames = new List<Sprite>();
                var all = AssetDatabase.LoadAllAssetsAtPath("Assets/Characters/Prota/hurt/sprite sheets/hard hit.png");
                for (int i = 0; i <= 17; i++)
                    foreach (var a in all)
                        if (a is Sprite s && s.name == $"hard hit_{i}") { frames.Add(s); break; }
                Undo.RecordObject(protaScript, "Assign Death Frames");
                protaScript.deathFrames = frames.ToArray();
                EditorUtility.SetDirty(protaScript);
            }
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
        chest.transform.position = new Vector3(CHEST_X * 0.32f, (GROUND_Y + 1) * 0.32f, 0f);
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

        Sprite[] deathFx = LoadFrames("Assets/Sprites/fx/enemy-death/enemy-death-sprites", "enemy-death");

        // Ghouls: (tile X, altura de la superficie donde se apoyan, rango de patrulla en metros)
        var ghouls = new (int x, int top, float patrol)[]
        {
            (52,  GROUND_Y,      2.0f),  // 1. Prado: primer combate en terreno seguro
            (98,  GROUND_Y,      2.5f),  // 2. Tras los saltos
            (121, GROUND_Y,      1.2f),  // 3. Entre las coberturas del mago
            (198, GROUND_Y,      1.5f),  // 4. Esperando a la salida del túnel
            (221, GROUND_Y,      1.2f),  // 5. Pista entre grietas
            (260, GROUND_Y + 3,  0.6f),  // 6. Prueba: sobre la plataforma
            (268, GROUND_Y + 3,  0.7f),  // 7. Prueba: sobre el bloque
            (281, GROUND_Y,      1.5f),  // 8. Prueba: antes de la arena
        };
        for (int i = 0; i < ghouls.Length; i++)
        {
            GameObject ghoul = new GameObject($"Enemy_Ghoul_{i}");
            Undo.RegisterCreatedObjectUndo(ghoul, $"Spawn Ghoul {i}");
            ghoul.transform.SetParent(enemiesParent.transform, false);
            ghoul.transform.position = new Vector3(ghouls[i].x * 0.32f, (ghouls[i].top + 1) * 0.32f + 0.02f, 0f);
            ghoul.transform.localScale = new Vector3(1.55f, 1.55f, 1f);

            ConfigureGhoul(ghoul, playerTrans);
            ghoul.GetComponent<GhoulAI>().patrolRange = ghouls[i].patrol;
            ghoul.GetComponent<HitReceiver>().deathFxFrames = deathFx;
        }

        // Magos: disparan desde posiciones elevadas
        var wizards = new (int x, int top)[]
        {
            (129, GROUND_Y + 4),   // torre del puesto del mago
            (167, GROUND_Y + 12),  // vigía de la cumbre
            (275, GROUND_Y + 6),   // nido de la prueba final
        };
        for (int i = 0; i < wizards.Length; i++)
        {
            var w = CreateWizard($"Enemy_Wizard_{i}", wizards[i].x, wizards[i].top, playerTrans, deathFx);
            w.transform.SetParent(enemiesParent.transform, true);
        }

        // Jefe final
        var boss = CreateAngelBoss(playerTrans, deathFx);
        boss.transform.SetParent(enemiesParent.transform, true);
    }

    private static Sprite[] LoadFrames(string folder, string prefix = "f-")
    {
        var list = new List<(string name, Sprite sprite)>();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!file.StartsWith(prefix)) continue;
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (a is Sprite s) { list.Add((file, s)); break; }
            }
        }
        // Orden natural: f-2 antes que f-10
        list.Sort((a, b) => ExtractNumber(a.name).CompareTo(ExtractNumber(b.name)));
        var result = new Sprite[list.Count];
        for (int i = 0; i < list.Count; i++) result[i] = list[i].sprite;
        return result;
    }

    private static int ExtractNumber(string s)
    {
        string digits = "";
        foreach (char c in s) if (char.IsDigit(c)) digits += c;
        return digits.Length > 0 ? int.Parse(digits) : 0;
    }

    // Coloca el objeto para que la parte de abajo del sprite toque la superficie (los sprites tienen pivote centrado)
    private static void PlaceOnSurface(GameObject go, Sprite sprite, float tileX, int surfaceTop, float scale)
    {
        float floorY = (surfaceTop + 1) * 0.32f;
        float bottom = sprite != null ? (sprite.bounds.min.y * scale) : 0f;
        go.transform.position = new Vector3(tileX * 0.32f, floorY - bottom + 0.02f, 0f);
    }

    private static void FitCollider(BoxCollider2D col, Sprite sprite, Vector2 fraction)
    {
        if (sprite == null) return;
        var b = sprite.bounds;
        col.size = new Vector2(b.size.x * fraction.x, b.size.y * fraction.y);
        col.offset = new Vector2(b.center.x, b.min.y + col.size.y * 0.5f);
    }

    private GameObject CreateWizard(string name, int tileX, int surfaceTop, Transform player, Sprite[] deathFx)
    {
        var idle = LoadFrames("Assets/Sprites/wizard/Idle/sprites");
        var cast = LoadFrames("Assets/Sprites/wizard/Fire/sprites");
        var fireball = LoadFrames("Assets/Sprites/fx/fireball/fireball-sprites", "fireball");

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Spawn {name}");
        go.tag = "Enemy";
        const float scale = 1.1f;
        go.transform.localScale = new Vector3(scale, scale, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = idle.Length > 0 ? idle[0] : null;
        sr.sortingOrder = 3;
        PlaceOnSurface(go, sr.sprite, tileX, surfaceTop, scale);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 2.5f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        var col = go.AddComponent<BoxCollider2D>();
        FitCollider(col, sr.sprite, new Vector2(0.45f, 0.85f));

        var hr = go.AddComponent<HitReceiver>();
        hr.maxHealth = 50;
        hr.currentHealth = 50;
        hr.damagePerHit = 25;
        hr.baseKnockbackForce = 3.0f;

        go.AddComponent<SpriteFrameAnimator>();
        var ai = go.AddComponent<WizardAI>();
        ai.targetPlayer = player;
        ai.idleFrames = idle;
        ai.castFrames = cast;
        ai.fireballFrames = fireball;
        ai.groundLayer = 1 << 3;
        return go;
    }

    private GameObject CreateAngelBoss(Transform player, Sprite[] deathFx)
    {
        var idle = LoadFrames("Assets/Sprites/angel/idle/sprites");
        var attack = LoadFrames("Assets/Sprites/angel/attack/sprites");
        var fireball = LoadFrames("Assets/Sprites/fx/fireball/fireball-sprites", "fireball");

        var go = new GameObject("Boss_Angel");
        Undo.RegisterCreatedObjectUndo(go, "Spawn Boss");
        go.tag = "Enemy";
        go.transform.localScale = Vector3.one;
        float floorY = (GROUND_Y + 1) * 0.32f;
        go.transform.position = new Vector3((BOSS_ARENA_MIN_X + BOSS_ARENA_MAX_X) * 0.5f * 0.32f, floorY + 2.6f, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = idle.Length > 0 ? idle[0] : null;
        sr.sortingOrder = 4;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true; // no empuja al jugador; recibe golpes por OverlapBox
        if (sr.sprite != null)
        {
            col.size = new Vector2(sr.sprite.bounds.size.x * 0.35f, sr.sprite.bounds.size.y * 0.7f);
            col.offset = sr.sprite.bounds.center;
        }

        var hr = go.AddComponent<HitReceiver>();
        hr.isBoss = true;
        hr.maxHealth = 400;
        hr.currentHealth = 400;
        hr.damagePerHit = 25;

        go.AddComponent<SpriteFrameAnimator>();
        var ai = go.AddComponent<AngelBossAI>();
        ai.targetPlayer = player;
        ai.idleFrames = idle;
        ai.attackFrames = attack;
        ai.fireballFrames = fireball;
        ai.arenaMinX = BOSS_ARENA_MIN_X * 0.32f;
        ai.arenaMaxX = BOSS_ARENA_MAX_X * 0.32f;
        ai.arenaFloorY = floorY;
        // Cámara fija: toda la arena (desde el arco hasta el muro final) con el suelo cerca del borde inferior
        float arenaLeft = 285 * 0.32f, arenaRight = 323 * 0.32f;
        float arenaWidth = arenaRight - arenaLeft;
        ai.cameraArea = new Rect(arenaLeft, floorY - 0.6f, arenaWidth, arenaWidth / (16f / 9f));
        return go;
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
        hr.baseKnockbackForce = 4.5f;
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
        ai.attackCooldown = 1.0f;
        ai.attackHitDelay = 0.2f;
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
