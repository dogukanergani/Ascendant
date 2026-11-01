using UnityEditor;
using UnityEngine;

// PixelArtImporter turns every PNG under Assets/Art into a (sliced) multi-sprite sheet at 32 PPU. The generated theme
// sprites need single sprites at 100 PPU with their 9-slice border, so this runs after it for Assets/Art/UI/Theme only.
// The border (in art pixels) travels in the importer's userData, written by UIThemeBuilder.
public sealed class UIThemeImportSettings : AssetPostprocessor
{
    const string ThemeFolder = "Assets/Art/UI/Theme/";

    public override int GetPostprocessOrder() => 100;

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(ThemeFolder) || !assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        // The font atlas is a plain texture, not a sprite.
        if (assetPath.EndsWith("_Atlas.png"))
        {
            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.alphaIsTransparency = true;
        int border = int.TryParse(importer.userData, out int value) ? value : 0;
        importer.spriteBorder = new Vector4(border, border, border, border);
    }
}
