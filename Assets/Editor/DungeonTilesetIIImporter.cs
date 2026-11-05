using System.IO;
using UnityEditor;
using UnityEngine;

// Import rules for 0x72's 16x16 DungeonTileset II (Assets/Art/DungeonTilesetII, CC0): one sprite per file, 16 PPU so a
// 16 px tile is one world unit (the level grid), point filter, no compression or mipmaps. Characters, enemies, props,
// weapons and pickups stand on their bottom edge (feet pivot, for Y sorting); floor and wall tiles and the atlases are
// centred so they sit on tilemap cells.
public class DungeonTilesetIIImporter : AssetPostprocessor
{
    public const string Folder = "Assets/Art/DungeonTilesetII/";
    public const int Ppu = 16;

    static readonly string[] CenteredPrefixes = { "floor_", "wall_", "edge_", "atlas_", "hole", "column_wall" };

    public static bool Handles(string path) => path.StartsWith(Folder) && path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase);

    static bool Centered(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        if (!path.StartsWith(Folder + "frames/")) return true; // atlases
        // Wall decorations hang on a wall tile but are drawn tile sized too.
        foreach (string prefix in CenteredPrefixes)
        {
            if (name.StartsWith(prefix)) return true;
        }

        return false;
    }

    void OnPreprocessTexture()
    {
        if (!Handles(assetPath)) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;

        // SetTextureSettings overwrites the importer, so mode, PPU and pivot all go through the settings object.
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMode = (int)SpriteImportMode.Single;
        settings.spritePixelsPerUnit = Ppu;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        bool centered = Centered(assetPath);
        settings.spriteAlignment = (int)(centered ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
        settings.spritePivot = centered ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0f);
        importer.SetTextureSettings(settings);
    }
}
