using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

// Dungeon art pass for Level_01: paints the Floor / Walls tilemaps with the Blender-made tileset
// (tileset_topdown.png, rows 0-1 normal theme, rows 2-3 boss tint) and scatters the prop sprites
// (tileset_props.png) as prefabs. Used by LevelBuilder and as a standalone "Dress Level_01" pass.
public static class DungeonDressing
{
    public const string TopDownFolder = "Assets/Art/Environment/TopDown";
    const string TileSheet = TopDownFolder + "/tileset_topdown.png";
    const string PropSheet = TopDownFolder + "/tileset_props.png";
    const string TileFolder = TopDownFolder + "/Tiles";
    const string PropPrefabFolder = "Assets/Prefabs/Environment/Props";
    public const int TileCount = 32;
    public const int WallThickness = 2;

    // Tile sheet indices (row 0: basics, row 1: edge variants; the boss theme repeats both rows at +BossRow).
    public const int FloorA = 0, FloorCracked = 1, FloorFlag = 2, WallTop = 3, WallFront = 4, DoorClosed = 5, DoorOpen = 6, Exit = 7;
    public const int WallTopLeft = 8, WallTopRight = 9, WallTopBack = 10, WallTopCorner = 11, WallFrontLeft = 12, WallFrontRight = 13;
    public const int FloorShadow = 14, FloorGrate = 15, BossRow = 16;

    // Prop sheet indices.
    const int PTorch = 0, PChest = 1, PBarrel = 2, PCrate = 3, PBones = 4, PSkull = 5, PWeb = 6, PCrack = 7,
        PBrokenColumn = 8, PCandles = 9, PRubble = 10, PPillar = 11, PSack = 12, PBrazier = 13;

    const int PropOrder = 10;      // same as the player and enemies; the project sorts by Y, so the pivot (feet) decides
    const int WallDecorOrder = -85; // above floor (-100) and walls (-90), below everything that moves
    const int FloorDecalOrder = -95;

    static readonly (string name, int index, Vector2 colliderSize, Vector2 colliderOffset, bool circle, int order)[] PropDefs =
    {
        ("Torch", PTorch, Vector2.zero, Vector2.zero, false, WallDecorOrder),
        ("Chest", PChest, new Vector2(0.62f, 0.3f), new Vector2(0f, 0.17f), false, PropOrder),
        ("Barrel", PBarrel, new Vector2(0.4f, 0f), new Vector2(0f, 0.2f), true, PropOrder),
        ("Crate", PCrate, new Vector2(0.52f, 0.3f), new Vector2(0f, 0.17f), false, PropOrder),
        ("BonePile", PBones, Vector2.zero, Vector2.zero, false, PropOrder),
        ("Skull", PSkull, Vector2.zero, Vector2.zero, false, PropOrder),
        ("Cobweb", PWeb, Vector2.zero, Vector2.zero, false, FloorDecalOrder),
        ("CrackDecal", PCrack, Vector2.zero, Vector2.zero, false, FloorDecalOrder),
        ("BrokenColumn", PBrokenColumn, new Vector2(0.5f, 0.3f), new Vector2(0f, 0.17f), false, PropOrder),
        ("Candles", PCandles, Vector2.zero, Vector2.zero, false, PropOrder),
        ("Rubble", PRubble, Vector2.zero, Vector2.zero, false, PropOrder),
        ("Pillar", PPillar, new Vector2(0.48f, 0.3f), new Vector2(0f, 0.17f), false, PropOrder),
        ("Sack", PSack, new Vector2(0.3f, 0f), new Vector2(0f, 0.15f), true, PropOrder),
        ("Brazier", PBrazier, new Vector2(0.4f, 0f), new Vector2(0f, 0.2f), true, PropOrder),
    };

    [MenuItem("Tools/Level/Dress Level_01 (Dungeon Art)")]
    public static void DressOpenLevel()
    {
        var floorMap = GameObject.Find("Floor")?.GetComponent<Tilemap>();
        var wallMap = GameObject.Find("Walls")?.GetComponent<Tilemap>();
        if (floorMap == null || wallMap == null)
        {
            Debug.LogWarning("DungeonDressing: open Level_01 first (needs the Floor and Walls tilemaps).");
            return;
        }

        CreateTileAssets();
        var cells = new HashSet<Vector2Int>();
        foreach (Vector3Int p in floorMap.cellBounds.allPositionsWithin)
        {
            if (floorMap.HasTile(p))
            {
                cells.Add(new Vector2Int(p.x, p.y));
            }
        }

        floorMap.ClearAllTiles();
        wallMap.ClearAllTiles();
        Paint(cells, floorMap, wallMap);

        if (wallMap.GetComponent<TilemapColliderRefresh>() == null)
        {
            wallMap.gameObject.AddComponent<TilemapColliderRefresh>();
        }

        RefreshDoorsAndExit();

        GameObject oldProps = GameObject.Find("Props");
        Transform parent = oldProps != null ? oldProps.transform.parent : GameObject.Find("Level")?.transform;
        if (oldProps != null)
        {
            Object.DestroyImmediate(oldProps);
        }

        PlaceProps(parent);
        ConfigureSorting();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("DungeonDressing: Level_01 tilemaps, doors and props updated.");
    }

