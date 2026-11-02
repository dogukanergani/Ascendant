using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

// Level_01 with the Craftpix "Free 2D Top-Down Pixel Dungeon" pack (Assets/Art/ThirdParty/Craftpix/PixelDungeon,
// gitignored). The pack's tiles are 16 px at 32 PPU (the characters' pixel density), so they live on their own Grid
// with 0.5 unit cells; the old 1-unit Grid stays in the scene, switched off, as a backup.
// Same plain template as LevelPainter (rectangular rooms, 2 unit corridors at the Door objects); everything that is not
// floor is the pack's dark rock mass, its edge pieces chosen from the floor around each cell (see WallPiece). Collision
// is a separate invisible map covering the mass next to the floor.
public static class PixelDungeonPainter
{
    const string PackFolder = "Assets/Art/ThirdParty/Craftpix/PixelDungeon/";
    const string TileFolder = "Assets/Art/Environment/TopDown/PixelDungeon/Tiles";
    const string PaletteFolder = "Assets/Art/Environment/TopDown/Palettes";
    const string GridName = "Grid_PixelDungeon";
    const float Cell = 0.5f;
    const int WallLayer = 11; // the old Walls map's layer (unnamed in the project); RoomController's spawn check uses it
    static readonly Color BossTint = new(1f, 0.62f, 0.64f, 1f);

    // ---- level layout (world units, same rects as LevelPainter: room trigger + 1 cell, 2 cell corridors) ----

    struct R
    {
        public int x0, y0, x1, y1;
        public R(int x0, int y0, int x1, int y1) { this.x0 = x0; this.y0 = y0; this.x1 = x1; this.y1 = y1; }
        public float CenterX => (x0 + x1 + 1) * 0.5f;
    }

    static readonly R[] Rooms = { new(-6, -4, 5, 3), new(10, -6, 27, 5), new(12, 10, 25, 19), new(-14, 8, 7, 21), new(-14, 26, 5, 41) };
    static readonly R[] Corridors = { new(5, -1, 10, 0), new(18, 5, 19, 10), new(7, 14, 12, 15), new(-4, 21, -3, 26) };
    const int BossRoom = 4;
    static readonly int[] DoorRooms = { 0, 2, 4 }; // decorative wooden door in the top wall (no corridor there)

    // Boss room and its walls (pack cells): red tint.
    static bool IsBossCell(Vector2Int c) => c.x >= -31 && c.x <= 14 && c.y >= 50;

    // ---- tiles ----

    static readonly Dictionary<string, Tile> TileCache = new();

    // Tile for sprite `index` of a pack sheet (sliced 16 x 16 by CraftpixImporter), created on first use.
    public static Tile PD(string sheet, int index)
    {
        string name = $"PD_{sheet}_{index}";
        if (TileCache.TryGetValue(name, out Tile cached) && cached != null) return cached;
        string path = $"{TileFolder}/{name}.asset";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile == null)
        {
            Sprite sprite = null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath($"{PackFolder}{sheet}.png"))
            {
                if (o is Sprite s && s.name == $"{sheet}_{index}") { sprite = s; break; }
            }

