using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Paints Level_01 with the Craftpix dungeon pieces from code (Tools/Level/Paint Level_01), so it can be regenerated.
// Everything that is not floor is rock; each rock cell picks its piece the way the pack's preview is built:
//   face    rock with floor 1-4 cells below it: the 4-row wall_1 sheet (stone cap on top, cobble, ragged bottom edge);
//           a lower wall uses fewer rows of the same sheet (cap + bottom edge stay)
//   column  rock beside floor, or beside a face, or diagonal to floor: the 1-wide stone block column (wall_3)
//   cap     rock with floor right above it (a wall seen from the room above): the stone cap strip (wall_4)
//   top     everything else: dark cobble "top of the rock" (solid like the rest, never reachable)
// Floor under a face gets the pack's shadow cells. Rooms keep their Room trigger area; stubs, alcoves and props break
// up the boxes. Clears Ground / Decor / Walls / WallTop / Backdrop / Props first.
public static class LevelPainter
{
    const string TileFolder = "Assets/Art/Environment/TopDown/Tiles";
    const string PropFolder = "Assets/Prefabs/Environment/Props";
    const int FaceRows = 4, BackdropPad = 10;
    static readonly Color BossTint = new(1f, 0.72f, 0.74f, 1f);

    struct R
    {
        public int x0, y0, x1, y1;
        public R(int x0, int y0, int x1, int y1) { this.x0 = x0; this.y0 = y0; this.x1 = x1; this.y1 = y1; }
        public bool Contains(Vector2Int c) => c.x >= x0 && c.x <= x1 && c.y >= y0 && c.y <= y1;
    }

    // Floor: room rims (room trigger + 1 cell), corridors, alcoves. Inclusive cell rects.
    static readonly R[] Rooms = { new(-6, -4, 5, 3), new(10, -6, 27, 5), new(12, 10, 25, 19), new(-14, 8, 7, 21), new(-14, 26, 5, 41) };
    static readonly R[] Corridors = { new(5, -1, 10, 0), new(18, 5, 19, 10), new(7, 14, 12, 15), new(-4, 21, -3, 26) };
    static readonly R[] Alcoves =
    {
        new(-9, -2, -7, 0), new(12, 6, 15, 7), new(20, -9, 24, -7), new(15, 20, 18, 22), new(26, 13, 28, 16),
        new(-12, 22, -8, 23), new(-17, 32, -15, 36), new(6, 30, 8, 34), new(-9, 42, -6, 44),
    };

    // Rock cut back into the floor: wall stubs from the side walls (3 rows: a short wall with a column at its free
    // end) and 1-wide block columns standing on the bottom walls.
    static readonly R[] Solids =
    {
        new(24, -1, 27, 1), new(15, -6, 15, -4),
        new(22, 11, 25, 13), new(15, 10, 15, 12),
        new(-14, 14, -10, 16), new(3, 17, 7, 19), new(-9, 8, -9, 10), new(1, 8, 1, 10),
        new(-10, 26, -10, 28), new(1, 26, 1, 28),
    };

    // Arch gates (bottom-left cell of the 4 x 4 gate) set into a 4 row face.
    static readonly Vector2Int[] Arches = { new(-4, 4), new(21, 20), new(0, 42) };

    enum Kind { Floor, Face, Column, Cap, Top }

    // Red theme: the boss room block and its entrance corridor (room 3's top wall stays grey as one piece).
    static bool IsBoss(int x, int y) => (y >= 26 && x <= 10) || (y >= 22 && x >= -5 && x <= -2);

    static int Mod4(int v) => ((v % 4) + 4) % 4;

    static TileBase T(string name) => AssetDatabase.LoadAssetAtPath<TileBase>($"{TileFolder}/{name}.asset");

    // A wall piece, red-tinted inside the boss area (tinted copies are created next to the originals).
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

