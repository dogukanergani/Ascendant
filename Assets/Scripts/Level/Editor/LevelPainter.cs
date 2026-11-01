using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Paints Level_01 from code (Tools/Level/Paint Level_01) with one plain room template, built from the Craftpix pieces
// that tile without seams:
//   top wall    4 rows above the room: the pack's wall_1 sheet as drawn (stone cap, cobble, cobble, ragged foot).
//               The cobble only tiles horizontally, so it is never stacked.
//   side walls  3 wide: stone block column (wall_3) on the inner and outer edge, cap stones (wall_4) between.
//   bottom wall 3 rows of cap stones in running bond, ended by the outer columns.
//   corners     the side columns run the full height; a column end that touches nothing gets the single block (wall_9).
// Under every wall cell lies a flat dark "mortar" tile, so transparent edges read as a clean dark outline. Outside the
// walls is empty (camera background). Rooms are plain rectangles; corridors are 2 cells, lined up with the Door objects.
// Clears Ground / Decor / Walls / WallTop / Backdrop / Props first.
public static class LevelPainter
{
    const string TileFolder = "Assets/Art/Environment/TopDown/Tiles";
    const string PropFolder = "Assets/Prefabs/Environment/Props";
    const string SolidPng = "Assets/Level/Guide/solid_cell.png";
    const int Side = 3, Below = 3, Above = 4;
    static readonly Color BossTint = new(1f, 0.72f, 0.74f, 1f);
    static readonly Color Mortar = new(0.075f, 0.065f, 0.1f, 1f);

    struct R
    {
        public int x0, y0, x1, y1;
        public R(int x0, int y0, int x1, int y1) { this.x0 = x0; this.y0 = y0; this.x1 = x1; this.y1 = y1; }
        public bool Contains(Vector2Int c) => c.x >= x0 && c.x <= x1 && c.y >= y0 && c.y <= y1;
        public float CenterX => (x0 + x1 + 1) * 0.5f;
    }

    // Floor rects: Room trigger + 1 cell (rooms 0-3, boss, then the reference room east of the level) and corridors.
    static readonly R[] Rooms = { new(-6, -4, 5, 3), new(10, -6, 27, 5), new(12, 10, 25, 19), new(-14, 8, 7, 21), new(-14, 26, 5, 41), new(44, 2, 53, 8) };
    static readonly R[] Corridors = { new(5, -1, 10, 0), new(18, 5, 19, 10), new(7, 14, 12, 15), new(-4, 21, -3, 26) };
    const int BossRoom = 4;

    // Arch gate in the top wall of the rooms that have no corridor there (index into Rooms).
    static readonly int[] ArchRooms = { 0, 2, 4, 5 };

    enum Kind { Floor, Face, Column, Stone }

    static bool IsBoss(int x, int y) => y >= 26 && x <= 10;

    static int Mod4(int v) => ((v % 4) + 4) % 4;

    static TileBase T(string name) => AssetDatabase.LoadAssetAtPath<TileBase>($"{TileFolder}/{name}.asset");

    // A wall piece, red-tinted inside the boss area (tinted copies live next to the originals).
    static TileBase Piece(string name, bool boss)
    {
        if (!boss)
        {
            return T(name);
        }

        string path = $"{TileFolder}/{name}_boss.asset";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile == null)
        {
            var src = (Tile)T(name);
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = src.sprite;
            tile.colliderType = src.colliderType;
            tile.color = BossTint;
            AssetDatabase.CreateAsset(tile, path);
        }

