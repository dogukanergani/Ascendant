using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Import rules for the Craftpix Mage 1-3 and Dungeon Tileset packs (Assets/Art/ThirdParty, gitignored).
// Mage sheets: 64x64 cells, 4 direction rows (down, left, right, up), 32 PPU like the orcs, feet pivot.
// Dungeon art: 64 PPU so one 64 px tile is one world unit; wall/land sheets are sliced into 64x64 cells.
public class CraftpixImporter : AssetPostprocessor
{
    public const string MageFolder = "Assets/Art/ThirdParty/Craftpix/Mage1-3/";
    public const string DungeonFolder = "Assets/Art/ThirdParty/Craftpix/DungeonTileset/";
    public const string DungeonGeneratedFolder = DungeonFolder + "Generated/";

    const int MagePpu = 32;
    const int DungeonPpu = 64;
    const int Cell = 64;
    // Feet sit about 12 px above the cell bottom; the pivot lets the sprite stand on the Visual origin.
    public static readonly Vector2 MagePivot = new(0.5f, 0.19f);

    public static bool Handles(string path)
    {
        return IsMageSheet(path) || IsDungeon(path);
    }

    static bool IsPng(string path) => path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase);

    static bool IsMageSheet(string path) => path.StartsWith(MageFolder) && IsPng(path) && path.Contains("/With_shadow/");

    static bool IsDungeon(string path) => path.StartsWith(DungeonFolder) && IsPng(path);

    static bool IsSlicedDungeonSheet(string path)
    {
        if (!path.StartsWith(DungeonFolder + "PNG/"))
        {
            return false;
        }

        string name = Path.GetFileNameWithoutExtension(path);
        return name == "land" || name.StartsWith("wall_");
    }

    void OnPreprocessTexture()
    {
        if (!Handles(assetPath))
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;

        // SetTextureSettings overwrites the importer, so mode and PPU go through the settings object too.
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        if (IsMageSheet(assetPath))
        {
            settings.spriteMode = (int)SpriteImportMode.Multiple;
            settings.spritePixelsPerUnit = MagePpu;
        }
        else
        {
            bool sliced = IsSlicedDungeonSheet(assetPath);
            bool centered = sliced || assetPath.StartsWith(DungeonGeneratedFolder);
            settings.spriteMode = (int)(sliced ? SpriteImportMode.Multiple : SpriteImportMode.Single);
            settings.spritePixelsPerUnit = DungeonPpu;
            settings.spriteAlignment = (int)(centered ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
            settings.spritePivot = centered ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0f);
        }

        importer.SetTextureSettings(settings);
    }

    // Sprite rects can only be written once the texture is imported; a sheet whose rects already match is left alone.
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (string path in imported)
        {
            if (IsMageSheet(path))
            {
                SliceGrid(path, MagePivot, SpriteAlignment.Custom);
            }
            else if (IsSlicedDungeonSheet(path))
            {
                SliceGrid(path, new Vector2(0.5f, 0.5f), SpriteAlignment.Center);
            }
        }
    }

    static void SliceGrid(string path, Vector2 pivot, SpriteAlignment alignment)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (importer == null || texture == null || importer.spriteImportMode != SpriteImportMode.Multiple)
        {
            return;
        }

        string fileName = Path.GetFileNameWithoutExtension(path);
        int columns = texture.width / Cell;
        int rows = texture.height / Cell;
        if (columns == 0 || rows == 0 || texture.width % Cell != 0 || texture.height % Cell != 0)
        {
            return;
        }

        var wanted = new List<SpriteRect>();
        int index = 0;
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                wanted.Add(new SpriteRect
                {
                    name = $"{fileName}_{index}",
                    rect = new Rect(col * Cell, texture.height - (row + 1) * Cell, Cell, Cell),
                    alignment = alignment,
                    pivot = pivot,
                    spriteID = GUID.Generate(),
                });
                index++;
            }
        }

        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);
        dataProvider.InitSpriteEditorDataProvider();
        if (Matches(dataProvider.GetSpriteRects(), wanted))
        {
            return;
        }

        dataProvider.SetSpriteRects(wanted.ToArray());
        dataProvider.Apply();
        importer.SaveAndReimport();
    }

    static bool Matches(SpriteRect[] existing, List<SpriteRect> wanted)
    {
        if (existing.Length != wanted.Count)
        {
            return false;
        }

        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i].name != wanted[i].name || existing[i].rect != wanted[i].rect || existing[i].pivot != wanted[i].pivot)
            {
                return false;
            }
        }

        return true;
    }
}
