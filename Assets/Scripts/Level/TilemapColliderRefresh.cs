using UnityEngine;
using UnityEngine.Tilemaps;

// A saved scene loads the Walls tilemap with an empty CompositeCollider2D: the tile collider data is not
// available when the collider first builds, so walls never block anything. Toggling the TilemapCollider2D once
// the tiles are loaded rebuilds it. Add this next to the TilemapCollider2D / CompositeCollider2D pair.
[ExecuteAlways]
[RequireComponent(typeof(TilemapCollider2D), typeof(CompositeCollider2D))]
public sealed class TilemapColliderRefresh : MonoBehaviour
{
    TilemapCollider2D tileCollider;
    CompositeCollider2D composite;

    void OnEnable()
    {
        Rebuild();
    }

    // Start runs before the first physics step; covers the case where OnEnable ran before the tiles were ready.
    void Start()
    {
        if (composite == null || composite.pathCount == 0)
        {
            Rebuild();
        }
    }

    void Rebuild()
    {
        tileCollider = GetComponent<TilemapCollider2D>();
        composite = GetComponent<CompositeCollider2D>();
        tileCollider.enabled = false;
        tileCollider.enabled = true;
    }
}
