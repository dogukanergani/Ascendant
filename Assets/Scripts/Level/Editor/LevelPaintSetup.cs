using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Turns the open Level_01 into a hand-paintable level: the auto generated Floor / Walls become empty Ground / Walls
// tilemaps (plus Decor, WallTop, Props), a translucent Layout_Guide shows where the rooms, corridors and doors are, and a
// small Sample_Room east of the level shows how the palettes fit together. Level_01_Auto.unity keeps the old result.
public static class LevelPaintSetup
{
    const string TileFolder = "Assets/Art/Environment/TopDown/Tiles";
    // Outside Assets/Art: PixelArtImporter forces every png there to a multi-sprite sheet.
    const string GuidePng = "Assets/Level/Guide/guide_cell.png";
    const string PropFolder = "Assets/Prefabs/Environment/Props";

    [MenuItem("Tools/Level/Setup Level_01 For Painting")]
    public static void Setup()
    {
        var gridGo = GameObject.Find("Grid");
        var floorGo = GameObject.Find("Floor");
        var wallsGo = GameObject.Find("Walls");
        if (gridGo == null || floorGo == null || wallsGo == null)
        {
            Debug.LogError("LevelPaintSetup: open Level_01 (needs Grid/Floor and Grid/Walls).");
            return;
        }

        Tilemap floor = floorGo.GetComponent<Tilemap>();
        Tilemap walls = wallsGo.GetComponent<Tilemap>();

        // 1. Layout guide from the old floor + rooms + doors.
        Sprite guideSprite = EnsureGuideSprite();
        var roomTile = GuideTile("Guide_Room", guideSprite, new Color(0.2f, 0.8f, 1f, 0.28f));
        var corridorTile = GuideTile("Guide_Corridor", guideSprite, new Color(1f, 0.95f, 0.3f, 0.28f));
        var bossTile = GuideTile("Guide_Boss", guideSprite, new Color(1f, 0.25f, 0.25f, 0.3f));
        var doorTile = GuideTile("Guide_Door", guideSprite, new Color(1f, 0.55f, 0f, 0.55f));

        Tilemap guide = NewTilemap("Layout_Guide", gridGo.transform, 30);
        guide.gameObject.AddComponent<LayoutGuide>();
        var rooms = Object.FindObjectsByType<RoomController>(FindObjectsSortMode.None);
        var doors = Object.FindObjectsByType<Door>(FindObjectsSortMode.None);
        floor.CompressBounds();
        foreach (Vector3Int cell in floor.cellBounds.allPositionsWithin)
        {
            if (floor.GetTile(cell) == null)
            {
                continue;
            }

            Vector2 center = new(cell.x + 0.5f, cell.y + 0.5f);
            TileBase tile = corridorTile;
            foreach (RoomController room in rooms)
            {
                if (room.GetComponent<BoxCollider2D>().bounds.Contains(center))
                {
                    tile = room.name.Contains("Boss") ? bossTile : roomTile;
                }
            }

            foreach (Door door in doors)
            {
                var box = door.GetComponent<BoxCollider2D>();
                Vector2 c = (Vector2)door.transform.position + box.offset;
                if (Mathf.Abs(center.x - c.x) <= box.size.x * 0.5f && Mathf.Abs(center.y - c.y) <= box.size.y * 0.5f)
                {
                    tile = doorTile;
                }
            }

            guide.SetTile(cell, tile);
        }

        // 2. Paintable tilemaps.
        floor.ClearAllTiles();
        walls.ClearAllTiles();
        floorGo.name = "Ground";
        floorGo.GetComponent<TilemapRenderer>().sortingOrder = -100;
        wallsGo.GetComponent<TilemapRenderer>().sortingOrder = -90;
        NewTilemap("Decor", gridGo.transform, -95);
        NewTilemap("WallTop", gridGo.transform, 20);
        Tilemap props = new GameObject("Props").AddComponent<Tilemap>();
        props.transform.SetParent(gridGo.transform, false);

        // Old auto-placed props do not fit a hand painted level (the backup scene keeps them).
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t != null && t.name == "Props" && t.parent != null && t.parent.name == "Level")
            {
                Object.DestroyImmediate(t.gameObject);
            }
        }

        // 3. Sample room.
        PaintSampleRoom(floor, walls, gridGo.transform.Find("Decor").GetComponent<Tilemap>(), props);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("LevelPaintSetup: Level_01 ready for painting.");
    }

    static Tilemap NewTilemap(string name, Transform parent, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var map = go.AddComponent<Tilemap>();
        go.AddComponent<TilemapRenderer>().sortingOrder = order;
        return map;
    }

    static Sprite EnsureGuideSprite()
    {
        if (!File.Exists(GuidePng))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GuidePng));
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    bool edge = x == 0 || y == 0 || x == 15 || y == 15;
                    tex.SetPixel(x, y, edge ? Color.white : new Color(1f, 1f, 1f, 0.5f));
                }
            }

            File.WriteAllBytes(GuidePng, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(GuidePng);
            var importer = (TextureImporter)AssetImporter.GetAtPath(GuidePng);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(GuidePng);
    }

    static Tile GuideTile(string name, Sprite sprite, Color color)
    {
        string path = $"{TilePaletteBuilder.PaletteFolder}/{name}.asset";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, path);
        }

        tile.sprite = sprite;
        tile.color = color;
        tile.colliderType = Tile.ColliderType.None;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    static TileBase T(string name) => AssetDatabase.LoadAssetAtPath<TileBase>($"{TileFolder}/{name}.asset");

    // Example: 10 x 7 floor, 3 thick cobble walls (cap on top), an arch gate in the top wall, a few decals and props.
    public static void PaintSampleRoom(Tilemap ground, Tilemap walls, Tilemap decor, Tilemap props)
    {
        const int fx0 = 44, fx1 = 53, fy0 = 2, fy1 = 8;      // floor rect
        const int wx0 = 41, wx1 = 56, wy0 = -1, wy1 = 12;     // outer wall rect
        for (int x = wx0; x <= wx1; x++)
        {
            for (int y = wy0; y <= wy1; y++)
            {
                bool isFloor = x >= fx0 && x <= fx1 && y >= fy0 && y <= fy1;
                Vector3Int p = new(x, y, 0);
                if (isFloor)
                {
                    ground.SetTile(p, T("Floor_Auto"));
                }
                else
                {
                    walls.SetTile(p, T("Wall_Auto"));
                }
            }
        }

        // Arch gate over the top wall (4 x 4, decoration without collider).
        for (int i = 0; i < 16; i++)
        {
            int col = i % 4, row = i / 4;
            string name = col < 2 ? $"WL_10_{i}" : $"WL_11_{i}";
            var tile = T(name);
            if (tile != null)
            {
                walls.SetTile(new Vector3Int(47 + col, 12 - row, 0), tile);
            }
        }

        foreach (var (name, x, y) in new[] { ("crack_a", 46, 4), ("slime_a", 51, 6), ("weed_3", 45, 8), ("pebble_4", 49, 3), ("crack_b", 52, 3) })
        {
            decor.SetTile(new Vector3Int(x, y, 0), T($"DC_{name}"));
        }

        foreach (var (name, x, y) in new[] { ("Torch", 45.5f, 9.1f), ("Torch", 51.5f, 9.1f), ("Barrel", 44.5f, 2.6f), ("Crate", 45.6f, 2.5f), ("Chest", 52.5f, 2.6f), ("Pillar", 48.5f, 5.2f) })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropFolder}/{name}.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, props.transform);
            instance.transform.position = new Vector3(x, y, 0f);
        }

    }
}