    // ---- sorting ----

    // Top-down Y sort: sprites sharing a sorting order are drawn by Y, so props and characters overlap correctly.
    // Characters sort by their feet (sprite pivot) instead of the sprite centre.
    public static void ConfigureSorting()
    {
        GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis;
        GraphicsSettings.transparencySortAxis = Vector3.up;

        var player = Object.FindFirstObjectByType<PlayerController2D>();
        if (player != null)
        {
            foreach (SpriteRenderer sr in player.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
            }
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Enemies" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            bool changed = false;
            foreach (SpriteRenderer sr in prefab.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.spriteSortPoint != SpriteSortPoint.Pivot)
                {
                    sr.spriteSortPoint = SpriteSortPoint.Pivot;
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(prefab);
            }
        }

        AssetDatabase.SaveAssets();
    }

    // ---- tiles ----

    public static void CreateTileAssets()
    {
        if (!AssetDatabase.IsValidFolder(TileFolder))
        {
            AssetDatabase.CreateFolder(TopDownFolder, "Tiles");
        }

        for (int i = 0; i < TileCount; i++)
        {
            string path = $"{TileFolder}/Tile_TD_{i}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }

            tile.sprite = LoadSprite(TileSheet, $"tileset_topdown_{i}");
            // Grid collision for every tile; only the Walls tilemap has a collider, the Floor map ignores it.
            tile.colliderType = Tile.ColliderType.Grid;
            EditorUtility.SetDirty(tile);
        }

        AssetDatabase.SaveAssets();
    }

    public static Tile TileAt(int index) => AssetDatabase.LoadAssetAtPath<Tile>($"{TileFolder}/Tile_TD_{index}.asset");

    public static Sprite TileSprite(int index) => LoadSprite(TileSheet, $"tileset_topdown_{index}");

    // The boss room and the corridor leading to it (x -6..-1 from y 22) use the red tint rows.
    public static int ThemeOffset(Vector2Int c) => c.y >= 24 || (c.y >= 22 && c.x >= -6 && c.x <= -1) ? BossRow : 0;

    static int FloorVariant(Vector2Int c)
    {
        int h = (int)((((uint)(c.x * 73856093) ^ (uint)(c.y * 19349663)) * 2654435761u) >> 24);
        return h < 3 ? FloorGrate : h < 200 ? FloorA : h < 226 ? FloorCracked : FloorFlag;
    }

    public static void Paint(HashSet<Vector2Int> floor, Tilemap floorMap, Tilemap wallMap)
    {
        foreach (Vector2Int c in floor)
        {
            // Floor right below a wall face sits in its shadow.
            bool underWall = !floor.Contains(new Vector2Int(c.x, c.y + 1));
            int index = underWall ? FloorShadow : FloorVariant(c);
            floorMap.SetTile(new Vector3Int(c.x, c.y, 0), TileAt(index + ThemeOffset(c)));
        }

        var walls = new HashSet<Vector2Int>();
        foreach (Vector2Int c in floor)
        {
            for (int dx = -WallThickness; dx <= WallThickness; dx++)
            {
                for (int dy = -WallThickness; dy <= WallThickness; dy++)
                {
                    var n = new Vector2Int(c.x + dx, c.y + dy);
                    if (!floor.Contains(n))
                    {
                        walls.Add(n);
                    }
                }
            }
        }

        foreach (Vector2Int w in walls)
        {
            bool floorBelow = floor.Contains(new Vector2Int(w.x, w.y - 1));
            bool floorLeft = floor.Contains(new Vector2Int(w.x - 1, w.y));
            bool floorRight = floor.Contains(new Vector2Int(w.x + 1, w.y));
            bool floorAbove = floor.Contains(new Vector2Int(w.x, w.y + 1));
            int index;
            if (floorBelow)
            {
                index = floorLeft ? WallFrontLeft : floorRight ? WallFrontRight : WallFront;
            }
            else if (floorAbove)
            {
                index = floorLeft ? WallTopCorner : floorRight ? WallTopRight : WallTopBack;
            }
            else
            {
                index = floorLeft ? WallTopLeft : floorRight ? WallTopRight : WallTop;
            }

            wallMap.SetTile(new Vector3Int(w.x, w.y, 0), TileAt(index + ThemeOffset(w)));
        }
    }