        return tile;
    }

    // Flat dark tile under the walls (own 16 px white square, outside Assets/Art so PixelArtImporter leaves it alone).
    static Tile MortarTile()
    {
        if (!File.Exists(SolidPng))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SolidPng));
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            var pixels = new Color[256];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            File.WriteAllBytes(SolidPng, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(SolidPng);
            var importer = (TextureImporter)AssetImporter.GetAtPath(SolidPng);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        string path = $"{TileFolder}/Mortar.asset";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, path);
        }

        tile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SolidPng);
        tile.color = Mortar;
        tile.colliderType = Tile.ColliderType.None;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    static void Add(HashSet<Vector2Int> set, R r)
    {
        for (int x = r.x0; x <= r.x1; x++)
        {
            for (int y = r.y0; y <= r.y1; y++)
            {
                set.Add(new Vector2Int(x, y));
            }
        }
    }

    public static HashSet<Vector2Int> BuildFloor()
    {
        var floor = new HashSet<Vector2Int>();
        foreach (R r in Rooms) Add(floor, r);
        foreach (R r in Corridors) Add(floor, r);
        return floor;
    }

    static HashSet<Vector2Int> BuildWalls(HashSet<Vector2Int> floor)
    {
        var walls = new HashSet<Vector2Int>();
        foreach (R r in Rooms) Add(walls, new R(r.x0 - Side, r.y0 - Below, r.x1 + Side, r.y1 + Above));
        walls.ExceptWith(floor);
        return walls;
    }

    // Distance k (1-4) to the floor straight below and the height of that wall (rock run above the floor, max 4).
    static bool FaceInfo(HashSet<Vector2Int> floor, HashSet<Vector2Int> walls, Vector2Int c, out int k, out int height)
    {
        k = 0;
        height = 0;
        for (int d = 1; d <= Above; d++)
        {
            var below = new Vector2Int(c.x, c.y - d);
            if (floor.Contains(below)) { k = d; break; }
            if (!walls.Contains(below)) return false;
        }

        if (k == 0) return false;
        int run = 0;
        while (run < Above && walls.Contains(new Vector2Int(c.x, c.y - k + 1 + run))) run++;
        height = run;
        return true;
    }

    // Row of wall_1 for face row k (1 = foot) of a wall `height` rows tall: cap on top, ragged foot at the floor.
    static int FaceSheetRow(int k, int height)
    {
        if (k == height) return 0;
        if (k == 1) return 3;
        return height == 4 ? 4 - k : 1;
    }

    static Kind Classify(HashSet<Vector2Int> floor, HashSet<Vector2Int> walls, Vector2Int c, out int sheetRow)
    {
        sheetRow = 0;
        if (floor.Contains(c)) return Kind.Floor;
        bool F(int dx, int dy) => floor.Contains(new Vector2Int(c.x + dx, c.y + dy));
        bool Out(int dx, int dy) { var n = new Vector2Int(c.x + dx, c.y + dy); return !floor.Contains(n) && !walls.Contains(n); }

        if (F(-1, 0) || F(1, 0)) return Kind.Column;
        if (FaceInfo(floor, walls, c, out int k, out int h))
        {
            sheetRow = FaceSheetRow(k, h);
            return Kind.Face;
        }

        foreach (int dx in new[] { -1, 1 })
        {
            var side = new Vector2Int(c.x + dx, c.y);
            if (walls.Contains(side) && !F(dx, 0) && FaceInfo(floor, walls, side, out _, out _)) return Kind.Column;
        }

        if (F(-1, -1) || F(1, -1) || F(-1, 1) || F(1, 1)) return Kind.Column;
        if (Out(-1, 0) || Out(1, 0)) return Kind.Column;
        return Kind.Stone;
    }

    static Tilemap Map(Transform grid, string name, int order)
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

        return t.GetComponent<Tilemap>();
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
        Tilemap wallTop = Map(grid, "WallTop", 20), mortar = Map(grid, "Backdrop", -92), props = Map(grid, "Props", 0);
        mortar.color = Color.white;
        foreach (Tilemap m in new[] { ground, decor, walls, wallTop, mortar })
        {
            m.ClearAllTiles();
        }

        for (int i = props.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(props.transform.GetChild(i).gameObject);
        }

        HashSet<Vector2Int> floor = BuildFloor();
        HashSet<Vector2Int> wallCells = BuildWalls(floor);
        Tile mortarTile = MortarTile();
        var kinds = new Dictionary<Vector2Int, Kind>();
        var wPos = new List<Vector3Int>(); var wTiles = new List<TileBase>();
        var gPos = new List<Vector3Int>(); var gTiles = new List<TileBase>();
        var mPos = new List<Vector3Int>();

        foreach (Vector2Int c in floor)
        {
            bool boss = IsBoss(c.x, c.y);
            int cell = (3 - Mod4(c.y)) * 4 + Mod4(c.x);
            // The row right under a wall face sits in its shadow.
            bool shadow = wallCells.Contains(c + Vector2Int.up) && FaceInfo(floor, wallCells, c + Vector2Int.up, out int k, out _) && k == 1;
            gPos.Add(new Vector3Int(c.x, c.y, 0));
            gTiles.Add(T($"CP_{(shadow ? "shadow" : "floor")}_{cell}{(boss ? "_boss" : "")}"));
        }

        foreach (Vector2Int c in wallCells)
        {
            var p = new Vector3Int(c.x, c.y, 0);
            bool boss = IsBoss(c.x, c.y);
            Kind kind = Classify(floor, wallCells, c, out int row);
            kinds[c] = kind;
            mPos.Add(p);
            switch (kind)
            {
                case Kind.Face:
                    wTiles.Add(Piece($"WL_1_{row * 4 + Mod4(c.x)}", boss));
                    break;
                case Kind.Column:
                    wTiles.Add(Piece($"WL_3_{3 - Mod4(c.y)}", boss));
                    break;
                default:
                    // Running bond: every other row of cap stones shifted by half a strip.
                    wTiles.Add(Piece($"WL_4_{Mod4(c.x + (Mod4(c.y) % 2) * 2)}", boss));
                    break;
            }

            wPos.Add(p);
        }

        // A column end that runs into nothing (corridor mouth, outer corner) gets the single stone block.
        for (int i = 0; i < wPos.Count; i++)
        {
            var c = new Vector2Int(wPos[i].x, wPos[i].y);
            if (kinds[c] != Kind.Column) continue;
            bool colAbove = kinds.TryGetValue(c + Vector2Int.up, out Kind a) && a == Kind.Column;
            bool colBelow = kinds.TryGetValue(c + Vector2Int.down, out Kind b) && b == Kind.Column;
            bool endsUp = !wallCells.Contains(c + Vector2Int.up) || floor.Contains(c + Vector2Int.up);
            bool endsDown = !wallCells.Contains(c + Vector2Int.down) || floor.Contains(c + Vector2Int.down);
            if ((endsUp && !colAbove) || (endsDown && !colBelow))
            {
                wTiles[i] = Piece("WL_9_0", IsBoss(c.x, c.y));
            }
        }

        walls.SetTiles(wPos.ToArray(), wTiles.ToArray());
        ground.SetTiles(gPos.ToArray(), gTiles.ToArray());
        var mTiles = new TileBase[mPos.Count];
        for (int i = 0; i < mTiles.Length; i++) mTiles[i] = mortarTile;
        mortar.SetTiles(mPos.ToArray(), mTiles);

        List<RectInt> archRects = PaintArches(walls, mortar);
        var rng = new System.Random(77);
        PaintDecals(decor, floor, rng);
        PlaceProps(props, archRects);

        var player = Object.FindFirstObjectByType<PlayerController2D>();
        if (player != null)
        {
            player.transform.position = Vector3.zero;
        }

        // With the default offset the composite's convex decomposition can drop a cell; a slightly larger offset closes it.
        var composite = walls.GetComponent<CompositeCollider2D>();
        composite.offsetDistance = 0.005f;
        var tileCollider = walls.GetComponent<TilemapCollider2D>();
        tileCollider.enabled = false;
        tileCollider.enabled = true;
        tileCollider.ProcessTilemapChanges();
        composite.GenerateGeometry();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log($"LevelPainter: floor {floor.Count}, walls {wPos.Count}.");
    }

    // 4 x 4 arch gate centred in the top wall; the arch tiles have no collider, a box keeps the gate solid.
    static List<RectInt> PaintArches(Tilemap walls, Tilemap under)
    {
        Transform old = walls.transform.Find("ArchBlockers");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var blockers = new GameObject("ArchBlockers");
        blockers.transform.SetParent(walls.transform, false);
        blockers.layer = walls.gameObject.layer;
        var rects = new List<RectInt>();
        foreach (int r in ArchRooms)
        {
            R room = Rooms[r];
            int x0 = Mathf.RoundToInt(room.CenterX) - 2, y0 = room.y1 + 1;
            for (int i = 0; i < 16; i++)
            {
                int col = i % 4, row = i / 4;
                var p = new Vector3Int(x0 + col, y0 + 3 - row, 0);
                // The wall face continues behind the gate, so its transparent corners show cap and cobble, not mortar.
                under.SetTile(p, Piece($"WL_1_{row * 4 + Mod4(p.x)}", IsBoss(p.x, p.y)));
                TileBase tile = T(col < 2 ? $"WL_10_{i}" : $"WL_11_{i}");
                if (tile != null) walls.SetTile(p, tile);
            }

            var box = blockers.AddComponent<BoxCollider2D>();
            box.size = new Vector2(4f, 4f);
            box.offset = new Vector2(x0 + 2f, y0 + 2f);
            rects.Add(new RectInt(x0, y0, 4, 4));
        }

        return rects;
    }

    static bool NearCorridor(Vector2Int c, int margin)
    {
        foreach (R r in Corridors)
        {
            if (c.x >= r.x0 - margin && c.x <= r.x1 + margin && c.y >= r.y0 - margin && c.y <= r.y1 + margin) return true;
        }

        return false;
    }

    static readonly string[] DecalNames = { "crack_a", "crack_b", "crack_a", "pebble_1", "pebble_4", "pebble_5", "moss_1" };

    // A handful of cracks and pebbles per room, away from walls, corridors and props.
    static void PaintDecals(Tilemap decor, HashSet<Vector2Int> floor, System.Random rng)
    {
        foreach (R room in Rooms)
        {
            int area = (room.x1 - room.x0 + 1) * (room.y1 - room.y0 + 1);
            int count = Mathf.Clamp(area / 40, 3, 7);
            for (int n = 0; n < count; n++)
            {
                for (int tries = 0; tries < 20; tries++)
                {
                    var c = new Vector2Int(rng.Next(room.x0 + 2, room.x1 - 1), rng.Next(room.y0 + 2, room.y1 - 1));
                    if (NearCorridor(c, 2) || NearProp(c)) continue;
                    bool crowded = false;
                    for (int dx = -2; dx <= 2 && !crowded; dx++)
                        for (int dy = -2; dy <= 2 && !crowded; dy++)
                            crowded = decor.GetTile(new Vector3Int(c.x + dx, c.y + dy, 0)) != null;
                    if (crowded) continue;
                    decor.SetTile(new Vector3Int(c.x, c.y, 0), T("DC_" + DecalNames[rng.Next(DecalNames.Length)]));
                    break;
                }
            }
        }

        foreach (var (name, x, y) in new[] { ("gem_red", -12, 39), ("gem_blue", -10, 40), ("gem_gold", 2, 39), ("gem_purple", 4, 39) })
        {
            if (floor.Contains(new Vector2Int(x, y))) decor.SetTile(new Vector3Int(x, y, 0), T("DC_" + name));
        }
    }

    // name, x, y (prefab pivot is bottom centre). Torches sit symmetric on each top wall, the rest in corners away
    // from doors, so spawns and the walk between doors stay open.
    static readonly (string name, float x, float y)[] PropList =
    {
        // rest room (top wall y 4..7, arch x -2..1)
        ("Torch", -3.5f, 4.15f), ("Torch", 3.5f, 4.15f), ("Campfire", 2.5f, -1.8f), ("Barrel", 4.4f, -3.6f), ("Crate", 3.4f, -3.7f),
        // room 1 (corridor through the top wall at x 18..19)
        ("Torch", 13.5f, 6.15f), ("Torch", 24.5f, 6.15f), ("Barrel", 10.6f, -5.7f), ("Barrel", 11.6f, -5.5f), ("Crate", 10.6f, -4.6f), ("Chest", 26.4f, -5.5f),
        // room 2 (arch x 17..20)
        ("Torch", 14.5f, 20.15f), ("Torch", 23.5f, 20.15f), ("Crate", 24.4f, 18.4f), ("Crate", 23.5f, 18.6f), ("Barrel", 24.5f, 17.5f), ("Chest", 13.0f, 18.4f),
        // room 3 (corridor through the top wall at x -4..-3)
        ("Torch", -9.5f, 22.15f), ("Torch", 3.5f, 22.15f), ("Barrel", -13.4f, 8.5f), ("Barrel", -12.5f, 8.7f), ("Crate", 6.4f, 8.5f), ("Crate", 5.5f, 8.4f), ("Chest", -13.0f, 20.3f),
        // boss (arch x -6..-3): torches, banners at the gate, hoards in the top corners, two pillars
        ("Torch", -10.5f, 42.15f), ("Torch", 2.5f, 42.15f), ("Banner", -7.0f, 42.1f), ("Banner", -1.0f, 42.1f),
        ("GoldPileA", -12.0f, 40.2f), ("GoldPileC", 3.0f, 40.3f), ("Pillar", -9.5f, 33.0f), ("Pillar", 0.5f, 33.0f),
        // reference room (arch x 47..50)
        ("Torch", 45.5f, 9.15f), ("Torch", 52.5f, 9.15f), ("Barrel", 44.6f, 2.4f), ("Crate", 45.6f, 2.5f), ("Chest", 52.5f, 2.6f),
    };

    static bool NearProp(Vector2Int c)
    {
        var center = new Vector2(c.x + 0.5f, c.y + 0.5f);
        foreach (var (_, x, y) in PropList)
        {
            if (Vector2.Distance(center, new Vector2(x, y + 0.4f)) < 2f) return true;
        }

        return false;
    }

    static void PlaceProps(Tilemap props, List<RectInt> archRects)
    {
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
    }
}