    static TileBase TopRock(int x, int y, bool boss)
    {
        int i = 4 + Mod4(y) % 2 * 4 + Mod4(x);
        string name = $"BD_{i}{(boss ? "_boss" : "")}";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>($"{TileFolder}/{name}.asset");
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, $"{TileFolder}/{name}.asset");
        }

        tile.colliderType = Tile.ColliderType.Grid;
        tile.sprite = ((Tile)T($"WL_8_{i}")).sprite;
        tile.color = boss ? new Color(0.42f, 0.24f, 0.26f) : new Color(0.36f, 0.36f, 0.42f);
        EditorUtility.SetDirty(tile);
        return tile;
    }

    static void Add(HashSet<Vector2Int> set, R r, bool remove = false)
    {
        for (int x = r.x0; x <= r.x1; x++)
        {
            for (int y = r.y0; y <= r.y1; y++)
            {
                if (remove) set.Remove(new Vector2Int(x, y)); else set.Add(new Vector2Int(x, y));
            }
        }
    }

    public static HashSet<Vector2Int> BuildFloor()
    {
        var floor = new HashSet<Vector2Int>();
        foreach (R r in Rooms) Add(floor, r);
        foreach (R r in Corridors) Add(floor, r);
        foreach (R r in Alcoves) Add(floor, r);
        foreach (R r in Solids) Add(floor, r, true);
        return floor;
    }

    // Face rows: distance k to the floor below (through rock) and the height of that wall (rock run above the floor, max 4).
    static bool FaceInfo(HashSet<Vector2Int> floor, Vector2Int c, out int k, out int height)
    {
        k = 0;
        height = 0;
        for (int d = 1; d <= FaceRows; d++)
        {
            if (floor.Contains(new Vector2Int(c.x, c.y - d)))
            {
                k = d;
                break;
            }
        }

        if (k == 0) return false;
        int run = 0;
        while (run < FaceRows && !floor.Contains(new Vector2Int(c.x, c.y - k + 1 + run))) run++;
        height = run;
        return true;
    }

    // Row of wall_1 for face row k (1 = bottom) of a wall `height` rows tall: always the cap on top and the ragged
    // bottom edge at the floor, cobble rows in between.
    static int FaceSheetRow(int k, int height)
    {
        if (k == height) return 0;
        if (k == 1) return 3;
        return height == 4 ? 4 - k : 1;
    }

    static Kind Classify(HashSet<Vector2Int> floor, Vector2Int c, out int sheetRow)
    {
        sheetRow = 0;
        if (floor.Contains(c)) return Kind.Floor;
        bool F(int dx, int dy) => floor.Contains(new Vector2Int(c.x + dx, c.y + dy));
        if (F(-1, 0) || F(1, 0)) return Kind.Column;
        if (FaceInfo(floor, c, out int k, out int h))
        {
            sheetRow = FaceSheetRow(k, h);
            return Kind.Face;
        }

        if (F(0, 1)) return Kind.Cap;
        foreach (int dx in new[] { -1, 1 })
        {
            var side = new Vector2Int(c.x + dx, c.y);
            if (!floor.Contains(side) && FaceInfo(floor, side, out _, out _)) return Kind.Column;
        }

        if (F(-1, -1) || F(1, -1) || F(-1, 1) || F(1, 1)) return Kind.Column;
        return Kind.Top;
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
        Tilemap wallTop = Map(grid, "WallTop", 20), backdrop = Map(grid, "Backdrop", -92), props = Map(grid, "Props", 0);
        backdrop.color = Color.white;
        foreach (Tilemap m in new[] { ground, decor, walls, wallTop, backdrop })
        {
            m.ClearAllTiles();
        }

        for (int i = props.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(props.transform.GetChild(i).gameObject);
        }

        HashSet<Vector2Int> floor = BuildFloor();
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (Vector2Int f in floor)
        {
            minX = Mathf.Min(minX, f.x); maxX = Mathf.Max(maxX, f.x);
            minY = Mathf.Min(minY, f.y); maxY = Mathf.Max(maxY, f.y);
        }

        var kinds = new Dictionary<Vector2Int, Kind>();
        var rows = new Dictionary<Vector2Int, int>();
        int bx0 = minX - BackdropPad, bx1 = maxX + BackdropPad, by0 = minY - BackdropPad, by1 = maxY + BackdropPad;
        for (int x = bx0; x <= bx1; x++)
        {
            for (int y = by0; y <= by1; y++)
            {
                var c = new Vector2Int(x, y);
                kinds[c] = Classify(floor, c, out int row);
                rows[c] = row;
            }
        }

        FillNarrowTops(kinds);
        DarkenShadows();

        var wPos = new List<Vector3Int>(); var wTiles = new List<TileBase>();
        var gPos = new List<Vector3Int>(); var gTiles = new List<TileBase>();
        for (int x = bx0; x <= bx1; x++)
        {
            for (int y = by0; y <= by1; y++)
            {
                var c = new Vector2Int(x, y);
                var p = new Vector3Int(x, y, 0);
                bool boss = IsBoss(x, y);
                Kind kind = kinds[c];
                int row = rows[c];
                int cell = (3 - Mod4(y)) * 4 + Mod4(x);
                switch (kind)
                {
                    case Kind.Floor:
                        bool shadow = !floor.Contains(c + Vector2Int.up);
                        gPos.Add(p);
                        gTiles.Add(T($"CP_{(shadow ? "shadow" : "floor")}_{cell}{(boss ? "_boss" : "")}"));
                        continue;
                    case Kind.Face:
                        wPos.Add(p); wTiles.Add(Piece($"WL_1_{row * 4 + Mod4(x)}", boss));
                        break;
                    case Kind.Column:
                        wPos.Add(p); wTiles.Add(Piece($"WL_3_{3 - Mod4(y)}", boss));
                        break;
                    case Kind.Cap:
                        wPos.Add(p); wTiles.Add(Piece($"WL_4_{Mod4(x)}", boss));
                        break;
                    case Kind.Top:
                        // On the Walls map too: one solid mass with only floor holes keeps the composite collider
                        // simple (separate rock-top islands produced composite paths that dropped wall cells).
                        wPos.Add(p); wTiles.Add(TopRock(x, y, boss));
                        continue;
                }

                // Walls stand on floor: their transparent edges show floor, not the dark rock top.
                gPos.Add(p);
                gTiles.Add(T($"CP_floor_{cell}{(boss ? "_boss" : "")}"));
            }
        }

        walls.SetTiles(wPos.ToArray(), wTiles.ToArray());
        ground.SetTiles(gPos.ToArray(), gTiles.ToArray());

        PaintArches(walls, floor);
        var rng = new System.Random(1234);
        PaintDecals(decor, floor, rng);
        PlaceWallDressing(props, floor, kinds, rng);
        PlaceClusters(props);

        LevelPaintSetup.PaintSampleRoom(Map(grid, "Ground", -100), walls, decor, props);

        var player = Object.FindFirstObjectByType<PlayerController2D>();
        if (player != null)
        {
            player.transform.position = Vector3.zero;
        }

        // With the default offset the composite's convex decomposition dropped a cell next to an alcove; a slightly
        // larger offset closes it.
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
        Debug.Log($"LevelPainter: floor {floor.Count}, walls + rock {wPos.Count}.");
    }

    // Slivers of rock top (1-2 cells) between two walls read as holes: widen the columns across them, and pave short
    // vertical gaps with the cap strip, so the rock between neighbouring rooms reads as one thick wall.
    static void FillNarrowTops(Dictionary<Vector2Int, Kind> kinds)
    {
        var snapshot = new Dictionary<Vector2Int, Kind>(kinds);
        bool Solid(Vector2Int c) => snapshot.TryGetValue(c, out Kind k) && k != Kind.Top && k != Kind.Floor;
        int Gap(Vector2Int c, Vector2Int dir)
        {
            for (int d = 1; d <= 3; d++)
            {
                var n = c + dir * d;
                if (!snapshot.TryGetValue(n, out Kind k)) return 99;
                if (k != Kind.Top) return Solid(n) ? d - 1 : 99;
            }

            return 99;
        }

        foreach (var kv in snapshot)
        {
            if (kv.Value != Kind.Top) continue;
            Vector2Int c = kv.Key;
            int left = Gap(c, Vector2Int.left), right = Gap(c, Vector2Int.right);
            int down = Gap(c, Vector2Int.down), up = Gap(c, Vector2Int.up);
            if (left < 99 && right < 99 && left + right + 1 <= 2) kinds[c] = Kind.Column;
            else if (down < 99 && up < 99 && down + up + 1 <= 2) kinds[c] = Kind.Cap;
        }
    }

    // The pack's shadow cells (floor under a wall face) a touch darker and less blue than the first dressing pass.
    static void DarkenShadows()
    {
        for (int i = 0; i < 16; i++)
        {
            foreach (bool boss in new[] { false, true })
            {
                var tile = AssetDatabase.LoadAssetAtPath<Tile>($"{TileFolder}/CP_shadow_{i}{(boss ? "_boss" : "")}.asset");
                if (tile == null) continue;
                Color c = new(0.7f, 0.68f, 0.72f, 1f);
                tile.color = boss ? c * BossTint : c;
                EditorUtility.SetDirty(tile);
            }
        }
    }

    static void PaintArches(Tilemap walls, HashSet<Vector2Int> floor)
    {
        Transform old = walls.transform.Find("ArchBlockers");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var blockers = new GameObject("ArchBlockers");
        blockers.transform.SetParent(walls.transform, false);
        blockers.layer = walls.gameObject.layer;
        foreach (Vector2Int a in Arches)
        {
            for (int i = 0; i < 16; i++)
            {
                int col = i % 4, row = i / 4;
                TileBase tile = T(col < 2 ? $"WL_10_{i}" : $"WL_11_{i}");
                if (tile != null) walls.SetTile(new Vector3Int(a.x + col, a.y + 3 - row, 0), tile);
            }

            // The arch tiles carry no collider; this keeps the gate solid.
            var box = blockers.AddComponent<BoxCollider2D>();
            box.size = new Vector2(4f, 4f);
            box.offset = new Vector2(a.x + 2f, a.y + 2f);
        }
    }

    static bool NearCorridor(Vector2Int c, int margin)
    {
        foreach (R r in Corridors)
        {
            if (c.x >= r.x0 - margin && c.x <= r.x1 + margin && c.y >= r.y0 - margin && c.y <= r.y1 + margin) return true;
        }

        return false;
    }

    static bool NearArch(Vector2Int c, int margin)
    {
        foreach (Vector2Int a in Arches)
        {
            if (c.x >= a.x - margin && c.x <= a.x + 3 + margin && c.y >= a.y - 1 - margin && c.y <= a.y + 4) return true;
        }

        return false;
    }

    static readonly string[] Cracks = { "crack_a", "crack_b" };
    static readonly string[] Pebbles = { "pebble_1", "pebble_2", "pebble_3", "pebble_4", "pebble_5" };
    static readonly string[] Moss = { "moss_1", "moss_2", "moss_3", "moss_4", "moss_5" };
    static readonly string[] Slime = { "slime_a", "slime_b", "slime_c" };

    static void PaintDecals(Tilemap decor, HashSet<Vector2Int> floor, System.Random rng)
    {
        for (int r = 0; r < Rooms.Length; r++)
        {
            R room = Rooms[r];
            bool boss = r == 4, rest = r == 0;
            int area = (room.x1 - room.x0 + 1) * (room.y1 - room.y0 + 1);
            var plan = new List<string[]> ();
            for (int i = 0; i < area / 22; i++) plan.Add(Cracks);
            for (int i = 0; i < area / 30; i++) plan.Add(Pebbles);
            for (int i = 0; i < area / 60; i++) plan.Add(Moss);
            if (!rest) for (int i = 0; i < 1 + area / 140; i++) plan.Add(Slime);
            foreach (string[] set in plan)
            {
                for (int tries = 0; tries < 12; tries++)
                {
                    var c = new Vector2Int(rng.Next(room.x0 + 1, room.x1), rng.Next(room.y0 + 1, room.y1));
                    if (!floor.Contains(c) || NearCorridor(c, 1) || NearCluster(c) || decor.GetTile(new Vector3Int(c.x, c.y, 0)) != null) continue;
                    if (!floor.Contains(c + Vector2Int.left) || !floor.Contains(c + Vector2Int.right)) continue;
                    decor.SetTile(new Vector3Int(c.x, c.y, 0), T("DC_" + set[rng.Next(set.Length)]));
                    break;
                }
            }
        }

        // Loose treasure only around the boss room's hoards.
        foreach (var (name, x, y) in new[] { ("gem_red", -5, 39), ("gem_blue", -2, 39), ("trophy", -4, 38), ("gem_gold", 3, 39), ("gem_purple", 5, 39), ("key_a", -11, 39), ("gem_green", -13, 40) })
        {
            if (floor.Contains(new Vector2Int(x, y))) decor.SetTile(new Vector3Int(x, y, 0), T("DC_" + name));
        }
    }

    static void Spawn(Tilemap props, string name, float x, float y)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropFolder}/{name}.prefab");
        if (prefab == null)
        {
            Debug.LogWarning("LevelPainter: missing prop " + name);
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, props.transform);
        instance.transform.position = new Vector3(x, y, 0f);
    }

    // Along the foot of every tall face (as in the preview): torches on the face, coral and the odd boulder at its base.
    static void PlaceWallDressing(Tilemap props, HashSet<Vector2Int> floor, Dictionary<Vector2Int, Kind> kinds, System.Random rng)
    {
        string[] coral = { "CoralA", "CoralB", "CoralC", "CoralD", "CoralBush" };
        string[] boulders = { "RockA", "RockB", "RockC" };
        var faceFoot = new List<Vector2Int>();
        foreach (Vector2Int f in floor)
        {
            var up = f + Vector2Int.up;
            if (kinds.TryGetValue(up, out Kind k) && k == Kind.Face && kinds.TryGetValue(up + Vector2Int.up, out Kind k2) && k2 == Kind.Face)
            {
                faceFoot.Add(f);
            }
        }

        faceFoot.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
        int sinceTorch = 3;
        Vector2Int prev = new(int.MinValue, int.MinValue);
        foreach (Vector2Int f in faceFoot)
        {
            if (f.y != prev.y || f.x != prev.x + 1) sinceTorch = 3;
            prev = f;
            sinceTorch++;
            if (NearArch(f, 0) || NearCorridor(f, 1)) continue;
            bool insideRoom = false;
            foreach (R room in Rooms) insideRoom |= room.Contains(f);
            if (sinceTorch >= 6 && floor.Contains(f + Vector2Int.left) && floor.Contains(f + Vector2Int.right))
            {
                Spawn(props, "Torch", f.x + 0.5f, f.y + 1.1f);
                sinceTorch = 0;
                continue;
            }

            double roll = rng.NextDouble();
            if (roll < 0.3) Spawn(props, coral[rng.Next(coral.Length)], f.x + 0.5f + (float)(rng.NextDouble() - 0.5) * 0.4f, f.y + 0.8f);
            else if (roll < 0.38 && insideRoom && !NearCorridor(f, 3) && floor.Contains(f + Vector2Int.down * 2)) Spawn(props, boulders[rng.Next(boulders.Length)], f.x + 0.5f, f.y + 0.25f);
        }
    }

    // Hand placed groups: storage corners, chests in alcoves, pillars, the boss hoard and banners at the gates.
    static readonly (string name, float x, float y)[] Clusters =
        {
            // rest room: campfire, a sign, supplies
            ("Campfire", 2.5f, -1.6f), ("Sign", -4.0f, 1.2f), ("Barrel", 4.3f, -3.4f), ("Crate", 3.3f, -3.5f), ("Planks", -1.0f, -3.5f), ("Chest", -8.0f, -1.6f),
            ("Banner", -5.0f, 4.1f), ("Banner", 1.0f, 4.1f),
            // room 1: storage in the south-west, chest in the south alcove, stub with barrels
            ("Barrel", 10.5f, -5.6f), ("BarrelBig", 11.6f, -5.5f), ("Crate", 10.6f, -4.5f), ("Chest", 22.0f, -8.6f), ("BarrelGreen", 23.4f, -8.5f),
            ("Pillar", 15.5f, 0.2f), ("Stump", 20.5f, 2.6f), ("Urn", 26.6f, 2.4f), ("GoldPileB", 13.5f, 6.2f),
            // room 2
            ("Crate", 12.6f, 18.4f), ("Crate", 13.5f, 18.6f), ("Barrel", 12.5f, 17.5f), ("Chest", 27.5f, 15.4f), ("Pillar", 19.0f, 15.5f),
            ("BrickStackC", 16.5f, 21.2f), ("Banner", 20.0f, 20.1f), ("Banner", 26.0f, 20.1f),
            // room 3
            ("Chest", -10.0f, 22.4f), ("Barrel", -13.5f, 8.4f), ("Barrel", -12.6f, 8.6f), ("Crate", -13.4f, 9.4f), ("Crate", 6.3f, 8.4f),
            ("BarrelBig", 5.2f, 8.5f), ("Pillar", -6.0f, 12.0f), ("Pillar", -1.0f, 13.0f), ("BrickStackA", 3.5f, 11.0f), ("RockTall", -12.0f, 19.5f),
            ("Pitchfork", 6.5f, 20.3f), ("Bowl", -11.5f, 22.4f),
            // boss: two rows of pillars, hoard by the gate, chests in the side alcoves
            ("Pillar", -9.5f, 30.0f), ("Pillar", 0.5f, 30.0f), ("Pillar", -9.5f, 37.0f), ("Pillar", 0.5f, 37.0f),
            ("GoldPileA", -3.5f, 40.2f), ("GoldPileC", 4.0f, 40.4f), ("GoldPileB", -12.0f, 40.3f), ("ChestGold", -16.0f, 35.6f), ("ChestBlack", 7.5f, 33.6f),
            ("Banner", -1.0f, 42.1f), ("Banner", 5.0f, 42.1f), ("RuinWall", -12.0f, 27.2f), ("BrickStackB", 4.2f, 27.0f),
        };

    // Keeps random decals from sliding under the hand placed props.
    static bool NearCluster(Vector2Int c)
    {
        var center = new Vector2(c.x + 0.5f, c.y + 0.5f);
        foreach (var (_, x, y) in Clusters)
        {
            if (Vector2.Distance(center, new Vector2(x, y + 0.4f)) < 1.8f) return true;
        }

        return false;
    }

    static void PlaceClusters(Tilemap props)
    {
        foreach (var (name, x, y) in Clusters)
        {
            Spawn(props, name, x, y);
        }
    }
}
