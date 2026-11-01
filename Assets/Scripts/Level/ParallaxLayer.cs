using UnityEngine;

// Horizontally repeating background layer. Attach to a GameObject with a SpriteRenderer;
// it is switched to Tiled mode and follows the camera, scrolling at 'factor' of the camera speed
// (0 = fixed to the world, 1 = glued to the camera).
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class ParallaxLayer : MonoBehaviour
{
    [SerializeField] Transform cameraTransform;
    [SerializeField, Range(0f, 1f)] float factor = 0.3f;
    [SerializeField] float verticalFactor = 0.1f;
    [SerializeField] int copies = 5;

    SpriteRenderer spriteRenderer;
    float baseY;
    float cameraBaseY;

    void OnEnable()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        baseY = transform.position.y;
        cameraBaseY = cameraTransform != null ? cameraTransform.position.y : 0f;

        spriteRenderer.drawMode = SpriteDrawMode.Tiled;
        spriteRenderer.tileMode = SpriteTileMode.Continuous;
        Vector2 size = spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.size : Vector2.one;
        spriteRenderer.size = new Vector2(size.x * copies, size.y);
    }

    void LateUpdate()
    {
        if (cameraTransform == null || spriteRenderer.sprite == null)
            return;

        float width = spriteRenderer.sprite.bounds.size.x;
        Vector3 cam = cameraTransform.position;

        // The pattern shifts by cam.x * factor; wrapping by one tile width keeps it seamless.
        float offset = Mathf.Repeat(cam.x * factor - cam.x + width * 0.5f, width) - width * 0.5f;

        transform.position = new Vector3(cam.x + offset, baseY + (cam.y - cameraBaseY) * verticalFactor, transform.position.z);
    }
}
