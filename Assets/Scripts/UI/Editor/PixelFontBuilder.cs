using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

// Draws the UI's bitmap pixel font in code (5x7 letters, Turkish accents composed from the base letters) and wraps it
// in a static TMP font asset. One atlas pixel is one font-size unit / 12, so sizes that are multiples of 12 stay sharp.
public static class PixelFontBuilder
{
    public const string FontPath = "Assets/Art/UI/Theme/PixelUI.asset";
    public const string BoldFontPath = "Assets/Art/UI/Theme/PixelUIBold.asset";


    // Cell layout: rows 0-2 accent space, rows 3-9 the 7 body rows, rows 10-11 descender and cedilla, row 12 spare for the bold variant.
    const int CellRows = 13;
    const int BodyTop = 3;
    const int Cell = 6;
    public const int PointSize = 12;

    static readonly Dictionary<char, string> Glyphs = new()
    {
        ['A'] = ".###./#...#/#...#/#####/#...#/#...#/#...#",
        ['B'] = "####./#...#/#...#/####./#...#/#...#/####.",
        ['C'] = ".###./#...#/#..../#..../#..../#...#/.###.",
        ['D'] = "####./#...#/#...#/#...#/#...#/#...#/####.",
        ['E'] = "#####/#..../#..../####./#..../#..../#####",
        ['F'] = "#####/#..../#..../####./#..../#..../#....",
        ['G'] = ".###./#...#/#..../#.###/#...#/#...#/.####",
        ['H'] = "#...#/#...#/#...#/#####/#...#/#...#/#...#",
        ['I'] = ".###./..#../..#../..#../..#../..#../.###.",
        ['J'] = "..###/...#./...#./...#./...#./#..#./.##..",
        ['K'] = "#...#/#..#./#.#../##.../#.#../#..#./#...#",
        ['L'] = "#..../#..../#..../#..../#..../#..../#####",
        ['M'] = "#...#/##.##/#.#.#/#.#.#/#...#/#...#/#...#",
        ['N'] = "#...#/##..#/#.#.#/#..##/#...#/#...#/#...#",
        ['O'] = ".###./#...#/#...#/#...#/#...#/#...#/.###.",
        ['P'] = "####./#...#/#...#/####./#..../#..../#....",
        ['Q'] = ".###./#...#/#...#/#...#/#.#.#/#..#./.##.#",
        ['R'] = "####./#...#/#...#/####./#.#../#..#./#...#",
        ['S'] = ".####/#..../#..../.###./....#/....#/####.",
        ['T'] = "#####/..#../..#../..#../..#../..#../..#..",
        ['U'] = "#...#/#...#/#...#/#...#/#...#/#...#/.###.",
        ['V'] = "#...#/#...#/#...#/#...#/#...#/.#.#./..#..",
        ['W'] = "#...#/#...#/#...#/#.#.#/#.#.#/##.##/#...#",
        ['X'] = "#...#/#...#/.#.#./..#../.#.#./#...#/#...#",
        ['Y'] = "#...#/#...#/.#.#./..#../..#../..#../..#..",
        ['Z'] = "#####/....#/...#./..#../.#.../#..../#####",
        ['0'] = ".###./#...#/#..##/#.#.#/##..#/#...#/.###.",
        ['1'] = "..#../.##../..#../..#../..#../..#../.###.",
        ['2'] = ".###./#...#/....#/...#./..#../.#.../#####",
        ['3'] = "####./....#/....#/.###./....#/....#/####.",
        ['4'] = "...#./..##./.#.#./#..#./#####/...#./...#.",
        ['5'] = "#####/#..../####./....#/....#/#...#/.###.",
        ['6'] = ".###./#..../#..../####./#...#/#...#/.###.",
        ['7'] = "#####/....#/...#./..#../.#.../.#.../.#...",
        ['8'] = ".###./#...#/#...#/.###./#...#/#...#/.###.",
        ['9'] = ".###./#...#/#...#/.####/....#/....#/.###.",
        ['a'] = "...../...../.###./....#/.####/#...#/.####",
        ['b'] = "#..../#..../####./#...#/#...#/#...#/####.",
        ['c'] = "...../...../.###./#...#/#..../#...#/.###.",
        ['d'] = "....#/....#/.####/#...#/#...#/#...#/.####",
        ['e'] = "...../...../.###./#...#/#####/#..../.###.",
        ['f'] = "..##./.#.../####./.#.../.#.../.#.../.#...",
        ['g'] = "...../...../.####/#...#/#...#/#...#/.####/....#/.###.",
        ['h'] = "#..../#..../####./#...#/#...#/#...#/#...#",
        ['i'] = "..#../...../.##../..#../..#../..#../.###.",
        ['j'] = "...#./...../..##./...#./...#./...#./...#./#..#./.##..",
        ['k'] = "#..../#..../#..#./#.#../##.../#.#../#..#.",
        ['l'] = ".##../..#../..#../..#../..#../..#../.###.",
        ['m'] = "...../...../##.#./#.#.#/#.#.#/#.#.#/#.#.#",
        ['n'] = "...../...../####./#...#/#...#/#...#/#...#",
        ['o'] = "...../...../.###./#...#/#...#/#...#/.###.",
        ['p'] = "...../...../####./#...#/#...#/#...#/####./#..../#....",
        ['q'] = "...../...../.####/#...#/#...#/#...#/.####/....#/....#",
        ['r'] = "...../...../#.##./##..#/#..../#..../#....",
        ['s'] = "...../...../.####/#..../.###./....#/####.",
        ['t'] = ".#.../.#.../###../.#.../.#.../.#..#/..##.",
        ['u'] = "...../...../#...#/#...#/#...#/#..##/.##.#",
        ['v'] = "...../...../#...#/#...#/#...#/.#.#./..#..",
        ['w'] = "...../...../#...#/#...#/#.#.#/#.#.#/.#.#.",
        ['x'] = "...../...../#...#/.#.#./..#../.#.#./#...#",
        ['y'] = "...../...../#...#/#...#/#...#/.####/....#/#...#/.###.",
        ['z'] = "...../...../#####/...#./..#../.#.../#####",
        ['ı'] = "...../...../.##../..#../..#../..#../.###.",
        ['!'] = "..#../..#../..#../..#../..#../...../..#..",
        ['.'] = "...../...../...../...../...../...../..#..",
        [','] = "...../...../...../...../...../..#../.#...",
        [':'] = "...../...../..#../...../...../..#../.....",
        [';'] = "...../...../..#../...../...../..#../.#...",
        ['-'] = "...../...../...../.###./...../...../.....",
        ['+'] = "...../..#../..#../.###./..#../..#../.....",
        ['='] = "...../...../.###./...../.###./...../.....",
        ['/'] = "....#/....#/...#./..#../.#.../#..../#....",
        ['('] = "...#./..#../.#.../.#.../.#.../..#../...#.",
        [')'] = ".#.../..#../...#./...#./...#./..#../.#...",
        ['['] = ".###./.#.../.#.../.#.../.#.../.#.../.###.",
        [']'] = ".###./...#./...#./...#./...#./...#./.###.",
        ['?'] = ".###./#...#/....#/...#./..#../...../..#..",
        ['\''] = "..#../..#../...../...../...../...../.....",
        ['"'] = ".#.#./.#.#./...../...../...../...../.....",
        ['%'] = "##..#/##.#./...#./..#../.#.../.#.##/#..##",
        ['*'] = "...../..#../#.#.#/.###./#.#.#/..#../.....",
        ['#'] = ".#.#./#####/.#.#./.#.#./#####/.#.#./.....",
        ['_'] = "...../...../...../...../...../...../#####",
        ['<'] = "...#./..#../.#.../#..../.#.../..#../...#.",
        ['>'] = ".#.../..#../...#./....#/...#./..#../.#...",
    };

