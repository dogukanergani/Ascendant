using UnityEngine;

// Smoothly follows the player; keeps the camera's own z.
[RequireComponent(typeof(Camera))]
public sealed class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float smoothTime = 0.15f;

    Vector3 velocity;

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
            Vector3 p = target.position;
            transform.position = new Vector3(p.x, p.y, transform.position.z);
        }
    }

    void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 goal = new Vector3(target.position.x, target.position.y, transform.position.z);
        transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
    }
}
