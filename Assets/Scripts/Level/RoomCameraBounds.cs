using UnityEngine;

// Optional camera clamp: while a room is active the camera is kept inside the room rect (centred when the view is
// larger than the room). Runs after CameraFollow and fades in/out so entering or leaving a room does not snap.
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(100)]
public sealed class RoomCameraBounds : MonoBehaviour
{
    [SerializeField] float fadeSpeed = 4f;

    public static RoomCameraBounds Instance { get; private set; }

    Camera cam;
    Rect bounds;
    bool hasBounds;
    float weight;
    object owner;

    void Awake()
    {
        cam = GetComponent<Camera>();
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SetBounds(object boundsOwner, Rect rect)
    {
        owner = boundsOwner;
        bounds = rect;
        hasBounds = true;
    }

    public void ClearBounds(object boundsOwner)
    {
        if (owner == boundsOwner)
        {
            hasBounds = false;
        }
    }

    void LateUpdate()
    {
        weight = Mathf.MoveTowards(weight, hasBounds ? 1f : 0f, fadeSpeed * Time.deltaTime);
        // The tilted camera is not at z = 0, so the clamp works on the ground area it sees and shifts the camera by
        // the difference.
        if (weight <= 0f || !GroundRay.TryGetFootprint(cam, out Vector2 focus, out float left, out float right, out float down, out float up))
        {
            return;
        }

        Vector2 clamped = new Vector2(Clamp(focus.x, bounds.xMin, bounds.xMax, left, right), Clamp(focus.y, bounds.yMin, bounds.yMax, down, up));
        transform.position += (Vector3)((clamped - focus) * weight);
        CameraFollow.SnapToPixelGrid(cam);
    }

    // Keeps [value - before, value + after] inside [min, max]; centres the view when it is larger than the room.
    static float Clamp(float value, float min, float max, float before, float after)
    {
        if (max - min <= before + after)
        {
            return (min + max) * 0.5f + (before - after) * 0.5f;
        }

        return Mathf.Clamp(value, min + before, max - after);
    }
}
