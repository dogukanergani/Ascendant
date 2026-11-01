using System.IO;
using UnityEditor;
using UnityEngine;

// Draws the top-down dungeon tileset (32x32 cells, small palette) into Art/Environment/TopDown/tileset_topdown.png.
// Row 0 is the normal cave theme, row 1 the same cells in a red tint for the boss room.
// Columns: 0-2 floor, 3 wall top, 4 wall front, 5 door closed, 6 door open, 7 exit pad.
public static class TopDownTilesetGenerator
{
    public const string SheetPath = "Assets/Art/Environment/TopDown/tileset_topdown.png";
    public const int Columns = 8;
    const int Cell = 32;

    static readonly Color32 Floor = new(58, 62, 80, 255);
    static readonly Color32 FloorDark = new(48, 52, 68, 255);
    static readonly Color32 FloorLight = new(72, 77, 98, 255);
    static readonly Color32 Crack = new(36, 39, 52, 255);
    static readonly Color32 WallTop = new(98, 102, 126, 255);
    static readonly Color32 WallTopLight = new(128, 132, 156, 255);
    static readonly Color32 WallTopDark = new(70, 74, 94, 255);
    static readonly Color32 Brick = new(78, 70, 96, 255);
    static readonly Color32 Mortar = new(50, 44, 64, 255);
    static readonly Color32 Wood = new(122, 84, 50, 255);
    static readonly Color32 WoodDark = new(84, 56, 34, 255);
    static readonly Color32 Iron = new(150, 152, 168, 255);
    static readonly Color32 Glow = new(90, 220, 235, 255);

    [MenuItem("Tools/Level/Generate TopDown Tileset")]
    public static void Generate()
    {
        var tex = new Texture2D(Columns * Cell, 2 * Cell, TextureFormat.RGBA32, false);
        var pixels = new Color32[tex.width * tex.height];
        for (int theme = 0; theme < 2; theme++)
        {
            for (int col = 0; col < Columns; col++)
            {
                DrawCell(pixels, tex.width, col, theme, theme == 1);
            }
        }

        tex.SetPixels32(pixels);
        Directory.CreateDirectory(Path.GetDirectoryName(SheetPath));
        File.WriteAllBytes(SheetPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceUpdate);
        Debug.Log("TopDownTilesetGenerator: wrote " + SheetPath);
    }

    static Color32 Tint(Color32 c, bool boss)
    {
        if (!boss)
        {
            return c;
        }

        return new Color32(
            (byte)Mathf.Min(255, c.r * 1.2f + 18),
            (byte)(c.g * 0.62f),
            (byte)(c.b * 0.78f),
            c.a);
    }

    static int Hash(int x, int y, int salt)
    {
        unchecked
        {
            int h = x * 73856093 ^ y * 19349663 ^ salt * 83492791;
            h ^= h >> 13;
            h *= 1274126177;
            return (h ^ (h >> 16)) & 0x7fffffff;
        }
    }

    // Local coordinates: x right, y down from the cell's top-left; the texture's y axis points up.
    static void DrawCell(Color32[] px, int width, int col, int theme, bool boss)
    {
        int height = 2 * Cell;
        // Row 0 of the sheet is the top in sprite space, so theme 0 sits in the upper half of the texture.
        int originX = col * Cell;
        int originY = height - (theme + 1) * Cell;
        void Put(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= Cell || y >= Cell)
            {
                return;
            }

            px[(originY + (Cell - 1 - y)) * width + originX + x] = Tint(c, boss);
        }

        void Rect(int x0, int y0, int w, int h, Color32 c)
        {
            for (int y = y0; y < y0 + h; y++)
            {
                for (int x = x0; x < x0 + w; x++)
                {
                    Put(x, y, c);
                }
            }
        }

        void FloorBase(int salt)
        {
            for (int y = 0; y < Cell; y++)
            {
                for (int x = 0; x < Cell; x++)
                {
                    int h = Hash(x, y, salt) % 100;
                    Put(x, y, h < 6 ? FloorDark : h < 11 ? FloorLight : Floor);
                }
            }

            Rect(0, Cell - 1, Cell, 1, FloorDark);
            Rect(Cell - 1, 0, 1, Cell, FloorDark);
        }

        switch (col)
        {
            case 0:
                FloorBase(1);
                break;
            case 1:
                FloorBase(2);
                for (int i = 0; i < 12; i++)
                {
                    Put(6 + i, 8 + i / 2, Crack);
                    Put(7 + i, 8 + i / 2, Crack);
                }

                for (int i = 0; i < 6; i++)
                {
                    Put(20 + i, 22 - i, Crack);
                }

                break;
            case 2:
                FloorBase(3);
                Rect(7, 20, 3, 2, FloorLight);
                Rect(20, 8, 2, 2, FloorLight);
                Rect(22, 24, 3, 2, FloorDark);
                break;
            case 3:
                for (int y = 0; y < Cell; y++)
                {
                    for (int x = 0; x < Cell; x++)
                    {
                        Put(x, y, Hash(x, y, 4) % 100 < 8 ? WallTopDark : WallTop);
                    }
                }

                Rect(0, 0, Cell, 2, WallTopLight);
                Rect(0, 0, 2, Cell, WallTopLight);
                Rect(0, Cell - 2, Cell, 2, WallTopDark);
                Rect(Cell - 2, 0, 2, Cell, WallTopDark);
                break;
            case 4:
                Rect(0, 0, Cell, Cell, Mortar);
                for (int row = 0; row < 4; row++)
                {
                    int offset = row % 2 == 0 ? 0 : 8;
                    for (int bx = -offset; bx < Cell; bx += 16)
                    {
                        int x0 = Mathf.Max(0, bx + 1);
                        int x1 = Mathf.Min(Cell, bx + 15);
                        Rect(x0, row * 8 + 1, x1 - x0, 6, Brick);
                        Rect(x0, row * 8 + 1, x1 - x0, 1, WallTopDark);
                    }
                }

                Rect(0, 0, Cell, 2, WallTopLight);
                Rect(0, Cell - 4, Cell, 4, Mortar);
                break;
            case 5:
                FloorBase(5);
                Rect(1, 1, 30, 30, WoodDark);
                for (int plank = 0; plank < 5; plank++)
                {
                    Rect(2 + plank * 6, 2, 5, 28, Wood);
                }

                Rect(1, 6, 30, 4, Iron);
                Rect(1, 22, 30, 4, Iron);
                Rect(14, 14, 4, 4, Iron);
                break;
            case 6:
                FloorBase(6);
                Rect(0, 0, 3, Cell, WallTopDark);
                Rect(Cell - 3, 0, 3, Cell, WallTopDark);
                Rect(3, 0, Cell - 6, 2, FloorDark);
                Rect(3, Cell - 2, Cell - 6, 2, FloorDark);
                break;
            default:
                FloorBase(7);
                for (int i = 0; i < 12; i++)
                {
                    Put(16 + i, 4 + i, Glow);
                    Put(16 - i, 4 + i, Glow);
                    Put(16 + i, 27 - i, Glow);
                    Put(16 - i, 27 - i, Glow);
                }

                Rect(14, 14, 4, 4, Glow);
                break;
        }
    }
}