    // Accent rows by cell row: breve, dots, dot, cedilla.
    static readonly string[] BreveCap = { "#...#", ".###." };
    static readonly string[] BreveLow = { "#...#", ".###." };

    static readonly Dictionary<char, (char baseLetter, string accent)> Turkish = new()
    {
        ['Ç'] = ('C', "cedilla"),
        ['Ğ'] = ('G', "breveCap"),
        ['İ'] = ('I', "dotCap"),
        ['Ö'] = ('O', "dotsCap"),
        ['Ş'] = ('S', "cedilla"),
        ['Ü'] = ('U', "dotsCap"),
        ['ç'] = ('c', "cedilla"),
        ['ğ'] = ('g', "breveLow"),
        ['ö'] = ('o', "dotsLow"),
        ['ş'] = ('s', "cedilla"),
        ['ü'] = ('u', "dotsLow"),
    };

    [MenuItem("Tools/UI/Build Pixel Font")]
    public static void Build()
    {
        BuildFont(false);
        BuildFont(true);
        AssetDatabase.SaveAssets();
    }

    static void BuildFont(bool bold)
    {
        string fontPath = bold ? BoldFontPath : FontPath;
        string atlasPath = fontPath.Replace(".asset", "_Atlas.png");
        Directory.CreateDirectory(Path.GetDirectoryName(fontPath));

        var cells = new Dictionary<char, bool[,]>();
        foreach (KeyValuePair<char, string> pair in Glyphs)
        {
            cells[pair.Key] = Compose(pair.Value, null, bold);
        }

        foreach (KeyValuePair<char, (char baseLetter, string accent)> pair in Turkish)
        {
            cells[pair.Key] = Compose(Glyphs[pair.Value.baseLetter], pair.Value.accent, bold);
        }

        // Pack into an atlas.
        const int atlasWidth = 256;
        const int atlasHeight = 128;
        var pixels = new Color32[atlasWidth * atlasHeight];
        var glyphList = new List<(char c, RectInt rect, int xOffset, int advance)>();
        int penX = 1;
        int penY = 1;
        List<char> keys = new(cells.Keys);
        keys.Sort();
        foreach (char c in keys)
        {
            bool[,] cell = cells[c];
            int minX = Cell;
            int maxX = -1;
            for (int y = 0; y < CellRows; y++)
            {
                for (int x = 0; x < Cell; x++)
                {
                    if (cell[x, y])
                    {
                        minX = Mathf.Min(minX, x);
                        maxX = Mathf.Max(maxX, x);
                    }
                }
            }

            int width = maxX - minX + 1;
            if (penX + width + 1 > atlasWidth)
            {
                penX = 1;
                penY += CellRows + 1;
            }

            for (int y = 0; y < CellRows; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (cell[minX + x, y])
                    {
                        // Atlas y runs bottom-up; cell row 0 is the top.
                        pixels[(atlasHeight - 1 - (penY + y)) * atlasWidth + penX + x] = new Color32(255, 255, 255, 255);
                    }
                }
            }

            glyphList.Add((c, new RectInt(penX, atlasHeight - 1 - (penY + CellRows - 1), width, CellRows), minX, width + 1));
            penX += width + 1;
        }

