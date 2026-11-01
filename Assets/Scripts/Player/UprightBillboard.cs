using UnityEngine;

// Keeps a standing object (character, prop, damage number) facing the tilted camera so it is not squashed against the
// ground. Put it on the object that carries the SpriteRenderer, with the sprite pivot at the feet: the object turns
// around its feet, so the foot position (and the collider on the parent) stays where it is. Do not put it on an
// object that owns a Collider2D: tilting a collider squashes it.
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class UprightBillboard : MonoBehaviour
{
    SpriteRenderer sprite;

    void OnEnable()
    {
        sprite = GetComponent<SpriteRenderer>();
        if (sprite != null)
        {
            // Sort by the feet rather than the (now tilted) sprite centre.
            sprite.spriteSortPoint = SpriteSortPoint.Pivot;
        }

        Apply();
    }

    void LateUpdate()
    {
        Apply();
    }

    void Apply()
    {
        Camera cam = Camera.main;
        if (cam != null && transform.rotation != cam.transform.rotation)
        {
            transform.rotation = cam.transform.rotation;
        }
    }
}
