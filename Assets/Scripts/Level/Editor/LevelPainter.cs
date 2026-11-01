using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Paints Level_01 with the dungeon palettes from code (Tools/Level/Paint Level_01), so the result can be regenerated:
// floor from room / corridor / alcove rects minus pillars, a wall ring around it (Wall_Auto: 3 cells at the sides and
// below, 4 above = stone cap + 3 face rows), dark cobble backdrop behind the ring, arch gates, decals, props.
// Clears Ground / Decor / Walls / WallTop / Backdrop / Props first; paint by hand afterwards if you like.
public static class LevelPainter
{
    const string TileFolder = "Assets/Art/Environment/TopDown/Tiles";
    const string PropFolder = "Assets/Prefabs/Environment/Props";
    const int RingSide = 3, RingBelow = 3, RingAbove = 4, BackdropPad = 9;

    struct R
    {
        public int x0, y0, x1, y1;
        public R(int x0, int y0, int x1, int y1) { this.x0 = x0; this.y0 = y0; this.x1 = x1; this.y1 = y1; }
    }

    // Floor: room rims (room trigger + 1 cell), corridors, alcoves. Inclusive cell rects.
    static readonly R[] FloorRects =
    {
        new(-6, -4, 5, 3), new(10, -6, 27, 5), new(12, 10, 25, 19), new(-14, 8, 7, 21), new(-14, 26, 5, 41),   // rooms 0-3, boss
        new(5, -1, 10, 0), new(18, 5, 19, 10), new(7, 14, 12, 15), new(-4, 21, -3, 26),                         // corridors
        new(-9, -2, -7, 0), new(12, 6, 15, 7), new(20, -9, 24, -7), new(15, 20, 18, 22), new(26, 13, 28, 16),  // alcoves
        new(-12, 22, -8, 23), new(-17, 32, -15, 36), new(6, 30, 8, 34), new(-9, 42, -6, 44),
    };

    // Solid blocks cut out of the floor: pillars and a stub hanging from a wall.
    static readonly R[] Solids =
    {
        new(-2, -1, -1, 0),
        new(16, -3, 17, -2), new(21, 1, 22, 2),
        new(16, 13, 17, 14), new(21, 15, 22, 16),
        new(-9, 13, -8, 14), new(-3, 17, -2, 18), new(1, 11, 2, 12), new(0, 17, 1, 21),
        new(-10, 30, -9, 31), new(-1, 30, 0, 31), new(-10, 36, -9, 37), new(-1, 36, 0, 37),
    };

    // Decorative arch gates (4 x 4, top-left origin) in the wall above a room.
    static readonly Vector2Int[] Arches = { new(-4, 7), new(0, 45) };

    // Rooms for decals: rim rect (decor stays away from its border cells next to doors by cell checks).
    static readonly R[] DecorRooms = { new(-5, -3, 4, 2), new(11, -5, 26, 4), new(13, 11, 24, 18), new(-13, 9, 6, 20), new(-13, 27, 4, 40) };

    // name, x, y (world, prefab pivot is bottom centre)
    static readonly (string, float, float)[] PropList =
    {
        // rest
        ("Torch", -4.5f, 4.15f), ("Torch", 0.5f, 4.15f), ("Torch", -8.0f, 1.15f), ("Chest", 4.4f, -3.0f), ("Barrel", 3.3f, -3.0f), ("Crate", -1.2f, -3.0f), ("Chest", -8.5f, -1.2f),
        // room 1
        ("Torch", 22.5f, 6.15f), ("Torch", 25.5f, 6.15f), ("Torch", 13.5f, 8.15f), ("Barrel", 26.4f, -5.0f), ("BarrelBig", 25.5f, -5.3f), ("Crate", 11.5f, -5.0f), ("Chest", 22.5f, -8.4f), ("RockA", 24.5f, 3.8f),
        // room 2
        ("Torch", 13.5f, 20.15f), ("Torch", 22.5f, 20.15f), ("Torch", 17.0f, 23.15f), ("Chest", 27.5f, 14.5f), ("Barrel", 24.5f, 11.0f), ("BarrelGreen", 23.4f, 10.8f), ("Crate", 13.4f, 18.2f), ("Crate", 14.3f, 18.5f), ("RockB", 21.0f, 17.8f),
        // room 3
        ("Torch", -6.5f, 22.15f), ("Torch", -0.5f, 22.15f), ("Torch", 4.5f, 22.15f), ("Torch", -10.0f, 24.15f), ("Chest", -12.5f, 20.0f), ("Barrel", -13.5f, 9.0f), ("Barrel", -12.6f, 9.3f), ("Crate", 6.4f, 9.2f), ("Crate", 5.5f, 9.0f), ("RockC", 4.0f, 20.0f), ("Pillar", -6.0f, 13.0f),
        // boss
        ("Torch", -12.5f, 42.15f), ("Torch", -2.5f, 42.15f), ("Torch", 4.5f, 42.15f), ("Torch", -7.5f, 45.15f), ("Torch", -15.5f, 37.15f), ("Torch", 7.5f, 35.15f),
        ("Chest", -12.5f, 28.3f), ("Chest", 3.5f, 28.3f), ("Barrel", -16.2f, 33.0f), ("BarrelBig", 7.5f, 31.0f), ("RockTall", -5.5f, 40.0f), ("RockA", 2.0f, 39.5f), ("Pillar", -6.0f, 33.0f), ("Pillar", 2.0f, 33.0f),
    };