        Texture2D tex = new(atlasWidth, atlasHeight, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        tex.SetPixels32(pixels);
        File.WriteAllBytes(atlasPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(atlasPath);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(atlasPath);
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = false;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.isReadable = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
        Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);

        // A fresh asset every time keeps the tables free of leftovers.
        AssetDatabase.DeleteAsset(fontPath);
        TMP_FontAsset font = ScriptableObject.CreateInstance<TMP_FontAsset>();
        font.name = bold ? "PixelUIBold" : "PixelUI";
        AssetDatabase.CreateAsset(font, fontPath);
        Material material = new(Shader.Find("TextMeshPro/Mobile/Bitmap")) { name = "PixelUI Material" };
        material.SetTexture("_MainTex", atlas);
        AssetDatabase.AddObjectToAsset(material, font);
        font.material = material;

        font.faceInfo = new FaceInfo
        {
            familyName = bold ? "PixelUIBold" : "PixelUI",
            styleName = "Regular",
            pointSize = PointSize,
            scale = 1f,
            lineHeight = 15f,
            ascentLine = 10f,
            capLine = 7f,
            meanLine = 5f,
            baseline = 0f,
            descentLine = -2f,
            underlineOffset = -2f,
            underlineThickness = 1f,
            strikethroughOffset = 3f,
            strikethroughThickness = 1f,
            tabWidth = 24f,
        };

        font.atlasTextures = new[] { atlas };
        // Without a version TMP "upgrades" the asset from the legacy format and wipes the tables.
        SetPrivate(font, "m_Version", "1.1.0");
        SetPrivate(font, "m_AtlasWidth", atlasWidth);
        SetPrivate(font, "m_AtlasHeight", atlasHeight);
        SetPrivate(font, "m_AtlasPadding", 0);
        SetPrivate(font, "m_AtlasRenderMode", GlyphRenderMode.RASTER);
        font.atlasPopulationMode = AtlasPopulationMode.Static;

        // Glyph 0 is the space.
        var spaceGlyph = new Glyph(0, new GlyphMetrics(0, 0, 0, 0, 3f), new GlyphRect(0, 0, 0, 0), 1f, 0);
        font.glyphTable.Add(spaceGlyph);
        font.characterTable.Add(new TMP_Character(' ', spaceGlyph));
        for (int i = 0; i < glyphList.Count; i++)
        {
            (char c, RectInt rect, int xOffset, int advance) = glyphList[i];
            // The baseline sits under body row 9, so the top of the 12-row cell is 10 pixels above it.
            var glyph = new Glyph((uint)(i + 1), new GlyphMetrics(rect.width, rect.height, 0f, 10f, advance),
                new GlyphRect(rect.x, rect.y, rect.width, rect.height), 1f, 0);
            font.glyphTable.Add(glyph);
            font.characterTable.Add(new TMP_Character(c, glyph));
        }

        font.ReadFontAssetDefinition();
        EditorUtility.SetDirty(font);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        Debug.Log($"PixelFontBuilder: {glyphList.Count} glyphs written to {fontPath}.");
    }

