using UnityEngine;

// Gentle vertical bob for title art and similar decorations; uses unscaled time.
public sealed class UIBob : MonoBehaviour
{
    [SerializeField] float amplitude = 6f;
    [SerializeField] float period = 3.2f;

    RectTransform rect;
    Vector2 home;

    void Awake()
    {
        rect = (RectTransform)transform;
        home = rect.anchoredPosition;
    }

    void Update()
    {
        // Whole pixels only, so the pixel art does not shimmer.
        float offset = Mathf.Round(Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / period) * amplitude);
        rect.anchoredPosition = home + new Vector2(0f, offset);
    }
}
