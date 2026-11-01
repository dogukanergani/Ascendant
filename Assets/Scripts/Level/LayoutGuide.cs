using UnityEngine;
using UnityEngine.Tilemaps;

// Marks the Layout_Guide tilemap (room outlines, doors) that only helps painting in the editor; hidden in play mode.
[RequireComponent(typeof(TilemapRenderer))]
public sealed class LayoutGuide : MonoBehaviour
{
    void Awake()
    {
        GetComponent<TilemapRenderer>().enabled = false;
    }
}