    // Builds a [x, y] grid (y = 0 is the top row) from a glyph string; accent adds the Turkish marks.
    static bool[,] Compose(string rows, string accent, bool bold)
    {
        bool[,] cell = new bool[Cell, CellRows];
        string[] lines = rows.Split('/');
        for (int r = 0; r < lines.Length; r++)
        {
            for (int x = 0; x < lines[r].Length; x++)
            {
                cell[x, BodyTop + r] = lines[r][x] == '#';
            }
        }

        // Bold: every body pixel also fills the one to its right and the one below it. Accents only grow sideways
        // so the gap between them and the letter stays.
        if (bold)
        {
            bool[,] thick = new bool[Cell, CellRows];
            for (int y = 0; y < CellRows; y++)
            {
                for (int x = 0; x < Cell; x++)
                {
                    if (!cell[x, y])
                    {
                        continue;
                    }

                    thick[x, y] = true;
                    if (x + 1 < Cell)
                    {
                        thick[x + 1, y] = true;
                    }

                    if (y + 1 < CellRows)
                    {
                        thick[x, y + 1] = true;
                    }
                }
            }

            cell = thick;
        }

        switch (accent)
        {
            case "cedilla":
                Mark(cell, 2, 10, bold);
                Mark(cell, 1, 11, bold);
                Mark(cell, 2, 11, bold);
                break;
            case "breveCap":
                Stamp(cell, BreveCap, 0, bold);
                break;
            case "breveLow":
                Stamp(cell, BreveLow, 2, bold);
                break;
            case "dotCap":
                Mark(cell, 2, 1, bold);
                break;
            case "dotsCap":
                Mark(cell, 1, 1, bold);
                Mark(cell, 3, 1, bold);
                break;
            case "dotsLow":
                Mark(cell, 1, 3, bold);
                Mark(cell, 3, 3, bold);
                break;
        }

        return cell;
    }

    static void Mark(bool[,] cell, int x, int y, bool bold)
    {
        cell[x, y] = true;
        if (bold && x + 1 < Cell)
        {
            cell[x + 1, y] = true;
        }
    }

    static void SetPrivate(object target, string field, object value)
    {
        System.Reflection.FieldInfo info = target.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        info.SetValue(target, value);
    }

    static void Stamp(bool[,] cell, string[] rows, int top, bool bold)
    {
        for (int r = 0; r < rows.Length; r++)
        {
            for (int x = 0; x < rows[r].Length; x++)
            {
                if (rows[r][x] == '#')
                {
                    Mark(cell, x, top + r, bold);
                }
            }
        }
    }
}
