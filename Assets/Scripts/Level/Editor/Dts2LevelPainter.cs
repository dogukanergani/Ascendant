using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

// Level_01 with 0x72's DungeonTileset II (Assets/Art/DungeonTilesetII, CC0, 16 px = 1 unit): Tools/Level/Paint Level_01 (DTS2).
// One plain template for every room, the way the pack draws walls: one cell thick, a dark top outlined by the light
// stone cap, and a brick face on every wall that has floor in front of it (south):
//   top wall     face row (wall_mid, banners) on the floor's top edge, cap row above it (wall_top_mid, corners
//                wall_top_left / wall_top_right)
//   side walls   wall_edge_mid_left / _right: dark top with the cap line on the outer side
//   bottom wall  cap row (wall_top_mid) below the floor, its outer brick face below that (wall_mid, ends wall_left /
//                wall_right), corners wall_edge_bottom_left / _right
// Outside is the camera background (the pack's near-black). Collision is a separate invisible map covering every
// non-floor cell next to the floor. The older Craftpix grids stay in the scene, switched off, as backups.
public static class Dts2LevelPainter
{
    const string Frames = DungeonTilesetIIImporter.Folder + "frames/";
    const string TileFolder = "Assets/Art/Environment/DTS2/Tiles";
    const string PropFolder = "Assets/Prefabs/Environment/DTS2";
    const string PaletteFolder = "Assets/Art/Environment/TopDown/Palettes";
    const string GridName = "Grid_DTS2";
    const int WallLayer = 11;
    static readonly Color Background = new(0.129f, 0.118f, 0.141f, 1f);
    static readonly Color BossTint = new(1f, 0.78f, 0.78f, 1f);

    struct R
    {
        public int x0, y0, x1, y1;
        public R(int x0, int y0, int x1, int y1) { this.x0 = x0; this.y0 = y0; this.x1 = x1; this.y1 = y1; }
        public bool Contains(Vector2Int c) => c.x >= x0 && c.x <= x1 && c.y >= y0 && c.y <= y1;
        public int CenterX => (x0 + x1 + 1) / 2;
    }

    // Same rects as the other painters: Room trigger + 1 cell, 2 cell corridors through the walls at the Door objects.
    static readonly R[] Rooms = { new(-6, -4, 5, 3), new(10, -6, 27, 5), new(12, 10, 25, 19), new(-14, 8, 7, 21), new(-14, 26, 5, 41) };
    static readonly R[] Corridors = { new(5, -1, 10, 0), new(18, 5, 19, 10), new(7, 14, 12, 15), new(-4, 21, -3, 26) };
    const int BossRoom = 4;

    static bool IsBoss(Vector2Int c) => c.y >= 24 && c.x >= -16 && c.x <= 7;

    // ---- tiles / sprites ----

    static readonly Dictionary<string, Tile> Cache = new();

