using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

// Dungeon art pass for Level_01 with the Craftpix Dungeon Tileset (Assets/Art/ThirdParty, gitignored): paints the
// Floor / Walls tilemaps (64 px tiles at 64 PPU = one world unit per cell), builds the door / exit sprites and
// prop prefabs, and scatters the props. Used by LevelBuilder and as a standalone "Dress Level_01" pass.
// The tile and prop assets reference the pack's sprites, so rerun this pass after re-importing the pack.
public static class DungeonDressing
{
    const string PackFolder = CraftpixImporter.DungeonFolder;
    const string PngFolder = PackFolder + "PNG/";
    const string GeneratedFolder = CraftpixImporter.DungeonGeneratedFolder;
    const string FireFolder = PackFolder + "Animations_fire/SCML/PNG/";
    const string TileFolder = "Assets/Art/Environment/TopDown/Tiles";
    const string PropPrefabFolder = "Assets/Prefabs/Environment/Props";
    public const int WallThickness = 2;

    // Pseudo tile indices kept for LevelBuilder: doors and the exit pad use generated sprites (see TileSprite);
    // adding BossRow selects the boss theme.
    public const int DoorClosed = 5, DoorOpen = 6, Exit = 7, BossRow = 16;

    static readonly Color BossTint = new(1f, 0.72f, 0.74f, 1f);
    static readonly Color ShadowTint = new(0.84f, 0.84f, 0.9f, 1f);

    const int PropOrder = 10;      // same as the player and enemies; the project sorts by Y, so the pivot (feet) decides
    const int WallDecorOrder = -85; // above floor (-100) and walls (-90), below everything that moves
    const int FloorDecalOrder = -95;

    // name, pack sprite, scale (the pack is drawn at about twice the size of the 32 px characters, so props shrink), solid collider
    // size and offset in world units, circle collider, sorting order.
    static readonly (string name, string sprite, float scale, Vector2 colliderSize, Vector2 colliderOffset, bool circle, int order)[] PropDefs =
    {
        ("Torch", "decor_5", 0.55f, Vector2.zero, Vector2.zero, false, WallDecorOrder),
        ("Chest", "decor_19", 0.5f, new Vector2(0.7f, 0.3f), new Vector2(0f, 0.17f), false, PropOrder),
        ("Barrel", "decor_2", 0.42f, new Vector2(0.5f, 0f), new Vector2(0f, 0.22f), true, PropOrder),
        ("Crate", "decor_21", 0.5f, new Vector2(0.6f, 0.3f), new Vector2(0f, 0.17f), false, PropOrder),
        ("Campfire", "decor_3", 0.6f, Vector2.zero, Vector2.zero, false, PropOrder),
        ("Urn", "decor_4", 0.6f, Vector2.zero, Vector2.zero, false, PropOrder),
        ("Weeds", "greenery_3", 0.6f, Vector2.zero, Vector2.zero, false, FloorDecalOrder),
        ("CrackDecal", "decor_14", 0.8f, Vector2.zero, Vector2.zero, false, FloorDecalOrder),
        ("Boulder", "stones_2", 0.4f, new Vector2(0.9f, 0.4f), new Vector2(0f, 0.22f), false, PropOrder),
        ("Sign", "decor_7", 0.45f, Vector2.zero, Vector2.zero, false, PropOrder),
        ("Rubble", "bricks_3", 0.6f, Vector2.zero, Vector2.zero, false, PropOrder),
        ("Pillar", "bricks_6", 0.4f, new Vector2(0.6f, 0.35f), new Vector2(0f, 0.2f), false, PropOrder),
        ("Planks", "decor_20", 0.5f, new Vector2(0.8f, 0.3f), new Vector2(0f, 0.17f), false, PropOrder),
        ("Stump", "decor_10", 0.6f, new Vector2(0.4f, 0f), new Vector2(0f, 0.2f), true, PropOrder),
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

    // Single-sprite PNGs load by path (subName null); sliced sheets by sub-sprite name ("land_5").
    static Sprite LoadSprite(string path, string subName)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (o is Sprite s && (subName == null || s.name == subName))
            {
                return s;
            }
        }

