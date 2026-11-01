using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Builds Assets/Scenes/Level_01.unity (a copy of the PlayerTest setup) from code: top-down tilemaps for five rooms
// joined by corridors with doors, room controllers with their waves, the boss room, props and the exit.
// Run the tileset generator first (Tools/Level/Generate TopDown Tileset). Rerunning rebuilds the level objects.
public static class LevelBuilder
{
    const string ScenePath = "Assets/Scenes/Level_01.unity";
    const string TopDownFolder = "Assets/Art/Environment/TopDown";
    const string TileFolder = TopDownFolder + "/Tiles";
    const string BossStatsPath = "Assets/Scripts/Enemies/Data/Orc3BossStats.asset";
    const string BossPrefabPath = "Assets/Prefabs/Enemies/Orc3Boss.prefab";
    const int WallLayer = 11;
    const int WallThickness = 2;

    // Sheet indices (row 0 normal theme, row 1 boss theme; see TopDownTilesetGenerator).
    const int FloorA = 0, WallTopTile = 3, WallFrontTile = 4, DoorClosedTile = 5, DoorOpenTile = 6, ExitTile = 7, BossRow = 8;

    [MenuItem("Tools/Level/Build Level_01")]
    public static void BuildLevel01()
    {
        CreateTileAssets();
        EnsureBoss();

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (string stale in new[] { "Room", "Orc1", "Orc2", "Orc3", "EnemySpawner", "Level", "Grid", "LevelExit", "ScoreScreen" })
        {
            GameObject old = GameObject.Find(stale);
            if (old != null)
            {
                Object.DestroyImmediate(old);
            }
        }

        var orc1 = Load<GameObject>("Assets/Prefabs/Enemies/Orc1.prefab");
        var orc2 = Load<GameObject>("Assets/Prefabs/Enemies/Orc2.prefab");
        var orc3 = Load<GameObject>("Assets/Prefabs/Enemies/Orc3.prefab");
        var boss = Load<GameObject>(BossPrefabPath);
        var heart = Load<GameObject>("Assets/Prefabs/Combat/HeartPickup.prefab");

        // Floor plan in tiles (1 tile = 1 world unit). Floors are x/y/w/h; corridors link them.
        var rest = new RectInt(-6, -4, 12, 8);
        var r1 = new RectInt(10, -6, 18, 12);
        var r2 = new RectInt(12, 10, 14, 10);
        var r3 = new RectInt(-14, 8, 22, 14);
        var bossRect = new RectInt(-14, 26, 20, 16);
        RectInt[] corridors =
        {
            new(6, -1, 4, 2),
            new(18, 6, 2, 4),
            new(8, 14, 4, 2),
            new(-4, 22, 2, 4),
        };

        var floorCells = new HashSet<Vector2Int>();
        foreach (RectInt r in new[] { rest, r1, r2, r3, bossRect })
        {
            AddRect(floorCells, r);
        }

        foreach (RectInt c in corridors)
        {
            AddRect(floorCells, c);
        }

        var level = new GameObject("Level");
        var grid = new GameObject("Grid").AddComponent<Grid>();
        Tilemap floorMap = NewTilemap("Floor", grid.transform, -100, false);
        Tilemap wallMap = NewTilemap("Walls", grid.transform, -90, true);
        PaintAll(floorCells, floorMap, wallMap);

        // Doors: closed they block a two-tile corridor. Index per room: [entrance, exit].
        Door d01 = NewDoor(level.transform, "Door_R1_Entrance", new Vector2(9.5f, 0f), new Vector2(1f, 2f));
        Door d1Exit = NewDoor(level.transform, "Door_R1_Exit", new Vector2(19f, 6.5f), new Vector2(2f, 1f));
        Door d2In = NewDoor(level.transform, "Door_R2_Entrance", new Vector2(19f, 9.5f), new Vector2(2f, 1f));
        Door d2Exit = NewDoor(level.transform, "Door_R2_Exit", new Vector2(11.5f, 15f), new Vector2(1f, 2f));
        Door d3In = NewDoor(level.transform, "Door_R3_Entrance", new Vector2(8.5f, 15f), new Vector2(1f, 2f));
        Door d3Exit = NewDoor(level.transform, "Door_R3_Exit", new Vector2(-3f, 22.5f), new Vector2(2f, 1f), false);
        Door dBossIn = NewDoor(level.transform, "Door_Boss_Entrance", new Vector2(-3f, 25.5f), new Vector2(2f, 1f), true);

        RoomController restRoom = NewRoom(level.transform, "Room_0_Rest", rest, null, new Door[0], null, null);
        RoomController room1 = NewRoom(level.transform, "Room_1_Combat", r1,
            new[] { new[] { (orc1, 4), (orc2, 1) }, new[] { (orc1, 5), (orc2, 2) } }, new[] { d01, d1Exit }, heart, null);
        RoomController room2 = NewRoom(level.transform, "Room_2_Combat", r2,
            new[] { new[] { (orc1, 5), (orc2, 1) }, new[] { (orc1, 5), (orc2, 2) } }, new[] { d2In, d2Exit }, heart, null);
        RoomController room3 = NewRoom(level.transform, "Room_3_Combat", r3,
            new[] { new[] { (orc1, 5) }, new[] { (orc1, 4), (orc2, 2) }, new[] { (orc1, 3), (orc2, 2), (orc3, 1) } }, new[] { d3In, d3Exit }, heart, null);
        RoomController bossRoom = NewRoom(level.transform, "Room_4_Boss", bossRect,
            new[] { new[] { (orc1, 6), (orc2, 2) }, new[] { (boss, 1), (orc2, 4) } }, new[] { dBossIn }, null, null);

        int total = room1.TotalEnemies + room2.TotalEnemies + room3.TotalEnemies + bossRoom.TotalEnemies;

        // Exit pad in the boss room, revealed by LevelManager when the room is cleared.
        var exit = new GameObject("LevelExit");
        exit.transform.SetParent(level.transform);
        exit.transform.position = new Vector3(-4f, 36f, 0f);
        var exitCol = exit.AddComponent<BoxCollider2D>();
        exitCol.isTrigger = true;
        exitCol.size = new Vector2(2f, 2f);
        var exitTrigger = exit.AddComponent<LevelCompleteTrigger>();
        var padObj = new GameObject("Pad");
        padObj.transform.SetParent(exit.transform, false);
        padObj.transform.localScale = new Vector3(2f, 2f, 1f);
        var padRenderer = padObj.AddComponent<SpriteRenderer>();
        padRenderer.sprite = SpriteAt(ExitTile);
        padRenderer.sortingOrder = -80;
        var screenGo = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Assets/Prefabs/UI/ScoreScreen.prefab"));
        var hudTimer = Object.FindFirstObjectByType<LevelTimer>();
        SetProps(exitTrigger, so =>
        {
            so.FindProperty("levelIndex").intValue = 0;
            so.FindProperty("totalCollectibles").intValue = total;
            so.FindProperty("parTime").floatValue = 300f;
            so.FindProperty("scoreScreen").objectReferenceValue = screenGo.GetComponent<ScoreScreen>();
            so.FindProperty("timer").objectReferenceValue = hudTimer;
        });

        var managerGo = new GameObject("LevelManager");
        managerGo.transform.SetParent(level.transform);
        var manager = managerGo.AddComponent<LevelManager>();
        SetProps(manager, so =>
        {
            SetObjectArray(so.FindProperty("rooms"), new Object[] { restRoom, room1, room2, room3, bossRoom });
            so.FindProperty("bossRoom").objectReferenceValue = bossRoom;
            so.FindProperty("exit").objectReferenceValue = exitTrigger;
        });

        PlaceProps(level.transform);

        Camera cam = Camera.main;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.035f, 0.07f);
        if (cam.GetComponent<RoomCameraBounds>() == null)
        {
            cam.gameObject.AddComponent<RoomCameraBounds>();
        }

