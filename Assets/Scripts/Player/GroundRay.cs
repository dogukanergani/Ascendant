using UnityEngine;

// Camera-to-world helpers for the tilted camera. The game world lives on the XY plane (z = 0), so anything that used
// Camera.ScreenToWorldPoint must intersect a ray with that plane instead; this works for flat, tilted and
// perspective cameras alike.
public static class GroundRay
{
    public static bool Intersect(Ray ray, out Vector2 point, float planeZ = 0f)
    {
        if (Mathf.Abs(ray.direction.z) > 1e-5f)
        {
            float t = (planeZ - ray.origin.z) / ray.direction.z;
            if (t > 0f)
            {
                point = ray.GetPoint(t);
                return true;
            }
        }

        point = default;
        return false;
    }

    public static bool ScreenToGround(Camera cam, Vector2 screenPosition, out Vector2 point)
    {
        point = default;
        return cam != null && Intersect(cam.ScreenPointToRay(screenPosition), out point);
    }

    public static bool ViewportToGround(Camera cam, Vector2 viewport, out Vector2 point)
    {
        point = default;
        return cam != null && Intersect(cam.ViewportPointToRay(viewport), out point);
    }

    // Ground point under the mouse, or the fallback when the mouse or camera is unavailable.
    public static Vector2 MouseToGround(Camera cam, Vector2 fallback)
    {
        UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && ScreenToGround(cam, mouse.position.ReadValue(), out Vector2 point))
        {
            return point;
        }

        return fallback;
    }

    // Part of the ground plane the camera sees, as distances from the point under the screen centre to the edges of
    // the visible area (the widest/tallest extent when perspective makes it a trapezoid).
    public static bool TryGetFootprint(Camera cam, out Vector2 focus, out float left, out float right, out float down, out float up)
    {
        focus = default;
        left = right = down = up = 0f;
        if (cam == null || !ViewportToGround(cam, new Vector2(0.5f, 0.5f), out focus))
        {
            return false;
        }

        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        for (int ix = 0; ix <= 2; ix++)
        {
            for (int iy = 0; iy <= 2; iy++)
            {
                if (!ViewportToGround(cam, new Vector2(ix * 0.5f, iy * 0.5f), out Vector2 p))
                {
                    return false;
                }

                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }
        }

        left = focus.x - minX;
        right = maxX - focus.x;
        down = focus.y - minY;
        up = maxY - focus.y;
        return true;
    }
}