    static readonly string[] DecorNames =
    {
        "crack_a", "crack_b", "slime_a", "slime_b", "slime_c", "weed_1", "weed_2", "weed_3", "moss_1", "moss_2", "pebble_1", "pebble_2", "pebble_3", "pebble_4", "pebble_5",
    };

    static bool IsBoss(int x, int y) => y >= 24 || (y >= 22 && x >= -6 && x <= -1);

    static TileBase T(string name) => AssetDatabase.LoadAssetAtPath<TileBase>($"{TileFolder}/{name}.asset");

    static void Add(HashSet<Vector2Int> set, R r, bool remove = false)
    {
        for (int x = r.x0; x <= r.x1; x++)
        {
            for (int y = r.y0; y <= r.y1; y++)
            {
                var c = new Vector2Int(x, y);
                if (remove) set.Remove(c); else set.Add(c);
            }
        }
    }

    public static HashSet<Vector2Int> BuildFloor()
    {
        var floor = new HashSet<Vector2Int>();
        foreach (R r in FloorRects) Add(floor, r);
        foreach (R r in Solids) Add(floor, r, true);
        return floor;
    }

    static Tilemap Map(Transform grid, string name, int order, Color? tint = null)
    {
        Transform t = grid.Find(name);
        if (t == null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(grid, false);
            go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>().sortingOrder = order;
            t = go.transform;
        }

        var map = t.GetComponent<Tilemap>();
        if (tint.HasValue) map.color = tint.Value;
        return map;
    }

    static TileBase Backdrop(int x, int y, bool boss)
    {
        int i = 4 + (Mathf.Abs(y) % 2) * 4 + (((x % 4) + 4) % 4);
        string name = $"BD_{i}{(boss ? "_boss" : "")}";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>($"{TileFolder}/{name}.asset");
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = ((Tile)T($"WL_8_{i}")).sprite;
            tile.color = boss ? new Color(0.62f, 0.34f, 0.36f) : new Color(0.52f, 0.52f, 0.58f);
            tile.colliderType = Tile.ColliderType.None;
            AssetDatabase.CreateAsset(tile, $"{TileFolder}/{name}.asset");
        }