            if (sprite == null) throw new System.Exception($"PixelDungeonPainter: missing sprite {sheet}_{index}");
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            tile.flags = TileFlags.None; // lets the painter tint the boss room per cell
            AssetDatabase.CreateAsset(tile, path);
        }

        TileCache[name] = tile;
        return tile;
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Art/Environment/TopDown/PixelDungeon")) AssetDatabase.CreateFolder("Assets/Art/Environment/TopDown", "PixelDungeon");
        if (!AssetDatabase.IsValidFolder(TileFolder)) AssetDatabase.CreateFolder("Assets/Art/Environment/TopDown/PixelDungeon", "Tiles");
        if (!AssetDatabase.IsValidFolder(PaletteFolder)) AssetDatabase.CreateFolder("Assets/Art/Environment/TopDown", "Palettes");
    }

    // ---- wall pieces (walls_floor sheet, 17 columns) ----
    // The pack draws a dungeon as one dark rock mass with the rooms cut out of it: a stone rim follows every edge of
    // the mass, and where the floor lies below the mass a 3 row brick face hangs down to it (rim + bricks, bricks,
    // bricks with the floor highlight). Pieces from the "pit" block (cols 5-7, rows 1-4) and the free block (cols 1-3).
    const int Top = 36;                                      // plain rock top
    static readonly int[] FaceMid = { 57, 40, 23 };          // face row k = 1 (foot) .. 3 (rim on top)
    static readonly int[] FaceLeftEnd = { 88, 71, 54 };      // face row with floor to the right: the free block's right end
    static readonly int[] FaceRightEnd = { 86, 69, 52 };     // face row with floor to the left
    static readonly int[] PitLeft = { 56, 39, 22 };          // mass left of a face (the pit's side wall up to the rim)
    static readonly int[] PitRight = { 58, 41, 24 };
    const int RimRight = 37, RimLeft = 35;                   // floor right / left of the mass
    const int RimBottom = 74, RimBottomLeft = 73, RimBottomRight = 75;   // floor above the mass, and its inner corners
    const int CornerFloorNW = 18, CornerFloorNE = 20;        // mass corner sticking into the floor
    const int FloorTile = 138;                               // plain light floor
    static readonly int[] FloorSeams = { 228, 229 };         // floor with faint brick seams
    static readonly Color RockColor = new(0.114f, 0.110f, 0.169f, 1f); // the rock top's colour, used as camera background
    const int FaceRows = 3, MassPad = 10, SolidDepth = 2;

    enum K { Void, Floor, Wall }

    // Face row k (1-3) when the floor lies straight below through mass, else 0.
    static int FaceK(HashSet<Vector2Int> floor, Vector2Int c)
    {
        for (int k = 1; k <= FaceRows; k++)
        {
            if (floor.Contains(new Vector2Int(c.x, c.y - k))) return k;
        }

        return 0;
    }

    static int WallPiece(HashSet<Vector2Int> floor, Vector2Int c)
    {
        bool F(int dx, int dy) => floor.Contains(new Vector2Int(c.x + dx, c.y + dy));
        int k = FaceK(floor, c);
        if (k > 0)
        {
            if (F(1, 0)) return FaceLeftEnd[k - 1];
            if (F(-1, 0)) return FaceRightEnd[k - 1];
            return FaceMid[k - 1];
        }

        // Beside a face: the pit's side wall up to the face's rim.
        int kRight = F(1, 0) ? 0 : FaceK(floor, c + Vector2Int.right);
        if (kRight > 0) return PitLeft[kRight - 1];
        int kLeft = F(-1, 0) ? 0 : FaceK(floor, c + Vector2Int.left);
        if (kLeft > 0) return PitRight[kLeft - 1];

        if (F(0, 1) && F(-1, 0)) return CornerFloorNW;
        if (F(0, 1) && F(1, 0)) return CornerFloorNE;
        if (F(1, 0)) return RimRight;
        if (F(-1, 0)) return RimLeft;
        if (F(0, 1)) return RimBottom;
        if (F(1, 1)) return RimBottomLeft;
        if (F(-1, 1)) return RimBottomRight;
        return Top;
    }

    // ---- painting ----

    [MenuItem("Tools/Level/Paint Level_01 (PixelDungeon)")]
    public static void Paint()
    {
        EnsureFolders();
        TileCache.Clear();

        // Floor in pack cells (2 per world unit).
        var floor = new HashSet<Vector2Int>();
        foreach (R r in Rooms) AddCells(floor, r);
        foreach (R r in Corridors) AddCells(floor, r);
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (Vector2Int f in floor)
        {
            minX = Mathf.Min(minX, f.x); maxX = Mathf.Max(maxX, f.x);
            minY = Mathf.Min(minY, f.y); maxY = Mathf.Max(maxY, f.y);
        }

        Transform grid = EnsureGrid(out Tilemap ground, out Tilemap shade, out Tilemap walls, out Tilemap decor, out Tilemap props, out Tilemap collision);
        foreach (Tilemap m in new[] { ground, shade, walls, decor, props, collision }) m.ClearAllTiles();

        var kinds = new Dictionary<Vector2Int, K>();
        var solidCells = new List<Vector3Int>();
        for (int x = minX - MassPad; x <= maxX + MassPad; x++)
        {
            for (int y = minY - MassPad; y <= maxY + MassPad; y++)
            {
                var c = new Vector2Int(x, y);
                var p = new Vector3Int(x, y, 0);
                if (floor.Contains(c))
                {
                    kinds[c] = K.Floor;
                    // One plain floor, with the pack's faint brick-seam tiles sprinkled in (fixed hash, ~1 in 80).
                    int h = (x * 73856093) ^ (y * 19349663);
                    int seam = ((h % 80) + 80) % 80;
                    Put(ground, p, "walls_floor", seam < FloorSeams.Length ? FloorSeams[seam] : FloorTile, c);
                    continue;
                }

                kinds[c] = K.Wall;
                // Plain rock top stays untinted, so the boss tint has no hard edge in the dark mass.
                int piece = WallPiece(floor, c);
                walls.SetTile(p, PD("walls_floor", piece));
                if (piece != Top && IsBossCell(c)) walls.SetColor(p, BossTint);
                if (NearFloor(floor, c, SolidDepth)) solidCells.Add(p);
            }
        }

        // Collision: the mass next to the floor (one world unit deep); the rest is never reachable.
        Tile solid = CollisionTile();
        var solids = new TileBase[solidCells.Count];
        for (int i = 0; i < solids.Length; i++) solids[i] = solid;
        collision.SetTiles(solidCells.ToArray(), solids);

        Dress(walls, decor, props, kinds, floor);

        // Past the painted rock the camera background takes over in the same colour, so the mass has no visible edge.
        if (Camera.main != null) Camera.main.backgroundColor = RockColor;

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
        Debug.Log($"PixelDungeonPainter: floor {floor.Count} cells, solid {solidCells.Count}.");
    }

    static bool NearFloor(HashSet<Vector2Int> floor, Vector2Int c, int r)
    {
        for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
                if (floor.Contains(new Vector2Int(c.x + dx, c.y + dy))) return true;
        return false;
    }

    static void AddCells(HashSet<Vector2Int> set, R r)
    {
        for (int x = r.x0 * 2; x <= r.x1 * 2 + 1; x++)
            for (int y = r.y0 * 2; y <= r.y1 * 2 + 1; y++)
                set.Add(new Vector2Int(x, y));
    }

    static void Put(Tilemap map, Vector3Int p, string sheet, int id, Vector2Int c)
    {
        map.SetTile(p, PD(sheet, id));
        if (IsBossCell(c)) map.SetColor(p, BossTint);
    }

    static Tile CollisionTile()
    {
        string path = $"{TileFolder}/PD_Collision.asset";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, path);
        }

        // Any sprite works for a Grid collider; the collision map's renderer is off.
        tile.sprite = PD("walls_floor", 36).sprite;
        tile.colliderType = Tile.ColliderType.Grid;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    static Transform EnsureGrid(out Tilemap ground, out Tilemap shade, out Tilemap walls, out Tilemap decor, out Tilemap props, out Tilemap collision)
    {
        GameObject old = GameObject.Find("Grid");
        if (old != null) old.SetActive(false); // the 1-unit dungeon tileset level, kept as a backup

        GameObject gridGo = GameObject.Find(GridName);
        if (gridGo == null)
        {
            gridGo = new GameObject(GridName);
            gridGo.AddComponent<Grid>();
        }

        gridGo.GetComponent<Grid>().cellSize = new Vector3(Cell, Cell, 0f);
        Transform t = gridGo.transform;
        ground = Layer(t, "PD_Ground", -100);
        shade = Layer(t, "PD_FloorShade", -99);
        decor = Layer(t, "PD_Decor", -97);
        walls = Layer(t, "PD_Walls", -90);
        props = Layer(t, "PD_Props", 10);
        props.GetComponent<TilemapRenderer>().mode = TilemapRenderer.Mode.Individual; // Y-sorted with the player
        collision = Layer(t, "PD_Collision", 0);
        GameObject col = collision.gameObject;
        col.GetComponent<TilemapRenderer>().enabled = false;
        col.layer = WallLayer;
        if (col.GetComponent<Rigidbody2D>() == null) col.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        if (col.GetComponent<TilemapCollider2D>() == null) col.AddComponent<TilemapCollider2D>().compositeOperation = Collider2D.CompositeOperation.Merge;
        if (col.GetComponent<CompositeCollider2D>() == null) col.AddComponent<CompositeCollider2D>().geometryType = CompositeCollider2D.GeometryType.Polygons;
        if (col.GetComponent<TilemapColliderRefresh>() == null) col.AddComponent<TilemapColliderRefresh>();
        return t;
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

    // ---- dressing ----

    // Multi-cell stamps from the pack sheets: (dx, dy up, sheet, index).
    static readonly (int, int, string, int)[] WoodenDoor = { (0, 0, "walls_floor", 467), (1, 0, "walls_floor", 468), (0, 1, "walls_floor", 450), (1, 1, "walls_floor", 451) };
    static readonly (int, int, string, int)[] Torch = { (0, 0, "fire_animation", 22), (1, 0, "fire_animation", 23), (0, 1, "fire_animation", 11), (1, 1, "fire_animation", 12), (0, 2, "fire_animation", 0), (1, 2, "fire_animation", 1) };
    static readonly (int, int, string, int)[] Barrel = { (0, 0, "Objects", 151), (0, 1, "Objects", 127) };
    static readonly (int, int, string, int)[] Crate = { (0, 0, "Objects", 150), (0, 1, "Objects", 126) };
    static readonly (int, int, string, int)[] Pot = { (0, 0, "Objects", 157), (0, 1, "Objects", 133) };
    static readonly (int, int, string, int)[] Vase = { (0, 0, "Objects", 160), (0, 1, "Objects", 136) };
    static readonly (int, int, string, int)[] Chest = { (0, 0, "Objects", 57), (1, 0, "Objects", 58), (0, 1, "Objects", 33), (1, 1, "Objects", 34) };
    static readonly (int, int, string, int)[] Shield = { (0, 0, "Objects", 175) };
    static readonly (int, int, string, int)[] Gold = { (0, 0, "Objects", 61), (1, 0, "Objects", 62), (0, 1, "Objects", 37), (1, 1, "Objects", 38) };
    static readonly int[] FloorCracks = { 2, 3, 4, 5, 7, 14 };

    static void Stamp(Tilemap map, (int, int, string, int)[] stamp, int x, int y)
    {
        foreach (var (dx, dy, sheet, id) in stamp)
        {
            var p = new Vector3Int(x + dx, y + dy, 0);
            map.SetTile(p, PD(sheet, id));
            if (IsBossCell(new Vector2Int(p.x, p.y))) map.SetColor(p, BossTint);
        }
    }

    static void Dress(Tilemap walls, Tilemap decor, Tilemap props, Dictionary<Vector2Int, K> kinds, HashSet<Vector2Int> floor)
    {
        var rng = new System.Random(5);
        for (int r = 0; r < Rooms.Length; r++)
        {
            R room = Rooms[r];
            int x0 = room.x0 * 2, x1 = room.x1 * 2 + 1, y0 = room.y0 * 2, y1 = room.y1 * 2 + 1;
            int cx = Mathf.RoundToInt(room.CenterX * 2);
            int wallFoot = y1 + 1; // first wall row above the room

            // Wooden door in the middle of the top wall, torches symmetric on both sides.
            if (System.Array.IndexOf(DoorRooms, r) >= 0) Stamp(walls, WoodenDoor, cx - 1, wallFoot);
            int spread = Mathf.Max(4, (x1 - x0) / 4);
            foreach (int tx in new[] { cx - spread - 1, cx + spread - 1 })
            {
                if (kinds.TryGetValue(new Vector2Int(tx, wallFoot), out K k) && k == K.Wall
                    && kinds.TryGetValue(new Vector2Int(tx + 1, wallFoot), out K k2) && k2 == K.Wall)
                {
                    Stamp(props, Torch, tx, wallFoot);
                }
            }

            // Cracks on the floor, away from the walls.
            for (int n = 0; n < 4 + (x1 - x0) * (y1 - y0) / 250; n++)
            {
                var c = new Vector2Int(rng.Next(x0 + 3, x1 - 2), rng.Next(y0 + 3, y1 - 3));
                if (NearCorridor(c)) continue;
                var p = new Vector3Int(c.x, c.y, 0);
                decor.SetTile(p, PD("decorative_cracks_floor", FloorCracks[rng.Next(FloorCracks.Length)]));
                if (IsBossCell(c)) decor.SetColor(p, BossTint);
            }

            // A few props in the bottom corners (doors are on the sides and the top), boss gets its hoard.
            if (r == BossRoom)
            {
                Stamp(props, Gold, x0 + 2, y1 - 3);
                Stamp(props, Gold, x1 - 3, y1 - 3);
                Stamp(props, Chest, x0 + 2, y0 + 1);
                Stamp(props, Shield, cx - 4, wallFoot + 1);
                Stamp(props, Shield, cx + 3, wallFoot + 1);
            }
            else
            {
                Stamp(props, Barrel, x0 + 1, y0 + 1);
                Stamp(props, Crate, x0 + 2, y0 + 1);
                Stamp(props, r % 2 == 0 ? Pot : Vase, x1 - 1, y0 + 1);
                if (r != 0) Stamp(props, Chest, x1 - 3, y1 - 2);
            }
        }
    }

    static bool NearCorridor(Vector2Int c)
    {
        foreach (R r in Corridors)
        {
            if (c.x >= r.x0 * 2 - 4 && c.x <= r.x1 * 2 + 5 && c.y >= r.y0 * 2 - 4 && c.y <= r.y1 * 2 + 5) return true;
        }

        return false;
    }

    // ---- palettes ----

    [MenuItem("Tools/Level/Build PixelDungeon Palettes")]
    public static void BuildPalettes()
    {
        EnsureFolders();
        TileCache.Clear();
        BuildPalette("PixelDungeon_Walls", ("walls_floor", 17, 29, 0, 0), ("decorative_cracks_walls", 8, 32, 19, 0));
        BuildPalette("PixelDungeon_Ground", ("walls_floor", 17, 29, 0, 0), ("decorative_cracks_floor", 8, 15, 19, 0));
        BuildPalette("PixelDungeon_Decor", ("decorative_cracks_floor", 8, 15, 0, 0), ("trap_animation", 13, 8, 10, 0));
        BuildPalette("PixelDungeon_Props", ("Objects", 24, 9, 0, 0), ("doors_lever_chest_animation", 10, 15, 0, -11), ("fire_animation", 11, 18, 12, -11), ("fire_animation2", 6, 12, 25, -11));
        AssetDatabase.SaveAssets();
        Debug.Log("PixelDungeonPainter: palettes built in " + PaletteFolder);
    }

    // Each sheet is laid out as in the png (top left first), its top left cell at (x0, y0).
    static void BuildPalette(string name, params (string sheet, int cols, int rows, int x0, int y0)[] sheets)
    {
        string path = $"{PaletteFolder}/{name}.prefab";
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
        GridPaletteUtility.CreateNewPalette(PaletteFolder, name, GridLayout.CellLayout.Rectangle, GridPalette.CellSizing.Automatic, new Vector3(Cell, Cell, 0f), GridLayout.CellSwizzle.XYZ);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        Tilemap map = root.GetComponentInChildren<Tilemap>();
        foreach (var (sheet, cols, rows, x0, y0) in sheets)
        {
            var sprites = new Dictionary<string, Sprite>();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath($"{PackFolder}{sheet}.png")) if (o is Sprite s) sprites[s.name] = s;
            for (int i = 0; i < cols * rows; i++)
            {
                if (!sprites.TryGetValue($"{sheet}_{i}", out Sprite sp) || IsEmpty(sp)) continue;
                map.SetTile(new Vector3Int(x0 + i % cols, y0 - i / cols, 0), PD(sheet, i));
            }
        }

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static readonly Dictionary<Texture2D, Texture2D> Readable = new();

    // Skips fully transparent cells so the palettes only hold real tiles (and no tile assets are made for empty cells).
    static bool IsEmpty(Sprite sprite)
    {
        Texture2D src = sprite.texture;
        if (!Readable.TryGetValue(src, out Texture2D copy))
        {
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0);
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            copy.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            Readable[src] = copy;
        }

        Rect r = sprite.rect;
        for (int y = (int)r.y; y < r.yMax; y++)
            for (int x = (int)r.x; x < r.xMax; x++)
                if (copy.GetPixel(x, y).a > 0.01f) return false;
        return true;
    }
}
