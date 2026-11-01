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
        if (weight <= 0f || !cam.orthographic)
        {
            return;
        }

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector3 pos = transform.position;
        Vector3 clamped = new Vector3(Clamp(pos.x, bounds.xMin, bounds.xMax, halfW), Clamp(pos.y, bounds.yMin, bounds.yMax, halfH), pos.z);
        transform.position = Vector3.Lerp(pos, clamped, weight);
    }

    static float Clamp(float value, float min, float max, float half)
    {
        if (max - min <= half * 2f)
        {
            return (min + max) * 0.5f;
        }

        return Mathf.Clamp(value, min + half, max - half);
    }
}
