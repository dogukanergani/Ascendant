using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Applies pixel-art import settings to every PNG under Assets/Art.
// Files named like sprite sheets (contains "_sheet" or idle/run/jump/attack)
// are sliced into 64x64 cells, named <fileName>_0, <fileName>_1, ...
public class PixelArtImporter : AssetPostprocessor
{
    const string ArtFolder = "Assets/Art/";
    const int PixelsPerUnit = 32;
    const int CellSize = 64;

    static readonly string[] SheetKeywords = { "_sheet", "idle", "run", "jump", "attack" };

    // Environment and enemy art: tileset_/slime_/bat_ sheets use 32x32 cells,
    // parallax_ images are kept as one full-size sprite.
    static readonly string[] SmallCellPrefixes = { "tileset_", "slime_", "bat_" };
    const string ParallaxPrefix = "parallax_";
    const int SmallCellSize = 32;

    // Third-party art (gitignored): props are single sprites with a bottom-center pivot,
    // orc sheets are sliced into 64x64 cells (4 direction rows).
    const string ThirdPartyFolder = "Assets/Art/ThirdParty/";
    const string OrcFolder = "Assets/Art/ThirdParty/Craftpix/orc/";

    void OnPreprocessTexture()
    {
        if (IsThirdPartyPng(assetPath))
        {
            ApplyThirdPartySettings((TextureImporter)assetImporter);
            return;
        }

        if (!IsArtPng(assetPath))
            return;

        var importer = (TextureImporter)assetImporter;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.mipmapEnabled = false;

        // Tiled SpriteRenderers (parallax layers) need full-rect meshes.
        if (Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant().StartsWith(ParallaxPrefix))
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.wrapMode = TextureWrapMode.Repeat;
            importer.SetTextureSettings(settings);
        }
    }

    // Sprite rects can only be persisted once the texture is imported, so slicing happens here
    // and triggers a reimport. A sheet whose rects already match is left alone, which ends the loop.
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (string path in imported)
        {
            if (path.StartsWith(OrcFolder) && path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
            {
                SliceGrid(path, CellSize, CellSize);
                continue;
            }

            if (!IsArtPng(path) || !IsSheetName(Path.GetFileNameWithoutExtension(path)))
                continue;

            SliceGrid(path, 0, 0);
        }
    }

    static bool IsThirdPartyPng(string path)
    {
        return path.StartsWith(ThirdPartyFolder) && path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase);
    }

    static void ApplyThirdPartySettings(TextureImporter importer)
    {
        bool sheet = IsOrcPath(importer.assetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = sheet ? SpriteImportMode.Multiple : SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.mipmapEnabled = false;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        importer.SetTextureSettings(settings);
    }

    static bool IsOrcPath(string path)
    {
        return path.StartsWith(OrcFolder);
    }

    static bool IsArtPng(string path)
    {
        return path.StartsWith(ArtFolder) && !path.StartsWith(ThirdPartyFolder) && path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase);
    }

    static bool IsSheetName(string fileName)
    {
        string lower = fileName.ToLowerInvariant();
        if (lower.StartsWith(ParallaxPrefix) || GetSmallCellPrefix(lower) != null)
            return true;
        foreach (string keyword in SheetKeywords)
        {
            if (lower.Contains(keyword))
                return true;
        }
        return false;
    }

    static string GetSmallCellPrefix(string lowerName)
    {
        foreach (string prefix in SmallCellPrefixes)
        {
            if (lowerName.StartsWith(prefix))
                return prefix;
        }
        return null;
    }

    // Cells are counted from the top-left of the sheet, texture rects start at the bottom-left.
    static void SliceGrid(string path, int forcedCellW, int forcedCellH)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (importer == null || texture == null)
            return;

        string fileName = Path.GetFileNameWithoutExtension(path);
        string lowerName = fileName.ToLowerInvariant();
        int cellW = CellSize, cellH = CellSize;
        if (forcedCellW > 0)
        {
            cellW = forcedCellW;
            cellH = forcedCellH;
        }
        else if (lowerName.StartsWith(ParallaxPrefix))
        {
            cellW = texture.width;
            cellH = texture.height;
        }
        else if (GetSmallCellPrefix(lowerName) != null)
        {
            cellW = cellH = SmallCellSize;
        }
        // Tilemap cells anchor sprites at their pivot, so tiles need a centered one.
        // Prop sheets (tileset_props*) are placed on the floor, so they keep the bottom-center pivot.
        bool centerPivot = lowerName.StartsWith("tileset_") && !lowerName.StartsWith("tileset_props");
        int columns = texture.width / cellW;
        int rows = texture.height / cellH;

        var wanted = new List<SpriteRect>();
        int index = 0;
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                wanted.Add(new SpriteRect
                {
                    name = $"{fileName}_{index}",
                    rect = new Rect(col * cellW, texture.height - (row + 1) * cellH, cellW, cellH),
                    alignment = centerPivot ? SpriteAlignment.Center : SpriteAlignment.BottomCenter,
                    pivot = centerPivot ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0f),
                    spriteID = GUID.Generate(),
                });
                index++;
            }
        }

        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);
        dataProvider.InitSpriteEditorDataProvider();

        if (MatchesExisting(dataProvider.GetSpriteRects(), wanted))
            return;

        dataProvider.SetSpriteRects(wanted.ToArray());
        dataProvider.Apply();
        importer.SaveAndReimport();
    }

    static bool MatchesExisting(SpriteRect[] existing, List<SpriteRect> wanted)
    {
        if (existing.Length != wanted.Count)
            return false;

        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i].name != wanted[i].name || existing[i].rect != wanted[i].rect || existing[i].pivot != wanted[i].pivot)
                return false;
        }
        return true;
    }
}