    static void RefreshDoorsAndExit()
    {
        foreach (Door door in Object.FindObjectsByType<Door>(FindObjectsSortMode.None))
        {
            int theme = door.transform.position.y >= 22f ? BossRow : 0;
            var so = new SerializedObject(door);
            so.FindProperty("closedSprite").objectReferenceValue = TileSprite(DoorClosed + theme);
            so.FindProperty("openSprite").objectReferenceValue = TileSprite(DoorOpen + theme);
            so.ApplyModifiedPropertiesWithoutUndo();
            foreach (SpriteRenderer sr in door.GetComponentsInChildren<SpriteRenderer>())
            {
                sr.sprite = TileSprite(DoorOpen + theme);
            }
        }

        GameObject pad = GameObject.Find("LevelExit");
        if (pad != null)
        {
            foreach (SpriteRenderer sr in pad.GetComponentsInChildren<SpriteRenderer>())
            {
                sr.sprite = TileSprite(Exit);
            }
        }
    }

    // ---- props ----

    static Sprite LoadSprite(string sheet, string name)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(sheet))
        {
            if (o is Sprite s && o.name == name)
            {
                return s;
            }
        }

        throw new System.Exception($"DungeonDressing: missing sprite {name} in {sheet}");
    }

    // Removes the Craftpix prop prefabs and writes one prefab per prop in the sheet.
    public static void EnsurePropPrefabs()
    {
        if (!AssetDatabase.IsValidFolder(PropPrefabFolder))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs/Environment", "Props");
        }

        string[] keep = new string[PropDefs.Length];
        for (int i = 0; i < PropDefs.Length; i++)
        {
            keep[i] = PropDefs[i].name;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PropPrefabFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.Array.IndexOf(keep, Path.GetFileNameWithoutExtension(path)) < 0)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        Material mat = FindSpriteMaterial();
        foreach (var def in PropDefs)
        {
            var go = new GameObject(def.name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite(PropSheet, $"tileset_props_{def.index}");
            sr.sortingOrder = def.order;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            if (mat != null)
            {
                sr.sharedMaterial = mat;
            }

            if (def.colliderSize != Vector2.zero)
            {
                if (def.circle)
                {
                    var col = go.AddComponent<CircleCollider2D>();
                    col.radius = def.colliderSize.x * 0.5f;
                    col.offset = def.colliderOffset;
                }
                else
                {
                    var col = go.AddComponent<BoxCollider2D>();
                    col.size = def.colliderSize;
                    col.offset = def.colliderOffset;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(go, $"{PropPrefabFolder}/{def.name}.prefab");
            Object.DestroyImmediate(go);
        }

        AssetDatabase.SaveAssets();
    }

    static Material FindSpriteMaterial()
    {
        var player = Object.FindFirstObjectByType<PlayerController2D>();
        var sr = player != null ? player.GetComponentInChildren<SpriteRenderer>() : null;
        return sr != null ? sr.sharedMaterial : null;
    }

    struct Placement
    {
        public string prefab;
        public Vector2Int cell;
        public bool flipX, flipY;

        public Placement(string prefab, int x, int y, bool flipX = false, bool flipY = false)
        {
            this.prefab = prefab;
            cell = new Vector2Int(x, y);
            this.flipX = flipX;
            this.flipY = flipY;
        }
    }

    static List<Placement> PropLayout()
    {
        var p = new List<Placement>();
        void Add(string prefab, int x, int y, bool fx = false, bool fy = false) => p.Add(new Placement(prefab, x, y, fx, fy));

        // Rest room (-6..5, -4..3): a camp. The bomb pocket (SW), lantern pocket (NE) and hidden heart (NW) stay clear.
        Add("Torch", -3, 4); Add("Torch", 0, 4);
        Add("Brazier", -1, 2); Add("Candles", 1, -3); Add("Chest", -1, -3);
        Add("Crate", 4, -3); Add("Barrel", 5, -3); Add("Sack", 4, -2); Add("Rubble", 2, 1);
        Add("Cobweb", 5, -4, true, true); Add("CrackDecal", -2, 0);

        // Room 1 (10..27, -6..5)
        foreach (int x in new[] { 12, 15, 22, 25 }) Add("Torch", x, 6);
        Add("Pillar", 14, -2); Add("Pillar", 23, -2); Add("Pillar", 14, 2); Add("Pillar", 23, 2);
        Add("Rubble", 19, -4); Add("Rubble", 12, -5); Add("BonePile", 25, -5); Add("BonePile", 20, 3); Add("Skull", 16, -4);
        Add("Barrel", 26, 4); Add("Barrel", 25, 4); Add("Crate", 11, 4); Add("BrokenColumn", 17, 0);
        Add("Cobweb", 27, 5, true); Add("Cobweb", 27, -6, true, true); Add("Cobweb", 10, -6, false, true);
        Add("CrackDecal", 18, 0); Add("CrackDecal", 12, -2, true); Add("CrackDecal", 24, 3);

        // Room 2 (12..25, 10..19)
        foreach (int x in new[] { 14, 17, 21, 24 }) Add("Torch", x, 20);
        Add("Pillar", 15, 13); Add("Pillar", 22, 13); Add("Pillar", 15, 17); Add("Pillar", 22, 17);
        Add("Rubble", 13, 11); Add("Barrel", 24, 11); Add("Barrel", 25, 11); Add("Crate", 13, 18);
        Add("BonePile", 20, 12); Add("Skull", 18, 18); Add("BrokenColumn", 19, 15);
        Add("Cobweb", 12, 19); Add("Cobweb", 25, 19, true); Add("Cobweb", 25, 10, true, true);
        Add("CrackDecal", 18, 14); Add("CrackDecal", 21, 11, true);

        // Room 3 (-14..7, 8..21)
        foreach (int x in new[] { -11, -8, 0, 3, 6 }) Add("Torch", x, 22);
        Add("Pillar", -10, 12); Add("Pillar", -10, 18); Add("Pillar", 2, 12); Add("Pillar", 2, 18);
        Add("Chest", -13, 20); Add("Barrel", -13, 9); Add("Barrel", -12, 9); Add("Crate", -13, 10); Add("Sack", -12, 10);
        Add("Rubble", -3, 9); Add("Rubble", 6, 20); Add("BonePile", -6, 14); Add("Skull", 0, 15); Add("BrokenColumn", -4, 16);
        Add("Candles", -13, 19);
        Add("Cobweb", -14, 21); Add("Cobweb", 7, 21, true); Add("Cobweb", -14, 8, false, true);
        Add("CrackDecal", -5, 12); Add("CrackDecal", -2, 18, true); Add("CrackDecal", 4, 15);

        // Boss room (-14..5, 26..41): braziers flank the approach, the exit pad at (-5..-4, 35..36) stays clear.
        foreach (int x in new[] { -12, -8, -1, 3 }) Add("Torch", x, 42);
        Add("Brazier", -12, 28); Add("Brazier", 3, 28); Add("Brazier", -12, 39); Add("Brazier", 3, 39);
        Add("Pillar", -9, 31); Add("Pillar", -1, 31); Add("Pillar", -9, 37); Add("Pillar", -1, 37);
        Add("BonePile", -7, 28); Add("BonePile", 1, 34); Add("Skull", -11, 33); Add("Skull", 2, 38); Add("Skull", -3, 29);
        Add("Rubble", -13, 31); Add("Rubble", 4, 36); Add("BrokenColumn", -6, 40); Add("BrokenColumn", 0, 40);
        Add("Cobweb", -14, 41); Add("Cobweb", 5, 41, true); Add("Cobweb", -14, 26, false, true); Add("Cobweb", 5, 26, true, true);
        Add("CrackDecal", -6, 33); Add("CrackDecal", -2, 36, true); Add("CrackDecal", 0, 29);
        return p;
    }

    public static void PlaceProps(Transform parent)
    {
        EnsurePropPrefabs();
        var root = new GameObject("Props").transform;
        if (parent != null)
        {
            root.SetParent(parent);
        }

        var floorMap = GameObject.Find("Floor")?.GetComponent<Tilemap>();
        var wallMap = GameObject.Find("Walls")?.GetComponent<Tilemap>();
        foreach (Placement pl in PropLayout())
        {
            bool torch = pl.prefab == "Torch";
            bool decal = pl.prefab == "Cobweb" || pl.prefab == "CrackDecal";
            // Torches hang on a wall face (a wall tile with floor below it); everything else stands on floor.
            var check = new Vector3Int(pl.cell.x, pl.cell.y, 0);
            if (torch && wallMap != null && !wallMap.HasTile(check))
            {
                Debug.LogWarning($"DungeonDressing: no wall at {pl.cell} for {pl.prefab}, skipped.");
                continue;
            }

            if (!torch && floorMap != null && !floorMap.HasTile(check))
            {
                Debug.LogWarning($"DungeonDressing: no floor at {pl.cell} for {pl.prefab}, skipped.");
                continue;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropPrefabFolder}/{pl.prefab}.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            // Sprites pivot at the bottom centre; a Y-flipped decal hangs below its pivot, so it starts at the cell top.
            float y = torch ? pl.cell.y : decal ? pl.cell.y + (pl.flipY ? 1f : 0f) : pl.cell.y + 0.15f;
            go.transform.position = new Vector3(pl.cell.x + 0.5f, y, 0f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.flipX = pl.flipX;
            sr.flipY = pl.flipY;
        }
    }
}
