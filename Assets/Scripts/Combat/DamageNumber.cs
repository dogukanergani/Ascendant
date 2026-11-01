using TMPro;
using UnityEngine;

// One floating number in world space. Owned and recycled by DamageNumberSpawner.
public sealed class DamageNumber : MonoBehaviour
{
    TextMeshPro text;
    Vector2 velocity;
    float age;
    float lifetime;
    float startScale;
    Color color;

    public bool InUse { get; private set; }

    public void Initialize(TMP_FontAsset font, int sortingOrder)
    {
        text = gameObject.AddComponent<TextMeshPro>();
        if (font != null)
        {
            text.font = font;
        }

        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.sortingOrder = sortingOrder;
        text.rectTransform.sizeDelta = new Vector2(4f, 1f);
        gameObject.AddComponent<UprightBillboard>();
        gameObject.SetActive(false);
    }

    public void Show(string value, Vector2 position, Color textColor, float scale, Vector2 initialVelocity, float duration)
    {
        text.text = value;
        color = textColor;
        text.color = textColor;
        transform.position = position;
        startScale = scale;
        transform.localScale = Vector3.one * scale;
        velocity = initialVelocity;
        lifetime = duration;
        age = 0f;
        InUse = true;
        gameObject.SetActive(true);
    }

    void Update()
    {
        age += Time.unscaledDeltaTime;
        float t = age / lifetime;
        if (t >= 1f)
        {
            InUse = false;
            gameObject.SetActive(false);
            return;
        }

        transform.position += (Vector3)(velocity * Time.unscaledDeltaTime);
        velocity.y = Mathf.Max(0f, velocity.y - 1.2f * Time.unscaledDeltaTime);
        // A quick pop at the start, then a fade over the second half of the life.
        transform.localScale = Vector3.one * (startScale * (1f + 0.35f * Mathf.Clamp01(1f - t * 6f)));
        color.a = t < 0.5f ? 1f : 1f - (t - 0.5f) * 2f;
        text.color = color;
    }
}
