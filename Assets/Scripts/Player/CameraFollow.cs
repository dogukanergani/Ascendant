using UnityEngine;

// Camera rig: smoothly follows the player on the XY ground plane. Default is the orthographic 3/4 view the Craftpix art
// is drawn for: no tilt, depth comes from Y sorting. Tilt Degrees > 0 (with Perspective When Tilted) stays available
// as an optional 2.5D mode: the camera then sits "south" of the focus point looking north and down, upright things
// stay readable through UprightBillboard, and mouse picking goes through GroundRay (valid for every mode).
// Pixel perfect: the orthographic size is derived from Reference Height so the screen shows a whole number of screen
// pixels per art pixel, and the camera position is snapped to the art pixel grid.
[RequireComponent(typeof(Camera))]
public sealed class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float smoothTime = 0.15f;

    [Header("Tilt rig")]
    [Tooltip("0 = orthographic 3/4 (default); 25-35 = optional tilted 2.5D view.")]
    [SerializeField, Range(0f, 60f)] float tiltDegrees = 0f;
    [Tooltip("Distance from the focus point along the view direction (perspective only).")]
    [SerializeField, Min(1f)] float distance = 27f;
    [Tooltip("Vertical field of view in degrees (perspective only).")]
    [SerializeField, Range(5f, 90f)] float fieldOfView = 25f;
    [Tooltip("Perspective camera while tilted; off keeps an orthographic tilted view. Always orthographic at tilt 0.")]
    [SerializeField] bool perspectiveWhenTilted;
    [Tooltip("Half height of the view at the focus point when orthographic (used when Pixel Perfect is off).")]
    [SerializeField, Min(0.5f)] float orthographicSize = 4.22f;
    [Header("Pixel perfect (orthographic)")]
    [SerializeField] bool pixelPerfect = true;
    [SerializeField, Min(1f)] float pixelsPerUnit = 32f;
    [Tooltip("Art pixels that fit the screen height at the base zoom (270 = 480x270, an integer divisor of 1080p/1440p/2160p).")]
    [SerializeField, Min(16f)] float referenceHeight = 270f;
    [Tooltip("Shifts the focus along the ground so the player sits lower/higher on screen.")]
    [SerializeField] float focusOffsetY;

    Camera cam;
    Vector2 focus;
    Vector2 velocity;

    public float TiltDegrees => tiltDegrees;

    // Snaps a camera position to the art pixel grid (call after every move that happens late in the frame).
    public static void SnapToPixelGrid(Camera camera)
    {
        CameraFollow rig = camera != null ? camera.GetComponent<CameraFollow>() : null;
        if (rig != null && rig.pixelPerfect && camera.orthographic)
        {
            rig.Snap();
        }
    }

    void Snap()
    {
        float step = 1f / pixelsPerUnit;
        Vector3 p = transform.position;
        transform.position = new Vector3(Mathf.Round(p.x / step) * step, Mathf.Round(p.y / step) * step, p.z);
    }

    // Largest whole number of screen pixels per art pixel that still shows the reference height; windows smaller
    // than the reference fall back to 1:1.
    float PixelPerfectSize()
    {
        float scale = Mathf.Max(1f, Mathf.Floor(cam.pixelHeight / referenceHeight));
        return cam.pixelHeight / (2f * scale * pixelsPerUnit);
    }

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void Start()
    {
        if (target == null)
        {
            PlayerController2D player = FindFirstObjectByType<PlayerController2D>();
            if (player != null)
            {
                target = player.transform;
            }
        }

        if (target != null)
        {
            focus = TargetFocus();
        }

        ApplyPose();
    }

    void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        focus = Vector2.SmoothDamp(focus, TargetFocus(), ref velocity, smoothTime);
        ApplyPose();
    }

    void OnValidate()
    {
        // Edit mode and live Inspector tweaks: re-pose around the player (or wherever the view currently points).
        if (cam == null)
        {
            cam = GetComponent<Camera>();
        }

        if (!Application.isPlaying)
        {
            if (target != null)
            {
                focus = TargetFocus();
            }
            else if (GroundRay.ViewportToGround(cam, new Vector2(0.5f, 0.5f), out Vector2 current))
            {
                focus = current;
            }
            else
            {
                focus = transform.position;
            }
        }

        ApplyPose();
    }

    Vector2 TargetFocus() => (Vector2)target.position + new Vector2(0f, focusOffsetY);

    void ApplyPose()
    {
        if (cam == null)
        {
            return;
        }

        bool perspective = perspectiveWhenTilted && tiltDegrees > 0.01f;
        cam.orthographic = !perspective;
        cam.orthographicSize = pixelPerfect && !perspective ? PixelPerfectSize() : orthographicSize;
        cam.fieldOfView = fieldOfView;
        cam.transparencySortMode = TransparencySortMode.CustomAxis;
        cam.transparencySortAxis = Vector3.up;

        // Euler(-tilt) turns the view direction towards +Y, so the camera ends up south of the focus.
        Quaternion rotation = Quaternion.Euler(-tiltDegrees, 0f, 0f);
        Vector3 position = new Vector3(focus.x, focus.y, 0f) - rotation * Vector3.forward * distance;
        transform.SetPositionAndRotation(position, rotation);
        if (!perspective && pixelPerfect)
        {
            Snap();
        }
    }
}