        var player = Object.FindFirstObjectByType<PlayerController2D>();
        player.transform.position = Vector3.zero;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EnsureBuildSettings();
        Debug.Log($"LevelBuilder: Level_01 built, {total} enemies in total.");
    }

    // Paints the selected RoomController's floor rect (plus walls where no floor exists) into the open scene's
    // Floor / Walls tilemaps; for adding or resizing rooms after the initial build.
    [MenuItem("Tools/Level/Build Room")]
    public static void BuildSelectedRoom()
    {
        var room = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<RoomController>() : null;
        var floorMap = GameObject.Find("Floor")?.GetComponent<Tilemap>();
        var wallMap = GameObject.Find("Walls")?.GetComponent<Tilemap>();
        if (room == null || floorMap == null || wallMap == null)
        {
            Debug.LogWarning("LevelBuilder: select a RoomController in a scene with Floor and Walls tilemaps.");
            return;
        }

        Rect r = room.FloorRect;
        var cells = new HashSet<Vector2Int>();
        AddRect(cells, new RectInt(Mathf.RoundToInt(r.x), Mathf.RoundToInt(r.y), Mathf.RoundToInt(r.width), Mathf.RoundToInt(r.height)));
        foreach (Vector3Int p in floorMap.cellBounds.allPositionsWithin)
        {
            if (floorMap.HasTile(p))
            {
                cells.Add(new Vector2Int(p.x, p.y));
            }
        }

        PaintAll(cells, floorMap, wallMap);
        Undo.RecordObject(floorMap, "Build Room");
    }

    // ---- tilemaps ----

    static void AddRect(HashSet<Vector2Int> cells, RectInt r)
    {
        for (int x = r.xMin; x < r.xMax; x++)
        {
            for (int y = r.yMin; y < r.yMax; y++)
            {
                cells.Add(new Vector2Int(x, y));
            }
        }
    }

    static Tilemap NewTilemap(string name, Transform parent, int order, bool solid)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var map = go.AddComponent<Tilemap>();
        var renderer = go.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = order;
        if (solid)
        {
            go.layer = WallLayer;
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var tileCollider = go.AddComponent<TilemapCollider2D>();
            tileCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
            var composite = go.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        }

        return map;
    }

    static void PaintAll(HashSet<Vector2Int> floorCells, Tilemap floorMap, Tilemap wallMap)
    {
        foreach (Vector2Int c in floorCells)
        {
            floorMap.SetTile(new Vector3Int(c.x, c.y, 0), TileAt(FloorTileIndex(c)));
        }

        var walls = new HashSet<Vector2Int>();
        foreach (Vector2Int c in floorCells)
        {
            for (int dx = -WallThickness; dx <= WallThickness; dx++)
            {
                for (int dy = -WallThickness; dy <= WallThickness; dy++)
                {
                    var n = new Vector2Int(c.x + dx, c.y + dy);
                    if (!floorCells.Contains(n))
                    {
                        walls.Add(n);
                    }
                }
            }
        }

        foreach (Vector2Int w in walls)
        {
            int index = floorCells.Contains(new Vector2Int(w.x, w.y - 1)) ? WallFrontTile : WallTopTile;
            wallMap.SetTile(new Vector3Int(w.x, w.y, 0), TileAt(index + ThemeOffset(w)));
        }
    }

    // The boss room and its corridor use the red tint row.
    static int ThemeOffset(Vector2Int c) => c.y >= 22 ? BossRow : 0;

    static int FloorTileIndex(Vector2Int c)
    {
        int h = (c.x * 7349 + c.y * 3571) & 0xff;
        int variant = h < 150 ? 0 : h < 205 ? 1 : 2;
        return FloorA + variant + ThemeOffset(c);
    }

    // ---- rooms and doors ----

    static Door NewDoor(Transform parent, string name, Vector2 center, Vector2 size, bool bossTheme = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = center;
        go.layer = WallLayer;
        var box = go.AddComponent<BoxCollider2D>();
        box.size = size;
        int theme = center.y >= 22f ? BossRow : 0;
        var renderers = new List<SpriteRenderer>();
        int cells = Mathf.RoundToInt(Mathf.Max(size.x, size.y));
        for (int i = 0; i < cells; i++)
        {
            var child = new GameObject("Tile" + i);
            child.transform.SetParent(go.transform, false);
            float offset = i - (cells - 1) * 0.5f;
            child.transform.localPosition = size.x > size.y ? new Vector3(offset, 0f, 0f) : new Vector3(0f, offset, 0f);
            var sr = child.AddComponent<SpriteRenderer>();
            sr.sortingOrder = -80;
            sr.sprite = SpriteAt(DoorOpenTile + theme);
            renderers.Add(sr);
        }

        var door = go.AddComponent<Door>();
        SetProps(door, so =>
        {
            SetObjectArray(so.FindProperty("renderers"), renderers.ToArray());
            so.FindProperty("closedSprite").objectReferenceValue = SpriteAt(DoorClosedTile + theme);
            so.FindProperty("openSprite").objectReferenceValue = SpriteAt(DoorOpenTile + theme);
        });
        // Doors begin open (box collider off) at runtime; Awake applies the state.
        return door;
    }

    static RoomController NewRoom(Transform parent, string name, RectInt floor, (GameObject prefab, int count)[][] waves, Door[] doors, GameObject reward, object unused)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = new Vector3(floor.center.x, floor.center.y, 0f);
        var trigger = go.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(floor.width - 2f, floor.height - 2f);
        var room = go.AddComponent<RoomController>();
        SetProps(room, so =>
        {
            so.FindProperty("floorSize").vector2Value = new Vector2(floor.width, floor.height);
            SetObjectArray(so.FindProperty("doors"), doors);
            so.FindProperty("clearReward").objectReferenceValue = reward;
            SerializedProperty waveArray = so.FindProperty("waves");
            int waveCount = waves?.Length ?? 0;
            waveArray.arraySize = waveCount;
            for (int w = 0; w < waveCount; w++)
            {
                SerializedProperty wave = waveArray.GetArrayElementAtIndex(w);
                wave.FindPropertyRelative("spawnInterval").floatValue = 0.2f;
                SerializedProperty entries = wave.FindPropertyRelative("entries");
                entries.arraySize = waves[w].Length;
                for (int e = 0; e < waves[w].Length; e++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(e);
                    entry.FindPropertyRelative("prefab").objectReferenceValue = waves[w][e].prefab;
                    entry.FindPropertyRelative("count").intValue = waves[w][e].count;
                }
            }
        });
        return room;
    }

    // ---- props ----

    static void PlaceProps(Transform parent)
    {
        var root = new GameObject("Props").transform;
        root.SetParent(parent);
        (string prefab, float x, float y)[] props =
        {
            ("Cave_Bonefire", -4f, 1.5f), ("Cave_Crystal_Blue", 4.5f, 2.5f), ("Cave_Crystal_Blue", -4.5f, -2.5f), ("Rock_Medium", 4.5f, -2.5f),
            ("Tree_Moss", 4.5f, 3.5f),
            ("Cave_Rock", 12.5f, 4f), ("Cave_Rock", 25.5f, -3.5f), ("Cave_Crystal_Blue", 12.5f, -3f), ("Cave_Crystal_White", 25.5f, 4.5f), ("Rock_Large", 19f, -4f),
            ("Cave_Crystal_White", 14.5f, 12.5f), ("Cave_Crystal_Blue", 23.5f, 17.5f), ("Rock_Medium", 23.5f, 12.5f), ("Rock_Boulder", 14.5f, 17.5f),
            ("Cave_Crystal_Blue", -11.5f, 10.5f), ("Cave_Crystal_Blue", -11.5f, 19.5f), ("Cave_Rock", 5f, 10.5f), ("Cave_Rock", 5f, 19.5f), ("Rock_Large", -3f, 9.5f),
            ("Cave_Crystal_White", -11.5f, 28.5f), ("Cave_Crystal_White", 3.5f, 28.5f), ("Cave_Crystal_White", -11.5f, 39.5f), ("Cave_Crystal_White", 3.5f, 39.5f),
        };
        foreach (var (prefab, x, y) in props)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>($"Assets/Prefabs/Environment/Props/{prefab}.prefab"), root);
            go.transform.position = new Vector3(x, y, 0f);
            foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>())
            {
                sr.sortingOrder = 1 - Mathf.RoundToInt(y);
            }
        }
    }

    // ---- assets ----

    static void CreateTileAssets()
    {
        if (!AssetDatabase.IsValidFolder(TileFolder))
        {
            AssetDatabase.CreateFolder(TopDownFolder, "Tiles");
        }

        for (int i = 0; i < 16; i++)
        {
            string path = $"{TileFolder}/Tile_TD_{i}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }

            tile.sprite = SpriteAt(i);
            // Grid collision for every tile; only the Walls tilemap has a collider, the Floor map ignores it.
            tile.colliderType = Tile.ColliderType.Grid;
            EditorUtility.SetDirty(tile);
        }

        AssetDatabase.SaveAssets();
    }

    static void EnsureBoss()
    {
        var stats = AssetDatabase.LoadAssetAtPath<EnemyStats>(BossStatsPath);
        if (stats == null)
        {
            stats = ScriptableObject.CreateInstance<EnemyStats>();
            AssetDatabase.CreateAsset(stats, BossStatsPath);
        }

        stats.maxHealth = 300;
        stats.skillPointReward = 15;
        stats.patrolSpeed = 0.6f;
        stats.chaseSpeed = 2.2f;
        stats.sightRange = 30f;
        stats.loseSightFactor = 2f;
        stats.attackRange = 2.2f;
        stats.attackCooldown = 1.6f;
        stats.damage = 3;
        stats.knockbackSpeed = 12f;
        stats.telegraphTime = 0.7f;
        stats.hitDelay = 0.4f;
        stats.attackDuration = 0.65f;
        stats.hitRadius = 2.0f;
        stats.recoveryTime = 0.9f;
        stats.hurtStun = 0.05f;
        stats.hurtKnockbackSpeed = 1f;
        stats.staggerResistance = 0.95f;
        stats.deathDuration = 1.2f;
        EditorUtility.SetDirty(stats);

        if (AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath) == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Assets/Prefabs/Enemies/Orc3.prefab"));
            instance.name = "Orc3Boss";
            instance.transform.localScale = new Vector3(1.8f, 1.8f, 1f);
            instance.GetComponent<SpriteRenderer>().color = new Color(1f, 0.65f, 0.65f);
            PrefabUtility.SaveAsPrefabAsset(instance, BossPrefabPath);
            Object.DestroyImmediate(instance);
        }

        var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
        var ai = bossPrefab.GetComponent<EnemyAI>();
        var so = new SerializedObject(ai);
        so.FindProperty("stats").objectReferenceValue = stats;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(bossPrefab);
        AssetDatabase.SaveAssets();
    }

    static void EnsureBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == ScenePath))
        {
            return;
        }

        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ---- helpers ----

    static T Load<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            throw new System.Exception("LevelBuilder: missing asset " + path);
        }

        return asset;
    }

    static Sprite SpriteAt(int index)
    {
        return Load<Sprite>($"{TopDownFolder}/tileset_topdown.png", $"tileset_topdown_{index}");
    }

    static T Load<T>(string path, string subName) where T : Object
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (o is T t && o.name == subName)
            {
                return t;
            }
        }

        throw new System.Exception($"LevelBuilder: missing {subName} in {path}");
    }

    static Tile TileAt(int index) => Load<Tile>($"{TileFolder}/Tile_TD_{index}.asset");

    static void SetProps(Object target, System.Action<SerializedObject> apply)
    {
        var so = new SerializedObject(target);
        apply(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetObjectArray(SerializedProperty prop, Object[] values)
    {
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
