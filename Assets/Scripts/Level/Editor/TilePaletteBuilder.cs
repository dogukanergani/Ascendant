using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

// Builds the Tile Palettes for hand painting levels with the Craftpix Dungeon Tileset (Assets/Art/ThirdParty, gitignored):
// Dungeon_Ground, Dungeon_Walls, Dungeon_Decor (tile palettes) and Dungeon_Props (GameObject Brush palette of prefabs).
// The tile and prop assets reference the pack's sprites, so rerun the menu after re-importing the pack. See docs/TILE_PALETTE.md.
public static class TilePaletteBuilder
{
    const string PngFolder = "Assets/Art/ThirdParty/Craftpix/DungeonTileset/PNG/";
    const string TileFolder = "Assets/Art/Environment/TopDown/Tiles";
    public const string PaletteFolder = "Assets/Art/Environment/TopDown/Palettes";
    const string PropFolder = "Assets/Prefabs/Environment/Props";
    const float Ppu = 64f;
    static readonly Color BossTint = new(1f, 0.72f, 0.74f, 1f);

    [MenuItem("Tools/Level/Build Dungeon Palettes")]
    public static void BuildAll()
    {
        EnsureFolder("Assets/Art/Environment/TopDown", "Palettes");
        BuildAutoTiles();
        BuildGround();
        BuildWalls();
        BuildDecor();
        BuildProps();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("TilePaletteBuilder: palettes built in " + PaletteFolder);
    }

    // ---- sprites / tiles ----

    static Sprite Sub(string sheet, int index) => Load($"{PngFolder}{sheet}.png", $"{sheet}_{index}");

    static Sprite Single(string file) => Load($"{PngFolder}{file}.png", null);