    static Sprite Sprite(string frame)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>($"{Frames}{frame}.png");
        if (s == null) throw new System.Exception("Dts2LevelPainter: missing frame " + frame);
        return s;
    }

    public static Tile T(string frame)
    {
        if (Cache.TryGetValue(frame, out Tile t) && t != null) return t;
        string path = $"{TileFolder}/{frame}.asset";
        t = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (t == null)
        {
            t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = Sprite(frame);
            t.colliderType = Tile.ColliderType.None;
            t.flags = TileFlags.None; // lets the boss room be tinted per cell
            AssetDatabase.CreateAsset(t, path);
        }

        Cache[frame] = t;
        return t;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // ---- classification ----

    enum K { Void, Floor, Face, Cap, BottomCap, BottomFace, LeftWall, RightWall, TopLeft, TopRight, FaceLeftEnd, FaceRightEnd, BottomLeft, BottomRight, BottomFaceLeft, BottomFaceRight }

    static K Classify(HashSet<Vector2Int> floor, Vector2Int c)
    {
        bool F(int dx, int dy) => floor.Contains(new Vector2Int(c.x + dx, c.y + dy));
        if (F(0, 0)) return K.Floor;
        if (F(0, -1)) return K.Face;
        if (F(0, -2)) return K.Cap;
        if (F(0, 1)) return K.BottomCap;
        if (F(0, 2)) return K.BottomFace;
        if (F(1, 0)) return K.LeftWall;
        if (F(-1, 0)) return K.RightWall;
        // Corners: the cell beside a face / cap / bottom row continues the side wall into it.
        if (F(1, -1)) return K.FaceLeftEnd;
        if (F(-1, -1)) return K.FaceRightEnd;
        if (F(1, -2)) return K.TopLeft;
        if (F(-1, -2)) return K.TopRight;
        if (F(1, 1)) return K.BottomLeft;
        if (F(-1, 1)) return K.BottomRight;
        if (F(1, 2)) return K.BottomFaceLeft;
        if (F(-1, 2)) return K.BottomFaceRight;
        return K.Void;
    }

    static string WallFrame(K k) => k switch
    {
        K.Face => "wall_mid",
        K.Cap => "wall_top_mid",
        K.BottomCap => "wall_top_mid",
        K.BottomFace => "wall_mid",
        K.LeftWall => "wall_edge_mid_left",
        K.RightWall => "wall_edge_mid_right",
        K.FaceLeftEnd => "wall_edge_mid_left",
        K.FaceRightEnd => "wall_edge_mid_right",
        K.TopLeft => "wall_top_left",
        K.TopRight => "wall_top_right",
        K.BottomLeft => "wall_edge_bottom_left",
        K.BottomRight => "wall_edge_bottom_right",
        K.BottomFaceLeft => "wall_left",
        K.BottomFaceRight => "wall_right",
        _ => null,
    };

    // ---- painting ----

    [MenuItem("Tools/Level/Paint Level_01 (DTS2)")]
    public static void Paint()
    {
        EnsureFolder(TileFolder);
        EnsureFolder(PropFolder);
        Cache.Clear();
        BuildProps();

        var floor = new HashSet<Vector2Int>();
        foreach (R r in Rooms) Add(floor, r);
        foreach (R r in Corridors) Add(floor, r);

        Transform grid = EnsureGrid(out Tilemap ground, out Tilemap walls, out Tilemap wallFront, out Tilemap decor, out Tilemap collision, out Transform props);
        foreach (Tilemap m in new[] { ground, walls, wallFront, decor, collision }) m.ClearAllTiles();
        for (int i = props.childCount - 1; i >= 0; i--) Object.DestroyImmediate(props.GetChild(i).gameObject);

        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (Vector2Int f in floor)
        {
            minX = Mathf.Min(minX, f.x); maxX = Mathf.Max(maxX, f.x);
            minY = Mathf.Min(minY, f.y); maxY = Mathf.Max(maxY, f.y);
        }

        var kinds = new Dictionary<Vector2Int, K>();
        var solid = new List<Vector3Int>();
        Tile solidTile = CollisionTile();
        for (int x = minX - 3; x <= maxX + 3; x++)
        {
            for (int y = minY - 3; y <= maxY + 3; y++)
            {
                var c = new Vector2Int(x, y);
                var p = new Vector3Int(x, y, 0);
                K k = Classify(floor, c);
                kinds[c] = k;
                if (k == K.Floor)
                {
                    Put(ground, p, FloorFrame(c));
                    continue;
                }

                string frame = WallFrame(k);
                if (frame == null) continue;
                Put(walls, p, frame);
                if (Near(floor, c)) solid.Add(p);
            }
        }

        var solids = new TileBase[solid.Count];
        for (int i = 0; i < solids.Length; i++) solids[i] = solidTile;
        collision.SetTiles(solid.ToArray(), solids);

        Dress(walls, decor, props, kinds, floor);
        DressDoors();

        if (Camera.main != null) Camera.main.backgroundColor = Background;
        var composite = collision.GetComponent<CompositeCollider2D>();
        composite.offsetDistance = 0.005f;
        var tileCollider = collision.GetComponent<TilemapCollider2D>();
        tileCollider.enabled = false;
        tileCollider.enabled = true;
        tileCollider.ProcessTilemapChanges();
        composite.GenerateGeometry();

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log($"Dts2LevelPainter: floor {floor.Count}, solid {solid.Count}.");
    }

    static void Add(HashSet<Vector2Int> set, R r)
    {
        for (int x = r.x0; x <= r.x1; x++)
            for (int y = r.y0; y <= r.y1; y++)
                set.Add(new Vector2Int(x, y));
    }

    static bool Near(HashSet<Vector2Int> floor, Vector2Int c)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (floor.Contains(new Vector2Int(c.x + dx, c.y + dy))) return true;
        return false;
    }

    static int Hash(Vector2Int c) => Mathf.Abs((c.x * 73856093) ^ (c.y * 19349663) ^ 0x5bd1e995);

    // Mostly the plain slab, a cracked variant now and then (more in the boss room).
    static string FloorFrame(Vector2Int c)
    {
        int h = Hash(c) % 100;
        int crackedShare = IsBoss(c) ? 20 : 6;
        return h < crackedShare ? $"floor_{2 + h % 7}" : "floor_1";
    }

    static void Put(Tilemap map, Vector3Int p, string frame)
    {
        map.SetTile(p, T(frame));
        if (IsBoss(new Vector2Int(p.x, p.y))) map.SetColor(p, BossTint);
    }

    static Tile CollisionTile()
    {
        string path = $"{TileFolder}/DTS2_Collision.asset";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, path);
        }

        tile.sprite = Sprite("wall_mid"); // any sprite; the collision map's renderer is off
        tile.colliderType = Tile.ColliderType.Grid;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    static Transform EnsureGrid(out Tilemap ground, out Tilemap walls, out Tilemap wallFront, out Tilemap decor, out Tilemap collision, out Transform props)
    {
        // Earlier looks stay in the scene as switched-off backups.
        foreach (string old in new[] { "Grid", "Grid_PixelDungeon" })
        {
            GameObject g = FindInScene(old);
            if (g != null) g.SetActive(false);
        }

        GameObject gridGo = FindInScene(GridName);
        if (gridGo == null)
        {
            gridGo = new GameObject(GridName);
            gridGo.AddComponent<Grid>();
        }

        gridGo.SetActive(true);
        gridGo.GetComponent<Grid>().cellSize = Vector3.one;
        Transform t = gridGo.transform;
        ground = Layer(t, "DTS2_Ground", -100);
        decor = Layer(t, "DTS2_Decor", -98);
        walls = Layer(t, "DTS2_Walls", -90);
        wallFront = Layer(t, "DTS2_WallFront", 20);
        collision = Layer(t, "DTS2_Collision", 0);
        GameObject col = collision.gameObject;
        col.GetComponent<TilemapRenderer>().enabled = false;
        col.layer = WallLayer;
        if (col.GetComponent<Rigidbody2D>() == null) col.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        if (col.GetComponent<TilemapCollider2D>() == null) col.AddComponent<TilemapCollider2D>().compositeOperation = Collider2D.CompositeOperation.Merge;
        if (col.GetComponent<CompositeCollider2D>() == null) col.AddComponent<CompositeCollider2D>().geometryType = CompositeCollider2D.GeometryType.Polygons;
        if (col.GetComponent<TilemapColliderRefresh>() == null) col.AddComponent<TilemapColliderRefresh>();

        props = t.Find("DTS2_Props");
        if (props == null)
        {
            props = new GameObject("DTS2_Props").transform;
            props.SetParent(t, false);
        }

        return t;
    }

    static GameObject FindInScene(string name)
    {
        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == name) return root;
        }

        return null;
    }

    static Tilemap Layer(Transform grid, string name, int order)
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

    // ---- props ----

    // name, frames (animated when more than one), collider width / height at the feet (0 = none), sorting order.
    static readonly (string name, string[] frames, float colW, float colH, int order)[] PropDefs =
    {
        ("Crate", new[] { "crate" }, 0.8f, 0.5f, 10),
        ("Column", new[] { "column" }, 0.7f, 0.5f, 10),
        ("Chest", new[] { "chest_full_open_anim_f0" }, 0.8f, 0.4f, 10),
        ("ChestEmpty", new[] { "chest_empty_open_anim_f0" }, 0.8f, 0.4f, 10),
        ("Skull", new[] { "skull" }, 0f, 0f, -97),
        ("Spikes", new[] { "floor_spikes_anim_f0", "floor_spikes_anim_f1", "floor_spikes_anim_f2", "floor_spikes_anim_f3", "floor_spikes_anim_f3", "floor_spikes_anim_f2", "floor_spikes_anim_f1", "floor_spikes_anim_f0" }, 0f, 0f, -97),
        ("FountainMidBlue", new[] { "wall_fountain_mid_blue_anim_f0", "wall_fountain_mid_blue_anim_f1", "wall_fountain_mid_blue_anim_f2" }, 0f, 0f, -89),
        ("FountainBasinBlue", new[] { "wall_fountain_basin_blue_anim_f0", "wall_fountain_basin_blue_anim_f1", "wall_fountain_basin_blue_anim_f2" }, 0f, 0f, -89),
        ("FountainMidRed", new[] { "wall_fountain_mid_red_anim_f0", "wall_fountain_mid_red_anim_f1", "wall_fountain_mid_red_anim_f2" }, 0f, 0f, -89),
        ("FountainBasinRed", new[] { "wall_fountain_basin_red_anim_f0", "wall_fountain_basin_red_anim_f1", "wall_fountain_basin_red_anim_f2" }, 0f, 0f, -89),
    };

    static void BuildProps()
    {
        foreach (var (name, frames, colW, colH, order) in PropDefs)
        {
            string path = $"{PropFolder}/{name}.prefab";
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite(frames[0]);
            sr.sortingOrder = order;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            if (frames.Length > 1)
            {
                var anim = go.AddComponent<SpriteFrameAnimator>();
                var so = new SerializedObject(anim);
                SerializedProperty list = so.FindProperty("frames");
                list.arraySize = frames.Length;
                for (int i = 0; i < frames.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = Sprite(frames[i]);
                so.FindProperty("framesPerSecond").floatValue = name == "Spikes" ? 6f : 8f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (colW > 0f)
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.size = new Vector2(colW, colH);
                box.offset = new Vector2(0f, colH * 0.5f);
            }

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }
    }

    static void Spawn(Transform parent, string prop, float x, float y, bool boss = false)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropFolder}/{prop}.prefab");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.position = new Vector3(x, y, 0f);
        if (boss) go.GetComponent<SpriteRenderer>().color = BossTint;
    }

    // Per room: banners and a fountain on the top wall (symmetric), a skull or two, props in the bottom corners.
    static void Dress(Tilemap walls, Tilemap decor, Transform props, Dictionary<Vector2Int, K> kinds, HashSet<Vector2Int> floor)
    {
        for (int r = 0; r < Rooms.Length; r++)
        {
            R room = Rooms[r];
            bool boss = r == BossRoom;
            int cx = room.CenterX;
            int faceY = room.y1 + 1;
            bool FaceAt(int x) => kinds.TryGetValue(new Vector2Int(x, faceY), out K k) && k == K.Face
                && kinds.TryGetValue(new Vector2Int(x - 1, faceY), out K l) && l == K.Face
                && kinds.TryGetValue(new Vector2Int(x + 1, faceY), out K n) && n == K.Face;

            // Fountain in the middle of the top wall (rooms without a corridor there): top on the cap, spout on
            // the face, basin on the floor in front.
            if (FaceAt(cx))
            {
                Put(walls, new Vector3Int(cx, faceY + 1, 0), "wall_fountain_top_1");
                Spawn(props, boss ? "FountainMidRed" : "FountainMidBlue", cx + 0.5f, faceY + 0.5f, boss);
                Spawn(props, boss ? "FountainBasinRed" : "FountainBasinBlue", cx + 0.5f, room.y1 + 0.5f, boss);
            }

            // Banners left and right, symmetric about the middle.
            string banner = boss ? "wall_banner_red" : r % 2 == 0 ? "wall_banner_blue" : "wall_banner_green";
            int spread = Mathf.Max(3, (room.x1 - room.x0) / 4);
            foreach (int bx in new[] { cx - spread, cx + spread - 1 })
            {
                if (FaceAt(bx)) Put(walls, new Vector3Int(bx, faceY, 0), banner);
            }

            if (boss)
            {
                // Goo dripping from the boss room's wall, between banner and fountain.
                foreach (int gx in new[] { cx - spread / 2 - 1, cx + spread / 2 })
                {
                    if (FaceAt(gx)) Put(walls, new Vector3Int(gx, faceY, 0), "wall_goo");
                }
            }

            // Props: crates + chest in the bottom left, a column pair flanking the room, a skull.
            float left = room.x0 + 0.5f, right = room.x1 + 0.5f, bottom = room.y0 + 0.15f;
            if (r == 0)
            {
                Spawn(props, "Crate", left + 1f, bottom);
                Spawn(props, "Chest", left + 2f, bottom);
                Spawn(props, "Skull", right - 2f, room.y1 - 1f);
            }
            else if (boss)
            {
                Spawn(props, "Column", room.x0 + 4.5f, room.y0 + 4f, true);
                Spawn(props, "Column", room.x1 - 3.5f, room.y0 + 4f, true);
                Spawn(props, "Column", room.x0 + 4.5f, room.y1 - 5f, true);
                Spawn(props, "Column", room.x1 - 3.5f, room.y1 - 5f, true);
                Spawn(props, "Chest", left + 1f, room.y1 - 1.8f, true);
                Spawn(props, "Skull", right - 2f, room.y0 + 2f, true);
                Spawn(props, "Skull", left + 3f, room.y0 + 1.5f, true);
            }
            else
            {
                Spawn(props, "Crate", left + 1f, bottom);
                Spawn(props, "Crate", left + 2f, bottom);
                Spawn(props, "ChestEmpty", right - 1f, bottom);
                Spawn(props, "Skull", cx - 3.5f, room.y0 + 3f);
                if (r == 1 || r == 3) Spawn(props, "Spikes", cx + 3.5f, room.y0 + 2.5f);
            }
        }
    }

    // ---- doors: the real Door objects get the pack's leaf, a jamb either side and a lintel ----

    static void DressDoors()
    {
        foreach (Door door in Object.FindObjectsByType<Door>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var box = door.GetComponent<BoxCollider2D>();
            bool horizontal = box.size.x > box.size.y; // a door across a north-south corridor, seen from the front
            Vector2 c = door.transform.position;
            var kids = new List<Transform>();
            foreach (Transform t in door.transform) kids.Add(t);
            for (int i = 1; i < kids.Count; i++) Object.DestroyImmediate(kids[i].gameObject);
            Transform leaf = kids.Count > 0 ? kids[0] : new GameObject("Leaf").transform;
            leaf.name = "Leaf";
            leaf.SetParent(door.transform, false);
            var sr = leaf.GetComponent<SpriteRenderer>();
            if (sr == null) sr = leaf.gameObject.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 10;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            sr.color = Color.white;

            float bottom;
            if (horizontal)
            {
                // In the face row of the room below it, or in the bottom wall of the room above it.
                var cell = new Vector2Int(Mathf.FloorToInt(c.x - 0.5f), Mathf.FloorToInt(c.y));
                bool roomBelow = false;
                foreach (R room in Rooms) roomBelow |= room.Contains(cell + Vector2Int.down);
                bottom = roomBelow ? cell.y : cell.y - 1;
                leaf.localScale = Vector3.one;
            }
            else
            {
                // Side doors have no art in the pack: the front leaf, narrowed to the one-cell wall.
                bottom = c.y - 1f;
                leaf.localScale = new Vector3(0.5f, 1f, 1f);
            }

            leaf.localPosition = new Vector3(0f, bottom - c.y, 0f);
            Sprite closed = Sprite("doors_leaf_closed"), open = Sprite("doors_leaf_open");
            sr.sprite = open;
            var so = new SerializedObject(door);
            SerializedProperty renderers = so.FindProperty("renderers");
            renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = sr;
            so.FindProperty("closedSprite").objectReferenceValue = closed;
            so.FindProperty("openSprite").objectReferenceValue = open;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(door);
        }
    }

    // ---- palettes ----

    [MenuItem("Tools/Level/Build DTS2 Palettes")]
    public static void BuildPalettes()
    {
        EnsureFolder(TileFolder);
        Cache.Clear();
        var all = new List<string>();
        foreach (string f in Directory.GetFiles(Frames, "*.png")) all.Add(Path.GetFileNameWithoutExtension(f));
        all.Sort();
        Palette("DTS2_Ground", all.FindAll(n => n.StartsWith("floor_") || n == "hole" || n == "edge_down"));
        Palette("DTS2_Walls", all.FindAll(n => (n.StartsWith("wall_") && !n.Contains("_anim_")) || n.StartsWith("doors_frame")));
        Palette("DTS2_Decor", all.FindAll(n => n.StartsWith("wall_banner") || n.StartsWith("wall_goo") || n.StartsWith("wall_hole") || n.StartsWith("wall_fountain")));
        PropPalette("DTS2_Props");
        AssetDatabase.SaveAssets();
        Debug.Log("Dts2LevelPainter: DTS2 palettes built.");
    }

    static GameObject NewPalette(string name)
    {
        string path = $"{PaletteFolder}/{name}.prefab";
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
        GridPaletteUtility.CreateNewPalette(PaletteFolder, name, GridLayout.CellLayout.Rectangle, GridPalette.CellSizing.Automatic, Vector3.one, GridLayout.CellSwizzle.XYZ);
        return PrefabUtility.LoadPrefabContents(path);
    }

    static void Palette(string name, List<string> frames)
    {
        GameObject root = NewPalette(name);
        Tilemap map = root.GetComponentInChildren<Tilemap>();
        const int perRow = 8;
        for (int i = 0; i < frames.Count; i++)
        {
            map.SetTile(new Vector3Int((i % perRow) * 2, -(i / perRow) * 3, 0), T(frames[i]));
        }

        PrefabUtility.SaveAsPrefabAsset(root, $"{PaletteFolder}/{name}.prefab");
        PrefabUtility.UnloadPrefabContents(root);
    }

    static void PropPalette(string name)
    {
        BuildProps();
        GameObject root = NewPalette(name);
        Tilemap map = root.GetComponentInChildren<Tilemap>();
        int i = 0;
        foreach (var def in PropDefs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropFolder}/{def.name}.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, map.transform);
            go.transform.position = new Vector3((i % 5) * 2 + 0.5f, -(i / 5) * 3 + 0.5f, 0f);
            i++;
        }

        PrefabUtility.SaveAsPrefabAsset(root, $"{PaletteFolder}/{name}.prefab");
        PrefabUtility.UnloadPrefabContents(root);
    }
}