        throw new System.Exception($"DungeonDressing: missing sprite {subName ?? path} in {path} (is the Craftpix Dungeon Tileset imported under {PackFolder}?)");
    }

    static Sprite Sheet(string sheet, int index) => LoadSprite($"{PngFolder}{sheet}.png", $"{sheet}_{index}");

    // Floor tiles are the 16 cells of land.png laid out by world position (the texture tiles seamlessly 4x4).
    static string FloorName(int cell, bool shadow, bool boss) => $"CP_{(shadow ? "shadow" : "floor")}_{cell}{(boss ? "_boss" : "")}";

    public static void CreateTileAssets()
    {
        GenerateArt();
        if (!AssetDatabase.IsValidFolder(TileFolder))
        {
            AssetDatabase.CreateFolder("Assets/Art/Environment/TopDown", "Tiles");
        }

        foreach (bool boss in new[] { false, true })
        {
            Color tint = boss ? BossTint : Color.white;
            for (int cell = 0; cell < 16; cell++)
            {
                MakeTile(FloorName(cell, false, boss), Sheet("land", cell), tint);
                MakeTile(FloorName(cell, true, boss), Sheet("land", cell), tint * ShadowTint);
            }

            // The pack's walls are 4x4 sheets: wall_1 row 0 is the thin stone cap with the first cobble courses under it,
            // wall_8 rows 1-2 are plain cobble (the dark wall face), wall_3 is the 1-tile-wide block column used for side walls.
            // All of them tile seamlessly over 4 cells, so they are picked by world x / y modulo 4.
            string sfx = boss ? "_boss" : "";
            MakeTile($"CP_post{sfx}", Sheet("wall_9", 0), tint);
            for (int col = 0; col < 4; col++)
            {
                MakeTile($"CP_cap_{col}{sfx}", Sheet("wall_1", col), tint);
                MakeTile($"CP_face_{col}{sfx}", Sheet("wall_8", 4 + col), tint);
                MakeTile($"CP_face2_{col}{sfx}", Sheet("wall_8", 8 + col), tint);
                MakeTile($"CP_side_{col}{sfx}", Sheet("wall_3", col), tint);
            }
        }

        AssetDatabase.SaveAssets();
    }

    static Tile MakeTile(string name, Sprite sprite, Color color)
    {
        string path = $"{TileFolder}/{name}.asset";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, path);
        }

        tile.sprite = sprite;
        tile.color = color;
        // Grid collision for every tile; only the Walls tilemap has a collider, the Floor map ignores it.
        tile.colliderType = Tile.ColliderType.Grid;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    static Tile TileByName(string name) => AssetDatabase.LoadAssetAtPath<Tile>($"{TileFolder}/{name}.asset");

    static int Mod4(int v) => ((v % 4) + 4) % 4;

    // Sprite for the pseudo indices LevelBuilder uses (closed / open door, exit pad, plus BossRow for the boss theme).
    public static Sprite TileSprite(int index)
    {
        bool boss = index >= BossRow;
        int baseIndex = boss ? index - BossRow : index;
        string file = baseIndex == Exit ? "exit_pad" : baseIndex == DoorClosed ? (boss ? "door_closed_boss" : "door_closed") : "door_open";
        return LoadSprite($"{GeneratedFolder}{file}.png", null);
    }

    // The boss room and the corridor leading to it (x -6..-1 from y 22) use the red tint.
    public static bool IsBossTheme(Vector2Int c) => c.y >= 24 || (c.y >= 22 && c.x >= -6 && c.x <= -1);

    public static void Paint(HashSet<Vector2Int> floor, Tilemap floorMap, Tilemap wallMap)
    {
        foreach (Vector2Int c in floor)
        {
            // Floor right below a wall face sits in its shadow.
            bool underWall = !floor.Contains(new Vector2Int(c.x, c.y + 1));
            int cell = (3 - Mod4(c.y)) * 4 + Mod4(c.x);
            floorMap.SetTile(new Vector3Int(c.x, c.y, 0), TileByName(FloorName(cell, underWall, IsBossTheme(c))));
        }

        foreach (KeyValuePair<Vector2Int, string> kv in ClassifyWalls(floor))
        {
            wallMap.SetTile(new Vector3Int(kv.Key.x, kv.Key.y, 0), TileByName(kv.Value + (IsBossTheme(kv.Key) ? "_boss" : "")));
        }
    }

    // Wall layout as in the pack's preview: a wall seen from the room below it is a cap (thin stone edge with cobble
    // under it) on top of FaceRows rows of dark cobble face; left/right walls are a single block column that also runs
    // up the sides of the face and down to the bottom wall (so corners are column cells); a wall below a room is one
    // cap row. Priority: side column, top wall face, column continuation (corners), bottom cap.
    public const int FaceRows = 2;

    public static Dictionary<Vector2Int, string> ClassifyWalls(HashSet<Vector2Int> floor)
    {
        var result = new Dictionary<Vector2Int, string>();
        var candidates = new HashSet<Vector2Int>();
        foreach (Vector2Int c in floor)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -(FaceRows + 1); dy <= FaceRows + 1; dy++)
                {
                    var n = new Vector2Int(c.x + dx, c.y + dy);
                    if (!floor.Contains(n))
                    {
                        candidates.Add(n);
                    }
                }
            }
        }

        bool F(int x, int y) => floor.Contains(new Vector2Int(x, y));
        foreach (Vector2Int w in candidates)
        {
            string name = null;
            if (F(w.x - 1, w.y) || F(w.x + 1, w.y))
            {
                name = $"CP_side_{3 - Mod4(w.y)}";
            }

            if (name == null)
            {
                for (int k = 1; k <= FaceRows + 1 && name == null; k++)
                {
                    if (F(w.x, w.y - k))
                    {
                        name = k <= FaceRows ? $"CP_{(k == 1 ? "face2" : "face")}_{Mod4(w.x)}" : $"CP_cap_{Mod4(w.x)}";
                        break;
                    }

                }
            }

            if (name == null)
            {
                bool columnNearby = F(w.x - 1, w.y + 1) || F(w.x + 1, w.y + 1);
                for (int j = 1; j <= FaceRows + 1 && !columnNearby; j++)
                {
                    columnNearby = F(w.x - 1, w.y - j) || F(w.x + 1, w.y - j);
                }

                if (columnNearby)
                {
                    name = $"CP_side_{3 - Mod4(w.y)}";
                }
            }

            if (name == null && F(w.x, w.y + 1))
            {
                name = $"CP_cap_{Mod4(w.x)}";
            }

            if (name != null)
            {
                result[w] = name;
            }
        }

        // Second pass: neighbour mask. The first pass decided the kind of every wall cell (side column, face, cap, bottom
        // cap); here the 4 orthogonal neighbours in the wall set pick the piece, so no strip ends "in the air":
        //   column, wall above and below  -> column strip (3 - y%4)     column with no wall above or below -> post block
        //   bottom cap, walls W and E     -> cap strip (x%4)            bottom cap with no wall W or E      -> post block
        //   face / cap rows               -> unchanged (columns flank them)
        var refined = new Dictionary<Vector2Int, string>(result);
        foreach (KeyValuePair<Vector2Int, string> kv in result)
        {
            Vector2Int p = kv.Key;
            bool n = result.ContainsKey(p + Vector2Int.up), s = result.ContainsKey(p + Vector2Int.down);
            bool e = result.ContainsKey(p + Vector2Int.right), w = result.ContainsKey(p + Vector2Int.left);
            if (kv.Value.StartsWith("CP_side_") && (!n || !s))
            {
                refined[p] = "CP_post";
            }
            else if (kv.Value.StartsWith("CP_cap_") && F(p.x, p.y + 1) && (!e || !w))
            {
                refined[p] = "CP_post";
            }
        }

        return refined;
    }

    // ---- generated sprites (doors, exit pad) ----

    // Door planks are cropped from wall_10.png, the exit pad is drawn. Files land in the pack's Generated folder.
    static void GenerateArt()
    {
        Directory.CreateDirectory(GeneratedFolder.TrimEnd('/'));
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        source.LoadImage(File.ReadAllBytes($"{PngFolder}wall_10.png"));

        var closed = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        var boss = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        var open = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        var pad = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        var clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                bool border = x < 3 || y < 3 || x > 60 || y > 60;
                Color planks = border ? new Color(0.09f, 0.06f, 0.07f, 1f) : source.GetPixel(60 + x, 70 + y);
                closed.SetPixel(x, y, planks);
                boss.SetPixel(x, y, planks * new Color(0.75f, 0.45f, 0.62f, 1f));
                open.SetPixel(x, y, clear);

                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32f, 32f));
                Color c = clear;
                if (d >= 25f && d < 29f)
                {
                    c = new Color(0.95f, 0.72f, 0.25f, 1f);
                }
                else if (d >= 14f && d < 17f)
                {
                    c = new Color(0.55f, 0.4f, 0.95f, 1f);
                }
                else if (d < 8f)
                {
                    c = new Color(0.75f, 0.65f, 1f, 1f);
                }
                else if (d < 29f)
                {
                    c = new Color(0.1f, 0.09f, 0.2f, 0.85f);
                }

                pad.SetPixel(x, y, c);
            }
        }

        WritePng(closed, "door_closed");
        WritePng(boss, "door_closed_boss");
        WritePng(open, "door_open");
        WritePng(pad, "exit_pad");
        Object.DestroyImmediate(source);
        AssetDatabase.Refresh();
    }

    static void WritePng(Texture2D texture, string name)
    {
        string path = $"{GeneratedFolder}{name}.png";
        byte[] bytes = texture.EncodeToPNG();
        if (!File.Exists(path) || !System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(path), bytes))
        {
            File.WriteAllBytes(path, bytes);
        }

        Object.DestroyImmediate(texture);
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

    // Writes one prefab per prop (Torch also gets an animated flame child).
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
            go.transform.localScale = Vector3.one * def.scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite($"{PngFolder}{def.sprite}.png", null);
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
                    col.radius = def.colliderSize.x * 0.5f / def.scale;
                    col.offset = def.colliderOffset / def.scale;
                }
                else
                {
                    var col = go.AddComponent<BoxCollider2D>();
                    col.size = def.colliderSize / def.scale;
                    col.offset = def.colliderOffset / def.scale;
                }
            }

            if (def.name == "Torch")
            {
                AddFlame(go.transform, sr, mat);
            }

            PrefabUtility.SaveAsPrefabAsset(go, $"{PropPrefabFolder}/{def.name}.prefab");
            Object.DestroyImmediate(go);
        }

        AssetDatabase.SaveAssets();
    }

    static void AddFlame(Transform torch, SpriteRenderer stick, Material mat)
    {
        var flame = new GameObject("Flame");
        flame.transform.SetParent(torch, false);
        // The bowl of the torch is at the top of the stick; the flame sprite pivots at its bottom.
        flame.transform.localPosition = new Vector3(0f, stick.sprite.bounds.size.y * 0.9f, 0f);
        flame.transform.localScale = Vector3.one * 0.8f;
        var sr = flame.AddComponent<SpriteRenderer>();
        sr.sortingOrder = stick.sortingOrder + 1;
        sr.spriteSortPoint = SpriteSortPoint.Pivot;
        if (mat != null)
        {
            sr.sharedMaterial = mat;
        }

        var frames = new Sprite[7];
        for (int i = 0; i < frames.Length; i++)
        {
            frames[i] = LoadSprite($"{FireFolder}{i + 1}.png", null);
        }

        sr.sprite = frames[0];
        var animator = flame.AddComponent<SpriteFrameAnimator>();
        var so = new SerializedObject(animator);
        SerializedProperty array = so.FindProperty("frames");
        array.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++)
        {
            array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
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
        Add("Stump", -1, 2); Add("Sign", 1, -3); Add("Chest", -1, -3);
        Add("Crate", 4, -3); Add("Barrel", 5, -3); Add("Planks", 4, -2); Add("Rubble", 2, 1);
        Add("Weeds", 5, -4, true, true); Add("CrackDecal", -2, 0);

        // Room 1 (10..27, -6..5)
        Add("Pillar", 14, -2); Add("Pillar", 23, -2); Add("Pillar", 14, 2); Add("Pillar", 23, 2);
        Add("Rubble", 19, -4); Add("Rubble", 12, -5); Add("Campfire", 25, -5); Add("Campfire", 20, 3); Add("Urn", 16, -4);
        Add("Barrel", 26, 4); Add("Barrel", 25, 4); Add("Crate", 11, 4); Add("Boulder", 17, 0);
        Add("Weeds", 27, 5, true); Add("Weeds", 27, -6, true, true); Add("Weeds", 10, -6, false, true);
        Add("CrackDecal", 18, 0); Add("CrackDecal", 12, -2, true); Add("CrackDecal", 24, 3);

        // Room 2 (12..25, 10..19)
        Add("Pillar", 15, 13); Add("Pillar", 22, 13); Add("Pillar", 15, 17); Add("Pillar", 22, 17);
        Add("Rubble", 13, 11); Add("Barrel", 24, 11); Add("Barrel", 25, 11); Add("Crate", 13, 18);
        Add("Campfire", 20, 12); Add("Urn", 18, 18); Add("Boulder", 19, 15);
        Add("Weeds", 12, 19); Add("Weeds", 25, 19, true); Add("Weeds", 25, 10, true, true);
        Add("CrackDecal", 18, 14); Add("CrackDecal", 21, 11, true);

        // Room 3 (-14..7, 8..21)
        Add("Pillar", -10, 12); Add("Pillar", -10, 18); Add("Pillar", 2, 12); Add("Pillar", 2, 18);
        Add("Chest", -13, 20); Add("Barrel", -13, 9); Add("Barrel", -12, 9); Add("Crate", -13, 10); Add("Planks", -12, 10);
        Add("Rubble", -3, 9); Add("Rubble", 6, 20); Add("Campfire", -6, 14); Add("Urn", 0, 15); Add("Boulder", -4, 16);
        Add("Sign", -13, 19);
        Add("Weeds", -14, 21); Add("Weeds", 7, 21, true); Add("Weeds", -14, 8, false, true);
        Add("CrackDecal", -5, 12); Add("CrackDecal", -2, 18, true); Add("CrackDecal", 4, 15);

        // Boss room (-14..5, 26..41): braziers flank the approach, the exit pad at (-5..-4, 35..36) stays clear.
        Add("Stump", -12, 28); Add("Stump", 3, 28); Add("Stump", -12, 39); Add("Stump", 3, 39);
        Add("Pillar", -9, 31); Add("Pillar", -1, 31); Add("Pillar", -9, 37); Add("Pillar", -1, 37);
        Add("Campfire", -7, 28); Add("Campfire", 1, 34); Add("Urn", -11, 33); Add("Urn", 2, 38); Add("Urn", -3, 29);
        Add("Rubble", -13, 31); Add("Rubble", 4, 36); Add("Boulder", -6, 40); Add("Boulder", 0, 40);
        Add("Weeds", -14, 41); Add("Weeds", 5, 41, true); Add("Weeds", -14, 26, false, true); Add("Weeds", 5, 26, true, true);
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
            bool decal = pl.prefab == "Weeds" || pl.prefab == "CrackDecal";
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

        PlaceWallTorches(root, floorMap, wallMap);
    }

    // Torches hang on the dark face of every top wall, one every TorchSpacing cells along each run of face cells
    // (face cells = wall cells with floor right below them), keeping a cell clear of the run ends.
    const int TorchSpacing = 4;

    static void PlaceWallTorches(Transform root, Tilemap floorMap, Tilemap wallMap)
    {
        if (floorMap == null || wallMap == null)
        {
            return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropPrefabFolder}/Torch.prefab");
        var rows = new SortedDictionary<int, List<int>>();
        foreach (Vector3Int p in wallMap.cellBounds.allPositionsWithin)
        {
            if (wallMap.HasTile(p) && floorMap.HasTile(new Vector3Int(p.x, p.y - 1, 0)))
            {
                if (!rows.TryGetValue(p.y, out List<int> xs))
                {
                    rows[p.y] = xs = new List<int>();
                }

                xs.Add(p.x);
            }
        }

        foreach (KeyValuePair<int, List<int>> row in rows)
        {
            row.Value.Sort();
            int start = 0;
            for (int i = 1; i <= row.Value.Count; i++)
            {
                if (i < row.Value.Count && row.Value[i] == row.Value[i - 1] + 1)
                {
                    continue;
                }

                // Run row.Value[start .. i-1]; centre the torches inside it.
                int len = i - start;
                if (len >= 4)
                {
                    int count = (len - 2) / TorchSpacing + 1;
                    int first = row.Value[start] + (len - 1 - (count - 1) * TorchSpacing) / 2;
                    for (int t = 0; t < count; t++)
                    {
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                        go.transform.position = new Vector3(first + t * TorchSpacing + 0.5f, row.Key, 0f);
                    }
                }

                start = i;
            }
        }
    }
}