        return tile;
    }

    [MenuItem("Tools/Level/Paint Level_01")]
    public static void Paint()
    {
        var gridGo = GameObject.Find("Grid");
        if (gridGo == null || gridGo.transform.Find("Walls") == null)
        {
            Debug.LogError("LevelPainter: open Level_01 (needs Grid/Walls). Run Tools/Level/Setup Level_01 For Painting first on an auto level.");
            return;
        }

        Transform grid = gridGo.transform;
        Tilemap ground = Map(grid, "Ground", -100), decor = Map(grid, "Decor", -95), walls = Map(grid, "Walls", -90);
        Tilemap wallTop = Map(grid, "WallTop", 20), backdrop = Map(grid, "Backdrop", -92), props = Map(grid, "Props", 0);
        foreach (Tilemap m in new[] { ground, decor, walls, wallTop, backdrop })
        {
            m.ClearAllTiles();
        }

        for (int i = props.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(props.transform.GetChild(i).gameObject);
        }

        HashSet<Vector2Int> floor = BuildFloor();

        // Wall ring.
        var ring = new HashSet<Vector2Int>();
        foreach (Vector2Int f in floor)
        {
            for (int dx = -RingSide; dx <= RingSide; dx++)
            {
                for (int dy = -RingAbove; dy <= RingBelow; dy++)
                {
                    var c = new Vector2Int(f.x + dx, f.y + dy);
                    if (!floor.Contains(c)) ring.Add(c);
                }
            }
        }

        var wallPos = new List<Vector3Int>();
        var wallTiles = new List<TileBase>();
        foreach (Vector2Int c in ring)
        {
            wallPos.Add(new Vector3Int(c.x, c.y, 0));
            wallTiles.Add(T(IsBoss(c.x, c.y) ? "Wall_Auto_Boss" : "Wall_Auto"));
        }

        walls.SetTiles(wallPos.ToArray(), wallTiles.ToArray());

        // Arch gates.
        foreach (Vector2Int a in Arches)
        {
            for (int i = 0; i < 16; i++)
            {
                int col = i % 4, row = i / 4;
                TileBase tile = T(col < 2 ? $"WL_10_{i}" : $"WL_11_{i}");
                if (tile != null) walls.SetTile(new Vector3Int(a.x + col, a.y - row, 0), tile);
            }
        }

        // The arch tiles have no (reliable) tile collider; a solid blocker closes each gate.
        Transform oldBlockers = walls.transform.Find("ArchBlockers");
        if (oldBlockers != null) Object.DestroyImmediate(oldBlockers.gameObject);
        var blockers = new GameObject("ArchBlockers");
        blockers.transform.SetParent(walls.transform, false);
        blockers.layer = walls.gameObject.layer;
        foreach (Vector2Int a in Arches)
        {
            var box = blockers.AddComponent<BoxCollider2D>();
            box.size = new Vector2(4f, 4f);
            box.offset = new Vector2(a.x + 2f, a.y - 3f + 2f);
        }

        // Floor.
        var gPos = new List<Vector3Int>();
        var gTiles = new List<TileBase>();
        foreach (Vector2Int f in floor)
        {
            gPos.Add(new Vector3Int(f.x, f.y, 0));
            gTiles.Add(T(IsBoss(f.x, f.y) ? "Floor_Auto_Boss" : "Floor_Auto"));
        }

        ground.SetTiles(gPos.ToArray(), gTiles.ToArray());

        // Backdrop behind the ring.
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (Vector2Int c in ring)
        {
            minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
            minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y);
        }

        var bPos = new List<Vector3Int>();
        var bTiles = new List<TileBase>();
        for (int x = minX - BackdropPad; x <= maxX + BackdropPad; x++)
        {
            for (int y = minY - BackdropPad; y <= maxY + BackdropPad; y++)
            {
                var c = new Vector2Int(x, y);
                if (floor.Contains(c)) continue;
                bPos.Add(new Vector3Int(x, y, 0));
                bTiles.Add(Backdrop(x, y, IsBoss(x, y)));
            }
        }

        backdrop.SetTiles(bPos.ToArray(), bTiles.ToArray());

        // Decals: deterministic per room, kept off corridor mouths (cells whose 4 neighbours are not all floor stay clear).
        foreach (R room in DecorRooms)
        {
            var rng = new System.Random(room.x0 * 31 + room.y0);
            for (int n = 0; n < 9; n++)
            {
                var c = new Vector2Int(rng.Next(room.x0 + 1, room.x1), rng.Next(room.y0 + 1, room.y1));
                if (!floor.Contains(c) || !floor.Contains(c + Vector2Int.up) || !floor.Contains(c + Vector2Int.down)
                    || !floor.Contains(c + Vector2Int.left) || !floor.Contains(c + Vector2Int.right))
                {
                    continue;
                }

                decor.SetTile(new Vector3Int(c.x, c.y, 0), T("DC_" + DecorNames[rng.Next(DecorNames.Length)]));
            }
        }

        // Props.
        foreach (var (name, x, y) in PropList)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropFolder}/{name}.prefab");
            if (prefab == null)
            {
                Debug.LogWarning("LevelPainter: missing prop " + name);
                continue;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, props.transform);
            instance.transform.position = new Vector3(x, y, 0f);
        }

        // Reference room east of the level.
        LevelPaintSetup.PaintSampleRoom(ground, walls, decor, props);

        var player = Object.FindFirstObjectByType<PlayerController2D>();
        if (player != null)
        {
            player.transform.position = new Vector3(0f, 0f, 0f);
        }

        var walkWalls = walls.GetComponent<TilemapCollider2D>();
        walkWalls.enabled = false;
        walkWalls.enabled = true;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log($"LevelPainter: floor {floor.Count}, ring {ring.Count}, backdrop {bPos.Count}.");
    }
}
