using UnityEngine;
using UnityEngine.Tilemaps;

// Auto wall for hand painting: paint any cell of the Walls tilemap with this one tile and it picks the Craftpix piece
// from its four neighbours (a cell counts as wall when the tilemap has any tile there):
//   no wall above  -> stone cap (thin stone edge with cobble under it); left / right ends use the rounded corners
//   wall above     -> dark cobble face; left / right ends use the ragged rock edge; no wall below -> the face's bottom edge
//   no wall left and right -> single-cell column / post
// Thick walls are just several painted rows: cap on top, cobble fill below. All pieces tile seamlessly over 4 cells,
// so the world position picks the variant. Fill the arrays with the editor menu Tools/Level/Build Dungeon Palettes.
[CreateAssetMenu(menuName = "Level/Dungeon Wall Tile")]
public sealed class DungeonWallTile : TileBase
{
    public Sprite[] cap = new Sprite[4];          // wall_1 row 0 (x % 4)
    public Sprite topLeft, topRight, post;        // wall_6[0], wall_5[3], wall_9[0]
    public Sprite[] column = new Sprite[4];       // wall_3 (3 - y % 4), 1-wide wall
    public Sprite[] leftEdge = new Sprite[3];     // wall_6 col 0 rows 1-3 (last = bottom)
    public Sprite[] rightEdge = new Sprite[3];    // wall_5 col 3 rows 1-3 (last = bottom)
    public Sprite[] face = new Sprite[8];         // wall_8 rows 1-2 (row * 4 + x % 4)
    public Sprite[] bottom = new Sprite[4];       // wall_8 row 3 (x % 4)
    public Color tint = Color.white;

    public override void RefreshTile(Vector3Int position, ITilemap tilemap)
    {
        base.RefreshTile(position, tilemap);
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx != 0 || dy != 0)
                {
                    tilemap.RefreshTile(position + new Vector3Int(dx, dy, 0));
                }
            }
        }
    }

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        bool n = Has(tilemap, position, Vector3Int.up), s = Has(tilemap, position, Vector3Int.down);
        bool e = Has(tilemap, position, Vector3Int.right), w = Has(tilemap, position, Vector3Int.left);
        int x = Mod4(position.x), y = Mod4(position.y);

        Sprite sprite;
        if (!w && !e)
        {
            sprite = n && s ? Pick(column, 3 - y) : post;
        }
        else if (!n)
        {
            sprite = !w ? topLeft : !e ? topRight : Pick(cap, x);
        }
        else if (!w)
        {
            sprite = Pick(leftEdge, s ? y % 2 : leftEdge.Length - 1);
        }
        else if (!e)
        {
            sprite = Pick(rightEdge, s ? y % 2 : rightEdge.Length - 1);
        }
        else
        {
            sprite = s ? Pick(face, (y % 2) * 4 + x) : Pick(bottom, x);
        }

        tileData.sprite = sprite;
        tileData.color = tint;
        tileData.colliderType = Tile.ColliderType.Grid;
        tileData.flags = TileFlags.LockTransform | TileFlags.LockColor;
    }

    static bool Has(ITilemap tilemap, Vector3Int position, Vector3Int dir) => tilemap.GetTile(position + dir) != null;

    static int Mod4(int v) => ((v % 4) + 4) % 4;

    static Sprite Pick(Sprite[] sprites, int index) => index >= 0 && index < sprites.Length ? sprites[index] : null;
}
