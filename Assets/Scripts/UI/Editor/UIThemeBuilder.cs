using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

// Draws the theme's 9-slice panel/button/bar sprites and the icons in code (point filtered, PPU 1) and fills UITheme.asset.
// Colors come from the asset itself, so edit UITheme.asset and run Tools/UI/Rebuild Theme to apply them.
public static class UIThemeBuilder
{
    public const string ThemeDir = "Assets/Art/UI/Theme";
    public const string ThemePath = ThemeDir + "/UITheme.asset";

    static UITheme theme;

    [MenuItem("Tools/UI/Rebuild Theme")]
    public static UITheme Build()
    {
        Directory.CreateDirectory(ThemeDir);
        theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<UITheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
        }

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PixelFontBuilder.FontPath);
        if (font == null || AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PixelFontBuilder.BoldFontPath) == null)
        {
            PixelFontBuilder.Build();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PixelFontBuilder.FontPath);
        }

        theme.font = font;
        theme.fontBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PixelFontBuilder.BoldFontPath);
        Color border = theme.border;

        // Panels, cards, slots, bars.
        theme.panelSprite = Frame("panel", 16, 4, border, theme.borderLight, Mul(theme.panel, 0.7f), theme.panel, theme.panelDark, true);
        theme.cardSprite = Frame("card", 16, 4, border, Mul(theme.borderLight, 0.75f), Mul(theme.panelDark, 0.9f), Mul(theme.panel, 0.8f), Mul(theme.panelDark, 0.75f), true);
        theme.cardSelectedSprite = Frame("card_selected", 16, 4, theme.accentDark, theme.accent, theme.accentDark, Mul(theme.panel, 0.95f), Mul(theme.panelDark, 0.85f), true);
        theme.slotSprite = Frame("slot", 16, 4, border, Mul(theme.borderLight, 0.8f), Mul(theme.panelDark, 0.9f), Mul(theme.panelDark, 0.7f), Mul(theme.panelDark, 0.45f), false);
        theme.slotSelectedSprite = Frame("slot_selected", 16, 4, theme.accentDark, theme.accent, theme.accentDark, Mul(theme.panelDark, 0.7f), Mul(theme.panelDark, 0.45f), false);
        theme.badgeSprite = Frame("badge", 8, 3, border, theme.borderLight, Mul(theme.borderLight, 0.55f), Mul(theme.panelDark, 0.6f), Mul(theme.panelDark, 0.4f), false);
        theme.barFrameSprite = Frame("bar_frame", 12, 3, border, Mul(theme.borderLight, 0.8f), Mul(theme.panelDark, 0.9f), Mul(theme.panelDark, 0.6f), Mul(theme.panelDark, 0.4f), false);
        theme.barFillSprite = BarFill();
        theme.whiteSprite = Pixels("white", new[] { "#" }, new Dictionary<char, Color> { ['#'] = Color.white }, false);

        // Buttons.
        theme.buttonDefault = Buttons("button", new Color(0.3f, 0.25f, 0.58f), new Color(0.5f, 0.44f, 0.82f), new Color(0.17f, 0.13f, 0.36f));
        theme.buttonPrimary = Buttons("button_primary", new Color(0.88f, 0.52f, 0.15f), new Color(1f, 0.82f, 0.4f), new Color(0.55f, 0.27f, 0.1f));

        BuildIcons();
        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
        Debug.Log("UIThemeBuilder: theme sprites rebuilt.");
        return theme;
    }

    static Color Mul(Color c, float f) => new(c.r * f, c.g * f, c.b * f, c.a);

    static Color Lerp(Color a, Color b, float t) => Color.Lerp(a, b, t);

    // ---- frames ----

    static UITheme.ButtonSprites Buttons(string name, Color fill, Color hi, Color lo)
    {
        Color border = theme.border;
        Color hoverFill = Lerp(fill, Color.white, 0.18f);
        Color hoverHi = Lerp(hi, Color.white, 0.3f);
        Color pressedFill = Mul(fill, 0.72f);
        Color grayFill = Mul(theme.disabled, 0.7f);
        return new UITheme.ButtonSprites
        {
            normal = Frame($"{name}_normal", 16, 4, border, hi, lo, fill, Mul(fill, 0.82f), true),
            hover = Frame($"{name}_hover", 16, 4, border, hoverHi, lo, hoverFill, Mul(hoverFill, 0.85f), true),
            // Pressed swaps the bevel so the button looks pushed in.
            pressed = Frame($"{name}_pressed", 16, 4, border, lo, hi, pressedFill, Mul(pressedFill, 0.7f), true),
            disabled = Frame($"{name}_disabled", 16, 4, Mul(border, 1.2f), Mul(theme.disabled, 0.9f), Mul(theme.disabled, 0.45f), grayFill, Mul(grayFill, 0.85f), true),
        };
    }

    // Square pixel-art frame: 1 px outline, 1 px bevel (light on top/left, dark on bottom/right) and an inner shadow
    // along the top and left of the face. Corners lose their outline pixel when notch is set.
    static Sprite Frame(string name, int size, int borderSlice, Color outline, Color hi, Color lo, Color fill, Color shade, bool notch)
    {
        Texture2D tex = new(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int d = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));
                Color c;
                bool corner = (x == 0 || x == size - 1) && (y == 0 || y == size - 1);
                if (d == 0)
                {
                    c = corner && notch ? Color.clear : outline;
                }
                else if (d == 1)
                {
                    c = y == size - 2 || x == 1 ? hi : lo;
                }
                else if (d == 2 && (y == size - 3 || x == 2))
                {
                    c = shade;
                }
                else
                {
                    c = fill;
                }

                tex.SetPixel(x, y, c);
            }
        }

        return Save(tex, name, new Vector4(borderSlice, borderSlice, borderSlice, borderSlice));
    }

    static Sprite BarFill()
    {
        float[] rows = { 0.5f, 0.6f, 0.7f, 0.8f, 0.85f, 0.85f, 0.92f, 1f };
        Texture2D tex = new(4, rows.Length, TextureFormat.RGBA32, false);
        for (int y = 0; y < rows.Length; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                tex.SetPixel(x, y, new Color(rows[y], rows[y], rows[y], 1f));
            }
        }

        return Save(tex, "bar_fill", Vector4.zero);
    }

    // ---- icons ----

    static void BuildIcons()
    {
        Color outline = Mul(theme.border, 1.3f);
        var red = new Dictionary<char, Color> { ['#'] = theme.health, ['h'] = Lerp(theme.health, Color.white, 0.55f), ['d'] = Mul(theme.health, 0.7f) };
        var empty = new Dictionary<char, Color> { ['#'] = theme.healthDark, ['h'] = Lerp(theme.healthDark, Color.white, 0.12f), ['d'] = Mul(theme.healthDark, 0.8f) };
        string[] heart =
        {
            ".##...##.",
            "#hh######",
            "#h#######",
            "#########",
            ".#######.",
            "..#####..",
            "...###...",
            "....#....",
        };
        theme.heartFull = Outlined("heart_full", heart, red, outline);
        theme.heartEmpty = Outlined("heart_empty", heart, empty, outline);
        string[] halfRows = new string[heart.Length];
        for (int i = 0; i < heart.Length; i++)
        {
            char[] row = heart[i].ToCharArray();
            for (int x = 5; x < row.Length; x++)
            {
                if (row[x] != '.')
                {
                    row[x] = 'e';
                }
            }

            halfRows[i] = new string(row);
        }

        var half = new Dictionary<char, Color>(red) { ['e'] = empty['#'] };
        theme.heartHalf = Outlined("heart_half", halfRows, half, outline);

        string[] star =
        {
            "....#....",
            "...#h#...",
            "...###...",
            "#h#######",
            ".#######.",
            "..#####..",
            "..#####..",
            ".###.###.",
            ".##...##.",
        };
        theme.starFull = Outlined("star_full", star, new Dictionary<char, Color> { ['#'] = theme.accent, ['h'] = Color.white }, outline);
        theme.starEmpty = Outlined("star_empty", star, new Dictionary<char, Color> { ['#'] = Mul(theme.disabled, 0.6f), ['h'] = Mul(theme.disabled, 0.8f) }, outline);

        theme.lockIcon = Outlined("lock", new[]
        {
            "..#####..",
            ".##...##.",
            ".##...##.",
            ".##...##.",
            "#########",
            "#hhhhhhh#",
            "####k####",
            "####k####",
            "#########",
        }, new Dictionary<char, Color> { ['#'] = theme.textDim, ['h'] = theme.text, ['k'] = theme.border }, outline);

        theme.coinIcon = Outlined("coin", new[]
        {
            "..####..",
            ".#hh###.",
            "#h######",
            "#h###d##",
            "#h###d##",
            "#####d##",
            ".#ddd##.",
            "..####..",
        }, new Dictionary<char, Color> { ['#'] = theme.accent, ['h'] = Color.white, ['d'] = theme.accentDark }, outline);

        theme.clockIcon = Outlined("clock", new[]
        {
            "..#####..",
            ".#######.",
            "####k####",
            "####k####",
            "####kkk##",
            "#########",
            "#########",
            ".#######.",
            "..#####..",
        }, new Dictionary<char, Color> { ['#'] = theme.text, ['k'] = theme.border }, outline);

        string[] pip =
        {
            "...#...",
            "..###..",
            ".#####.",
            "#######",
            ".#####.",
            "..###..",
            "...#...",
        };
        theme.pipFull = Outlined("pip_full", pip, new Dictionary<char, Color> { ['#'] = theme.accent }, outline);
        theme.pipEmpty = Outlined("pip_empty", pip, new Dictionary<char, Color> { ['#'] = Mul(theme.disabled, 0.85f) }, outline);

        string[] arrow =
        {
            "#....",
            "##...",
            "###..",
            "####.",
            "###..",
            "##...",
            "#....",
        };
        string[] arrowL = new string[arrow.Length];
        for (int i = 0; i < arrow.Length; i++)
        {
            char[] row = arrow[i].ToCharArray();
            System.Array.Reverse(row);
            arrowL[i] = new string(row);
        }

        var gold = new Dictionary<char, Color> { ['#'] = theme.accent };
        theme.arrowRight = Outlined("arrow_right", arrow, gold, outline);
        theme.arrowLeft = Outlined("arrow_left", arrowL, gold, outline);

        theme.dashIcon = Outlined("icon_dash", new[]
        {
            "............",
            "##..##......",
            ".##..##.....",
            "..##..##....",
            "...##..##...",
            "..##..##....",
            ".##..##.....",
            "##..##......",
        }, new Dictionary<char, Color> { ['#'] = theme.stamina }, outline);

        theme.staffIcon = Outlined("icon_staff", new[]
        {
            "........#h#.",
            ".......#####",
            "........###.",
            "........w...",
            ".......w....",
            "......w.....",
            ".....w......",
            "....w.......",
            "...w........",
            "..w.........",
        }, new Dictionary<char, Color> { ['#'] = new Color(0.45f, 0.82f, 1f), ['h'] = Color.white, ['w'] = new Color(0.66f, 0.42f, 0.22f) }, outline);

        theme.emberSprite = Pixels("ember", new[] { ".#.", "###", ".#." }, new Dictionary<char, Color> { ['#'] = Color.white }, false);
    }

    // Draws a mask (any character other than '.' is a pixel, coloured through the palette) with a 1 px outline around it.
    static Sprite Outlined(string name, string[] rows, Dictionary<char, Color> palette, Color outline)
    {
        return Pixels(name, rows, palette, true, outline);
    }

    static Sprite Pixels(string name, string[] rows, Dictionary<char, Color> palette, bool addOutline, Color? outline = null)
    {
        int pad = addOutline ? 1 : 0;
        int srcW = rows[0].Length;
        int srcH = rows.Length;
        Texture2D tex = new(srcW + pad * 2, srcH + pad * 2, TextureFormat.RGBA32, false);
        for (int y = 0; y < tex.height; y++)
        {
            for (int x = 0; x < tex.width; x++)
            {
                tex.SetPixel(x, y, Color.clear);
            }
        }

        for (int y = 0; y < srcH; y++)
        {
            for (int x = 0; x < srcW; x++)
            {
                char c = rows[srcH - 1 - y][x];
                if (c != '.' && palette.TryGetValue(c, out Color color))
                {
                    tex.SetPixel(x + pad, y + pad, color);
                }
            }
        }

        if (addOutline)
        {
            Color[] src = tex.GetPixels();
            int w = tex.width;
            int h = tex.height;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (src[y * w + x].a > 0f)
                    {
                        continue;
                    }

                    bool near = (x > 0 && src[y * w + x - 1].a > 0f) || (x < w - 1 && src[y * w + x + 1].a > 0f)
                        || (y > 0 && src[(y - 1) * w + x].a > 0f) || (y < h - 1 && src[(y + 1) * w + x].a > 0f);
                    if (near)
                    {
                        tex.SetPixel(x, y, outline ?? Color.black);
                    }
                }
            }
        }

        return Save(tex, name, Vector4.zero);
    }

    static Sprite Save(Texture2D tex, string name, Vector4 border)
    {
        string path = $"{ThemeDir}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.userData = ((int)border.x).ToString();
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
