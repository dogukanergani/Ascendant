using UnityEngine;
using UnityEngine.Tilemaps;

// Auto floor for hand painting: the 16 cells of the pack's land.png tile seamlessly over 4x4 world cells, so this tile
// picks the cell by position (paint anywhere, never a visible seam). A floor cell with no floor above it (it sits under
// a wall face) uses the slightly darker shadow set. Fill the arrays with Tools/Level/Build Dungeon Palettes.
[CreateAssetMenu(menuName = "Level/Dungeon Floor Tile")]
public sealed class DungeonFloorTile : TileBase
{
    public Sprite[] floor = new Sprite[16];
    public Color tint = Color.white;
    public Color shadowTint = new(0.68f, 0.66f, 0.74f, 1f);

    public override void RefreshTile(Vector3Int position, ITilemap tilemap)
    {
        base.RefreshTile(position, tilemap);
        tilemap.RefreshTile(position + Vector3Int.up);
        tilemap.RefreshTile(position + Vector3Int.down);
    }

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        int cell = (3 - Mod4(position.y)) * 4 + Mod4(position.x);
        bool underWall = tilemap.GetTile(position + Vector3Int.up) == null;
        tileData.sprite = cell < floor.Length ? floor[cell] : null;
        tileData.color = underWall ? tint * shadowTint : tint;
        tileData.colliderType = Tile.ColliderType.None;
        tileData.flags = TileFlags.LockTransform | TileFlags.LockColor;
    }

    static int Mod4(int v) => ((v % 4) + 4) % 4;
}
