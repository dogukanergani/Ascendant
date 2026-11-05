using System.Collections;
using UnityEngine;

// The DungeonTileset II pack has no death animation: when the Health above dies, this visual spins a quarter turn,
// shrinks and fades out over the death duration (the owner destroys or reloads afterwards).
public sealed class DeathFade : MonoBehaviour
{
    [SerializeField] float duration = 0.8f;
    [SerializeField] float spinDegrees = 90f;
    [SerializeField] float endScale = 0.3f;

    Health health;

    public float Duration { get => duration; set => duration = value; }

    void Awake()
    {
        health = GetComponentInParent<Health>();
    }

    void OnEnable()
    {
        if (health != null) health.Died += OnDied;
    }

    void OnDisable()
    {
        if (health != null) health.Died -= OnDied;
    }

    void OnDied()
    {
        StartCoroutine(Fade());
    }

    IEnumerator Fade()
    {
        var renderers = GetComponentsInChildren<SpriteRenderer>();
        var colors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) colors[i] = renderers[i].color;
        Vector3 startScale = transform.localScale;
        Quaternion startRotation = transform.localRotation;
        // Falls away from the side it faces.
        var main = GetComponent<SpriteRenderer>();
        float side = main != null && main.flipX ? 1f : -1f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            float ease = k * k;
            transform.localScale = Vector3.Lerp(startScale, startScale * endScale, ease);
            transform.localRotation = startRotation * Quaternion.Euler(0f, 0f, side * spinDegrees * Mathf.Min(1f, k * 2f));
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color c = colors[i];
                c.a *= 1f - ease;
                renderers[i].color = c;
            }

            yield return null;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].enabled = false;
        }
    }
}