    static Sprite Load(string path, string subName)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (o is Sprite s && (subName == null || s.name == subName))
            {
                return s;
            }
        }

        throw new System.Exception($"TilePaletteBuilder: missing sprite {subName ?? path} in {path}");
    }

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    static Tile MakeTile(string name, Sprite sprite, Tile.ColliderType collider, Color color, Matrix4x4? transform = null)
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
        tile.colliderType = collider;
        tile.transform = transform ?? Matrix4x4.identity;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    static T MakeAsset<T>(string name) where T : ScriptableObject
    {
        string path = $"{TileFolder}/{name}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }

        return asset;
    }

    // Tile that scales the sprite and keeps a bottom-pivot sprite centred on its cell.
    static Matrix4x4 DecalTransform(Sprite sprite, float scale)
    {
        bool bottomPivot = sprite.pivot.y < sprite.rect.height * 0.25f;
        float h = sprite.rect.height / Ppu * scale;
        return Matrix4x4.TRS(new Vector3(0f, bottomPivot ? -h * 0.5f : 0f, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
    }

    // ---- auto tiles ----

    static void BuildAutoTiles()
    {
        foreach (bool boss in new[] { false, true })
        {
            DungeonWallTile wall = MakeAsset<DungeonWallTile>(boss ? "Wall_Auto_Boss" : "Wall_Auto");
            wall.tint = boss ? BossTint : Color.white;
            for (int i = 0; i < 4; i++)
            {
                wall.cap[i] = Sub("wall_1", i);
                wall.column[i] = Sub("wall_3", i);
                wall.bottom[i] = Sub("wall_8", 12 + i);
                for (int r = 0; r < 2; r++)
                {
                    wall.face[r * 4 + i] = Sub("wall_8", 4 + r * 4 + i);
                }
            }

            wall.topLeft = Sub("wall_6", 0);
            wall.topRight = Sub("wall_5", 3);
            wall.post = Sub("wall_9", 0);
            for (int r = 0; r < 3; r++)
            {
                wall.leftEdge[r] = Sub("wall_6", 4 + r * 4);
                wall.rightEdge[r] = Sub("wall_5", 7 + r * 4);
            }

            EditorUtility.SetDirty(wall);

            DungeonFloorTile floor = MakeAsset<DungeonFloorTile>(boss ? "Floor_Auto_Boss" : "Floor_Auto");
            floor.tint = boss ? BossTint : Color.white;
            for (int i = 0; i < 16; i++)
            {
                floor.floor[i] = Sub("land", i);
            }

            EditorUtility.SetDirty(floor);
        }
    }

    // ---- palettes ----

    static GameObject NewPalette(string name)
    {
        string path = $"{PaletteFolder}/{name}.prefab";
        if (File.Exists(path))
        {
            AssetDatabase.DeleteAsset(path);
        }

        GridPaletteUtility.CreateNewPalette(PaletteFolder, name, GridLayout.CellLayout.Rectangle, GridPalette.CellSizing.Automatic, Vector3.one, GridLayout.CellSwizzle.XYZ);
        return PrefabUtility.LoadPrefabContents(path);
    }

    static void SavePalette(GameObject root, string name)
    {
        PrefabUtility.SaveAsPrefabAsset(root, $"{PaletteFolder}/{name}.prefab");
        PrefabUtility.UnloadPrefabContents(root);
    }

    static void Put(Tilemap map, TileBase tile, int x, int y)
    {
        map.SetTile(new Vector3Int(x, y, 0), tile);
    }

    static TileBase T(string name) => AssetDatabase.LoadAssetAtPath<TileBase>($"{TileFolder}/{name}.asset");

    // A sheet laid out like the png (index 0 = top left) with its lower left corner at (x0, y0).
    static void PutSheet(Tilemap map, string sheet, string tilePrefix, int cols, int rows, int x0, int y0, Tile.ColliderType collider, System.Func<int, bool> include = null)
    {
        for (int i = 0; i < cols * rows; i++)
        {
            if (include != null && !include(i))
            {
                continue;
            }

            Tile tile = MakeTile($"{tilePrefix}_{i}", Sub(sheet, i), collider, Color.white);
            Put(map, tile, x0 + i % cols, y0 + (rows - 1) - i / cols);
        }
    }

    static void BuildGround()
    {
        // Floor / shadow cells exist from the first Craftpix pass (CP_floor_*, CP_shadow_*); the Floor map has no collider.
        foreach (string file in Directory.GetFiles(TileFolder, "CP_floor_*.asset").Concat(Directory.GetFiles(TileFolder, "CP_shadow_*.asset")))
        {
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(file.Replace('\\', '/'));
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
        }

        GameObject root = NewPalette("Dungeon_Ground");
        Tilemap map = root.GetComponentInChildren<Tilemap>();
        Put(map, T("Floor_Auto"), 0, 5);
        Put(map, T("Floor_Auto_Boss"), 1, 5);
        string[] sets = { "CP_floor_", "CP_shadow_", "CP_floor_{0}_boss", "CP_shadow_{0}_boss" };
        for (int s = 0; s < sets.Length; s++)
        {
            for (int i = 0; i < 16; i++)
            {
                string name = sets[s].Contains("{0}") ? string.Format(sets[s], i) : sets[s] + i;
                Put(map, T(name), s * 5 + i % 4, 3 - i / 4);
            }
        }

        SavePalette(root, "Dungeon_Ground");
    }

    static void BuildWalls()
    {
        GameObject root = NewPalette("Dungeon_Walls");
        Tilemap map = root.GetComponentInChildren<Tilemap>();
        var grid = Tile.ColliderType.Grid;

        // Row 0: the auto tiles (paint walls with these).
        Put(map, T("Wall_Auto"), 0, 12);
        Put(map, T("Wall_Auto_Boss"), 1, 12);

        // The pack's wall sheets as 4x4 blocks: top wall with cap, right end, left end, plain cobble fill.
        PutSheet(map, "wall_1", "WL_1", 4, 4, 0, 6, grid);
        PutSheet(map, "wall_5", "WL_5", 4, 4, 5, 6, grid);
        PutSheet(map, "wall_6", "WL_6", 4, 4, 10, 6, grid);
        PutSheet(map, "wall_8", "WL_8", 4, 4, 15, 6, grid);

        // Arch gate (left half wall_10 + right half wall_11 = 4 wide, 4 tall): decoration, no collider.
        var none = Tile.ColliderType.None;
        PutSheet(map, "wall_10", "WL_10", 4, 4, 0, 0, none, i => i % 4 < 2);
        PutSheet(map, "wall_11", "WL_11", 4, 4, 0, 0, none, i => i % 4 >= 2);

        // Block column (wall_3), column with stone top (wall_2), cobble column (wall_7), cap strips (wall_4), post (wall_9).
        PutSheet(map, "wall_3", "WL_3", 1, 4, 5, 0, grid);
        PutSheet(map, "wall_2", "WL_2", 1, 4, 7, 0, grid);
        PutSheet(map, "wall_7", "WL_7", 1, 4, 9, 0, grid);
        PutSheet(map, "wall_4", "WL_4", 4, 1, 11, 3, grid);
        Put(map, MakeTile("WL_9_0", Sub("wall_9", 0), grid, Color.white), 11, 1);

        SavePalette(root, "Dungeon_Walls");
    }

    // name, sprite file, scale
    static readonly (string name, string sprite, float scale)[] DecorDefs =
    {
        ("crack_a", "decor_14", 0.8f), ("crack_b", "decor_15", 0.8f),
        ("slime_a", "decor_26", 0.6f), ("slime_b", "decor_27", 0.6f), ("slime_c", "decor_28", 0.6f),
        ("spikes", "decor_33", 0.6f), ("trap_a", "decor_31", 0.5f), ("trap_b", "decor_32", 0.5f),
        ("gold_a", "decor_23", 0.5f), ("gold_b", "decor_24", 0.5f), ("gold_c", "decor_25", 0.5f),
        ("key_a", "decor_34", 0.5f), ("key_b", "decor_35", 0.5f), ("key_c", "decor_36", 0.5f), ("trophy", "decor_37", 0.5f),
        ("gem_red", "decor_38", 0.8f), ("gem_gold", "decor_39", 0.8f), ("gem_blue", "decor_40", 0.8f), ("gem_green", "decor_41", 0.8f), ("gem_purple", "decor_42", 0.8f),
        ("weed_1", "greenery_1", 0.6f), ("weed_2", "greenery_2", 0.6f), ("weed_3", "greenery_3", 0.6f), ("weed_4", "greenery_4", 0.6f), ("weed_5", "greenery_5", 0.6f),
        ("moss_1", "greenery_6", 0.6f), ("moss_2", "greenery_7", 0.6f), ("moss_3", "greenery_8", 0.6f), ("moss_4", "greenery_9", 0.6f), ("moss_5", "greenery_10", 0.6f),
        ("pebble_1", "stones_6", 0.4f), ("pebble_2", "stones_7", 0.4f), ("pebble_3", "stones_8", 0.4f), ("pebble_4", "stones_9", 0.5f), ("pebble_5", "stones_10", 0.5f),
    };

    static void BuildDecor()
    {
        GameObject root = NewPalette("Dungeon_Decor");
        Tilemap map = root.GetComponentInChildren<Tilemap>();
        const int perRow = 8;
        for (int i = 0; i < DecorDefs.Length; i++)
        {
            var (name, spriteFile, scale) = DecorDefs[i];
            Sprite sprite = Single(spriteFile);
            Tile tile = MakeTile($"DC_{name}", sprite, Tile.ColliderType.None, Color.white, DecalTransform(sprite, scale));
            Put(map, tile, (i % perRow) * 3, -(i / perRow) * 3);
        }

        SavePalette(root, "Dungeon_Decor");
    }

    // ---- props (GameObject Brush palette) ----

    const int PropOrder = 10, WallDecorOrder = -85;

    // name, sprite, scale, solid collider (width as fraction of the sprite, world height), order
    static readonly (string name, string sprite, float scale, float colliderWidth, int order)[] NewProps =
    {
        ("Banner", "decor_1", 0.5f, 0f, WallDecorOrder), ("TorchB", "decor_30", 0.55f, 0f, WallDecorOrder), ("TorchC", "decor_29", 0.55f, 0f, WallDecorOrder),
        ("Bowl", "decor_8", 0.5f, 0f, PropOrder), ("Pitchfork", "decor_9", 0.5f, 0f, PropOrder),
        ("RuinWall", "decor_11", 0.45f, 0.8f, PropOrder), ("RuinHouse", "decor_12", 0.4f, 0.8f, PropOrder), ("ArchGate", "decor_13", 0.5f, 0.8f, PropOrder),
        ("BarrelGreen", "decor_16", 0.42f, 0.7f, PropOrder), ("BarrelBig", "decor_17", 0.42f, 0.8f, PropOrder),
        ("ChestRed", "decor_6", 0.5f, 0.8f, PropOrder), ("ChestGold", "decor_18", 0.5f, 0.8f, PropOrder), ("ChestBlack", "decor_22", 0.5f, 0.8f, PropOrder),
        ("GoldPileA", "decor_23", 0.5f, 0f, PropOrder), ("GoldPileB", "decor_24", 0.5f, 0f, PropOrder), ("GoldPileC", "decor_25", 0.5f, 0f, PropOrder),
        ("RockTall", "stones_1", 0.4f, 0.8f, PropOrder), ("RockTallB", "stones_2", 0.4f, 0.8f, PropOrder), ("RockA", "stones_3", 0.4f, 0.8f, PropOrder),
        ("RockB", "stones_5", 0.4f, 0.8f, PropOrder), ("RockC", "stones_7", 0.4f, 0.8f, PropOrder),
        ("BrickStackA", "bricks_1", 0.45f, 0.8f, PropOrder), ("BrickStackB", "bricks_2", 0.45f, 0.8f, PropOrder), ("BrickStackC", "bricks_4", 0.45f, 0.8f, PropOrder),
        ("LogA", "bricks_7", 0.5f, 0.8f, PropOrder), ("LogB", "bricks_8", 0.5f, 0.8f, PropOrder), ("StumpB", "bricks_9", 0.6f, 0.6f, PropOrder),
        ("CoralBush", "greenery_3", 0.6f, 0f, PropOrder),
    };

    static void BuildProps()
    {
        foreach (var (name, spriteFile, scale, colliderWidth, order) in NewProps)
        {
            string path = $"{PropFolder}/{name}.prefab";
            if (File.Exists(path))
            {
                continue;
            }

            var go = new GameObject(name);
            go.transform.localScale = new Vector3(scale, scale, scale);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Single(spriteFile);
            sr.sortingOrder = order;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            if (colliderWidth > 0f)
            {
                // Base collider in world units (feet only), divided by the prop scale like the first prop pass.
                var box = go.AddComponent<BoxCollider2D>();
                box.size = new Vector2(sr.sprite.rect.width / Ppu * colliderWidth, 0.3f / scale);
                box.offset = new Vector2(0f, 0.17f / scale);
            }

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        AssetDatabase.Refresh();
        GameObject root = NewPalette("Dungeon_Props");
        Tilemap map = root.GetComponentInChildren<Tilemap>();
        var prefabs = new List<GameObject>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PropFolder }))
        {
            prefabs.Add(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        prefabs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        const int perRow = 6, pitch = 4;
        for (int i = 0; i < prefabs.Count; i++)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], map.transform);
            instance.transform.position = new Vector3((i % perRow) * pitch + 0.5f, -(i / perRow) * pitch + 0.5f, 0f);
        }

        SavePalette(root, "Dungeon_Props");
    }
}
