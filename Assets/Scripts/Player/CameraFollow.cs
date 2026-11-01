using UnityEngine;

// Camera rig: smoothly follows the player on the XY ground plane and tilts the view (Octopath-style 2.5D).
// Everything about the look is set here: Tilt Degrees = 0 gives the original flat top-down orthographic camera.
// The camera sits "south" of the focus point, looking north and down at the ground; upright things (player, enemies,
// props) stay readable through UprightBillboard, and mouse picking goes through GroundRay.
[RequireComponent(typeof(Camera))]
public sealed class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float smoothTime = 0.15f;

    [Header("Tilt rig")]
    [Tooltip("0 = flat top-down; 25-35 = tilted 2.5D view.")]
    [SerializeField, Range(0f, 60f)] float tiltDegrees = 30f;
    [Tooltip("Distance from the focus point along the view direction (perspective only).")]
    [SerializeField, Min(1f)] float distance = 27f;
    [Tooltip("Vertical field of view in degrees (perspective only).")]
    [SerializeField, Range(5f, 90f)] float fieldOfView = 25f;
    [Tooltip("Perspective camera while tilted; off keeps an orthographic tilted view. Always orthographic at tilt 0.")]
    [SerializeField] bool perspectiveWhenTilted = true;
    [Tooltip("Half height of the view at the focus point when orthographic.")]
    [SerializeField, Min(0.5f)] float orthographicSize = 6f;
    [Tooltip("Shifts the focus along the ground so the player sits lower/higher on screen.")]
    [SerializeField] float focusOffsetY;

    Camera cam;
    Vector2 focus;
    Vector2 velocity;

    public float TiltDegrees => tiltDegrees;

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
        cam.orthographicSize = orthographicSize;
        cam.fieldOfView = fieldOfView;
        cam.transparencySortMode = TransparencySortMode.CustomAxis;
        cam.transparencySortAxis = Vector3.up;

        // Euler(-tilt) turns the view direction towards +Y, so the camera ends up south of the focus.
        Quaternion rotation = Quaternion.Euler(-tiltDegrees, 0f, 0f);
        Vector3 position = new Vector3(focus.x, focus.y, 0f) - rotation * Vector3.forward * distance;
        transform.SetPositionAndRotation(position, rotation);
    }
}
